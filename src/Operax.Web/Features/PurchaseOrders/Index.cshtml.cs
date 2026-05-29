using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.PurchaseOrders;

/// <summary>
/// Satınalma Siparişleri liste sayfası.
/// Sekme (durum) ve serbest metin araması destekler.
/// Tüm sayısal değerler veritabanından gelir; hardcoded fallback yoktur.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    // Query-string parametreleri: ?tab=DRAFT&q=...
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public string? Q  { get; set; }

    public List<OrderDto>   Orders        { get; set; } = [];
    public StatusCountsDto  StatusCounts  { get; set; } = new(0, 0, 0, 0);
    public decimal          TotalActive   { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        await LoadStatusCountsAsync(conn, p);
        await LoadOrdersAsync(conn);
    }

    // Sekme rozet sayıları için durum bazlı toplam evrak adedi
    private async Task LoadStatusCountsAsync(System.Data.IDbConnection conn, object p)
    {
        const string sql = @"
            SELECT
                COUNT(*) AS Total,
                SUM(CASE WHEN Status = 'DRAFT'                 THEN 1 ELSE 0 END) AS Draft,
                SUM(CASE WHEN Status IN ('POSTED','APPROVED')  THEN 1 ELSE 0 END) AS Posted, -- DocStatus.Posted|Approved
                SUM(CASE WHEN Status = 'CANCELLED'             THEN 1 ELSE 0 END) AS Cancelled
            FROM PurchaseOrderHeader
            WHERE CompanyId = @CompanyId AND IsDeleted = 0";
        StatusCounts = await conn.QuerySingleAsync<StatusCountsDto>(sql, p);
    }

    // Sekme + arama filtresine göre evrak listesi (header total = sum of QtyOrdered * Price)
    private async Task LoadOrdersAsync(System.Data.IDbConnection conn)
    {
        // İş kuralı: Filtreler SARGable kalsın diye Status WHERE'de parametre ile karşılaştırılır
        var sql = @"
            ;WITH HeaderTotals AS (
                SELECT HeaderId, SUM(QtyOrdered * Price) AS LineTotal, COUNT(*) AS LineCount
                FROM PurchaseOrderLine
                GROUP BY HeaderId
            )
            SELECT TOP 200
                h.Id, h.OrderNo, h.OrderDate, h.Status,
                p.Name AS PartnerName,
                p.Code AS PartnerCode,
                c.Name AS PartnerCity,
                p.TaxNumber,
                ISNULL(ht.LineTotal, 0) AS TotalAmount,
                ISNULL(ht.LineCount, 0) AS LineCount,
                DATEADD(DAY, 14, h.OrderDate) AS DueDate
            FROM PurchaseOrderHeader h
            JOIN Partner p ON p.Id = h.PartnerId
            LEFT JOIN City c ON c.Id = p.CityId
            LEFT JOIN HeaderTotals ht ON ht.HeaderId = h.Id
            WHERE h.CompanyId = @CompanyId AND h.IsDeleted = 0";

        var parms = new DynamicParameters();
        parms.Add("CompanyId", company.Id);

        // Sekme filtresi: Tab değeri DocStatus sabitleri ile eşlenir
        if (Tab == DocStatus.Draft)
            sql += " AND h.Status = @Status";
        else if (Tab == DocStatus.Posted)
            sql += $" AND h.Status IN ('{DocStatus.Posted}','{DocStatus.Approved}')";
        else if (Tab == DocStatus.Cancelled)
            sql += " AND h.Status = @Status";

        if (Tab != "all" && Tab != DocStatus.Posted)
            parms.Add("Status", Tab);

        // Serbest metin arama: evrak no veya tedarikçi adı
        if (!string.IsNullOrWhiteSpace(Q))
        {
            sql += " AND (h.OrderNo LIKE @Q OR p.Name LIKE @Q)";
            parms.Add("Q", $"%{Q.Trim()}%");
        }

        sql += " ORDER BY h.OrderDate DESC, h.OrderNo DESC";

        var rows = (await conn.QueryAsync<OrderDto>(sql, parms)).ToList();
        Orders = rows;

        // İş kuralı: İptal hariç toplam aktif tutar
        TotalActive = rows.Where(r => r.Status != DocStatus.Cancelled).Sum(r => r.TotalAmount);
    }

    // ─── DTO'lar ────────────────────────────────────────────────
    public record OrderDto(
        Guid     Id,
        string   OrderNo,
        DateTime OrderDate,
        string   Status,
        string   PartnerName,
        string   PartnerCode,
        string?  PartnerCity,
        string?  TaxNumber,
        decimal  TotalAmount,
        int      LineCount,
        DateTime DueDate);

    public record StatusCountsDto(int Total, int Draft, int Posted, int Cancelled);
}
