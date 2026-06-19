using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Manufacturing.WorkOrders;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<RouteDto> Routes { get; set; } = [];

    [BindProperty]
    public RouteFormDto Form { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Şirkete ait üretim rotalarını ve adım sayılarını listeler
        using var conn = db.Open();
        Routes = await conn.QueryAsync<RouteDto>(@"
            SELECT
                r.Id, r.Code, r.Name, r.IsActive,
                (SELECT COUNT(*) FROM ProductRouteStep WHERE ProductRouteId = r.Id) AS StepCount
            FROM ProductRoute r
            WHERE r.CompanyId = @CompanyId
            ORDER BY r.Code",
            new { CompanyId = company.Id });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Yeni rota ekler veya mevcut rotayı günceller
        using var conn = db.Open();

        if (Form.Id == Guid.Empty)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO ProductRoute (Id, CompanyId, Code, Name, IsActive)
                VALUES (NEWID(), @CompanyId, @Code, @Name, @IsActive)",
                new { CompanyId = company.Id, Form.Code, Form.Name, Form.IsActive });
        }
        else
        {
            // CompanyId: başka şirketin rotasını güncelleyemez
            await conn.ExecuteAsync(@"
                UPDATE ProductRoute
                SET Code = @Code, Name = @Name, IsActive = @IsActive
                WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Form.Id, CompanyId = company.Id, Form.Code, Form.Name, Form.IsActive });
        }

        return RedirectToPage();
    }

    public record RouteDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public int StepCount { get; set; }
    }

    public record RouteFormDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }
}
