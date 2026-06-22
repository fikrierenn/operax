using Dapper;
using Microsoft.Extensions.Caching.Memory;

namespace Operax.Web.Lib;

/// <summary>
/// Sözlük (DictionaryValue) tabanlı enum etiket okuyucu (Plan 42). Tek kaynak: etiket VT'den gelir,
/// kod tarafında hardcode edilmez. Şirket-özel satır global'i ezer; ikisi de yoksa ham koda düşülür
/// (asla boş). Cache 5 dk (sözlük nadir değişir; admin edit sonrası TTL ile yansır).
/// </summary>
public interface IDictionaryLabels
{
    /// <summary>type (DictionaryType.Code) + code → Türkçe etiket. context şimdilik kullanılmıyor
    /// (gelecekte gettext-msgctxt tarzı bağlam override için imza hazır). Bulunamazsa kodu döner.</summary>
    string Label(string type, string code, string? context = null);
}

public sealed class DictionaryLabels(Db db, ICurrentCompany company, IMemoryCache cache) : IDictionaryLabels
{
    private const string CacheKey = "dict-labels-v1";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public string Label(string type, string code, string? context = null)
    {
        // İş kuralı: boş kod → boş etiket (DEVİR/sentinel satırlarında SourceDocType null olabilir)
        if (string.IsNullOrEmpty(code)) return "";

        var map = GetMap();
        // Çözüm sırası: şirket-özel satır → global (CompanyId=0) → ham kod (asla boş)
        if (map.TryGetValue((company.Id, type, code), out var tr)) return tr;
        if (map.TryGetValue((Guid.Empty, type, code), out var g)) return g;
        return code;
    }

    // Tüm aktif sözlük değerlerini tek sorguda cache'e yükler. (CompanyId, TypeCode, Code) → NameTr.
    // Senkron yükleme bilinçli: etiket okuma view'larda sync çağrılır; 5 dk'da bir tek seferlik maliyet.
    private Dictionary<(Guid, string, string), string> GetMap()
        => cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            using var conn = db.Open();
            var rows = conn.Query<LabelRow>(@"
                SELECT v.CompanyId, t.Code AS TypeCode, v.Code, v.NameTr
                FROM DictionaryValue v
                JOIN DictionaryType t ON t.Id = v.TypeId
                WHERE v.IsDeleted = 0 AND ISNULL(v.IsActive, 1) = 1");

            var map = new Dictionary<(Guid, string, string), string>();
            foreach (var r in rows)
                map[(r.CompanyId, r.TypeCode, r.Code)] = r.NameTr;
            return map;
        })!;

    private sealed record LabelRow(Guid CompanyId, string TypeCode, string Code, string NameTr);
}
