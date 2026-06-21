using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Shipping;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ShipmentDto> Shipments { get; set; } = [];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public int DraftCount { get; set; } = 0;
    public int PostedCount { get; set; } = 0;
    public int CancelledCount { get; set; } = 0;

    public async Task OnGetAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        // Sayfa satırları + durum sayaçları (rows yerine SQL) tek round-trip (PF-1)
        const string sql = @"
            SELECT s.Id, s.DocNo, s.DocDate, s.Status, s.CarrierName, s.VehiclePlate, wh.Code as WarehouseCode
            FROM ShippingHeader s
            JOIN Warehouse wh ON wh.Id = s.WarehouseId
            WHERE s.CompanyId = @CompanyId AND s.IsDeleted = 0
            ORDER BY s.DocDate DESC, s.DocNo DESC
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT
                COUNT(*) AS Total,
                SUM(CASE WHEN Status = @StDraft     THEN 1 ELSE 0 END) AS Draft,
                SUM(CASE WHEN Status = @StPosted    THEN 1 ELSE 0 END) AS Posted,
                SUM(CASE WHEN Status = @StCancelled THEN 1 ELSE 0 END) AS Cancelled
            FROM ShippingHeader WHERE CompanyId = @CompanyId AND IsDeleted = 0;";

        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            CompanyId = company.Id, Page = page, PageSize,
            StDraft = DocStatus.Draft, StPosted = DocStatus.Posted, StCancelled = DocStatus.Cancelled
        }, cancellationToken: ct));
        Shipments = (await grid.ReadAsync<ShipmentDto>()).ToList();
        var c = await grid.ReadSingleAsync<CountRow>();
        FilteredCount  = c.Total;
        DraftCount     = c.Draft;
        PostedCount    = c.Posted;
        CancelledCount = c.Cancelled;
    }

    public record ShipmentDto(Guid Id, string DocNo, DateTime DocDate, string Status, string? CarrierName, string? VehiclePlate, string WarehouseCode);
    private record CountRow(int Total, int Draft, int Posted, int Cancelled);
}
