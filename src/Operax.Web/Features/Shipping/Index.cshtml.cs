using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Shipping;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ShipmentDto> Shipments { get; set; } = [];

    public int DraftCount { get; set; } = 0;
    public int PostedCount { get; set; } = 0;
    public int CancelledCount { get; set; } = 0;

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        const string sql = @"
            SELECT s.Id, s.DocNo, s.DocDate, s.Status, s.CarrierName, s.VehiclePlate, wh.Code as WarehouseCode
            FROM ShippingHeader s
            JOIN Warehouse wh ON wh.Id = s.WarehouseId
            WHERE s.CompanyId = @CompanyId AND s.IsDeleted = 0
            ORDER BY s.DocDate DESC, s.DocNo DESC";

        Shipments = await conn.QueryAsync<ShipmentDto>(sql, new { CompanyId = company.Id });

        DraftCount = Shipments.Count(x => x.Status == DocStatus.Draft);
        PostedCount = Shipments.Count(x => x.Status == DocStatus.Posted);
        CancelledCount = Shipments.Count(x => x.Status == DocStatus.Cancelled);
    }

    public record ShipmentDto(Guid Id, string DocNo, DateTime DocDate, string Status, string? CarrierName, string? VehiclePlate, string WarehouseCode);
}
