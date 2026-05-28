using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.SalesInvoices;

/// <summary>
/// Satış faturaları listesi (M04).
/// Sevkiyat POSTED olunca sp_GenerateSalesInvoiceFromShipping ile otomatik üretilir
/// (Parameter.InvoiceMode = INSTANT).
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public string? Q     { get; set; }

    public List<InvoiceRowDto>  Invoices { get; set; } = [];
    public InvoiceCountsDto     Counts   { get; set; } = new(0, 0, 0, 0);
    public decimal              TotalAmount { get; set; }
    public decimal              TotalPaid   { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        Counts = await conn.QuerySingleAsync<InvoiceCountsDto>(@"
            SELECT
                COUNT(*) AS Total,
                SUM(CASE WHEN Status = 'DRAFT'     THEN 1 ELSE 0 END) AS Draft,
                SUM(CASE WHEN Status = 'POSTED'    THEN 1 ELSE 0 END) AS Posted,
                SUM(CASE WHEN Status = 'CANCELLED' THEN 1 ELSE 0 END) AS Cancelled
            FROM SalesInvoice
            WHERE CompanyId = @CompanyId AND IsDeleted = 0", p);

        TotalAmount = await conn.ExecuteScalarAsync<decimal>(@"
            SELECT ISNULL(SUM(GrandTotal), 0)
            FROM SalesInvoice
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND Status <> 'CANCELLED'", p);
        TotalPaid = await conn.ExecuteScalarAsync<decimal>(@"
            SELECT ISNULL(SUM(PaidAmount), 0)
            FROM SalesInvoice
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND Status <> 'CANCELLED'", p);

        var sql = @"
            SELECT
                si.Id, si.InvoiceNo, si.InvoiceDate, si.DueDate,
                si.PartnerId, p.Name AS PartnerName,
                si.Subtotal, si.TaxAmount, si.GrandTotal, si.PaidAmount,
                si.Status, si.EBelgeType, si.EBelgeStatus,
                (SELECT COUNT(*) FROM SalesInvoiceLine WHERE InvoiceId = si.Id) AS LineCount,
                DATEDIFF(DAY, si.DueDate, GETUTCDATE()) AS DaysOverdue
            FROM SalesInvoice si
            JOIN Partner p ON p.Id = si.PartnerId
            WHERE si.CompanyId = @CompanyId AND si.IsDeleted = 0";

        var parms = new DynamicParameters();
        parms.Add("CompanyId", company.Id);

        if (Status != "all")
        {
            sql += " AND si.Status = @Status";
            parms.Add("Status", Status);
        }
        if (!string.IsNullOrWhiteSpace(Q))
        {
            sql += " AND (si.InvoiceNo LIKE @Q OR p.Name LIKE @Q)";
            parms.Add("Q", $"%{Q.Trim()}%");
        }

        sql += " ORDER BY si.InvoiceDate DESC, si.InvoiceNo DESC";

        Invoices = (await conn.QueryAsync<InvoiceRowDto>(sql, parms)).ToList();
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
