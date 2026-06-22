using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.SalesOrders;

/// <summary>
/// Satış Siparişleri liste sayfası.
/// Sekme (durum) ve serbest metin araması destekler.
/// Tüm sayısal değerler veritabanından gelir; hardcoded fallback yoktur.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public string? Q  { get; set; }

    public List<OrderDto>   Orders        { get; set; } = [];
    public StatusCountsDto  StatusCounts  { get; set; } = new(0, 0, 0, 0);
    public decimal          TotalActive   { get; set; }

    // Sipariş listesi ve sekme sayaçlarını veritabanından yükler
    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            using var conn = db.Open();
            // DocStatus sabitleri parametre olarak geçilir; SQL içinde literal yasak
            var p = new
            {
                CompanyId   = company.Id,
                StDraft     = DocStatus.Draft,
                StPosted    = DocStatus.Posted,
                StCancelled = DocStatus.Cancelled,
                StApproved  = DocStatus.Approved
            };

            await LoadStatusCountsAsync(conn, p, ct);
            await LoadOrdersAsync(conn, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Satış siparişleri liste veri yükleme hatası");
            TempData["Error"] = "Veriler yüklenirken bir hata oluştu.";
        }
    }

    private async Task LoadStatusCountsAsync(System.Data.IDbConnection conn, object p, CancellationToken ct)
    {
        // İş kuralı: SalesOrder için APPROVED durumu POSTED ile eşdeğer sayılır
        const string sql = @"
            SELECT
                COUNT(*) AS Total,
                SUM(CASE WHEN Status = @StDraft                        THEN 1 ELSE 0 END) AS Draft,
                SUM(CASE WHEN Status IN (@StApproved, @StPosted)       THEN 1 ELSE 0 END) AS Posted,
                SUM(CASE WHEN Status = @StCancelled                    THEN 1 ELSE 0 END) AS Cancelled
            FROM SalesOrderHeader
            WHERE CompanyId = @CompanyId AND IsDeleted = 0";
        StatusCounts = await conn.QuerySingleAsync<StatusCountsDto>(
            new CommandDefinition(sql, p, cancellationToken: ct));
    }

    private async Task LoadOrdersAsync(System.Data.IDbConnection conn, CancellationToken ct)
    {
        var sql = @"
            ;WITH HeaderTotals AS (
                SELECT HeaderId, SUM(QtyOrdered * Price) AS LineTotal, COUNT(*) AS LineCount
                FROM SalesOrderLine
                GROUP BY HeaderId
            )
            SELECT TOP 200
                h.Id, h.OrderNo, h.OrderDate, h.Status, h.RequestedDeliveryDate AS DueDate,
                p.Name AS PartnerName,
                p.Code AS PartnerCode,
                c.Name AS PartnerCity,
                p.TaxNumber,
                ISNULL(ht.LineTotal, 0) AS TotalAmount,
                ISNULL(ht.LineCount, 0) AS LineCount
            FROM SalesOrderHeader h
            JOIN Partner p ON p.Id = h.PartnerId
            LEFT JOIN City c ON c.Id = p.CityId
            LEFT JOIN HeaderTotals ht ON ht.HeaderId = h.Id
            WHERE h.CompanyId = @CompanyId AND h.IsDeleted = 0";

        var parms = new DynamicParameters();
        parms.Add("CompanyId", company.Id);
        // Sekme + arama filtresi tek yerde kurulur (liste ve export ortak kullanır)
        sql += BuildOrderFilter(parms);
        sql += " ORDER BY h.OrderDate DESC, h.OrderNo DESC";

        var rows = (await conn.QueryAsync<OrderDto>(
            new CommandDefinition(sql, parms, cancellationToken: ct))).ToList();
        Orders = rows;

        TotalActive = rows.Where(r => r.Status != DocStatus.Cancelled).Sum(r => r.TotalAmount);
    }

    // Sekme + arama filtresini DocStatus parametreleriyle kurar (liste ve export ortak kullanır)
    private string BuildOrderFilter(DynamicParameters parms)
    {
        parms.Add("StDraft",     DocStatus.Draft);
        parms.Add("StPosted",    DocStatus.Posted);
        parms.Add("StCancelled", DocStatus.Cancelled);
        parms.Add("StApproved",  DocStatus.Approved);

        var filter = "";
        if (Tab == DocStatus.Draft)          filter += " AND h.Status = @StDraft";
        else if (Tab == DocStatus.Posted)    filter += " AND h.Status IN (@StApproved, @StPosted)";
        else if (Tab == DocStatus.Cancelled) filter += " AND h.Status = @StCancelled";
        if (!string.IsNullOrWhiteSpace(Q))
        {
            filter += " AND (h.OrderNo LIKE @Q OR p.Name LIKE @Q)";
            parms.Add("Q", $"%{Q.Trim()}%");
        }
        return filter;
    }

    /// <summary>Filtrelenmiş satış siparişi listesini CSV olarak dışa aktarır (sayfasız).</summary>
    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        var parms = new DynamicParameters();
        parms.Add("CompanyId", company.Id);
        var filter = BuildOrderFilter(parms);

        // Liste ile aynı projeksiyon, TOP/sayfalama yok — kullanıcı filtresi (sekme + arama) korunur
        var sql = $@"
            ;WITH HeaderTotals AS (
                SELECT HeaderId, SUM(QtyOrdered * Price) AS LineTotal, COUNT(*) AS LineCount
                FROM SalesOrderLine GROUP BY HeaderId
            )
            SELECT
                h.Id, h.OrderNo, h.OrderDate, h.Status, h.RequestedDeliveryDate AS DueDate,
                p.Name AS PartnerName, p.Code AS PartnerCode, c.Name AS PartnerCity, p.TaxNumber,
                ISNULL(ht.LineTotal, 0) AS TotalAmount, ISNULL(ht.LineCount, 0) AS LineCount
            FROM SalesOrderHeader h
            JOIN Partner p ON p.Id = h.PartnerId
            LEFT JOIN City c ON c.Id = p.CityId
            LEFT JOIN HeaderTotals ht ON ht.HeaderId = h.Id
            WHERE h.CompanyId = @CompanyId AND h.IsDeleted = 0{filter}
            ORDER BY h.OrderDate DESC, h.OrderNo DESC";
        var rows = (await conn.QueryAsync<OrderDto>(new CommandDefinition(sql, parms, cancellationToken: ct))).ToList();

        var headers = new[] { "Sipariş No", "Tarih", "Durum", "Cari", "Cari Kodu", "Şehir", "VKN", "Tutar", "Satır", "Teslim" };
        var csvRows = rows.Select(r => new object?[]
            { r.OrderNo, r.OrderDate, UiHelpers.StatusText(r.Status), r.PartnerName, r.PartnerCode,
              r.PartnerCity, r.TaxNumber, r.TotalAmount, r.LineCount, r.DueDate });
        return CsvExport.ToFile($"Satis_Siparis_{DateTime.Today:yyyyMMdd}.csv", headers, csvRows);
    }

    public record OrderDto(
        Guid      Id,
        string    OrderNo,
        DateTime  OrderDate,
        string    Status,
        DateTime? DueDate,
        string    PartnerName,
        string    PartnerCode,
        string?   PartnerCity,
        string?   TaxNumber,
        decimal   TotalAmount,
        int       LineCount);

    public record StatusCountsDto(int Total, int Draft, int Posted, int Cancelled);
}
