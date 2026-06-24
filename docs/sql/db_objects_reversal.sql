-- =============================================================================
-- REVERSAL SP'LERİ — Plan 14 (StockMovement immutability)
-- Cancel = ters hareket (MovementType='REVERSAL') + IsCancelled=1 flag.
-- Silme yok; bakiye = SUM(QtyBase WHERE IsCancelled=0).
-- THROW aralığı: 51300-51399.
-- =============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- =============================================================================
-- sp_ReceivingReverse — Mal Kabul İptali
-- NE YAPAR: POSTED mal kabul belgesini CANCELLED yapar; mevcut RECEIPT hareketlerini
--           IsCancelled=1 ile kapatır ve ters satır YAZMAZ (flag-only; bakiye SUM WHERE IsCancelled=0 ile düzelir — çift-sayım önlenir).
-- PARAMETRELERİ:
--   @HeaderId  UNIQUEIDENTIFIER — iptal edilecek ReceivingHeader.Id
--   @CompanyId UNIQUEIDENTIFIER — tenant filtresi
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: StockMovement (IsCancelled güncelleme + REVERSAL INSERT), ReceivingHeader (Status → CANCELLED)
-- THROW: 51300-51302 (PageModel catch: >= 50000 && < 60000)
-- BAĞIMLILIK: sp_GuardPeriodOpen
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ReceivingReverse
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi (Plan 14)
        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        -- Belge kontrolü
        DECLARE @Status NVARCHAR(20), @DocNo NVARCHAR(50);
        SELECT @Status = Status, @DocNo = DocNo
        FROM ReceivingHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51300, N'Mal kabul belgesi bulunamadı.', 1;
        IF @Status = 'CANCELLED'
            THROW 51301, N'Belge zaten iptal edilmiş.', 1;
        IF @Status <> 'POSTED'
            THROW 51302, N'Yalnızca onaylanmış belgeler iptal edilebilir.', 1;

        -- Mevcut hareketleri IsCancelled=1 ile kapat (bakiye = SUM(QtyBase WHERE IsCancelled=0))
        UPDATE StockMovement
        SET IsCancelled = 1, CancelledAt = GETUTCDATE(), CancelledBy = @UserId
        WHERE SourceDocType = 'RECEIVING' AND SourceDocId = @HeaderId AND IsCancelled = 0;

        -- İş kuralı: iptal edilecek aktif hareket yoksa NORMALDE veri tutarsızlığı — AMA Plan 52'den sonra
        -- yalnız SERVICE (hizmet) satırı taşıyan belge sıfır StockMovement yazar (hizmet fiziksel değil).
        -- Bu MEŞRU sıfır-hareket halini gerçek tutarsızlıktan ayır: stoklu (non-SERVICE) satır VARSA hareket
        -- beklenir → yoksa THROW. SERVICE-only belgede hareket yokluğu normaldir → header iptaline devam.
        -- Ters (REVERSAL) satır YAZILMAZ: bakiye IsCancelled=0 filtresiyle hesaplandığından flag yeterli
        -- (ters satır eklemek çift-sayıma yol açar — stok 2x geri gelir).
        IF @@ROWCOUNT = 0 AND EXISTS (
            SELECT 1 FROM ReceivingLine rl JOIN Item i ON i.Id = rl.ItemId
            WHERE rl.HeaderId = @HeaderId AND i.ItemType <> 'SERVICE'
              AND (rl.QtyBase > 0 OR rl.ReturnQty > 0))
            THROW 51303, N'İptal edilecek mal kabul hareketi bulunamadı (veri tutarsızlığı).', 1;

        -- Header iptal
        UPDATE ReceivingHeader
        SET Status = 'CANCELLED', UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @HeaderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_ShippingReverse — Sevkiyat İptali
