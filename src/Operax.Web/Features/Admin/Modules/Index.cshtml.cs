using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Modules;

[Authorize(Roles = "Administrator")]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ModuleDto> Modules { get; set; } = [];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        const string sql = @"
            SELECT m.Id, m.Code, m.NameTr, m.NameEn,
                   CAST(CASE WHEN cm.IsActive = 1 THEN 1 ELSE 0 END AS BIT) as IsActive
            FROM Module m
            LEFT JOIN CompanyModule cm ON cm.ModuleId = m.Id AND cm.CompanyId = @CompanyId
            WHERE m.IsActive = 1
            ORDER BY m.Code
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM Module WHERE IsActive = 1;";

        using var grid = await conn.QueryMultipleAsync(sql, new { CompanyId = company.Id, Page = page, PageSize });
        Modules = (await grid.ReadAsync<ModuleDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
    }

    public async Task<IActionResult> OnPostAsync(Guid moduleId, bool active)
    {
        using var conn = db.Open();

        const string sql = @"
            IF EXISTS (SELECT 1 FROM CompanyModule WHERE CompanyId = @CompanyId AND ModuleId = @ModuleId)
                UPDATE CompanyModule SET IsActive = @Active WHERE CompanyId = @CompanyId AND ModuleId = @ModuleId
            ELSE
                INSERT INTO CompanyModule (CompanyId, ModuleId, IsActive) VALUES (@CompanyId, @ModuleId, @Active)";

        await conn.ExecuteAsync(sql, new { CompanyId = company.Id, ModuleId = moduleId, Active = active });

        return RedirectToPage();
    }

    public record ModuleDto(Guid Id, string Code, string NameTr, string NameEn, bool IsActive);
}
