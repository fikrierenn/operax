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

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Şirkete ait üretim rotalarını ve adım sayılarını listeler
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;
        const string sql = @"
            SELECT
                r.Id, r.Code, r.Name, r.IsActive,
                (SELECT COUNT(*) FROM ProductRouteStep WHERE ProductRouteId = r.Id) AS StepCount
            FROM ProductRoute r
            WHERE r.CompanyId = @CompanyId
            ORDER BY r.Code
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM ProductRoute WHERE CompanyId = @CompanyId;";
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
        Routes = (await grid.ReadAsync<RouteDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Yeni rota ekler veya mevcut rotayı günceller
        using var conn = db.Open();

        if (Form.Id == Guid.Empty)
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO ProductRoute (Id, CompanyId, Code, Name, IsActive)
                VALUES (NEWID(), @CompanyId, @Code, @Name, @IsActive)",
                new { CompanyId = company.Id, Form.Code, Form.Name, Form.IsActive }, cancellationToken: ct));
        }
        else
        {
            // CompanyId: başka şirketin rotasını güncelleyemez
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE ProductRoute
                SET Code = @Code, Name = @Name, IsActive = @IsActive
                WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Form.Id, CompanyId = company.Id, Form.Code, Form.Name, Form.IsActive }, cancellationToken: ct));
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
