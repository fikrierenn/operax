using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.SalesInvoices;

/// <summary>
/// Satış faturaları listesi (M04).
/// Sevkiyat POSTED olunca sp_GenerateSalesInvoiceFromShipping ile otomatik üretilir
/// (Parameter.InvoiceMode = INSTANT).
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public string? Q     { get; set; }

    public List<InvoiceRowDto>  Invoices { get; set; } = [];
    public InvoiceCountsDto     Counts   { get; set; } = new(0, 0, 0, 0);
    public decimal              TotalAmount { get; set; }
    public decimal              TotalPaid   { get; set; }

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    // Fatura listesi verilerini ve sekme sayaçlarını veritabanından yükler
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
                StCancelled = DocStatus.Cancelled
            };

            Counts = await conn.QuerySingleAsync<InvoiceCountsDto>(new CommandDefinition(@"
                SELECT
                    COUNT(*) AS Total,
                    SUM(CASE WHEN Status = @StDraft     THEN 1 ELSE 0 END) AS Draft,
                    SUM(CASE WHEN Status = @StPosted    THEN 1 ELSE 0 END) AS Posted,
                    SUM(CASE WHEN Status = @StCancelled THEN 1 ELSE 0 END) AS Cancelled
                FROM SalesInvoice
                WHERE CompanyId = @CompanyId AND IsDeleted = 0", p, cancellationToken: ct));

            TotalAmount = await conn.ExecuteScalarAsync<decimal>(new CommandDefinition(@"
                SELECT ISNULL(SUM(GrandTotal), 0)
                FROM SalesInvoice
                WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND Status <> @StCancelled", p, cancellationToken: ct));
            TotalPaid = await conn.ExecuteScalarAsync<decimal>(new CommandDefinition(@"
                SELECT ISNULL(SUM(PaidAmount), 0)
                FROM SalesInvoice
                WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND Status <> @StCancelled", p, cancellationToken: ct));

            var page = Page < 1 ? 1 : Page;
            const string fromWhere = @"
                FROM SalesInvoice si
                JOIN Partner p ON p.Id = si.PartnerId
                WHERE si.CompanyId = @CompanyId AND si.IsDeleted = 0";

            var parms = new DynamicParameters();
            parms.Add("CompanyId", company.Id);
            parms.Add("Page", page);
            parms.Add("PageSize", PageSize);

            var filter = "";
            if (Status != "all") { filter += " AND si.Status = @Status"; parms.Add("Status", Status); }
            if (!string.IsNullOrWhiteSpace(Q)) { filter += " AND (si.InvoiceNo LIKE @Q OR p.Name LIKE @Q)"; parms.Add("Q", $"%{Q.Trim()}%"); }

            // Sayfa satırları + aynı filtrenin toplam sayısı tek round-trip (PF-1)
            var sql = $@"
                SELECT
                    si.Id, si.InvoiceNo, si.InvoiceDate, si.DueDate,
                    si.PartnerId, p.Name AS PartnerName,
                    si.Subtotal, si.TaxAmount, si.GrandTotal, si.PaidAmount,
                    si.Status, si.EBelgeType, si.EBelgeStatus,
                    (SELECT COUNT(*) FROM SalesInvoiceLine WHERE InvoiceId = si.Id) AS LineCount,
                    DATEDIFF(DAY, si.DueDate, GETUTCDATE()) AS DaysOverdue
                {fromWhere}{filter}
                ORDER BY si.InvoiceDate DESC, si.InvoiceNo DESC
                OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

                SELECT COUNT(*) {fromWhere}{filter};";

            using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, parms, cancellationToken: ct));
            Invoices = (await grid.ReadAsync<InvoiceRowDto>()).ToList();
            FilteredCount = await grid.ReadSingleAsync<int>();
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Satış faturaları liste veri yükleme hatası");
            TempData["Error"] = "Veriler yüklenirken bir hata oluştu.";
        }
    }

    public record InvoiceRowDto(
        Guid     Id,
        string   InvoiceNo,
        DateTime InvoiceDate,
        DateTime? DueDate,
        Guid     PartnerId,
        string   PartnerName,
        decimal  Subtotal,
        decimal  TaxAmount,
        decimal  GrandTotal,
        decimal  PaidAmount,
        string   Status,
        string?  EBelgeType,
        string?  EBelgeStatus,
        int      LineCount,
        int      DaysOverdue);

    public record InvoiceCountsDto(int Total, int Draft, int Posted, int Cancelled);
}
