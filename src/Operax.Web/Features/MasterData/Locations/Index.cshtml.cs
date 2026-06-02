using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Locations;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<WarehouseDto> Warehouses { get; set; } = [];
    public IEnumerable<BinDto> Bins { get; set; } = [];
    public Guid? SelectedWhId { get; set; }

    public async Task OnGetAsync(Guid? whId)
    {
        SelectedWhId = whId;
        using var conn = db.Open();

        // Depoları getir
        Warehouses = await conn.QueryAsync<WarehouseDto>(@"
            SELECT Id, Code, Name FROM Warehouse 
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 
            ORDER BY Code", 
            new { CompanyId = company.Id });

        // Depo seçilmediyse ilk depoyu varsayılan seç
        if (whId == null && Warehouses.Any())
        {
            SelectedWhId = Warehouses.First().Id;
        }

        // Seçili depoya ait hücreleri getir
        if (SelectedWhId != null)
        {
            // CompanyId JOIN: URL manipülasyonuyla yabancı depo rafları görülemesin
            Bins = await conn.QueryAsync<BinDto>(@"
                SELECT b.Id, b.Code, b.Zone, b.IsPickingArea, b.IsReceivingArea
                FROM Bin b
                JOIN Warehouse w ON w.Id = b.WarehouseId
                WHERE b.WarehouseId = @WarehouseId AND w.CompanyId = @CompanyId AND b.IsDeleted = 0
                ORDER BY b.SortNo, b.Code",
                new { WarehouseId = SelectedWhId, CompanyId = company.Id });
        }
    }

    public record WarehouseDto(Guid Id, string Code, string Name);
    public record BinDto(Guid Id, string Code, string Zone, bool IsPickingArea, bool IsReceivingArea);
}
