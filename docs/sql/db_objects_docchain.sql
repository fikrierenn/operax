-- Plan 05: Belge Zinciri SP'leri — Downstream Create
-- Her SP: kaynak belgeden DRAFT hedef belge + satırlar oluşturur.
-- İdempotent: CREATE OR ALTER
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- =============================================================================
-- sp_CreateReceivingFromPO
-- NE YAPAR: POSTED PO'dan DRAFT ReceivingHeader + ReceivingLine'ları oluşturur.
--           Miktar: QtyOrdered - QtyReceived (kalan). Tüm satırlar teslim alınmışsa THROW.
-- PARAMETRELERİ:
--   @PoId          UNIQUEIDENTIFIER — kaynak PO
--   @CompanyId     UNIQUEIDENTIFIER — firma izolasyonu
--   @UserId        UNIQUEIDENTIFIER — oluşturan kullanıcı
--   @NewHeaderId   UNIQUEIDENTIFIER OUTPUT — oluşturulan Receiving ID
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CreateReceivingFromPO
    @PoId        UNIQUEIDENTIFIER,
    @CompanyId   UNIQUEIDENTIFIER,
    @UserId      UNIQUEIDENTIFIER,
    @NewHeaderId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- PO bilgilerini al ve sahiplik doğrula
        DECLARE @WarehouseId UNIQUEIDENTIFIER,
                @PartnerId   UNIQUEIDENTIFIER,
                @Status      NVARCHAR(50),
                @OrderNo     NVARCHAR(100);

        SELECT @WarehouseId = WarehouseId, @PartnerId = PartnerId,
               @Status = Status, @OrderNo = OrderNo
        FROM PurchaseOrderHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @PoId AND CompanyId = @CompanyId AND IsDeleted = 0;

        IF @WarehouseId IS NULL
            THROW 51500, N'Satınalma siparişi bulunamadı.', 1;

        -- İş kuralı: yalnızca POSTED PO'dan mal kabul açılabilir
        IF @Status NOT IN ('POSTED', 'APPROVED')
            THROW 51501, N'Yalnızca onaylanmış siparişten mal kabul oluşturulabilir.', 1;

        -- İş kuralı: kalan miktar > 0 olan satır olmalı
        IF NOT EXISTS (
            SELECT 1 FROM PurchaseOrderLine
            WHERE HeaderId = @PoId
              AND (QtyOrdered - ISNULL(QtyReceived, 0)) > 0.0001
        )
            THROW 51502, N'Tüm satırlar teslim alınmış; yeni mal kabul açılamaz.', 1;

        -- Belge no üret (RCV-YYYYMMDD-HHmmss)
        DECLARE @DocNo NVARCHAR(100) =
            'RCV-' + FORMAT(GETUTCDATE(), 'yyyyMMdd-HHmmss');

        SET @NewHeaderId = NEWID();

        -- DRAFT Receiving başlığı oluştur
        INSERT INTO ReceivingHeader
            (Id, CompanyId, WarehouseId, PartnerId, PurchaseOrderId,
             DocNo, Status, CreatedBy)
        VALUES
            (@NewHeaderId, @CompanyId, @WarehouseId, @PartnerId, @PoId,
             @DocNo, 'DRAFT', @UserId);

        -- Kalan miktar > 0 olan PO satırlarını kopyala
        INSERT INTO ReceivingLine
            (HeaderId, PurchaseOrderLineId, ItemId, UomId, QtyOriginal, QtyBase)
        SELECT
            @NewHeaderId,
            pol.Id,
            pol.ItemId,
            pol.UomId,
            pol.QtyOrdered - ISNULL(pol.QtyReceived, 0),   -- kalan miktar
            pol.QtyOrdered - ISNULL(pol.QtyReceived, 0)    -- QtyBase = QtyOriginal (temel birim varsayılır)
        FROM PurchaseOrderLine pol
        WHERE pol.HeaderId = @PoId
          AND (pol.QtyOrdered - ISNULL(pol.QtyReceived, 0)) > 0.0001;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_CreatePurchaseInvoiceFromReceiving (Plan 24 Faz C — eski sp_CreateExpenseInvoiceFromReceiving yerine)
