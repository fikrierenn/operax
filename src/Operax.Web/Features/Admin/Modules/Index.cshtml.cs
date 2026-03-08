using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Modules;

[Authorize(Roles = "Admin")]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ModuleDto> Modules { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        Modules = await conn.QueryAsync<ModuleDto>(@"
            SELECT m.Id, m.Code, m.NameTr, m.NameEn, 
                   CAST(CASE WHEN cm.IsActive = 1 THEN 1 ELSE 0 END AS BIT) as IsActive
            FROM Module m
            LEFT JOIN CompanyModule cm ON cm.ModuleId = m.Id AND cm.CompanyId = @CompanyId
            WHERE m.IsActive = 1
            ORDER BY m.Code", 
            new { CompanyId = company.Id });
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
