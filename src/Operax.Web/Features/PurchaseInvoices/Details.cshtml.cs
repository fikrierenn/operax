using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.PurchaseInvoices;

/// <summary>
/// Mal alım faturası detayı: başlık + kalemler + tedarikçi belge bilgisi düzenleme + Onayla/İptal/Ödeme.
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public EditDto Edit { get; set; } = new();

    public HeaderDto? Header { get; set; }
    public List<LineDto> Lines { get; set; } = [];
    public DocFlowVm? DocFlow { get; set; }

    // Fatura başlığı, kalemleri ve ödeme smart button'unu yükler
    public async Task<IActionResult> OnGetAsync()
    {
        if (Id == Guid.Empty) return RedirectToPage("Index");

        using var conn = db.Open();
        Header = await conn.QueryFirstOrDefaultAsync<HeaderDto>(@"
            SELECT pi.Id, pi.DocNo, pi.SupplierInvoiceNo, pi.SupplierInvoiceDate, pi.SupplierInvoiceUuid,
                   pi.InvoiceDate, pi.DueDate, pi.PartnerId, p.Name AS PartnerName,
                   pi.Subtotal, pi.TaxAmount, pi.GrandTotal, pi.PaidAmount, pi.Currency, pi.Status
            FROM PurchaseInvoice pi
            JOIN Partner p ON p.Id = pi.PartnerId
            WHERE pi.Id = @Id AND pi.CompanyId = @CompanyId",
            new { Id, CompanyId = company.Id });

        if (Header == null) return NotFound();

        Lines = (await conn.QueryAsync<LineDto>(@"
            /* isolation-guard:ignore: üst belge PurchaseInvoice yukarıda CompanyId ile doğrulandı */
            SELECT pil.Id, i.Code AS ItemCode, i.Name AS ItemName, dv.Code AS UomCode,
                   pil.Qty, pil.UnitPrice, pil.LineSubtotal, pil.TaxRatePercent, pil.TaxAmount, pil.LineTotal
            FROM PurchaseInvoiceLine pil
            JOIN Item i ON i.Id = pil.ItemId
            LEFT JOIN DictionaryValue dv ON dv.Id = pil.UomId
            WHERE pil.InvoiceId = @Id
            ORDER BY pil.CreatedAt", new { Id })).ToList();

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
                    CanCreate: remaining > 0 && user.HasRole("Administrator", "Finance", "Purchasing"),
                    IsGetCreate: true)
            ]);
        }
        return Page();
    }

    // Tedarikçi belge bilgilerini günceller (yalnızca DRAFT)
    public async Task<IActionResult> OnPostSaveAsync(Guid id)
    {
        using var conn = db.Open();
        var affected = await conn.ExecuteAsync(@"
            UPDATE PurchaseInvoice
            SET SupplierInvoiceNo = @No, SupplierInvoiceDate = @Date,
                SupplierInvoiceUuid = @Uuid, DueDate = @DueDate, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND CompanyId = @CompanyId AND Status = @Draft",
            new
            {
                No = Edit.SupplierInvoiceNo, Date = Edit.SupplierInvoiceDate,
                Uuid = Edit.SupplierInvoiceUuid, DueDate = Edit.DueDate,
                Id = id, CompanyId = company.Id, Draft = DocStatus.Draft
            });

        TempData[affected > 0 ? "Success" : "Error"] =
            affected > 0 ? "Fatura bilgileri kaydedildi." : "Yalnızca taslak fatura düzenlenebilir.";
        return RedirectToPage(new { id });
    }

    // sp_PurchaseInvoicePost: DRAFT → POSTED, cari borç + ödeme planı
    public async Task<IActionResult> OnPostPostAsync(Guid id)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_PurchaseInvoicePost",
                new { InvoiceId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: System.Data.CommandType.StoredProcedure);
            TempData["Success"] = "Fatura onaylandı, cari borç oluşturuldu.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Alış faturası onay hatası: {InvoiceId}", id);
            TempData["Error"] = "Fatura onaylanırken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    // sp_PurchaseInvoiceReverse: POSTED → CANCELLED, AccountMovement ters-satır
    public async Task<IActionResult> OnPostReverseAsync(Guid id)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_PurchaseInvoiceReverse",
                new { InvoiceId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: System.Data.CommandType.StoredProcedure);
            TempData["Success"] = "Fatura iptal edildi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Alış faturası iptal hatası: {InvoiceId}", id);
            TempData["Error"] = "Fatura iptal edilirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public record HeaderDto(
        Guid Id, string DocNo, string? SupplierInvoiceNo, DateTime? SupplierInvoiceDate,
        string? SupplierInvoiceUuid, DateTime InvoiceDate, DateTime? DueDate,
        Guid PartnerId, string PartnerName,
        decimal Subtotal, decimal TaxAmount, decimal GrandTotal, decimal PaidAmount,
        string Currency, string Status);

    public record LineDto(
        Guid Id, string ItemCode, string ItemName, string? UomCode,
        decimal Qty, decimal UnitPrice, decimal LineSubtotal,
        decimal TaxRatePercent, decimal TaxAmount, decimal LineTotal);

    public class EditDto
    {
        public string? SupplierInvoiceNo { get; set; }
        public DateTime? SupplierInvoiceDate { get; set; }
        public string? SupplierInvoiceUuid { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
