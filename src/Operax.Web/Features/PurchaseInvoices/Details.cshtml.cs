using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Operax.Web.Lib.Ai;

namespace Operax.Web.Features.PurchaseInvoices;

/// <summary>
/// Mal alım faturası detayı: başlık + kalemler + tedarikçi belge bilgisi düzenleme + Onayla/İptal/Ödeme.
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<DetailsModel> logger, IOperaxAiClient ai, ParameterStore parameters) : PageModel
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
                   pil.Qty, pil.UnitPrice, pil.LineSubtotal, pil.TaxRatePercent, pil.TaxAmount, pil.LineTotal,
                   pil.SourceLinkType, poh.OrderNo AS PoOrderNo
            FROM PurchaseInvoiceLine pil
            JOIN Item i ON i.Id = pil.ItemId
            LEFT JOIN DictionaryValue dv ON dv.Id = pil.UomId
            LEFT JOIN PurchaseOrderLine pol ON pol.Id = pil.PurchaseOrderLineId
            LEFT JOIN PurchaseOrderHeader poh ON poh.Id = pol.HeaderId
            WHERE pil.InvoiceId = @Id
            ORDER BY pil.CreatedAt", new { Id })).ToList();

        // Plan 27: bu faturaya ait fiyat farkları (PO fiyatından sapma — tolerans yok)
        Variances = (await conn.QueryAsync<VarianceDto>(@"
            /* isolation-guard:ignore: üst belge PurchaseInvoice yukarıda CompanyId ile doğrulandı */
            SELECT v.Id, i.Code AS ItemCode, i.Name AS ItemName,
                   v.ExpectedPrice, v.ActualPrice, v.Variance, v.VariancePercent,
                   v.Status, v.OverrideReason, v.AiVerdict, v.AiComment
            FROM PriceVariance v
            JOIN Item i ON i.Id = v.ItemId
            WHERE v.SourceDocType = 'PURCHASE_INVOICE' AND v.SourceDocId = @Id
              AND v.CompanyId = @CompanyId AND v.IsDeleted = 0
            ORDER BY v.CreatedAt", new { Id, CompanyId = company.Id })).ToList();

        // Düzeltme yetkisi: POSTED + ödenmiş plan yok + yetkili rol
        if (Header.Status == DocStatus.Posted && user.HasRole(Roles.Administrator, Roles.Finance, Roles.Purchasing))
        {
            var paid = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM PaymentPlan
                WHERE SourceDocType='PURCHASE_INVOICE' AND SourceDocId=@Id
                  AND Status IN (@Paid, @Partial) AND FinancialTransactionId IS NOT NULL",
                new { Id, Paid = DocStatus.Paid, Partial = DocStatus.Partial });
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

    // Kalem birim fiyatlarını günceller (yalnızca DRAFT) + satır ve başlık toplamlarını yeniden hesaplar
    public async Task<IActionResult> OnPostUpdatePricesAsync(Guid id, Guid[] lineId, string[] unitPrice)
    {
        using var conn = db.Open();

        // İş kuralı: yalnızca taslak fatura kalemleri düzenlenebilir
        var status = await conn.ExecuteScalarAsync<string?>(
            "SELECT Status FROM PurchaseInvoice WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id });
        if (status != DocStatus.Draft)
        {
            TempData["Error"] = "Yalnızca taslak fatura düzenlenebilir.";
            return RedirectToPage(new { id });
        }

        // Her satır için fiyat + bağlı tutarları (ara toplam, KDV, toplam) güncelle.
        // HTML number input değeri DAİMA nokta-ondalıklı (invariant) gönderir; tr-TR model
        // binding'i '12.5'i 125 olarak okur (nokta=binlik) → parayı invariant parse et.
        for (int i = 0; i < lineId.Length; i++)
        {
            decimal price = decimal.TryParse(unitPrice[i],
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            if (price < 0) price = 0;
            await conn.ExecuteAsync(@"
                UPDATE PurchaseInvoiceLine
                SET UnitPrice    = @Price,
                    LineSubtotal = Qty * @Price,
                    TaxAmount    = ROUND(Qty * @Price * TaxRatePercent / 100.0, 2),
                    LineTotal    = ROUND(Qty * @Price * (1 + TaxRatePercent / 100.0), 2)
                WHERE Id = @LineId AND InvoiceId = @Id",
                new { Price = price, LineId = lineId[i], Id = id });
        }

        // Başlık toplamlarını kalemlerden yeniden hesapla (tek kaynak: satırlar)
        await conn.ExecuteAsync(@"
            UPDATE PurchaseInvoice
            SET Subtotal   = t.Sub,
                TaxAmount  = t.Tax,
                GrandTotal = t.Sub + t.Tax,
                UpdatedAt  = GETUTCDATE()
            FROM PurchaseInvoice pi
            CROSS APPLY (
                SELECT ISNULL(SUM(LineSubtotal), 0) AS Sub, ISNULL(SUM(TaxAmount), 0) AS Tax
                FROM PurchaseInvoiceLine WHERE InvoiceId = @Id
            ) t
            WHERE pi.Id = @Id AND pi.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id });

        TempData["Success"] = "Kalem fiyatları güncellendi.";
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

    // Plan 27: Fiyat farkını gerekçeyle override onayı. Gerekçe zorunlu; yerel AI gerekçeyi
    // denetler (advisory — IMPLAUSIBLE olsa da kullanıcı onaylayabilir, AI yorumu kayda geçer).
    public async Task<IActionResult> OnPostApproveVarianceAsync(Guid id, Guid varianceId, string? reason)
    {
        // İş kuralı: override için gerekçe zorunlu
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Fiyat farkı override'ı için gerekçe girmek zorunludur.";
            return RedirectToPage(new { id });
        }

        using var conn = db.Open();

        // Sapma bağlamını AI'a ver (Expected/Actual) — gerekçe makul mu?
        var ctx = await conn.QueryFirstOrDefaultAsync<(decimal Expected, decimal Actual, string Item)>(@"
            SELECT v.ExpectedPrice, v.ActualPrice, i.Name
            FROM PriceVariance v JOIN Item i ON i.Id = v.ItemId
            WHERE v.Id = @VarianceId AND v.CompanyId = @CompanyId AND v.Status = @Draft",
            new { VarianceId = varianceId, CompanyId = company.Id, Draft = DocStatus.Draft });

        if (ctx == default)
        {
            TempData["Error"] = "Onaylanacak fiyat farkı bulunamadı (zaten işlenmiş olabilir).";
            return RedirectToPage(new { id });
        }

        // Yerel AI gerekçe denetimi (soft-fail → UNCHECKED, iş bloke olmaz).
        // İnference'i HTTP istek-abort'tan AYIR: yavaş CPU inference'inde tarayıcı/proxy
        // isteği iptal ederse 0 token'da kesilmesin diye kendi timeout'lu CTS (120s).
        var context = $"Ürün: {ctx.Item}. Sipariş fiyatı {ctx.Expected:N2}, fatura fiyatı {ctx.Actual:N2}.";
        var aiTimeout = await parameters.GetIntAsync("AI_INFERENCE_TIMEOUT_SECONDS", 120);
        using var aiCts = new CancellationTokenSource(TimeSpan.FromSeconds(aiTimeout));
        var verdict = await ai.CheckJustificationAsync(context, reason, aiCts.Token);

        await conn.ExecuteAsync(@"
            UPDATE PriceVariance
            SET Status = @Approved, OverrideReason = @Reason,
                AiVerdict = @Verdict, AiComment = @Comment, AiCheckedAt = GETUTCDATE(),
                ApprovedBy = @UserId, ApprovedAt = GETUTCDATE()
            WHERE Id = @VarianceId AND CompanyId = @CompanyId AND Status = @Draft",
            new
            {
                Approved = DocStatus.Approved, Reason = reason,
                Verdict = verdict.Verdict, Comment = verdict.Comment,
                UserId = user.Id, VarianceId = varianceId, CompanyId = company.Id, Draft = DocStatus.Draft
            });

        TempData["Success"] = verdict.Verdict == AiReasonVerdict.Implausible
            ? $"Fiyat farkı override edildi. ⚠ AI gerekçeyi zayıf buldu: {verdict.Comment}"
            : "Fiyat farkı override edildi.";
        return RedirectToPage(new { id });
    }

    // Fiyat farkını reddet — fark kaydı REJECTED (fatura fiyatı yine de geçerli, sadece izlenir)
    public async Task<IActionResult> OnPostRejectVarianceAsync(Guid id, Guid varianceId)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(@"
            UPDATE PriceVariance SET Status = @Rejected, ApprovedBy = @UserId, ApprovedAt = GETUTCDATE()
            WHERE Id = @VarianceId AND CompanyId = @CompanyId AND Status = @Draft",
            new { VarianceId = varianceId, CompanyId = company.Id, UserId = user.Id, Draft = DocStatus.Draft, Rejected = DocStatus.Rejected });
        TempData["Success"] = "Fiyat farkı reddedildi.";
        return RedirectToPage(new { id });
    }

    public record VarianceDto(
        Guid Id, string ItemCode, string ItemName,
        decimal ExpectedPrice, decimal ActualPrice, decimal Variance, decimal VariancePercent,
        string Status, string? OverrideReason, string? AiVerdict, string? AiComment);

    // Plan 27: POSTED fatura satır fiyat DÜZELTME (veri-giriş hatası). Fatura POSTED kalır;
    // ledger TTK md.65 append-only ters+yeni kayıt. Gerekçe zorunlu. sp_CorrectPurchaseInvoiceLine.
    public async Task<IActionResult> OnPostCorrectLineAsync(Guid id, Guid lineId, string unitPrice, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Düzeltme için gerekçe girmek zorunludur.";
            return RedirectToPage(new { id });
        }
        // HTML number input invariant ('12.5') gönderir — tr-TR 125 okumasın
        decimal price = decimal.TryParse(unitPrice, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : -1;
        if (price < 0)
        {
            TempData["Error"] = "Geçerli bir birim fiyat giriniz.";
            return RedirectToPage(new { id });
        }

        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_CorrectPurchaseInvoiceLine",
                new { InvoiceId = id, LineId = lineId, NewUnitPrice = price,
                      CompanyId = company.Id, UserId = user.Id, Reason = reason },
                commandType: System.Data.CommandType.StoredProcedure);
            TempData["Success"] = "Fatura kalemi düzeltildi (cari defter satırı yeni tutara güncellendi, iz denetim kaydında).";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Fatura düzeltme hatası: {InvoiceId}", id);
            TempData["Error"] = "Fatura düzeltilirken veritabanı hatası oluştu.";
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
