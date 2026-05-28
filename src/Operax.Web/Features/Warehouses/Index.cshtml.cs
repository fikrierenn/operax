using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Warehouses;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<WarehouseDto> Warehouses { get; set; } = [];
    
    public int ActiveWarehouses { get; set; }
    public int TotalBins { get; set; }
    public decimal AverageCapacity { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        Warehouses = await conn.QueryAsync<WarehouseDto>(
            "SELECT * FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code",
            new { CompanyId = company.Id });

        ActiveWarehouses = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Warehouse WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0",
            new { CompanyId = company.Id });

        TotalBins = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) 
            FROM Bin b 
            JOIN Warehouse w ON w.Id = b.WarehouseId 
            WHERE w.CompanyId = @CompanyId AND b.IsActive = 1 AND b.IsDeleted = 0",
            new { CompanyId = company.Id });

        int occupiedBins = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(DISTINCT BinId) 
            FROM tvf_InventoryBalance(@CompanyId) 
            WHERE BinId IS NOT NULL",
            new { CompanyId = company.Id });

        if (TotalBins > 0)
        {
            AverageCapacity = ((decimal)occupiedBins / TotalBins) * 100;
        }
        else
        {
            AverageCapacity = 0;
        }
    }

    public record WarehouseDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public bool IsActive { get; set; } }
}
