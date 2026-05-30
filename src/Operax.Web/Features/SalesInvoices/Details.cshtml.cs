using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.SalesInvoices;

/// <summary>
/// Fatura detayı: başlık + kalemler + e-Belge gönderim durumu + ödeme planı.
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }

    public InvoiceHeaderDto? Header { get; set; }
    public List<LineDto>     Lines  { get; set; } = [];
    public List<EnvelopeDto> Envelopes { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (Id == Guid.Empty) return RedirectToPage("Index");

        using var conn = db.Open();
        var p = new { CompanyId = company.Id, Id };

        Header = await conn.QuerySingleOrDefaultAsync<InvoiceHeaderDto>(@"
            SELECT si.Id, si.InvoiceNo, si.InvoiceDate, si.DueDate,
                   si.PartnerId, p.Name AS PartnerName, p.TaxNumber, p.TaxOffice,
                   si.Subtotal, si.TaxAmount, si.GrandTotal, si.PaidAmount,
                   si.Currency, si.Status, si.EBelgeType, si.EBelgeStatus, si.EBelgeUuid,
                   si.ShippingId, si.SalesOrderId, si.Notes
            FROM SalesInvoice si
            JOIN Partner p ON p.Id = si.PartnerId
            WHERE si.Id = @Id AND si.CompanyId = @CompanyId AND si.IsDeleted = 0", p);

        if (Header == null) return NotFound();

        Lines = (await conn.QueryAsync<LineDto>(@"
            SELECT sil.Id, sil.ItemId, i.Code AS ItemCode, i.Name AS ItemName,
                   sil.Description, sil.UomId, dv.Code AS UomCode,
                   sil.Qty, sil.UnitPrice, sil.LineSubtotal,
                   sil.TaxRatePercent, sil.TaxAmount, sil.LineTotal, sil.UnitCost
            FROM SalesInvoiceLine sil
            JOIN Item i ON i.Id = sil.ItemId
            LEFT JOIN DictionaryValue dv ON dv.Id = sil.UomId
            WHERE sil.InvoiceId = @Id
            ORDER BY sil.CreatedAt", p)).ToList();

        Envelopes = (await conn.QueryAsync<EnvelopeDto>(@"
            SELECT Id, DocumentType, Uuid, EttN, Status, SentAt, AcceptedAt,
                   RejectedAt, ResponseText, RetryCount
            FROM InvoiceEnvelope
            WHERE InvoiceId = @Id AND IsDeleted = 0
            ORDER BY CreatedAt DESC", p)).ToList();

        return Page();
    }

    public record InvoiceHeaderDto(
        Guid Id, string InvoiceNo, DateTime InvoiceDate, DateTime? DueDate,
        Guid PartnerId, string PartnerName, string? TaxNumber, string? TaxOffice,
        decimal Subtotal, decimal TaxAmount, decimal GrandTotal, decimal PaidAmount,
        string Currency, string Status,
        string? EBelgeType, string? EBelgeStatus, Guid? EBelgeUuid,
        Guid? ShippingId, Guid? SalesOrderId, string? Notes);

    public record LineDto(
        Guid Id, Guid ItemId, string ItemCode, string ItemName, string? Description,
        Guid UomId, string? UomCode, decimal Qty, decimal UnitPrice,
        decimal LineSubtotal, decimal TaxRatePercent, decimal TaxAmount, decimal LineTotal,
        decimal? UnitCost);

    public record EnvelopeDto(
        Guid Id, string DocumentType, Guid Uuid, string? EttN, string Status,
        DateTime? SentAt, DateTime? AcceptedAt, DateTime? RejectedAt,
        string? ResponseText, int RetryCount);
}
