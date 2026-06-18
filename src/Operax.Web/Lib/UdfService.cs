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
    public async Task<IReadOnlyList<UdfFieldDef>> LoadDefinitionsAsync(string entityName)
    {
        using var conn = db.Open();
        var rows = await conn.QueryAsync<UdfFieldDef>(@"
            SELECT Id, FieldName, LabelText, FieldType, DataSourceType, DataSourceKey,
                   DefaultValue, OrderNo, IsRequired
            FROM UserFieldDefinition
            WHERE CompanyId = @CompanyId AND EntityName = @EntityName
              AND IsActive = 1 AND IsDeleted = 0
            ORDER BY OrderNo, LabelText",
            new { CompanyId = company.Id, EntityName = entityName });
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
    public string BuildValidatedJson(IFormCollection form, IReadOnlyList<UdfFieldDef> defs, out List<string> errors)
    {
        errors = new();
        var result = new Dictionary<string, string>();

        // İş kuralı: yalnızca tanımlı alanlar işlenir — form'daki başka UDF_ anahtarı yok sayılır
        foreach (var def in defs)
        {
            var raw = form["UDF_" + def.FieldName].ToString()?.Trim() ?? "";

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
                    // Faz 1: yalnız STATIC kaynak desteklenir (Açık 4: DICTIONARY/TABLE ertelendi)
                    if (def.DataSourceType != "STATIC")
                    {
                        errors.Add($"'{def.LabelText}' için desteklenmeyen veri kaynağı.");
                        break;
                    }
                    if (!GetStaticOptions(def.DataSourceKey).Contains(raw))
                    {
                        errors.Add($"'{def.LabelText}' geçersiz seçim.");
                        break;
                    }
                    result[def.FieldName] = raw;
                    break;

                default:
                    // DATE/BOOLEAN Faz 2 — Faz 1'de reddedilir
                    errors.Add($"'{def.LabelText}' alan tipi henüz desteklenmiyor.");
                    break;
            }
        }

        return JsonSerializer.Serialize(result);
    }

    // STATIC kaynak anahtarını (virgüllü liste) seçeneklere ayırır.
    public static IReadOnlyList<string> GetStaticOptions(string? dataSourceKey)
    {
        if (string.IsNullOrWhiteSpace(dataSourceKey)) return [];
        return dataSourceKey.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
