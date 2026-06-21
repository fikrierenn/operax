using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Operax.Web.Lib;

/// <summary>
/// Dinamik Kullanıcı Tanımlı Alanlar (UDF) servisi: tanımları okur, AdditionalFields
/// JSON'ını çözer ve form gönderimini whitelist + sunucu-taraf validasyondan geçirip
/// güvenli JSON üretir. SQL-First mimaride ince yardımcı — iş kuralı taşımaz.
/// </summary>
public sealed class UdfService(Db db, ICurrentCompany company, ILogger<UdfService> logger)
{
    // Bir entity için aktif UDF tanımlarını sıralı getirir (şirket-kapsamlı).
    public async Task<IReadOnlyList<UdfFieldDef>> LoadDefinitionsAsync(string entityName, CancellationToken ct = default)
    {
        using var conn = db.Open();
        var rows = await conn.QueryAsync<UdfFieldDef>(new CommandDefinition(@"
            SELECT Id, FieldName, LabelText, FieldType, DataSourceType, DataSourceKey,
                   DefaultValue, OrderNo, IsRequired
            FROM UserFieldDefinition
            WHERE CompanyId = @CompanyId AND EntityName = @EntityName
              AND IsActive = 1 AND IsDeleted = 0
            ORDER BY OrderNo, LabelText",
            new { CompanyId = company.Id, EntityName = entityName },
            cancellationToken: ct));
        return rows.ToList();
    }

