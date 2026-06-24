using Microsoft.AspNetCore.Mvc;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.PurchaseOrders;

// Satınalma siparişi detay ekranı — POST handler'ları + fiyat farkı kontrolü
// (Details.cshtml.cs'ten ayrıldı — dosya boyutu disiplini). partial class: primary
// constructor parametreleri (db/company/user/audit/logger) ve DTO'lar erişilebilir.
public partial class DetailsModel
{
    // PO'ya yeni satır ekler; ürünün temel birimini yansıtır ve fiyat farkı (PriceVariance) kontrolü yapar
    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, decimal qty, decimal? price, CancellationToken ct)
    {
        // Guard: geçersiz satır girişi reddedilir (ürün seçili + pozitif miktar zorunlu)
        if (itemId == Guid.Empty || qty <= 0)
        {
            TempData["Error"] = "Geçersiz satır: ürün seçimi ve pozitif miktar zorunlu.";
            return RedirectToPage(new { id });
        }

        using var conn = db.Open();
        // Evrak bütünlüğü: bu siparişe mal kabul yapılmışsa satır eklenemez (document-immutability §3)
        if (await DocumentLock.PoHasReceivingAsync(conn, id, company.Id))
        {
            TempData["Error"] = "Belge kilitli: bu siparişe bağlı mal kabul mevcut, satır eklenemez.";
            return RedirectToPage(new { id });
        }
        // İş kuralı: Ürünün temel birimi DB'den okunur, satıra yansıtılır
        var baseUomId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT BaseUomId FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId",
            new { ItemId = itemId, CompanyId = company.Id }, cancellationToken: ct));

        if (baseUomId is null) return RedirectToPage(new { id });

        // İş kuralı: Yeni satır Id'si geri alınır (fiyat farkı kontrolü için gerekli)
        var newLineId = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: eklenen Item bu handler'da WHERE Id = @ItemId AND CompanyId = @CompanyId ile
            -- doğrulandı; bulunamazsa işlem iptal edildi (BaseUomId null döndü).
            -- @HeaderId değeri LoadHeaderAsync'te WHERE o.Id = @Id AND o.CompanyId = @CompanyId ile
            -- yüklenen PurchaseOrderHeader.Id'dir; LoadLinesAsync'te de aynı satır
            -- JOIN PurchaseOrderHeader oh ON oh.Id = l.HeaderId ... AND oh.CompanyId = @CompanyId
            -- ile sahiplik doğrulanır; farklı firmanın siparişine satır eklenemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            INSERT INTO PurchaseOrderLine (HeaderId, ItemId, UomId, QtyOrdered, Price, Currency)
            OUTPUT INSERTED.Id
            VALUES (@HeaderId, @ItemId, @UomId, @Qty, @Price, 'TRY')",
            new { HeaderId = id, ItemId = itemId, UomId = baseUomId, Qty = qty, Price = price ?? 0 }, cancellationToken: ct));

        await audit.LogAsync("ADD_LINE", "PurchaseOrderHeader", id, $"Item: {itemId} Qty: {qty}");

        // İş kuralı: Fiyat farkı kontrolü — tedarikçi fiyat listesinden sapma eşik üstü ise PriceVariance kaydı
        await CheckPriceVarianceAsync(conn, id, newLineId, itemId, price ?? 0, ct);

        return RedirectToPage(new { id });
    }

    // PO satırını siler — yalnız DRAFT sipariş + bağlı mal kabul yoksa (evrak bütünlüğü). IDOR: JOIN ile firma scope.
    public async Task<IActionResult> OnPostDeleteLineAsync(Guid id, Guid lineId, CancellationToken ct)
    {
        using var conn = db.Open();

        // Evrak bütünlüğü: bağlı mal kabul varsa satır silinemez (document-immutability §2.5)
        if (await DocumentLock.PoHasReceivingAsync(conn, id, company.Id))
        {
            TempData["Error"] = "Belge kilitli: bu siparişe bağlı mal kabul mevcut, satır silinemez.";
            return RedirectToPage(new { id });
        }

        // Yalnız DRAFT siparişin satırı silinebilir; IDOR: satır bu firmanın siparişine ait olmalı
        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
            DELETE pol FROM PurchaseOrderLine pol
            JOIN PurchaseOrderHeader poh ON poh.Id = pol.HeaderId
            WHERE pol.Id = @LineId AND poh.Id = @Id AND poh.CompanyId = @CompanyId AND poh.Status = @Draft",
            new { LineId = lineId, Id = id, CompanyId = company.Id, Draft = DocStatus.Draft }, cancellationToken: ct));

        if (affected > 0)
            await audit.LogAsync("DELETE_LINE", "PurchaseOrderHeader", id, $"Satır silindi: {lineId}");
        else
            TempData["Error"] = "Satır silinemedi (yalnız taslak siparişte silinebilir).";

        return RedirectToPage(new { id });
    }

    // Satır fiyatını tedarikçi liste fiyatıyla karşılaştırır; sapma eşik üstü ise
    // sp_CheckPriceVariance bir PriceVariance (DRAFT) kaydı açar, kullanıcıya uyarı gösterilir.
    private async Task CheckPriceVarianceAsync(
        System.Data.IDbConnection conn, Guid headerId, Guid lineId, Guid itemId, decimal actualPrice, CancellationToken ct)
    {
        // Tedarikçi (PartnerId) ve belge şubesi (Warehouse → BranchId) header'dan okunur.
        // Plan 30: şube boyutu fiyat önceliğine girer (cari baskın, Partner×2+Branch×1).
        var ctx = await conn.QuerySingleOrDefaultAsync<PriceCheckCtx>(new CommandDefinition(
            @"SELECT h.PartnerId, w.BranchId
              FROM PurchaseOrderHeader h
              JOIN Warehouse w ON w.Id = h.WarehouseId
              WHERE h.Id = @Id AND h.CompanyId = @CompanyId",
            new { Id = headerId, CompanyId = company.Id }, cancellationToken: ct));

        // Güvenlik: header veya bağlı depo kaydı bulunamazsa (silinmiş/tutarsız) kontrol atlanır.
        // PartnerId şemada NOT NULL olduğundan ayrıca null kontrolü gereksiz.
        if (ctx is null) return;

        var prm = new DynamicParameters();
        prm.Add("CompanyId",  company.Id);
        prm.Add("PoHeaderId", headerId);
        prm.Add("PoLineId",   lineId);
        prm.Add("ItemId",     itemId);
        prm.Add("PartnerId",  ctx.PartnerId);
        prm.Add("ActualPrice", actualPrice);
        prm.Add("BranchId",   ctx.BranchId);   // NULL = genel (şube ataması yoksa)
        prm.Add("UserId",     user.Id);
        prm.Add("VarianceId", dbType: System.Data.DbType.Guid, direction: System.Data.ParameterDirection.Output);

        await conn.ExecuteAsync(new CommandDefinition("sp_CheckPriceVariance", prm,
            commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));

        var varianceId = prm.Get<Guid?>("VarianceId");
        if (varianceId.HasValue)
        {
            // İş kuralı: Sapma tespit edildi — kullanıcı detayı PriceVariance ekranında görür
            TempData["PriceWarning"] =
                "Bu satırın fiyatı tedarikçi liste fiyatından saptı. Fiyat farkı onaya gönderildi.";
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct)
    {
        // İş kuralı: DRAFT → POSTED geçişi. sp_PoPost StatusTransition doğrulamasını yapar,
        // sonrasında sp_GeneratePaymentPlanFromPO ile tedarikçi vade planını otomatik üretir.
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "sp_PoPost",
                new { PoHeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));
            await audit.LogAsync("POST", "PurchaseOrderHeader", id,
                "Satınalma siparişi onaylandı, ödeme planı oluşturuldu");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "PO onay hatası: {PoId}", id);
            TempData["Error"] = "Sipariş onaylanırken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id, CancellationToken ct)
    {
        // İş kuralı: POSTED → CANCELLED; sp_ValidateStatusTransition bypass engeli
        using var conn = db.Open();
        try
        {
            // Atomiklik: statü geçişi + header iptal + PaymentPlan iptal tek transaction'da
            // (kısmi-commit → hayalet borç önlenir; architecture §4 çok-adımlı yazma tek TX)
            using var tx = conn.BeginTransaction();

            await conn.ExecuteAsync(new CommandDefinition("sp_ValidateStatusTransition",
                new { CompanyId = company.Id, DocumentType = SourceDoc.PurchaseOrder,
                      FromStatus = DocStatus.Posted, ToStatus = DocStatus.Cancelled,
                      UserId = user.Id },
                transaction: tx, commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));

            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE PurchaseOrderHeader SET Status=@Status, UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Status = DocStatus.Cancelled, UserId = user.Id, Id = id, CompanyId = company.Id },
                transaction: tx, cancellationToken: ct));

            // Hayalet borç önleme: PO'ya bağlı açık (tahmini) PaymentPlan'lar iptal edilir; ödenmişe dokunma.
            // PO PaymentPlan'ı taahhüttür (fatura değil) — sipariş iptalinde kapatılmazsa cari raporda hayalet kalır.
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE PaymentPlan SET Status = @CancelStatus, UpdatedAt = GETUTCDATE()
                WHERE SourceDocType = @SrcPo AND SourceDocId = @Id AND CompanyId = @CompanyId
                  AND Status NOT IN (@Paid, @CancelStatus)",
                new { CancelStatus = PaymentPlanStatus.Cancelled, SrcPo = SourceDoc.PurchaseOrder,
                      Id = id, CompanyId = company.Id, Paid = PaymentPlanStatus.Paid },
                transaction: tx, cancellationToken: ct));

            tx.Commit();
            await audit.LogAsync("CANCEL", "PurchaseOrderHeader", id, "Satınalma siparişi iptal edildi");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "PO iptal hatası: {PoId}", id);
            TempData["Error"] = "Sipariş iptal edilirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    // Kalanı kapat: kısmen teslim alınmış POSTED siparişi CLOSED_PARTIAL yapar (kalan miktar
    // artık beklenmez). Tam kabul zaten sp_ReceivingPost'ta otomatik CLOSED olur (Plan 54 Faz 3).
    public async Task<IActionResult> OnPostCloseRemainingAsync(Guid id, CancellationToken ct)
    {
        using var conn = db.Open();
        try
        {
            // Atomiklik: statü geçişi + header + PaymentPlan kapanışı tek transaction
            using var tx = conn.BeginTransaction();

            await conn.ExecuteAsync(new CommandDefinition("sp_ValidateStatusTransition",
                new { CompanyId = company.Id, DocumentType = SourceDoc.PurchaseOrder,
                      FromStatus = DocStatus.Posted, ToStatus = DocStatus.ClosedPartial, UserId = user.Id },
                transaction: tx, commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));

            // Yalnız POSTED sipariş kalan-kapatılabilir (DRAFT/CLOSED/CANCELLED reddedilir)
            var affected = await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE PurchaseOrderHeader SET Status=@Status, UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId WHERE Id=@Id AND CompanyId=@CompanyId AND Status=@Posted",
                new { Status = DocStatus.ClosedPartial, UserId = user.Id, Id = id, CompanyId = company.Id, Posted = DocStatus.Posted },
                transaction: tx, cancellationToken: ct));
            if (affected == 0)
            {
                tx.Rollback();
                TempData["Error"] = "Sipariş kapatılamadı (yalnız onaylı sipariş kalan-kapatılabilir).";
                return RedirectToPage(new { id });
            }

            // Kalan beklenmeyeceği için PO'nun açık (tahmini) PaymentPlan'ı iptal edilir (hayalet borç önleme)
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE PaymentPlan SET Status = @CancelStatus, UpdatedAt = GETUTCDATE()
                WHERE SourceDocType = @SrcPo AND SourceDocId = @Id AND CompanyId = @CompanyId
                  AND Status NOT IN (@Paid, @CancelStatus)",
                new { CancelStatus = PaymentPlanStatus.Cancelled, SrcPo = SourceDoc.PurchaseOrder,
                      Id = id, CompanyId = company.Id, Paid = PaymentPlanStatus.Paid },
                transaction: tx, cancellationToken: ct));

            tx.Commit();
            await audit.LogAsync("CLOSE_PARTIAL", "PurchaseOrderHeader", id, "Sipariş kalanı kapatıldı (kısmi)");
            TempData["Success"] = "Sipariş kapatıldı (kalan miktar beklenmiyor).";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "PO kalan-kapatma hatası: {PoId}", id);
            TempData["Error"] = "Sipariş kapatılırken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreateReceivingAsync(Guid id, CancellationToken ct)
    {
        // sp_CreateReceivingFromPO: POSTED PO'dan DRAFT Receiving oluşturur, satırları kopyalar
        using var conn = db.Open();
        try
        {
            var prm = new DynamicParameters();
            prm.Add("PoId",        id);
            prm.Add("CompanyId",   company.Id);
            prm.Add("UserId",      user.Id);
            prm.Add("NewHeaderId", dbType: System.Data.DbType.Guid,
                    direction: System.Data.ParameterDirection.Output);

            await conn.ExecuteAsync(new CommandDefinition("sp_CreateReceivingFromPO", prm,
                commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));

            var newId = prm.Get<Guid>("NewHeaderId");
            TempData["Success"] = "Mal kabul belgesi oluşturuldu.";
            return RedirectToPage("/Receiving/Details", new { id = newId });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
            return RedirectToPage(new { id });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Mal kabul oluşturma hatası: {PoId}", id);
            TempData["Error"] = "Mal kabul oluşturulurken veritabanı hatası oluştu.";
            return RedirectToPage(new { id });
        }
    }
}
