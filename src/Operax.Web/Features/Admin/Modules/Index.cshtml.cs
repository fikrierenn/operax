using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace Operax.Web.Features.Admin.Modules;

[Authorize(Roles = "Administrator")]
public class IndexModel(Db db, ICurrentCompany company, IMemoryCache cache) : PageModel
{
    public IEnumerable<ModuleDto> Modules { get; set; } = [];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    // Modül listesini sayfalayarak getirir; CompanyModule LEFT JOIN ile şirket etkin durumunu belirler (opt-out: satır yoksa açık)
    public async Task OnGetAsync(CancellationToken ct = default)
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        const string sql = @"
            SELECT m.Id, m.Code, m.NameTr, m.NameEn,
                   -- Etkin durum (opt-out): CompanyModule satırı yoksa (NULL) modül AÇIK; yalnız açık IsActive=0 kapalı
                   CAST(CASE WHEN cm.CompanyId IS NULL OR cm.IsActive = 1 THEN 1 ELSE 0 END AS BIT) as IsActive
            FROM Module m
            LEFT JOIN CompanyModule cm ON cm.ModuleId = m.Id AND cm.CompanyId = @CompanyId
            WHERE m.IsActive = 1
            ORDER BY m.Code
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM Module WHERE IsActive = 1;";

        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
        Modules = (await grid.ReadAsync<ModuleDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
    }

    // Modül etkinlik durumunu günceller (UPSERT) ve sidebar görünürlük cache'ini düşürür
    public async Task<IActionResult> OnPostAsync(Guid moduleId, bool active, CancellationToken ct = default)
    {
        using var conn = db.Open();

        const string sql = @"
            IF EXISTS (SELECT 1 FROM CompanyModule WHERE CompanyId = @CompanyId AND ModuleId = @ModuleId)
                UPDATE CompanyModule SET IsActive = @Active WHERE CompanyId = @CompanyId AND ModuleId = @ModuleId
            ELSE
                INSERT INTO CompanyModule (CompanyId, ModuleId, IsActive) VALUES (@CompanyId, @ModuleId, @Active)";

        await conn.ExecuteAsync(new CommandDefinition(sql, new { CompanyId = company.Id, ModuleId = moduleId, Active = active }, cancellationToken: ct));

        // Sidebar görünürlük cache'ini düşür — değişiklik anında yansısın (Faz 3)
        cache.Remove(CompanyModuleAccess.CacheKey(company.Id));

        return RedirectToPage();
    }

    public record ModuleDto(Guid Id, string Code, string NameTr, string NameEn, bool IsActive);
}
