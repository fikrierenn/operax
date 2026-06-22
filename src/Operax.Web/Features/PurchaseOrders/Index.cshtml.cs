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
public class IndexModel(Db db, ICurrentCompany company, IDictionaryLabels labels) : PageModel
{
    // Query-string parametreleri: ?tab=DRAFT&q=...
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public string? Q  { get; set; }

    public List<OrderDto>   Orders        { get; set; } = [];
    public StatusCountsDto  StatusCounts  { get; set; } = new(0, 0, 0, 0);
    public decimal          TotalActive   { get; set; }

    // Sayfalama (PF-1) — transactional liste varyantı (liste + count + aktif toplam)
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        await LoadStatusCountsAsync(conn, p, ct);
        await LoadOrdersAsync(conn, ct);
    }

    // Sekme rozet sayıları için durum bazlı toplam evrak adedi
    private async Task LoadStatusCountsAsync(System.Data.IDbConnection conn, object p, CancellationToken ct)
    {
        const string sql = @"
            SELECT
                COUNT(*) AS Total,
                SUM(CASE WHEN Status = @StDraft                        THEN 1 ELSE 0 END) AS Draft,
                SUM(CASE WHEN Status IN (@StPosted, @StApproved)       THEN 1 ELSE 0 END) AS Posted,
                SUM(CASE WHEN Status = @StCancelled                    THEN 1 ELSE 0 END) AS Cancelled
            FROM PurchaseOrderHeader
            WHERE CompanyId = @CompanyId AND IsDeleted = 0";
        // İş kuralı: durum parametreleri DocStatus sabitleriyle beslenir, magic string yok
        StatusCounts = await conn.QuerySingleAsync<StatusCountsDto>(new CommandDefinition(sql, new
        {
            ((dynamic)p).CompanyId,
            StDraft     = DocStatus.Draft,
            StPosted    = DocStatus.Posted,
            StApproved  = DocStatus.Approved,
            StCancelled = DocStatus.Cancelled
        }, cancellationToken: ct));
    }

    // Sekme + arama filtresine göre evrak listesi (header total = sum of QtyOrdered * Price)
    private async Task LoadOrdersAsync(System.Data.IDbConnection conn, CancellationToken ct)
    {
        var page = Page < 1 ? 1 : Page;
        var parms = new DynamicParameters();
        parms.Add("CompanyId", company.Id);
        parms.Add("Page", page);
        parms.Add("PageSize", PageSize);
        // Sekme + arama filtresi tek yerde kurulur — liste, count/aktif-toplam ve export ortak kullanır
        var filter = BuildOrderFilter(parms);

        // İş kuralı: SARGable filtre; sayfa satırları + (toplam adet + iptal-hariç aktif tutar) tek round-trip
        var sql = $@"
            ;WITH HeaderTotals AS (
                SELECT HeaderId, SUM(QtyOrdered * Price) AS LineTotal, COUNT(*) AS LineCount
                FROM PurchaseOrderLine GROUP BY HeaderId
            )
            SELECT
                h.Id, h.OrderNo, h.OrderDate, h.Status,
                p.Name AS PartnerName, p.Code AS PartnerCode, c.Name AS PartnerCity, p.TaxNumber,
                ISNULL(ht.LineTotal, 0) AS TotalAmount, ISNULL(ht.LineCount, 0) AS LineCount,
                DATEADD(DAY, ISNULL(h.PaymentTermDays, ISNULL(p.PaymentTermDays, 30)), h.OrderDate) AS DueDate
            FROM PurchaseOrderHeader h
            JOIN Partner p ON p.Id = h.PartnerId
            LEFT JOIN City c ON c.Id = p.CityId
            LEFT JOIN HeaderTotals ht ON ht.HeaderId = h.Id
            WHERE h.CompanyId = @CompanyId AND h.IsDeleted = 0{filter}
            ORDER BY h.OrderDate DESC, h.OrderNo DESC
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            ;WITH HeaderTotals AS (
                SELECT HeaderId, SUM(QtyOrdered * Price) AS LineTotal FROM PurchaseOrderLine GROUP BY HeaderId
            )
            SELECT COUNT(*) AS Cnt,
                   ISNULL(SUM(CASE WHEN h.Status <> @StCancelled THEN ISNULL(ht.LineTotal, 0) ELSE 0 END), 0) AS ActiveTotal
            FROM PurchaseOrderHeader h
            JOIN Partner p ON p.Id = h.PartnerId
            LEFT JOIN HeaderTotals ht ON ht.HeaderId = h.Id
            WHERE h.CompanyId = @CompanyId AND h.IsDeleted = 0{filter};";

        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, parms, cancellationToken: ct));
        Orders = (await grid.ReadAsync<OrderDto>()).ToList();
        var agg = await grid.ReadSingleAsync<AggDto>();
        FilteredCount = agg.Cnt;
        TotalActive = agg.ActiveTotal;
    }

    // Sekme + arama filtresini DocStatus parametreleriyle kurar (liste ve export ortak kullanır)
    private string BuildOrderFilter(DynamicParameters parms)
    {
        parms.Add("StDraft",     DocStatus.Draft);
        parms.Add("StPosted",    DocStatus.Posted);
        parms.Add("StApproved",  DocStatus.Approved);
        parms.Add("StCancelled", DocStatus.Cancelled);

        var filter = "";
        if (Tab == DocStatus.Draft)          filter += " AND h.Status = @StDraft";
        else if (Tab == DocStatus.Posted)    filter += " AND h.Status IN (@StPosted, @StApproved)";
        else if (Tab == DocStatus.Cancelled) filter += " AND h.Status = @StCancelled";
        if (!string.IsNullOrWhiteSpace(Q))
        {
            filter += " AND (h.OrderNo LIKE @Q OR p.Name LIKE @Q)";
            parms.Add("Q", $"%{Q.Trim()}%");
        }
        return filter;
    }

    /// <summary>Filtrelenmiş sipariş listesini CSV olarak dışa aktarır (sayfasız — tüm eşleşen satırlar).</summary>
    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        var parms = new DynamicParameters();
        parms.Add("CompanyId", company.Id);
        var filter = BuildOrderFilter(parms);

        // Liste ile aynı projeksiyon, sayfalama yok — kullanıcı filtresi (sekme + arama) korunur
        var sql = $@"
            ;WITH HeaderTotals AS (
                SELECT HeaderId, SUM(QtyOrdered * Price) AS LineTotal, COUNT(*) AS LineCount
                FROM PurchaseOrderLine GROUP BY HeaderId
            )
            SELECT
                h.Id, h.OrderNo, h.OrderDate, h.Status,
                p.Name AS PartnerName, p.Code AS PartnerCode, c.Name AS PartnerCity, p.TaxNumber,
                ISNULL(ht.LineTotal, 0) AS TotalAmount, ISNULL(ht.LineCount, 0) AS LineCount,
                DATEADD(DAY, ISNULL(h.PaymentTermDays, ISNULL(p.PaymentTermDays, 30)), h.OrderDate) AS DueDate
            FROM PurchaseOrderHeader h
            JOIN Partner p ON p.Id = h.PartnerId
            LEFT JOIN City c ON c.Id = p.CityId
            LEFT JOIN HeaderTotals ht ON ht.HeaderId = h.Id
            WHERE h.CompanyId = @CompanyId AND h.IsDeleted = 0{filter}
            ORDER BY h.OrderDate DESC, h.OrderNo DESC";
        var rows = (await conn.QueryAsync<OrderDto>(new CommandDefinition(sql, parms, cancellationToken: ct))).ToList();

        var headers = new[] { "Sipariş No", "Tarih", "Durum", "Cari", "Cari Kodu", "Şehir", "VKN", "Tutar", "Satır", "Vade" };
        var csvRows = rows.Select(r => new object?[]
            { r.OrderNo, r.OrderDate, labels.Label("STATUS", r.Status), r.PartnerName, r.PartnerCode,
              r.PartnerCity, r.TaxNumber, r.TotalAmount, r.LineCount, r.DueDate });
        return CsvExport.ToFile($"Satinalma_Siparis_{DateTime.Today:yyyyMMdd}.csv", headers, csvRows);
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

    // Sayfalama aggregate: filtreli toplam adet + iptal-hariç aktif tutar
    private record AggDto(int Cnt, decimal ActiveTotal);
}