-- NE YAPAR: POSTED sevkiyat belgesini CANCELLED yapar; mevcut ISSUE hareketlerini
--           IsCancelled=1 ile kapatır ve ters satır YAZMAZ (flag-only; bakiye SUM WHERE IsCancelled=0 ile düzelir — çift-sayım önlenir).
-- PARAMETRELERİ:
--   @HeaderId  UNIQUEIDENTIFIER — iptal edilecek ShippingHeader.Id
--   @CompanyId UNIQUEIDENTIFIER — tenant filtresi
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: StockMovement (IsCancelled güncelleme + REVERSAL INSERT), ShippingHeader (Status → CANCELLED)
-- THROW: 51310-51312 (PageModel catch: >= 50000 && < 60000)
-- BAĞIMLILIK: sp_GuardPeriodOpen
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ShippingReverse
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        DECLARE @Status NVARCHAR(20), @DocNo NVARCHAR(50);
        SELECT @Status = Status, @DocNo = DocNo
        FROM ShippingHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51310, N'Sevkiyat belgesi bulunamadı.', 1;
        IF @Status = 'CANCELLED'
            THROW 51311, N'Belge zaten iptal edilmiş.', 1;
        IF @Status <> 'POSTED'
            THROW 51312, N'Yalnızca onaylanmış belgeler iptal edilebilir.', 1;

        -- Mevcut hareketleri IsCancelled=1 ile kapat (bakiye = SUM(QtyBase WHERE IsCancelled=0))
        UPDATE StockMovement
        SET IsCancelled = 1, CancelledAt = GETUTCDATE(), CancelledBy = @UserId
        WHERE SourceDocType = 'SHIPPING' AND SourceDocId = @HeaderId AND IsCancelled = 0;

        -- İş kuralı (Plan 52): hareket yoksa NORMALDE tutarsızlık — ama yalnız SERVICE (hizmet) satırı taşıyan
        -- sevkiyat sıfır StockMovement yazar (hizmet fiziksel taşınmaz). Meşru sıfır-hareketi gerçek
        -- tutarsızlıktan ayır: stoklu (non-SERVICE) satır VARSA hareket beklenir → yoksa THROW.
        -- Ters (REVERSAL) satır YAZILMAZ (flag yeterli; ters satır çift-sayım = stok 2x geri gelir).
        IF @@ROWCOUNT = 0 AND EXISTS (
            SELECT 1 FROM ShippingLine sl JOIN Item i ON i.Id = sl.ItemId
            WHERE sl.HeaderId = @HeaderId AND i.ItemType <> 'SERVICE' AND sl.QtyBase > 0)
            THROW 51313, N'İptal edilecek sevkiyat hareketi bulunamadı (veri tutarsızlığı).', 1;

        UPDATE ShippingHeader
        SET Status = 'CANCELLED', UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @HeaderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_TransferReverse — Transfer İptali
-- NE YAPAR: POSTED transfer belgesini CANCELLED yapar; kaynak depodaki çıkış ve
--           hedef depodaki giriş hareketlerinin tamamını IsCancelled=1 yapar,
--           her ikisi için de ters satır YAZMAZ (flag-only; bakiye SUM WHERE IsCancelled=0 ile düzelir — çift-sayım önlenir).
-- PARAMETRELERİ:
--   @HeaderId  UNIQUEIDENTIFIER — iptal edilecek StockTransfer.Id
--   @CompanyId UNIQUEIDENTIFIER — tenant filtresi
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: StockMovement (IsCancelled güncelleme + REVERSAL INSERT), StockTransfer (Status → CANCELLED)
-- THROW: 51320-51322 (PageModel catch: >= 50000 && < 60000)
-- BAĞIMLILIK: sp_GuardPeriodOpen
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_TransferReverse
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        DECLARE @Status NVARCHAR(20), @DocNo NVARCHAR(50);
        SELECT @Status = Status, @DocNo = DocNo
        FROM StockTransfer WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51320, N'Transfer belgesi bulunamadı.', 1;
        IF @Status = 'CANCELLED'
            THROW 51321, N'Belge zaten iptal edilmiş.', 1;
        IF @Status <> 'POSTED'
            THROW 51322, N'Yalnızca onaylanmış belgeler iptal edilebilir.', 1;

        -- Çıkış ve giriş hareketlerinin tümünü IsCancelled=1 ile kapat
        -- (bakiye = SUM(QtyBase WHERE IsCancelled=0))
        UPDATE StockMovement
        SET IsCancelled = 1, CancelledAt = GETUTCDATE(), CancelledBy = @UserId
        WHERE SourceDocType = 'TRANSFER' AND SourceDocId = @HeaderId AND IsCancelled = 0;

        -- İş kuralı: iptal edilecek aktif hareket yoksa veri tutarsızlığı.
        -- Ters (REVERSAL) satır YAZILMAZ: bakiye IsCancelled=0 filtresiyle hesaplandığından flag
        -- hareketleri zaten bakiyeden düşürür; ayrıca ters satır eklemek çift-sayıma yol açar.
        IF @@ROWCOUNT = 0
            THROW 51323, N'İptal edilecek transfer hareketi bulunamadı (veri tutarsızlığı).', 1;

        UPDATE StockTransfer
        SET Status = 'CANCELLED', UpdatedBy = @UserId
        WHERE Id = @HeaderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_CycleCountReverse — Döngüsel Sayım İptali
