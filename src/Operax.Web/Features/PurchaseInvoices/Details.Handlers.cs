using Microsoft.AspNetCore.Mvc;
using Dapper;
using Operax.Web.Lib;
using Operax.Web.Lib.Ai;

namespace Operax.Web.Features.PurchaseInvoices;

/// <summary>
/// DetailsModel POST handler'ları (yazma/SP orkestrasyonu) — partial bölüm.
/// OnGet/okuma + DTO tanımları Details.cshtml.cs'te; bu dosya yalnız evrak akışı eylemlerini taşır
/// (kaydet, fiyat güncelle, onayla, fark override/ret, satır düzeltme, iptal). Davranış birebir aynı.
/// </summary>
public partial class DetailsModel
{
    // Tedarikçi belge bilgilerini günceller (yalnızca DRAFT)
    public async Task<IActionResult> OnPostSaveAsync(Guid id, CancellationToken ct)
    {
        using var conn = db.Open();
        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE PurchaseInvoice
            SET SupplierInvoiceNo = @No, SupplierInvoiceDate = @Date,
                SupplierInvoiceUuid = @Uuid, DueDate = @DueDate, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND CompanyId = @CompanyId AND Status = @Draft",
            new
            {
                No = Edit.SupplierInvoiceNo, Date = Edit.SupplierInvoiceDate,
                Uuid = Edit.SupplierInvoiceUuid, DueDate = Edit.DueDate,
                Id = id, CompanyId = company.Id, Draft = DocStatus.Draft
            }, cancellationToken: ct));

        TempData[affected > 0 ? "Success" : "Error"] =
            affected > 0 ? "Fatura bilgileri kaydedildi." : "Yalnızca taslak fatura düzenlenebilir.";
        return RedirectToPage(new { id });
    }

    // Kalem birim fiyatlarını günceller (yalnızca DRAFT) + satır ve başlık toplamlarını yeniden hesaplar
    public async Task<IActionResult> OnPostUpdatePricesAsync(Guid id, Guid[] lineId, string[] unitPrice, CancellationToken ct)
    {
        using var conn = db.Open();

        // İş kuralı: yalnızca taslak fatura kalemleri düzenlenebilir
        var status = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT Status FROM PurchaseInvoice WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct));
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
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE PurchaseInvoiceLine
                SET UnitPrice    = @Price,
                    LineSubtotal = Qty * @Price,
                    TaxAmount    = ROUND(Qty * @Price * TaxRatePercent / 100.0, 2),
                    LineTotal    = ROUND(Qty * @Price * (1 + TaxRatePercent / 100.0), 2)
                WHERE Id = @LineId AND InvoiceId = @Id",
                new { Price = price, LineId = lineId[i], Id = id }, cancellationToken: ct));
        }

        // Başlık toplamlarını kalemlerden yeniden hesapla (tek kaynak: satırlar)
        await conn.ExecuteAsync(new CommandDefinition(@"
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
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct));

        TempData["Success"] = "Kalem fiyatları güncellendi.";
        return RedirectToPage(new { id });
    }

    // sp_PurchaseInvoicePost: DRAFT → POSTED, cari borç + ödeme planı
    public async Task<IActionResult> OnPostPostAsync(Guid id, CancellationToken ct)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_PurchaseInvoicePost",
                new { InvoiceId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));
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
    public async Task<IActionResult> OnPostApproveVarianceAsync(Guid id, Guid varianceId, string? reason, CancellationToken ct)
    {
        // İş kuralı: override için gerekçe zorunlu
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Fiyat farkı override'ı için gerekçe girmek zorunludur.";
            return RedirectToPage(new { id });
        }

        using var conn = db.Open();

        // Sapma bağlamını AI'a ver (Expected/Actual) — gerekçe makul mu?
        var ctx = await conn.QueryFirstOrDefaultAsync<(decimal Expected, decimal Actual, string Item)>(new CommandDefinition(@"
            SELECT v.ExpectedPrice, v.ActualPrice, i.Name
            FROM PriceVariance v JOIN Item i ON i.Id = v.ItemId
            WHERE v.Id = @VarianceId AND v.CompanyId = @CompanyId AND v.Status = @Draft",
            new { VarianceId = varianceId, CompanyId = company.Id, Draft = DocStatus.Draft }, cancellationToken: ct));

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
        // Soft-fail: aiCts yalnız AI timeout'unu temsil eder (istek-abort'a bağlı değil). Süre aşılırsa
        // client OperationCanceledException fırlatır; onayı bloke etmemek için UNCHECKED'e düşürülür.
        AiReasonVerdict verdict;
        try
        {
            verdict = await ai.CheckJustificationAsync(context, reason, aiCts.Token);
        }
        catch (OperationCanceledException)
        {
            verdict = AiReasonVerdict.NotChecked("AI zaman aşımı");
        }

        await conn.ExecuteAsync(new CommandDefinition(@"
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
            }, cancellationToken: ct));

        TempData["Success"] = verdict.Verdict == AiReasonVerdict.Implausible
            ? $"Fiyat farkı override edildi. ⚠ AI gerekçeyi zayıf buldu: {verdict.Comment}"
            : "Fiyat farkı override edildi.";
        return RedirectToPage(new { id });
    }

    // Fiyat farkını reddet — fark kaydı REJECTED (fatura fiyatı yine de geçerli, sadece izlenir)
    public async Task<IActionResult> OnPostRejectVarianceAsync(Guid id, Guid varianceId, CancellationToken ct)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE PriceVariance SET Status = @Rejected, ApprovedBy = @UserId, ApprovedAt = GETUTCDATE()
            WHERE Id = @VarianceId AND CompanyId = @CompanyId AND Status = @Draft",
            new { VarianceId = varianceId, CompanyId = company.Id, UserId = user.Id, Draft = DocStatus.Draft, Rejected = DocStatus.Rejected }, cancellationToken: ct));
        TempData["Success"] = "Fiyat farkı reddedildi.";
        return RedirectToPage(new { id });
    }

    // Plan 27: POSTED fatura satır fiyat DÜZELTME (veri-giriş hatası). Fatura POSTED kalır;
    // ledger TTK md.65 append-only ters+yeni kayıt. Gerekçe zorunlu. sp_CorrectPurchaseInvoiceLine.
    public async Task<IActionResult> OnPostCorrectLineAsync(Guid id, Guid lineId, string unitPrice, string? reason, CancellationToken ct)
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
            await conn.ExecuteAsync(new CommandDefinition("sp_CorrectPurchaseInvoiceLine",
                new { InvoiceId = id, LineId = lineId, NewUnitPrice = price,
                      CompanyId = company.Id, UserId = user.Id, Reason = reason },
                commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));
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
    public async Task<IActionResult> OnPostReverseAsync(Guid id, CancellationToken ct)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_PurchaseInvoiceReverse",
                new { InvoiceId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));
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
}