-- NE YAPAR: POSTED Receiving'den DRAFT PurchaseInvoice + SATIRLAR oluşturur (boş fatura bug'ı çözüldü).
--           Mal satırı ItemId/UomId/Qty/UnitPrice (PO fiyatından) ile kopyalanır.
--           SourceReceivingLineId ile satır-bazlı bağ (N:1 hazır). InvoiceDate ön-dolu (POSTED'de override).
-- PARAMETRELERİ: @ReceivingId, @CompanyId, @UserId, @NewInvoiceId OUTPUT
-- THROW: 51510-51512
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CreatePurchaseInvoiceFromReceiving
    @ReceivingId  UNIQUEIDENTIFIER,
    @CompanyId    UNIQUEIDENTIFIER,
    @UserId       UNIQUEIDENTIFIER,
    @NewInvoiceId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @PartnerId UNIQUEIDENTIFIER, @Status NVARCHAR(50), @DocNo NVARCHAR(100);

        SELECT @PartnerId = PartnerId, @Status = Status, @DocNo = DocNo
        FROM ReceivingHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @ReceivingId AND CompanyId = @CompanyId AND IsDeleted = 0;

        IF @PartnerId IS NULL
            THROW 51510, N'Mal kabul belgesi bulunamadı.', 1;
        IF @Status <> 'POSTED'
            THROW 51511, N'Yalnızca onaylanmış mal kabulden fatura oluşturulabilir.', 1;

        -- İş kuralı: bu mal kabulden zaten fatura varsa yeni açma (satır-bazlı bağ üzerinden)
        IF EXISTS (
            SELECT 1
            FROM PurchaseInvoiceLine pil
            JOIN PurchaseInvoice pi ON pi.Id = pil.InvoiceId
            JOIN ReceivingLine rl ON rl.Id = pil.SourceReceivingLineId
            WHERE rl.HeaderId = @ReceivingId
              AND pi.Status <> 'CANCELLED' AND pi.CompanyId = @CompanyId
        )
            THROW 51512, N'Bu mal kabulden zaten fatura oluşturulmuş.', 1;

        DECLARE @InvDocNo NVARCHAR(50) = 'ALN-' + FORMAT(GETUTCDATE(), 'yyyyMMdd-HHmmss');
        SET @NewInvoiceId = NEWID();

        -- DRAFT başlık (tutarlar satır eklendikten sonra güncellenir)
        -- SupplierInvoiceNo/Date ön-dolu DocNo/bugün — kullanıcı POSTED öncesi gerçek değerle değiştirir
        INSERT INTO PurchaseInvoice
            (Id, CompanyId, PartnerId, DocNo, SupplierInvoiceNo, SupplierInvoiceDate,
             InvoiceDate, Subtotal, TaxAmount, GrandTotal, Status, CreatedBy)
        VALUES
            (@NewInvoiceId, @CompanyId, @PartnerId, @InvDocNo, @InvDocNo, CAST(GETUTCDATE() AS DATE),
             CAST(GETUTCDATE() AS DATE), 0, 0, 0, 'DRAFT', TRY_CAST(@UserId AS UNIQUEIDENTIFIER));

        -- Satırları kopyala (mal satırı — boş fatura bug'ı çözüldü)
        INSERT INTO PurchaseInvoiceLine
            (InvoiceId, ItemId, UomId, Qty, UnitPrice,
             LineSubtotal, TaxRatePercent, TaxAmount, LineTotal,
             SourceReceivingLineId, SourceLinkType)
        SELECT
            @NewInvoiceId, rl.ItemId, rl.UomId, rl.QtyBase, ISNULL(pol.Price, 0),
            rl.QtyBase * ISNULL(pol.Price, 0),
            20,
            rl.QtyBase * ISNULL(pol.Price, 0) * 0.20,
            rl.QtyBase * ISNULL(pol.Price, 0) * 1.20,
            rl.Id, 'LINKED'
        FROM ReceivingLine rl
        LEFT JOIN PurchaseOrderLine pol ON pol.Id = rl.PurchaseOrderLineId
        WHERE rl.HeaderId = @ReceivingId;

        -- Başlık tutarlarını satırlardan topla
        UPDATE PurchaseInvoice
        SET Subtotal   = (SELECT ISNULL(SUM(LineSubtotal), 0) FROM PurchaseInvoiceLine WHERE InvoiceId = @NewInvoiceId),
            TaxAmount  = (SELECT ISNULL(SUM(TaxAmount), 0)    FROM PurchaseInvoiceLine WHERE InvoiceId = @NewInvoiceId),
            GrandTotal = (SELECT ISNULL(SUM(LineTotal), 0)    FROM PurchaseInvoiceLine WHERE InvoiceId = @NewInvoiceId)
        WHERE Id = @NewInvoiceId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Eski yanlış SP'yi kaldır (ExpenseInvoice gider modeli mal satırı taşıyamıyordu — Plan 24)
IF OBJECT_ID('dbo.sp_CreateExpenseInvoiceFromReceiving', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_CreateExpenseInvoiceFromReceiving;
GO

-- =============================================================================
-- sp_CreateShippingFromSO
-- NE YAPAR: POSTED SO'dan DRAFT ShippingHeader + ShippingLine'ları oluşturur.
--           Miktar: SalesOrderLine.QtyOrdered - QtyShipped (kalan).
-- PARAMETRELERİ:
--   @SoId          UNIQUEIDENTIFIER — kaynak SO
--   @CompanyId     UNIQUEIDENTIFIER
--   @UserId        UNIQUEIDENTIFIER
--   @NewHeaderId   UNIQUEIDENTIFIER OUTPUT
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CreateShippingFromSO
    @SoId        UNIQUEIDENTIFIER,
    @CompanyId   UNIQUEIDENTIFIER,
    @UserId      UNIQUEIDENTIFIER,
    @NewHeaderId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- SO bilgilerini al
        DECLARE @WarehouseId UNIQUEIDENTIFIER,
                @PartnerId   UNIQUEIDENTIFIER,
                @Status      NVARCHAR(50),
                @OrderNo     NVARCHAR(100);

        SELECT @WarehouseId = WarehouseId, @PartnerId = PartnerId,
               @Status = Status, @OrderNo = OrderNo
        FROM SalesOrderHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @SoId AND CompanyId = @CompanyId AND IsDeleted = 0;

        IF @WarehouseId IS NULL
            THROW 51520, N'Satış siparişi bulunamadı.', 1;

        IF @Status NOT IN ('POSTED', 'APPROVED')
            THROW 51521, N'Yalnızca onaylanmış siparişten sevkiyat oluşturulabilir.', 1;

        -- Kalan miktar > 0 olan satır olmalı
        IF NOT EXISTS (
            SELECT 1 FROM SalesOrderLine
            WHERE HeaderId = @SoId
              AND (QtyOrdered - ISNULL(QtyShipped, 0)) > 0.0001
        )
            THROW 51522, N'Tüm satırlar sevk edilmiş; yeni sevkiyat açılamaz.', 1;

        DECLARE @ShipDocNo NVARCHAR(100) =
            'SHP-' + FORMAT(GETUTCDATE(), 'yyyyMMdd-HHmmss');

        SET @NewHeaderId = NEWID();

        -- DRAFT ShippingHeader oluştur
        INSERT INTO ShippingHeader
            (Id, CompanyId, WarehouseId, PartnerId, SalesOrderId,
             DocNo, Status, CreatedBy)
        VALUES
            (@NewHeaderId, @CompanyId, @WarehouseId, @PartnerId, @SoId,
             @ShipDocNo, 'DRAFT', @UserId);

        -- Kalan miktarlı SO satırlarını kopyala
        -- QtyOriginal NOT NULL — kalan miktar hem QtyOriginal hem QtyBase'e yazılır
        INSERT INTO ShippingLine
            (HeaderId, SalesOrderLineId, ItemId, UomId, QtyOriginal, QtyBase)
        SELECT
            @NewHeaderId,
            sol.Id,
            sol.ItemId,
            sol.UomId,
            sol.QtyOrdered - ISNULL(sol.QtyShipped, 0),   -- QtyOriginal
            sol.QtyOrdered - ISNULL(sol.QtyShipped, 0)    -- QtyBase = QtyOriginal
        FROM SalesOrderLine sol
        WHERE sol.HeaderId = @SoId
          AND (sol.QtyOrdered - ISNULL(sol.QtyShipped, 0)) > 0.0001;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_PurchaseInvoicePost — Mal alım faturası onaylama (DRAFT → POSTED)
-- NE YAPAR: PurchaseInvoice'ı POSTED yapar; cari deftere tek Credit (biz tedarikçiye borçlu);
--           PaymentPlan PAYABLE açar. Stok hareketi YAZMAZ (mal zaten Receiving'de girdi).
--           Cari besleme YALNIZCA burada (irsaliye/mal kabul beslemez — R0/K3 drift kapanır).
-- PARAMETRELERİ: @InvoiceId, @CompanyId, @UserId (NVARCHAR)
-- THROW: 51530-51539
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PurchaseInvoicePost
    @InvoiceId UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi (Plan 14) — tedarikçi belge tarihi ile (KDV/dönem doğru)
        DECLARE @Status      NVARCHAR(20),
                @PartnerId   UNIQUEIDENTIFIER,
                @DocNo       NVARCHAR(50),
                @SupInvNo    NVARCHAR(50),
                @SupInvDate  DATE,
                @DueDate     DATE,
                @Grand       DECIMAL(18,4),
                @Subtotal    DECIMAL(18,4),
                @TaxAmt      DECIMAL(18,4),
                @Currency    NVARCHAR(3);   -- ISO 4217; AccountMovement.Currency NVARCHAR(3) ile hizalı

        SELECT @Status = Status, @PartnerId = PartnerId, @DocNo = DocNo,
               @SupInvNo = SupplierInvoiceNo, @SupInvDate = SupplierInvoiceDate,
               @DueDate = DueDate, @Grand = GrandTotal, @Subtotal = Subtotal,
               @TaxAmt = TaxAmount, @Currency = ISNULL(Currency, 'TRY')
        FROM PurchaseInvoice WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @InvoiceId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51530, N'Alış faturası bulunamadı.', 1;
        IF @Status = 'POSTED'
            THROW 51531, N'Fatura zaten onaylanmış.', 1;
        IF @Status = 'CANCELLED'
            THROW 51532, N'İptal edilmiş fatura onaylanamaz.', 1;
        IF @PartnerId IS NULL
            THROW 51533, N'Fatura tedarikçi bilgisi eksik.', 1;
        -- Mevzuat: tedarikçi yasal belge no + tarihi zorunlu (BA/BS + KDV)
        IF @SupInvNo IS NULL OR LTRIM(RTRIM(@SupInvNo)) = ''
            THROW 51534, N'Tedarikçi fatura numarası zorunludur.', 1;
        IF @SupInvDate IS NULL
            THROW 51535, N'Tedarikçi fatura tarihi zorunludur.', 1;
        -- İş kuralı: birim fiyatı 0 (veya negatif) kalem ile fatura onaylanamaz
        IF EXISTS (SELECT 1 FROM PurchaseInvoiceLine WHERE InvoiceId = @InvoiceId AND UnitPrice <= 0)
            THROW 51536, N'Birim fiyatı girilmemiş kalem var. Tüm kalemlere fiyat giriniz.', 1;

        -- Dönem guard: cari borç tedarikçi belge tarihine ait
        DECLARE @nowDt DATETIME2 = CAST(@SupInvDate AS DATETIME2);
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowDt, @UserId;

        -- Mutabakat kilidi (Plan 19)
        EXEC dbo.sp_GuardPartnerReconciled @CompanyId, @PartnerId, @nowDt;

        UPDATE PurchaseInvoice
        SET Status = 'POSTED', UpdatedBy = TRY_CAST(@UserId AS UNIQUEIDENTIFIER), UpdatedAt = GETUTCDATE()
        WHERE Id = @InvoiceId;

        -- Ödeme planı: alış → PAYABLE (tedarikçiye borçluyuz)
        INSERT INTO PaymentPlan
            (Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
             PartnerId, Direction, InstallmentNo, TotalInstallments,
             DueDate, Amount, Status)
        VALUES
            (NEWID(), @CompanyId, 'PURCHASE_INVOICE', @InvoiceId, @DocNo,
             @PartnerId, 'PAYABLE', 1, 1,
             ISNULL(CAST(@DueDate AS DATETIME2), DATEADD(DAY, 30, @nowDt)), @Grand, 'OPEN');

        -- Cari hesap defteri: tek Credit (biz tedarikçiye borçluyuz) — cari besleme TEK NOKTA
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit,
             NetAmount, TaxAmount, DueDate, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        VALUES
            (NEWID(), @CompanyId, @PartnerId, @nowDt, 0, @Grand,
             @Subtotal, @TaxAmt, CAST(@DueDate AS DATETIME2), @Currency,
             'PURCHASE_INVOICE', @InvoiceId, @DocNo, N'Alış Faturası: ' + @SupInvNo, @UserId);

        -- Plan 27: Fatura↔Sipariş fiyat farkı (TOLERANS YOK — anlaşılan PO fiyatı bağlayıcı).
        -- Zincir: PurchaseInvoiceLine → ReceivingLine → PurchaseOrderLine.Price.
        -- Fatura fiyatı PO fiyatından her sapmada PriceVariance DRAFT açılır (onay yine başarılı).
        -- PO bağı olmayan (manuel) satır fark üretmez (pol.Price NULL → atlanır).
        INSERT INTO PriceVariance
            (Id, CompanyId, SourceDocType, SourceDocId, SourceLineId,
             ItemId, PartnerId, ExpectedPrice, ActualPrice,
             Variance, VariancePercent, Status, CreatedBy)
        SELECT
            NEWID(), @CompanyId, 'PURCHASE_INVOICE', @InvoiceId, pil.Id,
            pil.ItemId, @PartnerId, pol.Price, pil.UnitPrice,
            pil.UnitPrice - pol.Price,
            CASE WHEN pol.Price > 0 THEN (pil.UnitPrice - pol.Price) / pol.Price * 100 ELSE 0 END,
            'DRAFT', TRY_CAST(@UserId AS UNIQUEIDENTIFIER)
        FROM PurchaseInvoiceLine pil
        JOIN ReceivingLine    rl  ON rl.Id  = pil.SourceReceivingLineId
        JOIN PurchaseOrderLine pol ON pol.Id = rl.PurchaseOrderLineId
        WHERE pil.InvoiceId = @InvoiceId
          AND pol.Price IS NOT NULL
          AND pil.UnitPrice <> pol.Price;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_PurchaseInvoiceReverse — Mal alım faturası iptali (POSTED → CANCELLED)
-- NE YAPAR: AccountMovement ters-satır (REVERSAL), PaymentPlan iptal. Tahsilat/ödeme varsa reddet.
-- THROW: 51540-51549
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PurchaseInvoiceReverse
    @InvoiceId UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20), @PartnerId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50),
                @Grand DECIMAL(18,4), @Subtotal DECIMAL(18,4), @TaxAmt DECIMAL(18,4),
                @SupInvDate DATE;

        SELECT @Status = Status, @PartnerId = PartnerId, @DocNo = DocNo,
               @Grand = GrandTotal, @Subtotal = Subtotal, @TaxAmt = TaxAmount,
               @SupInvDate = SupplierInvoiceDate
        FROM PurchaseInvoice WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @InvoiceId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51540, N'Alış faturası bulunamadı.', 1;
        IF @Status = 'CANCELLED'
            THROW 51541, N'Fatura zaten iptal edilmiş.', 1;
        IF @Status <> 'POSTED'
            THROW 51542, N'Yalnızca onaylanmış faturalar iptal edilebilir.', 1;

        -- İmmutability: ödenmiş ödeme varsa reddet
        IF EXISTS (
            SELECT 1 FROM PaymentPlan
            WHERE SourceDocType = 'PURCHASE_INVOICE' AND SourceDocId = @InvoiceId
              AND Status IN ('PAID','PARTIAL') AND FinancialTransactionId IS NOT NULL
        )
            THROW 51543, N'Bağlı ödeme mevcut; önce ödemeyi iptal edin.', 1;

        DECLARE @nowDt DATETIME2 = CAST(@SupInvDate AS DATETIME2);

        UPDATE PurchaseInvoice
        SET Status = 'CANCELLED', UpdatedBy = TRY_CAST(@UserId AS UNIQUEIDENTIFIER), UpdatedAt = GETUTCDATE()
        WHERE Id = @InvoiceId;

        -- Açık PaymentPlan iptal
        UPDATE PaymentPlan
        SET Status = 'CANCELLED'
        WHERE SourceDocType = 'PURCHASE_INVOICE' AND SourceDocId = @InvoiceId AND Status = 'OPEN';

        -- AccountMovement ters kayıt: orijinal Credit'i nötrlemek için Debit
        -- DueDate ters satırda yazılmaz (vadesiz) — bakiye SUM(Debit-Credit) ile doğru nötrlenir
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit,
             NetAmount, TaxAmount, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        VALUES
            (NEWID(), @CompanyId, @PartnerId, @nowDt, @Grand, 0,
             -@Subtotal, -@TaxAmt, 'TRY',
             'REVERSAL', @InvoiceId, @DocNo, N'Alış Faturası İptali: ' + @DocNo, @UserId);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_CorrectPurchaseInvoiceLine — Alış faturası satır fiyat DÜZELTME (POSTED kalır)
-- Plan 27. Veri-giriş hatası düzeltme: tedarikçinin gerçek faturasını yanlış girdik.
-- TTK md.65: ledger silinmez → AccountMovement TERS-kayıt (eski) + YENİ kayıt (doğru) = append-only.
-- Fatura POSTED + aynı DocNo + tedarikçi fatura no/tarih DEĞİŞMEZ. Ödeme varsa REDDEDİLİR.
-- THROW: 51560-51569
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CorrectPurchaseInvoiceLine
    @InvoiceId    UNIQUEIDENTIFIER,
    @LineId       UNIQUEIDENTIFIER,
    @NewUnitPrice DECIMAL(18,4),
    @CompanyId    UNIQUEIDENTIFIER,
    @UserId       NVARCHAR(450),
    @Reason       NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20), @PartnerId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50),
                @SupInvNo NVARCHAR(50), @SupInvDate DATE, @DueDate DATE, @Currency NVARCHAR(3),
                @OldGrand DECIMAL(18,4), @OldSub DECIMAL(18,4), @OldTax DECIMAL(18,4);

        SELECT @Status = Status, @PartnerId = PartnerId, @DocNo = DocNo,
               @SupInvNo = SupplierInvoiceNo, @SupInvDate = SupplierInvoiceDate, @DueDate = DueDate,
               @Currency = ISNULL(Currency,'TRY'),
               @OldGrand = GrandTotal, @OldSub = Subtotal, @OldTax = TaxAmount
        FROM PurchaseInvoice WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @InvoiceId AND CompanyId = @CompanyId;

        IF @Status IS NULL          THROW 51560, N'Alış faturası bulunamadı.', 1;
        IF @Status <> 'POSTED'      THROW 51561, N'Yalnızca onaylanmış fatura düzeltilebilir.', 1;
        IF @NewUnitPrice < 0        THROW 51562, N'Birim fiyat negatif olamaz.', 1;
        IF @Reason IS NULL OR LTRIM(RTRIM(@Reason)) = ''
            THROW 51563, N'Düzeltme gerekçesi zorunludur.', 1;
        -- İş kuralı: ödenmiş/kısmi ödenmiş plan varsa düzeltme reddedilir (önce ödeme iptali)
        IF EXISTS (SELECT 1 FROM PaymentPlan
                   WHERE SourceDocType='PURCHASE_INVOICE' AND SourceDocId=@InvoiceId
                     AND Status IN ('PAID','PARTIAL') AND FinancialTransactionId IS NOT NULL)
            THROW 51564, N'Bağlı ödeme mevcut; önce ödemeyi iptal edin.', 1;
        IF NOT EXISTS (SELECT 1 FROM PurchaseInvoiceLine WHERE Id=@LineId AND InvoiceId=@InvoiceId)
            THROW 51565, N'Düzeltilecek fatura kalemi bulunamadı.', 1;

        -- Dönem kilidi: tedarikçi belge tarihiyle (KDV/dönem doğru)
        DECLARE @nowDt DATETIME2 = CAST(@SupInvDate AS DATETIME2);
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowDt, @UserId;

        -- 1) Satır fiyatını + bağlı tutarları güncelle
        UPDATE PurchaseInvoiceLine
        SET UnitPrice    = @NewUnitPrice,
            LineSubtotal = Qty * @NewUnitPrice,
            TaxAmount    = ROUND(Qty * @NewUnitPrice * TaxRatePercent / 100.0, 2),
            LineTotal    = ROUND(Qty * @NewUnitPrice * (1 + TaxRatePercent / 100.0), 2)
        WHERE Id = @LineId AND InvoiceId = @InvoiceId;

        -- 2) Başlık toplamlarını kalemlerden yeniden hesapla
        DECLARE @NewSub DECIMAL(18,4), @NewTax DECIMAL(18,4), @NewGrand DECIMAL(18,4);
        SELECT @NewSub = ISNULL(SUM(LineSubtotal),0), @NewTax = ISNULL(SUM(TaxAmount),0)
        FROM PurchaseInvoiceLine WHERE InvoiceId = @InvoiceId;
        SET @NewGrand = @NewSub + @NewTax;

        UPDATE PurchaseInvoice
        SET Subtotal=@NewSub, TaxAmount=@NewTax, GrandTotal=@NewGrand, UpdatedAt=GETUTCDATE(),
            UpdatedBy=TRY_CAST(@UserId AS UNIQUEIDENTIFIER)
        WHERE Id = @InvoiceId;

        -- 3) Cari defter: veri-giriş hatası düzeltmesi (ödeme yok, GİB'e gitmemiş alış faturası).
        --    Ters/delta kayıt GEREKSİZ — yanlış girilen movement satırı doğru tutara YERİNDE güncellenir.
        --    İz audit log'da tutulur (eski→yeni + gerekçe). Cari ekstre temiz kalır.
        UPDATE dbo.AccountMovement
        SET Credit = @NewGrand, NetAmount = @NewSub, TaxAmount = @NewTax, MovementDate = @nowDt
        WHERE SourceDocType = 'PURCHASE_INVOICE' AND SourceDocId = @InvoiceId;

        -- 4) Açık ödeme planını yeni tutara çek (ödenmemiş)
        UPDATE PaymentPlan SET Amount = @NewGrand
        WHERE SourceDocType='PURCHASE_INVOICE' AND SourceDocId=@InvoiceId AND Status='OPEN';

        -- 5) Denetim izi (gerekçe)
        INSERT INTO AuditLog (Id, CompanyId, UserId, Action, EntityType, EntityId, Details, CreatedAt)
        VALUES (NEWID(), @CompanyId, TRY_CAST(@UserId AS UNIQUEIDENTIFIER), 'CORRECT_INVOICE_LINE',
                'PurchaseInvoice', @InvoiceId,
                N'Eski tutar ' + CONVERT(NVARCHAR,@OldGrand) + N' → yeni ' + CONVERT(NVARCHAR,@NewGrand)
                + N'. Gerekçe: ' + @Reason, GETUTCDATE());

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