-- NE YAPAR: COMPLETED sayım belgesini CANCELLED yapar; COUNT_ADJ hareketlerini
--           IsCancelled=1 ile kapatır ve ters satır YAZMAZ (flag-only; bakiye SUM WHERE IsCancelled=0 ile düzelir — çift-sayım önlenir).
-- PARAMETRELERİ:
--   @HeaderId  UNIQUEIDENTIFIER — iptal edilecek CycleCount.Id
--   @CompanyId UNIQUEIDENTIFIER — tenant filtresi
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: StockMovement (IsCancelled güncelleme + REVERSAL INSERT), CycleCount (Status → CANCELLED)
-- THROW: 51330-51332 (PageModel catch: >= 50000 && < 60000)
-- BAĞIMLILIK: sp_GuardPeriodOpen
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CycleCountReverse
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        DECLARE @Status NVARCHAR(20), @DocNo NVARCHAR(50);
        SELECT @Status = Status, @DocNo = DocNo
        FROM CycleCount WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51330, N'Sayım belgesi bulunamadı.', 1;
        IF @Status = 'CANCELLED'
            THROW 51331, N'Belge zaten iptal edilmiş.', 1;
        IF @Status <> 'COMPLETED'
            THROW 51332, N'Yalnızca tamamlanmış sayımlar iptal edilebilir.', 1;

        -- Sayım düzeltme hareketlerini IsCancelled=1 ile kapat (bakiye = SUM(QtyBase WHERE IsCancelled=0))
        UPDATE StockMovement
        SET IsCancelled = 1, CancelledAt = GETUTCDATE(), CancelledBy = @UserId
        WHERE SourceDocType = 'COUNT' AND SourceDocId = @HeaderId AND IsCancelled = 0;

        -- İş kuralı: iptal edilecek aktif hareket yoksa veri tutarsızlığı.
        -- Ters (REVERSAL) satır YAZILMAZ: bakiye IsCancelled=0 filtresiyle hesaplandığından flag
        -- hareketi zaten bakiyeden düşürür; ayrıca ters satır eklemek çift-sayıma yol açar.
        IF @@ROWCOUNT = 0
            THROW 51333, N'İptal edilecek sayım hareketi bulunamadı (veri tutarsızlığı).', 1;

        UPDATE CycleCount
        SET Status = 'CANCELLED', UpdatedBy = @UserId
        WHERE Id = @HeaderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_ProductionReverse — Üretim Emri İptali
