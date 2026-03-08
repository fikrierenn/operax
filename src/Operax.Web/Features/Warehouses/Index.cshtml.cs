using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Warehouses;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<WarehouseDto> Warehouses { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        Warehouses = await conn.QueryAsync<WarehouseDto>(
            "SELECT * FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code",
            new { CompanyId = company.Id });
    }

    public record WarehouseDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public bool IsActive { get; set; } }
}
