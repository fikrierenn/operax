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
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }

    public InvoiceHeaderDto? Header { get; set; }
    public List<LineDto>     Lines  { get; set; } = [];
    public List<EnvelopeDto> Envelopes { get; set; } = [];
    public DocFlowVm?        DocFlow   { get; set; }

    // Fatura başlık, kalem ve e-Belge zarf bilgilerini veritabanından yükler
    public async Task<IActionResult> OnGetAsync()
    {
        if (Id == Guid.Empty) return RedirectToPage("Index");

        try
        {
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
                /* Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
                   Gerekçe: üst belge SalesInvoice aynı handler içinde daha önce
                   WHERE si.Id = @Id AND si.CompanyId = @CompanyId ile yüklendi; bulunamazsa NotFound döndü.
                   Bu sorgu yalnızca o doğrulanmış SalesInvoice.Id üzerinden kalem satırlarını okuyduğundan
                   başka firmanın fatura verisine erişilemez.
                   isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar) */
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
                /* Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
                   Gerekçe: üst belge SalesInvoice aynı handler içinde daha önce
                   WHERE si.Id = @Id AND si.CompanyId = @CompanyId ile yüklendi; bulunamazsa NotFound döndü.
                   @Id parametresi o doğrulanmış SalesInvoice.Id değeridir; InvoiceEnvelope yalnızca
                   o faturanın e-Belge zarf kayıtlarını getirir, farklı firmanın zarfına erişilemez.
                   isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar) */
                SELECT Id, DocumentType, Uuid, EttN, Status, SentAt, AcceptedAt,
                       RejectedAt, ResponseText, RetryCount
                FROM InvoiceEnvelope
                WHERE InvoiceId = @Id AND IsDeleted = 0
                ORDER BY CreatedAt DESC", p)).ToList();

            // Tahsilat smart button — POSTED faturalarda gösterilir
            if (Header?.Status == DocStatus.Posted)
            {
                var remaining = Header.GrandTotal - Header.PaidAmount;
                DocFlow = new DocFlowVm([
                    new DocFlowItem(
                        Label: "Tahsilat",
                        Count: 0,
                        CreateUrl: remaining > 0
                            ? $"/Finance/Payments/Create?partnerId={Header.PartnerId}&txType=INCOME&amount={remaining}&sourceDocId={Id}&sourceDocType=SALES_INVOICE"
                            : null,
                        CreateLabel: remaining > 0 ? "Tahsilat Al" : "Tam Tahsil Edildi",
                        CanCreate: remaining > 0 && user.HasRole("Administrator","Finance","Sales"))
                ]);
            }

            return Page();
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Satış faturası detay veri yükleme hatası: {InvoiceId}", Id);
            TempData["Error"] = "Veriler yüklenirken bir hata oluştu.";
            return Page();
        }
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

    public async Task<IActionResult> OnPostReverseAsync(Guid id)
    {
        // sp_SalesInvoiceReverse: POSTED faturayı CANCELLED yapar, AccountMovement ters-satır yazar
        // Guard: e-Fatura gönderilmişse SP THROW 51404 fırlatır
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_SalesInvoiceReverse",
                new { InvoiceId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: System.Data.CommandType.StoredProcedure);
            TempData["Success"] = "Satış faturası iptal edildi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı (e-Fatura gönderilmiş, dönem kilidi vb.)
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Satış faturası iptal hatası: {InvoiceId}", id);
            TempData["Error"] = "Fatura iptal edilirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }
}
