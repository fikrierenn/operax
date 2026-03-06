using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.MasterData.Locations;

public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<WarehouseDto> Warehouses { get; set; } = [];
    public IEnumerable<BinDto> Bins { get; set; } = [];
    public Guid? SelectedWhId { get; set; }

    public async Task OnGetAsync(Guid? whId)
    {
        SelectedWhId = whId;
        using var conn = db.Open();

        // Get Warehouses
        Warehouses = await conn.QueryAsync<WarehouseDto>(@"
            SELECT Id, Code, Name FROM Warehouse 
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 
            ORDER BY Code", 
            new { CompanyId = company.Id });

        // If no WH selected but there are warehouses, select the first one
        if (whId == null && Warehouses.Any())
        {
            SelectedWhId = Warehouses.First().Id;
        }

        // Get Bins for selected WH
        if (SelectedWhId != null)
        {
            Bins = await conn.QueryAsync<BinDto>(@"
                SELECT Id, Code, Zone, IsPickingArea, IsReceivingArea 
                FROM Bin 
                WHERE WarehouseId = @WarehouseId AND IsDeleted = 0 
                ORDER BY SortNo, Code", 
                new { WarehouseId = SelectedWhId });
        }
    }

    public record WarehouseDto(Guid Id, string Code, string Name);
    public record BinDto(Guid Id, string Code, string Zone, bool IsPickingArea, bool IsReceivingArea);
}