-- NE YAPAR: COMPLETED üretim emrini CANCELLED yapar; hammadde sarfı (ISSUE) ve
--           mamul girişi (RECEIPT) dahil tüm PRODUCTION hareketlerini IsCancelled=1
--           ile kapatır ve ters satır YAZMAZ (flag-only; bakiye SUM WHERE IsCancelled=0 ile düzelir — çift-sayım önlenir).
-- PARAMETRELERİ:
--   @OrderId   UNIQUEIDENTIFIER — iptal edilecek ProductionOrder.Id
--   @CompanyId UNIQUEIDENTIFIER — tenant filtresi
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: StockMovement (IsCancelled güncelleme + REVERSAL INSERT), ProductionOrder (Status → CANCELLED)
-- THROW: 51340-51342 (PageModel catch: >= 50000 && < 60000)
-- BAĞIMLILIK: sp_GuardPeriodOpen
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ProductionReverse
    @OrderId   UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        DECLARE @Status NVARCHAR(20), @DocNo NVARCHAR(50);
        SELECT @Status = Status, @DocNo = DocNo
        FROM ProductionOrder WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @OrderId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51340, N'Üretim emri bulunamadı.', 1;
        IF @Status = 'CANCELLED'
            THROW 51341, N'Belge zaten iptal edilmiş.', 1;
        IF @Status <> 'COMPLETED'
            THROW 51342, N'Yalnızca tamamlanmış üretim emirleri iptal edilebilir.', 1;

        -- Mamul girişi (RECEIPT) hareketlerini IsCancelled=1 ile kapat (bakiye = SUM(QtyBase WHERE IsCancelled=0))
        -- Ters (REVERSAL) satır YAZILMAZ: bakiye IsCancelled=0 filtresiyle hesaplandığından flag
        -- hareketleri zaten bakiyeden düşürür; ayrıca ters satır eklemek çift-sayıma yol açar.
        UPDATE StockMovement
        SET IsCancelled = 1, CancelledAt = GETUTCDATE(), CancelledBy = @UserId
        WHERE SourceDocType = 'PRODUCTION' AND SourceDocId = @OrderId AND IsCancelled = 0;

        -- İş kuralı: tamamlanmış emirde mamul girişi (RECEIPT) her zaman vardır; yoksa veri tutarsızlığı.
        IF @@ROWCOUNT = 0
            THROW 51343, N'İptal edilecek üretim hareketi bulunamadı (veri tutarsızlığı).', 1;

        -- İş kuralı (IMP-1 fix): hammadde sarfı (ISSUE) bu emrin pick task'larından
        -- SourceDocType='PICKING', SourceDocId=@TaskId ile yazılır (sp_PickLinePost). PickTask.SourceDocId
        -- üretim emrine bağlar. RECEIPT iptali bunları kapsamadığından ayrı kapatılır — aksi halde iptal
        -- sonrası hammadde sarf edilmiş kalır (hayalet sarfiyat). Malzeme toplanmamışsa 0 satır (hata değil).
        -- pt.ShipmentId IS NULL: yalnız sarfiyat (üretim) pick'leri — sp_PickLinePost ISSUE'yu da bu
        -- koşulla yazar (sevkiyat pick'i ledger yazmaz). Simetrik guard; sevkiyat pick'inden kesin izolasyon.
        UPDATE sm
        SET sm.IsCancelled = 1, sm.CancelledAt = GETUTCDATE(), sm.CancelledBy = @UserId
        FROM StockMovement sm
        JOIN PickTask pt ON pt.Id = sm.SourceDocId
        WHERE sm.SourceDocType = 'PICKING' AND sm.IsCancelled = 0
          AND pt.SourceDocId = @OrderId AND pt.CompanyId = @CompanyId
          AND pt.ShipmentId IS NULL;

        UPDATE ProductionOrder
        SET Status = 'CANCELLED', UpdatedBy = @UserId
        WHERE Id = @OrderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- FATURA + ÖDEME REVERSAL SP'LERİ — Plan 16 Faz 5A
-- AccountMovement immutability: silme yok — ters kayıt (REVERSAL).
-- THROW aralığı: 51400-51499.
-- =============================================================================

-- =============================================================================
-- sp_SalesInvoiceReverse — Satış Faturası İptali
-- NE YAPAR: POSTED satış faturasını CANCELLED yapar; açık PaymentPlan satırlarını
--           CANCELLED yapar ve AccountMovement'a negatif (Credit→ters Debit) REVERSAL
--           kaydı ekler. Bağlı tahsilat varsa işlemi reddeder.
-- PARAMETRELERİ:
--   @InvoiceId UNIQUEIDENTIFIER — iptal edilecek SalesInvoice.Id
--   @CompanyId UNIQUEIDENTIFIER — tenant filtresi
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: SalesInvoice (Status → CANCELLED), PaymentPlan (Status → CANCELLED), AccountMovement (REVERSAL INSERT)
-- THROW: 51400-51403 (PageModel catch: >= 50000 && < 60000)
-- BAĞIMLILIK: sp_GuardPeriodOpen
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_SalesInvoiceReverse
    @InvoiceId UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi (Plan 14)
        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        -- Belge bilgilerini al ve kilitle
        DECLARE @Status     NVARCHAR(20),
                @PartnerId  UNIQUEIDENTIFIER,
                @InvoiceNo  NVARCHAR(50),
                @GrandTotal DECIMAL(18,2),
                @Subtotal   DECIMAL(18,2),
                @TaxAmt     DECIMAL(18,2);

        SELECT @Status = Status, @PartnerId = PartnerId, @InvoiceNo = InvoiceNo,
               @GrandTotal = GrandTotal, @Subtotal = Subtotal, @TaxAmt = TaxAmount
        FROM SalesInvoice WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @InvoiceId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51400, N'Satış faturası bulunamadı.', 1;
        IF @Status = 'CANCELLED'
            THROW 51401, N'Fatura zaten iptal edilmiş.', 1;
        IF @Status <> 'POSTED'
            THROW 51402, N'Yalnızca onaylanmış faturalar iptal edilebilir.', 1;

        -- GİB e-belge koruması: OUTBOUND gönderilmiş fatura iptal edilemez
        -- Sadece DRAFT/CANCELLED/FAILED durumundaki envelope iptal akışına izin verir
        IF EXISTS (
            SELECT 1 FROM InvoiceEnvelope
            WHERE InvoiceId = @InvoiceId
              AND DirectionType = 'OUTBOUND'
              AND Status NOT IN ('DRAFT', 'CANCELLED', 'FAILED')
              AND IsDeleted = 0
        )
            THROW 51404, N'GİB e-belge gönderilmiş fatura iptal edilemez; iade faturası kesin.', 1;

        -- İmmutability: tahsil edilmiş ödeme varsa reject
        IF EXISTS (
            SELECT 1 FROM PaymentPlan
            WHERE SourceDocType = 'SALES_INVOICE' AND SourceDocId = @InvoiceId
              AND Status IN ('PAID','PARTIAL') AND FinancialTransactionId IS NOT NULL
        )
            THROW 51403, N'Bağlı tahsilat mevcut; önce tahsilatı iptal edin.', 1;

        -- Plan 18: faturanın AM hareketi açık-kalem eşleştirmesi varsa REJECT
        -- Önce sp_UnreconcileMovements ile eşleştirme geri alınmalı
        IF EXISTS (
            SELECT 1 FROM AccountReconciliation r
            JOIN AccountMovement am ON am.Id IN (r.DebitMovementId, r.CreditMovementId)
            WHERE am.SourceDocType = 'SALES_INVOICE' AND am.SourceDocId = @InvoiceId
              AND r.CompanyId = @CompanyId AND r.IsReversal = 0
        )
            THROW 51405, N'Fatura eşleştirilmiş; önce eşleştirmeyi geri alın (mutabakat).', 1;

        -- Mutabakat kilidi (Plan 19): onaylı mutabakatlı döneme iptal giremez
        EXEC dbo.sp_GuardPartnerReconciled @CompanyId, @PartnerId, @now;

        -- Fatura iptal
        UPDATE SalesInvoice
        SET Status = 'CANCELLED', UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @InvoiceId;

        -- PaymentPlan açıkları iptal
        UPDATE PaymentPlan
        SET Status = 'CANCELLED'
        WHERE SourceDocType = 'SALES_INVOICE' AND SourceDocId = @InvoiceId
          AND Status = 'OPEN';

        -- AM ters kayıt: Debit ters → Credit (REVERSAL SourceDocType ile unique)
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit,
             NetAmount, TaxAmount, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        VALUES (
            NEWID(), @CompanyId, @PartnerId, @now,
            0, @GrandTotal,
            -@Subtotal, -@TaxAmt, 'TRY',
            'REVERSAL', @InvoiceId, @InvoiceNo,
            N'Satış Faturası İptali: ' + @InvoiceNo, @UserId
        );

        -- Faturalama izini geri al: kaynak sevkiyat satırlarının InvoicedQty'sini düş (purchase ayna,
        -- db_objects_docchain.sql sp_PurchaseInvoiceReverse:571-576). Aksi halde iptal sonrası sevkiyat
        -- "faturalanmış" kalır → yeniden faturalanamaz (UI/SP çıkmazı). VUK 1:1 korunur: aynı anda yalnız
        -- TEK aktif fatura; iptal edilince sevkiyat tekrar faturalanabilir hale gelir.
        UPDATE sl
        SET sl.InvoicedQty = sl.InvoicedQty - x.TotalQty
        FROM ShippingLine sl
        JOIN (
            SELECT sil.SourceShipmentLineId, SUM(sil.Qty) AS TotalQty
            FROM SalesInvoiceLine sil
            WHERE sil.InvoiceId = @InvoiceId AND sil.SourceShipmentLineId IS NOT NULL
            GROUP BY sil.SourceShipmentLineId
        ) x ON x.SourceShipmentLineId = sl.Id;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_ExpenseInvoiceReverse — Alış Faturası İptali
