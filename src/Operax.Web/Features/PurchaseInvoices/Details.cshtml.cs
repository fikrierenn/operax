using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Operax.Web.Lib.Ai;

namespace Operax.Web.Features.PurchaseInvoices;

/// <summary>
/// Mal alım faturası detayı: başlık + kalemler + tedarikçi belge bilgisi düzenleme + Onayla/İptal/Ödeme.
/// POST handler'ları (yazma/SP orkestrasyonu) Details.Handlers.cs partial bölümünde.
/// </summary>
[Authorize]
public partial class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<DetailsModel> logger, IOperaxAiClient ai, ParameterStore parameters) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public EditDto Edit { get; set; } = new();

    public HeaderDto? Header { get; set; }
    public List<LineDto> Lines { get; set; } = [];
    public List<VarianceDto> Variances { get; set; } = [];
    public DocFlowVm? DocFlow { get; set; }
    // POSTED faturada gerekçeli satır düzeltme yetkisi (ödeme yoksa + yetkili rol)
    public bool CanCorrect { get; set; }

    // Fatura başlığı, kalemleri ve ödeme smart button'unu yükler
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Id == Guid.Empty) return RedirectToPage("Index");

        using var conn = db.Open();
        Header = await conn.QueryFirstOrDefaultAsync<HeaderDto>(new CommandDefinition(@"
            SELECT pi.Id, pi.DocNo, pi.SupplierInvoiceNo, pi.SupplierInvoiceDate, pi.SupplierInvoiceUuid,
                   pi.InvoiceDate, pi.DueDate, pi.PartnerId, p.Name AS PartnerName,
                   pi.Subtotal, pi.TaxAmount, pi.GrandTotal, pi.PaidAmount, pi.Currency, pi.Status
            FROM PurchaseInvoice pi
            JOIN Partner p ON p.Id = pi.PartnerId
            WHERE pi.Id = @Id AND pi.CompanyId = @CompanyId",
            new { Id, CompanyId = company.Id }, cancellationToken: ct));

        if (Header == null) return NotFound();

        Lines = (await conn.QueryAsync<LineDto>(new CommandDefinition(@"
            /* isolation-guard:ignore: üst belge PurchaseInvoice yukarıda CompanyId ile doğrulandı */
            SELECT pil.Id, i.Code AS ItemCode, i.Name AS ItemName, dv.Code AS UomCode,
                   pil.Qty, pil.UnitPrice, pil.LineSubtotal, pil.TaxRatePercent, pil.TaxAmount, pil.LineTotal,
                   pil.SourceLinkType, poh.OrderNo AS PoOrderNo
            FROM PurchaseInvoiceLine pil
            JOIN Item i ON i.Id = pil.ItemId
            LEFT JOIN DictionaryValue dv ON dv.Id = pil.UomId
            LEFT JOIN PurchaseOrderLine pol ON pol.Id = pil.PurchaseOrderLineId
            LEFT JOIN PurchaseOrderHeader poh ON poh.Id = pol.HeaderId
            WHERE pil.InvoiceId = @Id
            ORDER BY pil.CreatedAt", new { Id }, cancellationToken: ct))).ToList();

        // Plan 27: bu faturaya ait fiyat farkları (PO fiyatından sapma — tolerans yok)
        Variances = (await conn.QueryAsync<VarianceDto>(new CommandDefinition(@"
            /* isolation-guard:ignore: üst belge PurchaseInvoice yukarıda CompanyId ile doğrulandı */
            SELECT v.Id, i.Code AS ItemCode, i.Name AS ItemName,
                   v.ExpectedPrice, v.ActualPrice, v.Variance, v.VariancePercent,
                   v.Status, v.OverrideReason, v.AiVerdict, v.AiComment
            FROM PriceVariance v
            JOIN Item i ON i.Id = v.ItemId
            WHERE v.SourceDocType = 'PURCHASE_INVOICE' AND v.SourceDocId = @Id
              AND v.CompanyId = @CompanyId AND v.IsDeleted = 0
            ORDER BY v.CreatedAt", new { Id, CompanyId = company.Id }, cancellationToken: ct))).ToList();

        // Düzeltme yetkisi: POSTED + ödenmiş plan yok + yetkili rol
        if (Header.Status == DocStatus.Posted && user.HasRole(Roles.Administrator, Roles.Finance, Roles.Purchasing))
        {
            var paid = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
                SELECT COUNT(*) FROM PaymentPlan
                WHERE SourceDocType='PURCHASE_INVOICE' AND SourceDocId=@Id
                  AND CompanyId=@CompanyId
                  AND Status IN (@Paid, @Partial) AND FinancialTransactionId IS NOT NULL",
                new { Id, CompanyId = company.Id, Paid = DocStatus.Paid, Partial = DocStatus.Partial }, cancellationToken: ct));
            CanCorrect = paid == 0;
        }

        // Düzenleme formu için mevcut değerleri doldur
        Edit = new EditDto
        {
            SupplierInvoiceNo   = Header.SupplierInvoiceNo,
            SupplierInvoiceDate = Header.SupplierInvoiceDate,
            SupplierInvoiceUuid = Header.SupplierInvoiceUuid,
            DueDate             = Header.DueDate
        };

        // Ödeme smart button — POSTED + kalan tutar varsa
        if (Header.Status == DocStatus.Posted)
        {
            var remaining = Header.GrandTotal - Header.PaidAmount;
            DocFlow = new DocFlowVm([
                new DocFlowItem(
                    Label: "Ödeme",
                    Count: 0,
                    CreateUrl: remaining > 0
                        ? $"/Finance/Payments/Create?partnerId={Header.PartnerId}&txType=EXPENSE&amount={remaining}&sourceDocId={Id}&sourceDocType=PURCHASE_INVOICE"
                        : null,
                    CreateLabel: remaining > 0 ? "Ödeme Yap" : "Tam Ödendi",
                    CanCreate: remaining > 0 && user.HasRole(Roles.Administrator, Roles.Finance, Roles.Purchasing),
                    IsGetCreate: true)
            ]);
        }
        return Page();
    }

    public record VarianceDto(
        Guid Id, string ItemCode, string ItemName,
        decimal ExpectedPrice, decimal ActualPrice, decimal Variance, decimal VariancePercent,
        string Status, string? OverrideReason, string? AiVerdict, string? AiComment);

    public record HeaderDto(
        Guid Id, string DocNo, string? SupplierInvoiceNo, DateTime? SupplierInvoiceDate,
        string? SupplierInvoiceUuid, DateTime InvoiceDate, DateTime? DueDate,
        Guid PartnerId, string PartnerName,
        decimal Subtotal, decimal TaxAmount, decimal GrandTotal, decimal PaidAmount,
        string Currency, string Status);

    public record LineDto(
        Guid Id, string ItemCode, string ItemName, string? UomCode,
        decimal Qty, decimal UnitPrice, decimal LineSubtotal,
        decimal TaxRatePercent, decimal TaxAmount, decimal LineTotal,
        string? SourceLinkType = null, string? PoOrderNo = null);

    public class EditDto
    {
        public string? SupplierInvoiceNo { get; set; }
        public DateTime? SupplierInvoiceDate { get; set; }
        public string? SupplierInvoiceUuid { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
