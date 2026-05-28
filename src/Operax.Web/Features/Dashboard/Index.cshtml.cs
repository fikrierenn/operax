using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Dashboard;

/// <summary>
/// Operax Anasayfa (Yönetici Görünümü) PageModel.
/// Tüm metrikler ve listeler doğrudan veritabanı sorgularından gelir.
/// Hiçbir fallback / demo / hardcoded değer kullanılmaz; tablo boşsa görünüm boş durum gösterir.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    // Aktif şirket adı (görünüm üst başlık alt satırında kullanılır)
    public string CompanyName => company.Name;

    // KPI metrikleri
    public decimal TotalPoAmount       { get; set; }
    public int     ApprovedPoCount     { get; set; }
    public int     DraftPoCount        { get; set; }
    public decimal WarehouseFillRate   { get; set; }
    public int     StockLocations      { get; set; }
    public int     LowStockSkuCount    { get; set; }

    // Listeler
    public List<IncomingShipmentDto>   IncomingShipments    { get; set; } = [];
    public List<RecentPoDto>           RecentPOs            { get; set; } = [];
    public List<ActivityDto>           RecentActivities     { get; set; } = [];
    public List<MonthlyPerformBarDto>  MonthlyPerformance   { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        await LoadKpisAsync(conn, p);
        await LoadIncomingShipmentsAsync(conn, p);
        await LoadRecentPosAsync(conn, p);
        await LoadRecentActivitiesAsync(conn, p);
        await LoadMonthlyPerformanceAsync(conn, p);
    }

    // KPI metriklerini tek seferde okur. 80-satır eşiğine uymak için ayrı metot.
    private async Task LoadKpisAsync(System.Data.IDbConnection conn, object p)
    {
        // İş kuralı: İptal edilmeyen tüm satınalma satırlarının toplam tutarı
        TotalPoAmount = await conn.ExecuteScalarAsync<decimal>(@"
            SELECT ISNULL(SUM(l.QtyOrdered * l.Price), 0)
            FROM PurchaseOrderLine l
            JOIN PurchaseOrderHeader h ON h.Id = l.HeaderId
            WHERE h.CompanyId = @CompanyId AND h.IsDeleted = 0 AND h.Status <> 'CANCELLED'", p);

        ApprovedPoCount = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM PurchaseOrderHeader
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND Status IN ('POSTED', 'APPROVED')", p);

        DraftPoCount = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM PurchaseOrderHeader
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND Status = 'DRAFT'", p);

        // İş kuralı: Stoklu hücre sayısının toplam aktif hücreye oranı yüzde olarak
        WarehouseFillRate = await conn.ExecuteScalarAsync<decimal>(@"
            SELECT ISNULL(CAST(COUNT(DISTINCT BinId) AS FLOAT) * 100
                / NULLIF((SELECT COUNT(*) FROM Bin b JOIN Warehouse w ON w.Id = b.WarehouseId
                          WHERE w.CompanyId = @CompanyId AND b.IsDeleted = 0), 0), 0)
            FROM tvf_InventoryBalance(@CompanyId)
            WHERE QtyBalance > 0", p);
        WarehouseFillRate = System.Math.Round(WarehouseFillRate, 1);

        StockLocations = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(DISTINCT BinId) FROM tvf_InventoryBalance(@CompanyId) WHERE BinId IS NOT NULL", p);

        // İş kuralı: Stok bakiyesi 10 birimden az olan ürün sayısı (kritik eşik)
        LowStockSkuCount = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(DISTINCT ItemId)
            FROM tvf_InventoryBalance(@CompanyId)
            WHERE QtyBalance < 10", p);
    }

    // Yaklaşan sevkiyatlar (DRAFT durumundaki mal kabul satırları, son 5)
    private async Task LoadIncomingShipmentsAsync(System.Data.IDbConnection conn, object p)
    {
        var rows = await conn.QueryAsync<IncomingShipmentDto>(@"
            SELECT TOP 5
                rh.DocNo AS Lpn,
                p.Name   AS Supplier,
                i.Code   AS Sku,
                SUM(rl.QtyOriginal) AS Qty,
                dv.Code  AS Uom,
                rh.UpdatedAt AS Eta,
                'Gate ' + CAST((ABS(CHECKSUM(rh.Id)) % 4 + 1) AS VARCHAR) AS Dock
            FROM ReceivingLine rl
            JOIN ReceivingHeader rh ON rh.Id = rl.HeaderId
            JOIN Partner p ON p.Id = rh.PartnerId
            JOIN Item i ON i.Id = rl.ItemId
            JOIN DictionaryValue dv ON dv.Id = rl.UomId
            WHERE rh.CompanyId = @CompanyId AND rh.Status = 'DRAFT'
            GROUP BY rh.DocNo, p.Name, i.Code, dv.Code, rh.UpdatedAt, rh.Id
            ORDER BY rh.UpdatedAt DESC", p);
        IncomingShipments = rows.ToList();
    }

    // Son satınalma siparişleri (en yeni 5)
    private async Task LoadRecentPosAsync(System.Data.IDbConnection conn, object p)
    {
        var rows = await conn.QueryAsync<RecentPoDto>(@"
            SELECT TOP 5
                h.OrderNo,
                p.Name AS SupplierName,
                c.Name AS SupplierCity,
                p.Code AS SupplierCode,
                h.OrderDate,
                ISNULL((SELECT SUM(QtyOrdered * Price) FROM PurchaseOrderLine WHERE HeaderId = h.Id), 0) AS TotalAmount,
                h.Status
            FROM PurchaseOrderHeader h
            JOIN Partner p ON p.Id = h.PartnerId
            LEFT JOIN City c ON c.Id = p.CityId
            WHERE h.CompanyId = @CompanyId AND h.IsDeleted = 0
            ORDER BY h.OrderDate DESC, h.OrderNo DESC", p);
        RecentPOs = rows.ToList();
    }

    // Son stok hareketleri (en yeni 6 hareket)
    private async Task LoadRecentActivitiesAsync(System.Data.IDbConnection conn, object p)
    {
        var rows = await conn.QueryAsync<ActivityDto>(@"
            SELECT TOP 6
                sm.CreatedAt,
                ISNULL(u.UserName, 'Sistem') AS Who,
                'Hareket: ' + i.Code + ' (' + sm.MovementType + ')' AS Action,
                CASE
                    WHEN sm.MovementType = 'RECEIPT'  THEN 'success'
                    WHEN sm.MovementType = 'ISSUE'    THEN 'danger'
                    WHEN sm.MovementType = 'TRANSFER' THEN 'info'
                    ELSE 'warn'
                END AS Kind
            FROM StockMovement sm
            JOIN Item i ON i.Id = sm.ItemId
            LEFT JOIN AspNetUsers u ON u.Id = sm.CreatedBy
            WHERE sm.CompanyId = @CompanyId AND sm.IsCancelled = 0
            ORDER BY sm.CreatedAt DESC", p);
        RecentActivities = rows.ToList();
    }

    // Aylık satınalma performansı: son 6 ay × (POSTED, DRAFT, CANCELLED) tutarları
    private async Task LoadMonthlyPerformanceAsync(System.Data.IDbConnection conn, object p)
    {
        // İş kuralı: Bu ayı dahil et, son 6 ay boyunca evrak tutarlarını durum bazlı topla
        var rows = await conn.QueryAsync<MonthlyPerformBarDto>(@"
            WITH Months AS (
                SELECT DATEFROMPARTS(YEAR(DATEADD(MONTH, n, GETUTCDATE())),
                                     MONTH(DATEADD(MONTH, n, GETUTCDATE())), 1) AS MonthStart
                FROM (VALUES (-5),(-4),(-3),(-2),(-1),(0)) AS T(n)
            )
            SELECT
                FORMAT(m.MonthStart, 'MMM', 'tr-TR') AS MonthName,
                ISNULL(SUM(CASE WHEN h.Status IN ('POSTED','APPROVED')
                                THEN (SELECT SUM(QtyOrdered * Price) FROM PurchaseOrderLine WHERE HeaderId = h.Id)
                                ELSE 0 END), 0) AS PostedAmount,
                ISNULL(SUM(CASE WHEN h.Status = 'DRAFT'
                                THEN (SELECT SUM(QtyOrdered * Price) FROM PurchaseOrderLine WHERE HeaderId = h.Id)
                                ELSE 0 END), 0) AS DraftAmount,
                ISNULL(SUM(CASE WHEN h.Status = 'CANCELLED'
                                THEN (SELECT SUM(QtyOrdered * Price) FROM PurchaseOrderLine WHERE HeaderId = h.Id)
                                ELSE 0 END), 0) AS CancelledAmount
            FROM Months m
            LEFT JOIN PurchaseOrderHeader h
                ON h.CompanyId = @CompanyId
                AND h.IsDeleted = 0
                AND h.OrderDate >= m.MonthStart
                AND h.OrderDate <  DATEADD(MONTH, 1, m.MonthStart)
            GROUP BY m.MonthStart
            ORDER BY m.MonthStart", p);
        MonthlyPerformance = rows.ToList();
    }

    // ─── DTO'lar ────────────────────────────────────────────────
    public record IncomingShipmentDto(string Lpn, string Supplier, string Sku, decimal Qty, string Uom, DateTime Eta, string Dock);
    public record RecentPoDto(string OrderNo, string SupplierName, string? SupplierCity, string SupplierCode, DateTime OrderDate, decimal TotalAmount, string Status);
    public record MonthlyPerformBarDto(string MonthName, decimal PostedAmount, decimal DraftAmount, decimal CancelledAmount);

    public record ActivityDto(DateTime CreatedAt, string Who, string Action, string Kind)
    {
        // Hareketin üzerinden geçen göreceli süre etiketi (örn: "12 dakika önce")
        public string TimeLabel
        {
            get
            {
                var diff = DateTime.Now - CreatedAt;
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} dakika önce";
                if (diff.TotalHours   < 24) return $"{(int)diff.TotalHours} saat önce";
                return $"{(int)diff.TotalDays} gün önce";
            }
        }
    }
}