-- NE YAPAR: POSTED alış faturasını CANCELLED yapar; açık PaymentPlan satırlarını
--           CANCELLED yapar ve ExpenseInvoiceLine satır bazında AccountMovement'a
--           negatif (Debit→ters Credit) REVERSAL kaydı ekler. Ödenmiş kayıt varsa reddeder.
-- PARAMETRELERİ:
--   @InvoiceId UNIQUEIDENTIFIER — iptal edilecek ExpenseInvoice.Id
--   @CompanyId UNIQUEIDENTIFIER — tenant filtresi
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: ExpenseInvoice (Status → CANCELLED), PaymentPlan (Status → CANCELLED), AccountMovement (satır bazlı REVERSAL INSERT)
-- THROW: 51410-51413 (PageModel catch: >= 50000 && < 60000)
-- BAĞIMLILIK: sp_GuardPeriodOpen
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ExpenseInvoiceReverse
    @InvoiceId UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        DECLARE @Status    NVARCHAR(20),
                @PartnerId UNIQUEIDENTIFIER,
                @DocNo     NVARCHAR(50);

        SELECT @Status = Status, @PartnerId = PartnerId, @DocNo = DocNo
        FROM ExpenseInvoice WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @InvoiceId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51410, N'Alış faturası bulunamadı.', 1;
        IF @Status = 'CANCELLED'
            THROW 51411, N'Fatura zaten iptal edilmiş.', 1;
        IF @Status <> 'POSTED'
            THROW 51412, N'Yalnızca onaylanmış faturalar iptal edilebilir.', 1;

        -- GİB e-belge koruması: INBOUND (tedarikçi gönderdi) kayıtlı fatura
        -- sistem iptali yapılamaz — iade/düzeltme faturası ile çözülmeli
        IF EXISTS (
            SELECT 1 FROM InvoiceEnvelope
            WHERE InvoiceId = @InvoiceId
              AND DirectionType = 'INBOUND'
              AND Status NOT IN ('DRAFT', 'CANCELLED', 'FAILED')
              AND IsDeleted = 0
        )
            THROW 51414, N'GİB üzerinden alınan e-fatura iptal edilemez; iade faturası kesin.', 1;

        -- İmmutability: ödenmiş PaymentPlan varsa reject
        IF EXISTS (
            SELECT 1 FROM PaymentPlan
            WHERE SourceDocType = 'PURCHASE_INVOICE' AND SourceDocId = @InvoiceId
              AND Status IN ('PAID','PARTIAL') AND FinancialTransactionId IS NOT NULL
        )
            THROW 51413, N'Bağlı ödeme mevcut; önce ödemeyi iptal edin.', 1;

        -- Plan 18: faturanın AM hareketi açık-kalem eşleştirmesi varsa REJECT
        IF EXISTS (
            SELECT 1 FROM AccountReconciliation r
            JOIN AccountMovement am ON am.Id IN (r.DebitMovementId, r.CreditMovementId)
            WHERE am.SourceDocType = 'PURCHASE_INVOICE' AND am.SourceDocId = @InvoiceId
              AND r.CompanyId = @CompanyId AND r.IsReversal = 0
        )
            THROW 51415, N'Fatura eşleştirilmiş; önce eşleştirmeyi geri alın (mutabakat).', 1;

        -- Mutabakat kilidi (Plan 19): onaylı mutabakatlı döneme iptal giremez
        EXEC dbo.sp_GuardPartnerReconciled @CompanyId, @PartnerId, @now;

        -- Fatura iptal
        UPDATE ExpenseInvoice
        SET Status = 'CANCELLED', UpdatedBy = @UserId
        WHERE Id = @InvoiceId;

        -- PaymentPlan açıkları iptal
        UPDATE PaymentPlan
        SET Status = 'CANCELLED'
        WHERE SourceDocType = 'PURCHASE_INVOICE' AND SourceDocId = @InvoiceId
          AND Status = 'OPEN';

        -- AM ters kayıt: BAŞLIK bazlı tek Debit (ters Credit); SourceDocId=InvoiceId
        -- (sp_ExpenseInvoicePost ile simetrik — başlık bazlı, gider analitiği view'da)
        DECLARE @revTotal DECIMAL(18,2), @revNet DECIMAL(18,2), @revTax DECIMAL(18,2);
        SELECT @revTotal = ISNULL(SUM(TotalAmount), 0),
               @revNet   = ISNULL(SUM(Amount), 0),
               @revTax   = ISNULL(SUM(TaxAmount), 0)
        FROM ExpenseInvoiceLine WHERE ExpenseInvoiceId = @InvoiceId;

        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit,
             NetAmount, TaxAmount, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        VALUES (
            NEWID(), @CompanyId, @PartnerId, @now,
            @revTotal, 0,
            -@revNet, -@revTax, 'TRY',
            'REVERSAL', @InvoiceId, @DocNo,
            N'Alış Faturası İptali: ' + @DocNo, @UserId
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_PaymentReverse — Tahsilat / Ödeme İptali
-- NE YAPAR: FinancialTransaction soft-delete (IsDeleted=1) yapar — sistem-içi
--   kasa/banka kaydı, e-Belge değil, ters kayıt gerekmez. AccountMovement
--   cari subledger → REVERSAL ters kayıt (VUK denetim izi). PaymentPlan OPEN döner.
-- PARAMETRELERİ:
--   @TransactionId UNIQUEIDENTIFIER — iptal edilecek FinancialTransaction.Id
--   @CompanyId     UNIQUEIDENTIFIER — tenant filtresi
--   @UserId        NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: FinancialTransaction (IsDeleted → 1), PaymentPlan (Status → OPEN,
--   FinancialTransactionId → NULL), AccountMovement (REVERSAL INSERT)
-- THROW: 51420-51421 (PageModel catch: >= 50000 && < 60000)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PaymentReverse
    @TransactionId UNIQUEIDENTIFIER,
    @CompanyId     UNIQUEIDENTIFIER,
    @UserId        NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        DECLARE @TxType    NVARCHAR(20),
                @Amount    DECIMAL(18,2),
                @Currency  NVARCHAR(3),
                @PartnerId UNIQUEIDENTIFIER,
                @DocNo     NVARCHAR(50);

        SELECT @TxType = TransactionType, @Amount = Amount,
               @Currency = ISNULL(Currency,'TRY'), @PartnerId = PartnerId,
               @DocNo = SourceDocNo
        FROM FinancialTransaction WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @TransactionId AND CompanyId = @CompanyId AND IsDeleted = 0;

        IF @TxType IS NULL
            THROW 51420, N'Finansal işlem bulunamadı veya zaten iptal edilmiş.', 1;
        IF @PartnerId IS NULL
            THROW 51421, N'Cari hesabı olmayan işlem iptal edilemez.', 1;

        -- Plan 18: ödeme/tahsilat AM hareketi açık-kalem eşleştirmesi varsa REJECT
        IF EXISTS (
            SELECT 1 FROM AccountReconciliation r
            JOIN AccountMovement am ON am.Id IN (r.DebitMovementId, r.CreditMovementId)
            WHERE am.SourceDocType IN ('COLLECTION','PAYMENT') AND am.SourceDocId = @TransactionId
              AND r.CompanyId = @CompanyId AND r.IsReversal = 0
        )
            THROW 51423, N'İşlem eşleştirilmiş; önce eşleştirmeyi geri alın (mutabakat).', 1;

        -- Mutabakat kilidi (Plan 19): onaylı mutabakatlı döneme iptal giremez
        EXEC dbo.sp_GuardPartnerReconciled @CompanyId, @PartnerId, @now;

        -- Çift-iptal koruması: zaten silinmişse THROW
        -- FinancialTransaction sistem-içi kayıt (e-Belge değil) → soft-delete meşru
        -- AccountMovement cari subledger → REVERSAL ters kayıt (aşağıda, VUK uyumu)
        UPDATE FinancialTransaction
        SET IsDeleted = 1
        WHERE Id = @TransactionId;

        -- İlişkili PaymentPlan'ı OPEN'a döndür
        UPDATE PaymentPlan
        SET Status = 'OPEN', FinancialTransactionId = NULL
        WHERE FinancialTransactionId = @TransactionId AND Status IN ('PAID','PARTIAL');

        -- AM ters kayıt: INCOME iptali → Debit / EXPENSE iptali → Credit
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        VALUES (
            NEWID(), @CompanyId, @PartnerId, @now,
            CASE WHEN @TxType = 'INCOME'  THEN @Amount ELSE 0 END,
            CASE WHEN @TxType = 'EXPENSE' THEN @Amount ELSE 0 END,
            @Currency,
            'REVERSAL', @TransactionId, @DocNo,
            N'İşlem İptali: ' + ISNULL(@DocNo, CAST(@TransactionId AS NVARCHAR(36))),
            @UserId
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
