using Dapper;
using Microsoft.Extensions.Caching.Memory;

namespace Operax.Web.Lib;

/// <summary>
/// Şirket-seviyesi modül aktivasyonu (CompanyModule / Module M00-M17) erişimi.
/// Rol-seviyesi yetkiden (Authz.ModuleAccessHandler / RoleModuleAccess) AYRIDIR:
/// bu, modülün şirket için açık/kapalı (lisans/aktivasyon) olup olmadığını söyler.
/// Etkin kural (opt-out): bir modül, CompanyModule'de açıkça IsActive=0 satırı YOKSA AÇIK sayılır.
/// </summary>
public interface ICompanyModuleAccess
{
    /// <summary>Kapalı modül kodlarını şirket için yükler (cache'li). View render öncesi çağrılır.</summary>
    Task EnsureLoadedAsync(CancellationToken ct = default);

    /// <summary>Modül şirket için açık mı? (yüklenmemişse hepsi açık varsayılır).</summary>
    bool IsActive(string moduleCode);
}

/// <summary>CompanyModule tablosundan kapalı modül setini şirket-başına cache'le sağlar.</summary>
public sealed class CompanyModuleAccess(Db db, ICurrentCompany company, IMemoryCache cache) : ICompanyModuleAccess
{
    private HashSet<string> _offCodes = [];
    private bool _loaded;

    // Şirket-başına cache anahtarı (modül aç/kapa POST'unda bununla invalidate edilir)
    public static string CacheKey(Guid companyId) => $"cmod:{companyId}";

    // Kapalı (IsActive=0) modül kodlarını yükler — şirket başına 5 dk TTL cache, cold'da tek async sorgu.
    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        // Guard: zaten yüklendi veya kimlik/şirket bağlamı yok (login vb.) → tümü açık
        if (_loaded) return;
        if (company.Id == Guid.Empty) { _loaded = true; return; }

        _offCodes = await cache.GetOrCreateAsync(CacheKey(company.Id), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using var conn = db.Open();
            // İş kuralı: yalnızca açıkça kapatılmış (IsActive=0) modüller toplanır — satır yoksa açık
            var off = await conn.QueryAsync<string>(new CommandDefinition(@"
                SELECT m.Code
                FROM Module m
                JOIN CompanyModule cm ON cm.ModuleId = m.Id AND cm.CompanyId = @CompanyId
                WHERE cm.IsActive = 0",
                new { CompanyId = company.Id }, cancellationToken: ct));
            return off.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }) ?? [];

        _loaded = true;
    }

    // Etkin durum: açıkça kapatılmamış her modül açık
    public bool IsActive(string moduleCode) => !_offCodes.Contains(moduleCode);
}