    // AdditionalFields JSON'ını anahtar-değer sözlüğüne çözer; bozuk JSON sessizce yutulmaz.
    public Dictionary<string, string> ReadValues(string? additionalFieldsJson)
    {
        if (string.IsNullOrWhiteSpace(additionalFieldsJson)) return new();
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string?>>(additionalFieldsJson);
            // Null değerleri ele, string'e indir
            return dict?.Where(kv => kv.Value != null)
                       .ToDictionary(kv => kv.Key, kv => kv.Value!) ?? new();
        }
        catch (JsonException ex)
        {
            // İş kuralı: bozuk UDF JSON ekranı kırmamalı; boş değerle devam, uyarı loglanır
            logger.LogWarning(ex, "UDF AdditionalFields JSON ayrıştırma hatası");
            return new();
        }
    }

    // Form gönderimini tanımlara göre doğrular ve yalnız geçerli alanlardan JSON üretir.
    // Açık 2 (sunucu validasyon) + Açık 3 (anahtar enjeksiyonu) + Açık 5 (kültür) burada kapanır.
    public async Task<(string Json, List<string> Errors)> BuildValidatedJsonAsync(IFormCollection form, IReadOnlyList<UdfFieldDef> defs, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var result = new Dictionary<string, string>();

        // İş kuralı: yalnızca tanımlı alanlar işlenir — form'daki başka UDF_ anahtarı yok sayılır
        foreach (var def in defs)
        {
            var raw = form["UDF_" + def.FieldName].ToString()?.Trim() ?? "";

            // BOOLEAN: işaretsiz checkbox form'da hiç gelmez → "false" kabul (zorunluluk muaf)
            if (def.FieldType == "BOOLEAN")
            {
                result[def.FieldName] = (raw is "true" or "on" or "1") ? "true" : "false";
                continue;
            }

            // Zorunlu alan boş geçilemez (HTML5 required bypass edilse de durur)
            if (string.IsNullOrEmpty(raw))
            {
                if (def.IsRequired) errors.Add($"'{def.LabelText}' alanı zorunludur.");
                continue;
            }

            // Tip bazlı doğrulama
            switch (def.FieldType)
            {
                case "TEXT":
                    result[def.FieldName] = raw;
                    break;

                case "NUMBER":
                    // Kültür: tarayıcı invariant nokta-ondalık gönderir
                    if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
                    {
                        errors.Add($"'{def.LabelText}' sayısal olmalıdır.");
                        break;
                    }
                    result[def.FieldName] = num.ToString(CultureInfo.InvariantCulture);
                    break;

                case "SELECT":
                    // STATIC + DICTIONARY desteklenir (TABLE → sonraki faz). Seçim çözülmüş seçeneklerde olmalı.
                    var selOpts = await ResolveOptionsAsync(def, ct);
                    if (selOpts.Count == 0)
                    {
                        errors.Add($"'{def.LabelText}' için desteklenmeyen/boş veri kaynağı.");
                        break;
                    }
                    if (!selOpts.Any(o => o.Value == raw))
                    {
                        errors.Add($"'{def.LabelText}' geçersiz seçim.");
                        break;
                    }
                    result[def.FieldName] = raw;
                    break;

                case "DATE":
                    // Kültür: invariant yyyy-MM-dd (tarayıcı date input bu formatı gönderir)
                    if (!DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        errors.Add($"'{def.LabelText}' geçerli tarih olmalı (yyyy-AA-gg).");
                        break;
                    }
                    result[def.FieldName] = raw;
                    break;

                default:
                    // DICTIONARY/TABLE veri kaynağı → Faz 3+
                    errors.Add($"'{def.LabelText}' alan tipi henüz desteklenmiyor.");
                    break;
            }
        }

        return (JsonSerializer.Serialize(result), errors);
    }

    // STATIC kaynak anahtarını (virgüllü liste) seçeneklere ayırır.
    public static IReadOnlyList<string> GetStaticOptions(string? dataSourceKey)
    {
        if (string.IsNullOrWhiteSpace(dataSourceKey)) return [];
        return dataSourceKey.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // SELECT alanının seçeneklerini kaynağa göre çözer: STATIC (virgüllü) | DICTIONARY (sözlük tipi).
    // TABLE veri kaynağı → sonraki faz (boş döner).
    public async Task<IReadOnlyList<UdfOption>> ResolveOptionsAsync(UdfFieldDef def, CancellationToken ct = default)
    {
        if (def.FieldType != "SELECT") return [];
        switch (def.DataSourceType)
        {
            case "STATIC":
                return GetStaticOptions(def.DataSourceKey).Select(s => new UdfOption(s, s)).ToList();

            case "DICTIONARY":
                // DataSourceKey = DictionaryType.Code (örn. 'BRAND'); değer = DictionaryValue.Code, metin = NameTr
                if (string.IsNullOrWhiteSpace(def.DataSourceKey)) return [];
                using (var conn = db.Open())
                {
                    var rows = await conn.QueryAsync<UdfOption>(new CommandDefinition(@"
                        SELECT dv.Code AS Value, dv.NameTr AS Text
                        FROM DictionaryValue dv
                        JOIN DictionaryType dt ON dt.Id = dv.TypeId
                        WHERE dt.Code = @Key AND dt.CompanyId = @Cid
                          AND dv.IsActive = 1 AND dv.IsDeleted = 0
                        ORDER BY dv.OrderNo, dv.NameTr",
                        new { Key = def.DataSourceKey, Cid = company.Id },
                        cancellationToken: ct));
                    return rows.ToList();
                }

            case "TABLE":
                // DataSourceKey = beyaz-liste anahtarı (Partner/Warehouse/Item); SQL injection'a karşı whitelist
                var tableSql = UdfWhitelist.GetQuery(def.DataSourceKey);
                if (tableSql == null) return [];
                using (var conn = db.Open())
                {
                    var rows = await conn.QueryAsync<UdfOption>(new CommandDefinition(
                        tableSql,
                        new { Cid = company.Id },
                        cancellationToken: ct));
                    return rows.ToList();
                }

            default:
                return [];
        }
    }

    // Bir tanım listesindeki tüm SELECT alanları için seçenekleri çözer (render map'i).
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<UdfOption>>> ResolveAllAsync(IReadOnlyList<UdfFieldDef> defs, CancellationToken ct = default)
    {
        var map = new Dictionary<string, IReadOnlyList<UdfOption>>();
        foreach (var d in defs.Where(d => d.FieldType == "SELECT"))
            map[d.FieldName] = await ResolveOptionsAsync(d, ct);
        return map;
    }
}

/// <summary>
/// TABLE veri kaynağı için güvenli sorgu beyaz-listesi (SQL injection koruması — DYNAMIC_CUSTOM_FIELDS §4C).
/// DataSourceKey doğrudan SQL'e konmaz; yalnız buradaki sabit sorgular çalışır. Değer = Id, metin = Name.
/// </summary>
public static class UdfWhitelist
{
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Partner"]   = "SELECT CAST(Id AS NVARCHAR(36)) AS Value, Name AS Text FROM Partner   WHERE CompanyId = @Cid AND IsActive = 1 AND IsDeleted = 0 ORDER BY Name",
        ["Warehouse"] = "SELECT CAST(Id AS NVARCHAR(36)) AS Value, Name AS Text FROM Warehouse WHERE CompanyId = @Cid AND IsActive = 1 AND IsDeleted = 0 ORDER BY Name",
        ["Item"]      = "SELECT CAST(Id AS NVARCHAR(36)) AS Value, Name AS Text FROM Item      WHERE CompanyId = @Cid AND IsActive = 1 AND IsDeleted = 0 ORDER BY Name",
    };

    public static string? GetQuery(string? key) => key != null && Allowed.TryGetValue(key, out var q) ? q : null;
    public static IReadOnlyCollection<string> Keys => Allowed.Keys;
}
