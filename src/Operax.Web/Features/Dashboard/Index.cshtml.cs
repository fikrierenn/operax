using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Dashboard;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    // Operasyon KPI'ları
    public int PendingShipments       { get; set; }
    public int ActiveReceivings       { get; set; }
    public int ActiveProductionOrders { get; set; }
    public int LowStockItems          { get; set; }
    public int OpenPickTasks          { get; set; }
    public int OpenSalesOrders        { get; set; }
    public int ExpiredLots            { get; set; }
    public int ExpiringSoonLots       { get; set; }

    // Bugünkü aktivite
    public int TodayReceivingPosted { get; set; }
    public int TodayShippingPosted  { get; set; }
    public int TodayTransferPosted  { get; set; }

    // Stok özeti
    public int TotalSkus            { get; set; }
    public int StockLocations       { get; set; }

    // Listeler
    public List<ActivityDto>    RecentActivities  { get; set; } = [];
    public List<LowStockDto>    LowStockList      { get; set; } = [];
    public List<DailyTrendDto>  WeeklyTrend       { get; set; } = [];

    public async Task OnGetAsync()
    {
        // Dashboard KPI ve listeleri tek seferde yükler — performans için tüm sorgular paralel
        using var conn = db.Open();

        // — Operasyon sayaçları —
        var tasks = new[]
        {
            conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM ShippingHeader WHERE CompanyId = @CompanyId AND Status = 'DRAFT'",
                new { CompanyId = company.Id }),
            conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM PurchaseOrderHeader WHERE CompanyId = @CompanyId AND Status = 'APPROVED'",
                new { CompanyId = company.Id }),
            conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM ProductionOrder WHERE CompanyId = @CompanyId AND Status IN ('DRAFT','IN_PROGRESS')",
                new { CompanyId = company.Id }),
            conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(DISTINCT ItemId) FROM tvf_InventoryBalance(@CompanyId) WHERE QtyBalance < 10",
                new { CompanyId = company.Id }),
            conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM PickTask WHERE CompanyId = @CompanyId AND Status IN ('DRAFT','ASSIGNED','IN_PROGRESS')",
                new { CompanyId = company.Id }),
            conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM SalesOrderHeader WHERE CompanyId = @CompanyId AND Status = 'APPROVED'",
                new { CompanyId = company.Id }),
            conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM ItemLot WHERE CompanyId = @CompanyId AND ExpiryDate < GETUTCDATE() AND Status = 'AVAILABLE'",
                new { CompanyId = company.Id }),
            conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM ItemLot WHERE CompanyId = @CompanyId AND ExpiryDate >= GETUTCDATE() AND ExpiryDate <= DATEADD(DAY, 30, GETUTCDATE()) AND Status = 'AVAILABLE'",
                new { CompanyId = company.Id }),
        };

        await Task.WhenAll(tasks);

        PendingShipments       = await tasks[0];
        ActiveReceivings       = await tasks[1];
        ActiveProductionOrders = await tasks[2];
        LowStockItems          = await tasks[3];
        OpenPickTasks          = await tasks[4];
        OpenSalesOrders        = await tasks[5];
        ExpiredLots            = await tasks[6];
        ExpiringSoonLots       = await tasks[7];

        // — Bugünkü aktivite (POSTED belgeler) —
        TodayReceivingPosted = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ReceivingHeader WHERE CompanyId = @CompanyId AND Status = 'POSTED' AND CAST(UpdatedAt AS DATE) = CAST(GETUTCDATE() AS DATE)",
            new { CompanyId = company.Id });
        TodayShippingPosted = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ShippingHeader WHERE CompanyId = @CompanyId AND Status = 'POSTED' AND CAST(UpdatedAt AS DATE) = CAST(GETUTCDATE() AS DATE)",
            new { CompanyId = company.Id });
        TodayTransferPosted = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM StockTransfer WHERE CompanyId = @CompanyId AND Status = 'POSTED' AND CAST(UpdatedAt AS DATE) = CAST(GETUTCDATE() AS DATE)",
            new { CompanyId = company.Id });

        // — Stok özet —
        TotalSkus       = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(DISTINCT ItemId) FROM tvf_InventoryBalance(@CompanyId)",
            new { CompanyId = company.Id });
        StockLocations  = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(DISTINCT BinId) FROM tvf_InventoryBalance(@CompanyId) WHERE BinId IS NOT NULL",
            new { CompanyId = company.Id });

        // — Son 20 hareket —
        RecentActivities = (await conn.QueryAsync<ActivityDto>(@"
            SELECT TOP 20
                sm.CreatedAt, i.Code AS ItemCode, i.Name AS ItemName,
                sm.MovementType, sm.QtyBase AS Qty,
                sm.SourceDoc, w.Code AS WhCode, b.Code AS BinCode
            FROM StockMovement sm
            JOIN Item i ON i.Id = sm.ItemId
            LEFT JOIN Warehouse w ON w.Id = sm.WarehouseId
            LEFT JOIN Bin b ON b.Id = sm.BinId
            WHERE sm.CompanyId = @CompanyId AND sm.IsCancelled = 0
            ORDER BY sm.CreatedAt DESC",
            new { CompanyId = company.Id })).ToList();

        // — Kritik stok listesi (en düşük 10) —
        LowStockList = (await conn.QueryAsync<LowStockDto>(@"
            SELECT TOP 10
                inv.ItemId, i.Code AS ItemCode, i.Name AS ItemName,
                SUM(inv.QtyBalance) AS QtyBalance
            FROM tvf_InventoryBalance(@CompanyId) inv
            JOIN Item i ON i.Id = inv.ItemId
            WHERE inv.QtyBalance < 10
            GROUP BY inv.ItemId, i.Code, i.Name
            ORDER BY SUM(inv.QtyBalance) ASC",
            new { CompanyId = company.Id })).ToList();

        // — Son 7 gün hareket trendi (gün bazlı giriş/çıkış) —
        WeeklyTrend = (await conn.QueryAsync<DailyTrendDto>(@"
            SELECT
                CAST(sm.CreatedAt AS DATE) AS TrendDate,
                SUM(CASE WHEN sm.QtyBase > 0 THEN sm.QtyBase ELSE 0 END) AS QtyIn,
                SUM(CASE WHEN sm.QtyBase < 0 THEN ABS(sm.QtyBase) ELSE 0 END) AS QtyOut,
                COUNT(*) AS MoveCount
            FROM StockMovement sm
            WHERE sm.CompanyId = @CompanyId AND sm.IsCancelled = 0
              AND sm.CreatedAt >= DATEADD(DAY, -7, GETUTCDATE())
            GROUP BY CAST(sm.CreatedAt AS DATE)
            ORDER BY TrendDate",
            new { CompanyId = company.Id })).ToList();
    }

    public record ActivityDto
    {
        public DateTime CreatedAt    { get; set; }
        public string   ItemCode     { get; set; } = "";
        public string   ItemName     { get; set; } = "";
        public string   MovementType { get; set; } = "";
        public decimal  Qty          { get; set; }
        public string?  SourceDoc    { get; set; }
        public string?  WhCode       { get; set; }
        public string?  BinCode      { get; set; }
    }

    public record LowStockDto
    {
        public Guid    ItemId     { get; set; }
        public string  ItemCode   { get; set; } = "";
        public string  ItemName   { get; set; } = "";
        public decimal QtyBalance { get; set; }
    }

    public record DailyTrendDto
    {
        public DateTime TrendDate  { get; set; }
        public decimal  QtyIn      { get; set; }
        public decimal  QtyOut     { get; set; }
        public int      MoveCount  { get; set; }
    }
}
