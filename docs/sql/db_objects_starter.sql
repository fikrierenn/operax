-- =============================================================================
-- STARTER paketi için Stored Procedure'ler
-- Modüller: M02 Maliyet, M03 Satınalma, M04 Satış+Fatura, M11 Finans
-- Tüm SP'ler CREATE OR ALTER (idempotent, tekrar tekrar çalıştırılabilir)
-- =============================================================================
SET NOCOUNT ON;
GO

-- =============================================================================
-- M02 MALİYET: sp_UpdateItemCostMovingAvg
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_UpdateItemCostMovingAvg
    @CompanyId   UNIQUEIDENTIFIER,
    @ItemId      UNIQUEIDENTIFIER,
    @WarehouseId UNIQUEIDENTIFIER = NULL,
    @Qty         DECIMAL(18,6),       -- pozitif (RECEIPT) veya pozitif (ISSUE — adet, isaret @MovementType'tan)
    @UnitCost    DECIMAL(18,4) = NULL, -- RECEIPT'ta dolu, ISSUE'da NULL (mevcut AvgCost kullanilir)
    @MovementType NVARCHAR(20)        -- RECEIPT, ISSUE, ADJUSTMENT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Item cost satiri yoksa olustur
    IF NOT EXISTS (
        SELECT 1 FROM ItemCost
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId
          AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL))
    )
    BEGIN
        INSERT INTO ItemCost (Id, CompanyId, ItemId, WarehouseId, AvgCost, OnHandQty)
        VALUES (NEWID(), @CompanyId, @ItemId, @WarehouseId, 0, 0);
    END

    DECLARE @OldAvg DECIMAL(18,4), @OldQty DECIMAL(18,6);
    SELECT @OldAvg = AvgCost, @OldQty = OnHandQty
    FROM ItemCost
    WHERE CompanyId = @CompanyId AND ItemId = @ItemId
      AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL));

    IF @MovementType = 'RECEIPT'
    BEGIN
        -- İş kuralı: Hareketli ağırlıklı ortalama formülü
        DECLARE @NewQty DECIMAL(18,6) = @OldQty + @Qty;
        DECLARE @NewAvg DECIMAL(18,4) =
            CASE WHEN @NewQty > 0
                 THEN ((@OldQty * @OldAvg) + (@Qty * ISNULL(@UnitCost, @OldAvg))) / @NewQty
                 ELSE ISNULL(@UnitCost, @OldAvg)
            END;
        UPDATE ItemCost
        SET AvgCost          = @NewAvg,
            OnHandQty        = @NewQty,
            LastReceiptDate  = GETUTCDATE(),
            UpdatedAt        = GETUTCDATE()
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId
          AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL));
    END
    ELSE IF @MovementType IN ('ISSUE', 'ADJUSTMENT')
    BEGIN
        -- Çıkışta AvgCost değişmez; sadece OnHandQty düşer
        UPDATE ItemCost
        SET OnHandQty = OnHandQty - @Qty,
            UpdatedAt = GETUTCDATE()
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId
          AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL));
    END
END
GO

-- =============================================================================
-- M03 SATINALMA: sp_CheckPriceVariance
-- PO/PurchaseOrderLine eklenirken çağrılır; tedarikçi liste fiyatı ile karşılaştırır
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CheckPriceVariance
    @CompanyId      UNIQUEIDENTIFIER,
    @PoHeaderId     UNIQUEIDENTIFIER,
    @PoLineId       UNIQUEIDENTIFIER,
    @ItemId         UNIQUEIDENTIFIER,
    @PartnerId      UNIQUEIDENTIFIER,
    @ActualPrice    DECIMAL(18,4),
    @UserId         UNIQUEIDENTIFIER = NULL,
    @VarianceId     UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @VarianceId = NULL;

    DECLARE @ExpectedPrice DECIMAL(18,4), @Tolerance DECIMAL(8,4);

    -- İş kuralı: PriceList üzerinde önce tedarikçi-özel, yoksa genel liste; Direction=PURCHASE
    SELECT TOP 1 @ExpectedPrice = pll.UnitPrice
    FROM PriceList pl
    JOIN PriceListLine pll ON pll.PriceListId = pl.Id
    WHERE pl.CompanyId = @CompanyId
      AND pl.Direction = 'PURCHASE'
      AND pl.PartnerId = @PartnerId
      AND pl.IsActive = 1
      AND pll.ItemId = @ItemId
      AND (pl.ValidFrom IS NULL OR pl.ValidFrom <= CAST(GETUTCDATE() AS DATE))
      AND (pl.ValidTo   IS NULL OR pl.ValidTo   >= CAST(GETUTCDATE() AS DATE))
    ORDER BY pll.MinQty DESC;

    IF @ExpectedPrice IS NULL
        SELECT TOP 1 @ExpectedPrice = pll.UnitPrice
        FROM PriceList pl
        JOIN PriceListLine pll ON pll.PriceListId = pl.Id
        WHERE pl.CompanyId = @CompanyId
          AND pl.Direction = 'PURCHASE'
          AND pl.PartnerId IS NULL
          AND pl.IsActive = 1
          AND pll.ItemId = @ItemId
        ORDER BY pll.MinQty DESC;

    -- Liste fiyatı yoksa sapma kontrolü yapılamaz
    IF @ExpectedPrice IS NULL RETURN;

    -- Tolerans yüzdesini parametre tablosundan al
    SELECT @Tolerance = CAST(Value AS DECIMAL(8,4))
    FROM Parameter
    WHERE CompanyId = @CompanyId AND Code = 'PriceTolerancePercent';
    SET @Tolerance = ISNULL(@Tolerance, 5);  -- varsayılan %5

    -- Sapma hesabı
    DECLARE @Variance DECIMAL(18,4)        = @ActualPrice - @ExpectedPrice;
    DECLARE @VariancePct DECIMAL(8,4)      =
        CASE WHEN @ExpectedPrice > 0 THEN @Variance / @ExpectedPrice * 100 ELSE 0 END;

    -- Tolerans altında ise kayıt oluşturma
    IF ABS(@VariancePct) <= @Tolerance RETURN;

    -- Sapma var ve eşiğin üzerinde: PriceVariance kaydı
    SET @VarianceId = NEWID();
    INSERT INTO PriceVariance (
        Id, CompanyId, SourceDocType, SourceDocId, SourceLineId,
        ItemId, PartnerId, ExpectedPrice, ActualPrice,
        Variance, VariancePercent, Status, CreatedBy
    )
    VALUES (
        @VarianceId, @CompanyId, 'PURCHASE_ORDER', @PoHeaderId, @PoLineId,
        @ItemId, @PartnerId, @ExpectedPrice, @ActualPrice,
        @Variance, @VariancePct, 'DRAFT', @UserId
    );
END
GO

-- =============================================================================
-- M03 SATINALMA: sp_ApprovePriceVariance
-- Fiyat farkı onayı — PriceVariance DRAFT → APPROVED.
-- PO satır fiyatı gerçek (ActualPrice) değere güncellenir.
-- THROW kodları: 51100-51199 (M03 slot).
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ApprovePriceVariance
    @VarianceId UNIQUEIDENTIFIER,
    @UserId     UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20), @SourceLineId UNIQUEIDENTIFIER,
                @ActualPrice DECIMAL(18,4), @SourceDocType NVARCHAR(50);

        SELECT @Status = Status, @SourceLineId = SourceLineId,
               @ActualPrice = ActualPrice, @SourceDocType = SourceDocType
        FROM PriceVariance WHERE Id = @VarianceId AND IsDeleted = 0;

        IF @Status IS NULL
            THROW 51101, N'Fiyat farkı kaydı bulunamadı.', 1;
        IF @Status <> 'DRAFT'
            THROW 51102, N'Sadece bekleyen (DRAFT) fiyat farkları onaylanabilir.', 1;

        -- PO satır fiyatını gerçek değere güncelle
        IF @SourceDocType = 'PURCHASE_ORDER'
            UPDATE PurchaseOrderLine
            SET Price = @ActualPrice
            WHERE Id = @SourceLineId;

        -- Variance kaydını onayla
        UPDATE PriceVariance
        SET Status     = 'APPROVED',
            ApprovedBy = @UserId,
            ApprovedAt = GETUTCDATE()
        WHERE Id = @VarianceId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- M04 SATIŞ FATURASI: sp_GenerateSalesInvoiceFromShipping
-- Sevkiyat POSTED olunca otomatik fatura üretir, PaymentPlan kaydı oluşturur
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_GenerateSalesInvoiceFromShipping
    @ShippingId    UNIQUEIDENTIFIER,
    @UserId        UNIQUEIDENTIFIER = NULL,
    @NewInvoiceId  UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CompanyId   UNIQUEIDENTIFIER,
                @PartnerId   UNIQUEIDENTIFIER,
                @SoId        UNIQUEIDENTIFIER,
                @ShipStatus  NVARCHAR(20);

        SELECT @CompanyId  = CompanyId,
               @PartnerId  = PartnerId,
               @SoId       = SalesOrderId,
               @ShipStatus = Status
        FROM ShippingHeader
        WHERE Id = @ShippingId AND IsDeleted = 0;

        IF @CompanyId IS NULL THROW 70001, N'Sevkiyat bulunamadı.', 1;
        IF @ShipStatus <> 'POSTED' THROW 70002, N'Sadece POSTED sevkiyatlar faturalanabilir.', 1;

        -- Bu sevkiyat için zaten fatura varsa hata
        IF EXISTS (SELECT 1 FROM SalesInvoice WHERE ShippingId = @ShippingId AND IsDeleted = 0)
            THROW 70003, N'Bu sevkiyat için zaten fatura oluşturulmuş.', 1;

        -- PartnerId boşsa ilk satırdaki SO'dan al
        IF @PartnerId IS NULL
        BEGIN
            SELECT TOP 1 @PartnerId = soh.PartnerId, @SoId = soh.Id
            FROM ShippingLine sl
            JOIN SalesOrderLine sol ON sol.Id = sl.SalesOrderLineId
            JOIN SalesOrderHeader soh ON soh.Id = sol.HeaderId
            WHERE sl.HeaderId = @ShippingId;

            -- Şimdi shipping'e geriye yaz (bir sonraki çağrı için)
            UPDATE ShippingHeader SET PartnerId = @PartnerId, SalesOrderId = @SoId WHERE Id = @ShippingId;
        END

        IF @PartnerId IS NULL
            THROW 70004, N'Sevkiyat müşterisi belirlenemedi (Partner referansı yok).', 1;

        -- Fatura numarası üret (basit dizi — gelecekte sp_GenerateDocNo'dan alacak)
        DECLARE @InvoiceNo NVARCHAR(50);
        DECLARE @InvoiceCount INT;
        SELECT @InvoiceCount = ISNULL(MAX(CAST(SUBSTRING(InvoiceNo, 10, 5) AS INT)), 0)
        FROM SalesInvoice
        WHERE CompanyId = @CompanyId AND InvoiceNo LIKE 'INV-' + CAST(YEAR(GETUTCDATE()) AS NVARCHAR(4)) + '-%';
        SET @InvoiceNo = 'INV-' + CAST(YEAR(GETUTCDATE()) AS NVARCHAR(4)) + '-'
                       + RIGHT('00000' + CAST(@InvoiceCount + 1 AS NVARCHAR(5)), 5);

        SET @NewInvoiceId = NEWID();

        DECLARE @PaymentTermDays INT;
        SELECT @PaymentTermDays = ISNULL(PaymentTermDays, 30) FROM Partner WHERE Id = @PartnerId;
        DECLARE @DueDate DATE = DATEADD(DAY, @PaymentTermDays, CAST(GETUTCDATE() AS DATE));

        -- Boş fatura başlığı
        INSERT INTO SalesInvoice (
            Id, CompanyId, InvoiceNo, InvoiceDate, DueDate,
            PartnerId, ShippingId, SalesOrderId,
            Subtotal, TaxAmount, GrandTotal, Status, CreatedBy
        )
        VALUES (
            @NewInvoiceId, @CompanyId, @InvoiceNo, GETUTCDATE(), @DueDate,
            @PartnerId, @ShippingId, @SoId,
            0, 0, 0, 'POSTED', @UserId
        );

        -- Satırları sevkiyat satırlarından doldur
        INSERT INTO SalesInvoiceLine (
            Id, InvoiceId, ItemId, UomId, Description,
            Qty, UnitPrice, LineSubtotal, TaxRatePercent, TaxAmount, LineTotal, UnitCost
        )
        SELECT
            NEWID(),
            @NewInvoiceId,
            sl.ItemId,
            sl.UomId,
            i.Name,
            sl.QtyBase,
            ISNULL(sol.Price, ISNULL(i.SalesPrice, 0))                                 AS UnitPrice,
            sl.QtyBase * ISNULL(sol.Price, ISNULL(i.SalesPrice, 0))                    AS LineSubtotal,
            ISNULL(i.TaxRate, 20)                                                      AS TaxRatePercent,
            sl.QtyBase * ISNULL(sol.Price, ISNULL(i.SalesPrice, 0)) * ISNULL(i.TaxRate, 20) / 100 AS TaxAmount,
            sl.QtyBase * ISNULL(sol.Price, ISNULL(i.SalesPrice, 0)) * (1 + ISNULL(i.TaxRate, 20) / 100) AS LineTotal,
            ic.AvgCost                                                                 AS UnitCost
        FROM ShippingLine sl
        JOIN Item i ON i.Id = sl.ItemId
        LEFT JOIN SalesOrderLine sol ON sol.Id = sl.SalesOrderLineId
        LEFT JOIN ItemCost ic ON ic.CompanyId = @CompanyId AND ic.ItemId = sl.ItemId
        WHERE sl.HeaderId = @ShippingId;

        -- Başlık toplamlarını güncelle
        UPDATE SalesInvoice
        SET Subtotal   = (SELECT ISNULL(SUM(LineSubtotal), 0) FROM SalesInvoiceLine WHERE InvoiceId = @NewInvoiceId),
            TaxAmount  = (SELECT ISNULL(SUM(TaxAmount), 0)    FROM SalesInvoiceLine WHERE InvoiceId = @NewInvoiceId),
            GrandTotal = (SELECT ISNULL(SUM(LineTotal), 0)    FROM SalesInvoiceLine WHERE InvoiceId = @NewInvoiceId)
        WHERE Id = @NewInvoiceId;

        -- PaymentPlan kaydı oluştur (tek taksit varsayımıyla; çoklu taksit için ileride genişler)
        INSERT INTO PaymentPlan (
            Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
            PartnerId, Direction, InstallmentNo, TotalInstallments, DueDate, Amount, Status
        )
        SELECT
            NEWID(), @CompanyId, 'SALES_INVOICE', @NewInvoiceId, @InvoiceNo,
            @PartnerId, 'RECEIVABLE', 1, 1, @DueDate, GrandTotal, 'OPEN'
        FROM SalesInvoice WHERE Id = @NewInvoiceId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- M11 ÇEKİ: sp_DepositCheque
-- PORTFOLIO -> IN_BANK (bankaya tahsile verme)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_DepositCheque
    @ChequeId    UNIQUEIDENTIFIER,
    @AccountId   UNIQUEIDENTIFIER,
    @DepositDate DATETIME2 = NULL,
    @UserId      UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Status NVARCHAR(20);
    SELECT @Status = Status FROM Cheque WHERE Id = @ChequeId AND IsDeleted = 0;

    IF @Status IS NULL THROW 60001, N'Çek bulunamadı.', 1;
    IF @Status <> 'PORTFOLIO' THROW 60002, N'Sadece portföydeki çekler bankaya verilebilir.', 1;

    UPDATE Cheque
    SET Status               = 'IN_BANK',
        DepositedToAccountId = @AccountId,
        DepositedAt          = ISNULL(@DepositDate, GETUTCDATE()),
        UpdatedAt            = GETUTCDATE(),
        UpdatedBy            = @UserId
    WHERE Id = @ChequeId;
END
GO

-- =============================================================================
-- M11 ÇEKİ: sp_CollectCheque
-- IN_BANK -> COLLECTED + FinancialTransaction (INCOME)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CollectCheque
    @ChequeId    UNIQUEIDENTIFIER,
    @CollectDate DATETIME2 = NULL,
    @UserId      UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20), @Amount DECIMAL(18,2),
                @AccountId UNIQUEIDENTIFIER, @PartnerId UNIQUEIDENTIFIER,
                @CompanyId UNIQUEIDENTIFIER, @ChequeNo NVARCHAR(50);

        SELECT @Status = Status, @Amount = Amount, @AccountId = DepositedToAccountId,
               @PartnerId = PartnerId, @CompanyId = CompanyId, @ChequeNo = ChequeNo
        FROM Cheque WHERE Id = @ChequeId AND IsDeleted = 0;

        IF @Status IS NULL THROW 60001, N'Çek bulunamadı.', 1;
        IF @Status <> 'IN_BANK'
            THROW 60003, N'Sadece bankaya verilmiş çekler tahsil edilebilir.', 1;
        IF @AccountId IS NULL
            THROW 60005, N'Çekin tahsile verildiği banka hesabı bulunamadı.', 1;

        DECLARE @TxId UNIQUEIDENTIFIER = NEWID();
        DECLARE @Now  DATETIME2        = ISNULL(@CollectDate, GETUTCDATE());

        INSERT INTO FinancialTransaction (
            Id, CompanyId, AccountId, TransactionDate, TransactionType,
            Amount, Currency, AmountTRY, PartnerId, Description,
            InstrumentType, InstrumentId, SourceDocType, SourceDocNo, CreatedBy
        )
        VALUES (
            @TxId, @CompanyId, @AccountId, @Now, 'INCOME',
            @Amount, 'TRY', @Amount, @PartnerId, N'Çek tahsili: ' + @ChequeNo,
            'CHEQUE', @ChequeId, 'CHEQUE_COLLECTION', @ChequeNo, @UserId
        );

        UPDATE Cheque
        SET Status      = 'COLLECTED',
            CollectedAt = @Now,
            UpdatedAt   = GETUTCDATE(),
            UpdatedBy   = @UserId
        WHERE Id = @ChequeId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- M11 ÇEKİ: sp_ReturnCheque
-- Karşılıksız çek (IN_BANK/PORTFOLIO -> RETURNED). FinancialTransaction üretmez.
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ReturnCheque
    @ChequeId  UNIQUEIDENTIFIER,
    @Reason    NVARCHAR(500),
    @UserId    UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Cheque
    SET Status       = 'RETURNED',
        ReturnReason = @Reason,
        UpdatedAt    = GETUTCDATE(),
        UpdatedBy    = @UserId
    WHERE Id = @ChequeId AND Status IN ('IN_BANK', 'PORTFOLIO');

    IF @@ROWCOUNT = 0
        THROW 60004, N'Çek karşılıksız olarak işaretlenemiyor (statü uygun değil).', 1;
END
GO

-- =============================================================================
-- M11 KART: sp_CloseStatement — dönem ekstre kapatma
-- Periyot içi ekstresiz slipleri toplar, CreditCardStatement oluşturur,
-- slipleri ekstreye bağlar, önceki dönem devrini ekler.
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CloseStatement
    @CardId          UNIQUEIDENTIFIER,
    @PeriodStart     DATE,
    @PeriodEnd       DATE,
    @UserId          UNIQUEIDENTIFIER = NULL,
    @NewStatementId  UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @DueDay INT;
        SELECT @DueDay = DueDay FROM CreditCard WHERE Id = @CardId;
        IF @DueDay IS NULL THROW 51320, N'Kredi kartı bulunamadı.', 1;

        -- Periyot içi ekstreye girmemiş slip toplamı
        DECLARE @Total DECIMAL(18,2) = ISNULL((
            SELECT SUM(Amount) FROM CreditCardTransaction
            WHERE CardId = @CardId AND StatementId IS NULL
              AND CAST(TransactionDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd), 0);

        -- Önceki kapanmamış bakiye (devir)
        DECLARE @Opening DECIMAL(18,2) = ISNULL((
            SELECT TOP 1 ClosingBalance - PaidAmount FROM CreditCardStatement
            WHERE CardId = @CardId ORDER BY PeriodEnd DESC), 0);

        SET @NewStatementId = NEWID();
        DECLARE @DueDate DATE = DATEFROMPARTS(
            YEAR(DATEADD(MONTH, 1, @PeriodEnd)), MONTH(DATEADD(MONTH, 1, @PeriodEnd)), @DueDay);

        INSERT INTO CreditCardStatement (
            Id, CardId, PeriodStart, PeriodEnd, StatementDate, DueDate,
            OpeningBalance, TotalDebit, TotalCredit, ClosingBalance,
            MinPayment, PaidAmount, IsClosed)
        VALUES (
            @NewStatementId, @CardId, @PeriodStart, @PeriodEnd, @PeriodEnd, @DueDate,
            @Opening, @Total, 0, @Opening + @Total,
            (@Opening + @Total) * 0.20, 0, 1);

        UPDATE CreditCardTransaction
        SET StatementId = @NewStatementId
        WHERE CardId = @CardId AND StatementId IS NULL
          AND CAST(TransactionDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- M11 KART: sp_PayCreditCardStatement — ekstre ödeme
-- Banka hesabından gider yazar, ekstre PaidAmount günceller, kart limit iade.
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PayCreditCardStatement
    @StatementId    UNIQUEIDENTIFIER,
    @FromAccountId  UNIQUEIDENTIFIER,
    @Amount         DECIMAL(18,2),
    @PayDate        DATETIME2 = NULL,
    @UserId         UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CardId UNIQUEIDENTIFIER, @Closing DECIMAL(18,2), @Paid DECIMAL(18,2),
                @CompanyId UNIQUEIDENTIFIER, @CardName NVARCHAR(200);
        SELECT @CardId = CardId, @Closing = ClosingBalance, @Paid = PaidAmount
        FROM CreditCardStatement WHERE Id = @StatementId;
        IF @CardId IS NULL THROW 51321, N'Ekstre bulunamadı.', 1;
        IF @Amount <= 0 THROW 51322, N'Ödeme tutarı pozitif olmalıdır.', 1;
        IF @Paid + @Amount > @Closing + 0.01 THROW 51323, N'Ödeme tutarı ekstre borcunu aşamaz.', 1;

        SELECT @CompanyId = CompanyId, @CardName = CardNoMasked
        FROM CreditCard WHERE Id = @CardId;

        INSERT INTO FinancialTransaction (
            Id, CompanyId, AccountId, TransactionDate, TransactionType,
            Amount, Currency, AmountTRY, Description,
            InstrumentType, InstrumentId, SourceDocType, CreatedBy)
        VALUES (
            NEWID(), @CompanyId, @FromAccountId, ISNULL(@PayDate, GETUTCDATE()), 'EXPENSE',
            @Amount, 'TRY', @Amount, N'Kredi kartı ekstre ödemesi: ' + @CardName,
            'CARD', @StatementId, 'CARD_STATEMENT_PAYMENT', @UserId);

        UPDATE CreditCardStatement SET PaidAmount = PaidAmount + @Amount WHERE Id = @StatementId;
        UPDATE CreditCard SET AvailableLimit = AvailableLimit + @Amount WHERE Id = @CardId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- M11 SENET: sp_DepositNote — PORTFOLIO → IN_BANK (bankaya tahsile verme)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_DepositNote
    @NoteId      UNIQUEIDENTIFIER,
    @AccountId   UNIQUEIDENTIFIER,
    @DepositDate DATETIME2 = NULL,
    @UserId      UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Status NVARCHAR(20);
    SELECT @Status = Status FROM PromissoryNote WHERE Id = @NoteId AND IsDeleted = 0;

    IF @Status IS NULL THROW 51310, N'Senet bulunamadı.', 1;
    IF @Status <> 'PORTFOLIO' THROW 51311, N'Sadece portföydeki senetler bankaya verilebilir.', 1;

    UPDATE PromissoryNote
    SET Status               = 'IN_BANK',
        DepositedToAccountId = @AccountId,
        DepositedAt          = ISNULL(@DepositDate, GETUTCDATE()),
        UpdatedAt            = GETUTCDATE(),
        UpdatedBy            = @UserId
    WHERE Id = @NoteId;
END
GO

-- =============================================================================
-- M11 SENET: sp_CollectNote — IN_BANK → COLLECTED + FinancialTransaction (INCOME)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CollectNote
    @NoteId      UNIQUEIDENTIFIER,
    @CollectDate DATETIME2 = NULL,
    @UserId      UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20), @Amount DECIMAL(18,2),
                @AccountId UNIQUEIDENTIFIER, @PartnerId UNIQUEIDENTIFIER,
                @CompanyId UNIQUEIDENTIFIER, @NoteNo NVARCHAR(50);

        SELECT @Status = Status, @Amount = Amount, @AccountId = DepositedToAccountId,
               @PartnerId = PartnerId, @CompanyId = CompanyId, @NoteNo = NoteNo
        FROM PromissoryNote WHERE Id = @NoteId AND IsDeleted = 0;

        IF @Status IS NULL THROW 51310, N'Senet bulunamadı.', 1;
        IF @Status <> 'IN_BANK' THROW 51312, N'Sadece bankaya verilmiş senetler tahsil edilebilir.', 1;
        IF @AccountId IS NULL THROW 51313, N'Senetin tahsile verildiği banka hesabı bulunamadı.', 1;

        DECLARE @TxId UNIQUEIDENTIFIER = NEWID();
        DECLARE @Now  DATETIME2        = ISNULL(@CollectDate, GETUTCDATE());

        INSERT INTO FinancialTransaction (
            Id, CompanyId, AccountId, TransactionDate, TransactionType,
            Amount, Currency, AmountTRY, PartnerId, Description,
            InstrumentType, InstrumentId, SourceDocType, SourceDocNo, CreatedBy)
        VALUES (
            @TxId, @CompanyId, @AccountId, @Now, 'INCOME',
            @Amount, 'TRY', @Amount, @PartnerId, N'Senet tahsili: ' + @NoteNo,
            'NOTE', @NoteId, 'NOTE_COLLECTION', @NoteNo, @UserId);

        UPDATE PromissoryNote
        SET Status = 'COLLECTED', CollectedAt = @Now, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @NoteId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- M11 SENET: sp_ReturnNote — karşılıksız (IN_BANK/PORTFOLIO → RETURNED)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ReturnNote
    @NoteId  UNIQUEIDENTIFIER,
    @Reason  NVARCHAR(500),
    @UserId  UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE PromissoryNote
    SET Status = 'RETURNED', ReturnReason = @Reason,
        UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
    WHERE Id = @NoteId AND Status IN ('IN_BANK', 'PORTFOLIO');

    IF @@ROWCOUNT = 0 THROW 51314, N'Senet karşılıksız olarak işaretlenemiyor (statü uygun değil).', 1;
END
GO

-- =============================================================================
-- M11 KREDİ: sp_CreateLoan (Plan 01 Faz 1)
-- 7 kredi hesap yöntemi destekler:
--   ANUITE           — taksit sabit (geleneksel), faiz azalır anapara artar
--   EQUAL_PRINCIPAL  — anapara sabit (P/n), faiz azalan bakiye üzerinden
--   BALLOON          — ilk n-1 küçük taksit (sadece faiz + min anapara), son taksit @BalloonAmount + kalan
--   SPOT             — tek taksit vade sonunda: P + (P × r × t)
--   ROTATIVE         — taksit tablosu yok; kullanım üzerinden günlük faiz işler
--   KMH              — Kredili Mevduat Hesabı (revolving, taksit yok)
--   DBS              — Doğrudan Borçlandırma Sistemi (taksit yok, otomatik tahsilat tetiklenir)
-- THROW kodları: 51300-51399 aralığı M11 slot.
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CreateLoan
    @CompanyId        UNIQUEIDENTIFIER,
    @LoanNo           NVARCHAR(50),
    @BankName         NVARCHAR(200),
    @AccountId        UNIQUEIDENTIFIER,
    @Principal        DECIMAL(18,2),
    @InterestRate     DECIMAL(8,4),
    @TermMonths       INT,
    @StartDate        DATE,
    @CalcMethod       NVARCHAR(30) = 'ANUITE',
    @GracePeriodMonths INT          = 0,
    @BalloonAmount    DECIMAL(18,2) = NULL,
    @UserId           UNIQUEIDENTIFIER = NULL,
    @NewLoanId        UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Guard'lar
        IF @Principal <= 0
            THROW 51301, N'Anapara tutarı pozitif olmalıdır.', 1;
        IF @InterestRate < 0
            THROW 51302, N'Faiz oranı negatif olamaz.', 1;
        IF @CalcMethod NOT IN ('ANUITE','EQUAL_PRINCIPAL','BALLOON','SPOT','ROTATIVE','KMH','DBS')
            THROW 51303, N'Geçersiz kredi hesap yöntemi.', 1;
        IF @CalcMethod IN ('ANUITE','EQUAL_PRINCIPAL','BALLOON') AND (@TermMonths IS NULL OR @TermMonths <= 0)
            THROW 51304, N'Bu kredi tipinde vade ay sayısı pozitif olmalıdır.', 1;
        IF @CalcMethod = 'BALLOON' AND (@BalloonAmount IS NULL OR @BalloonAmount <= 0)
            THROW 51305, N'Balon ödemeli kredide @BalloonAmount belirtilmelidir.', 1;
        IF @CalcMethod = 'BALLOON' AND @BalloonAmount >= @Principal
            THROW 51306, N'Balon tutarı anaparadan büyük veya eşit olamaz.', 1;
        IF @GracePeriodMonths < 0 OR @GracePeriodMonths >= ISNULL(@TermMonths, 0)
            THROW 51307, N'Geçersiz ödemesiz dönem süresi.', 1;

        SET @NewLoanId = NEWID();

        DECLARE @MonthlyRate    DECIMAL(18,8) = @InterestRate / 100.0 / 12.0;
        DECLARE @MonthlyPayment DECIMAL(18,2) = 0;
        DECLARE @EndDate        DATE;

        -- ── Taksit hesap dağıtımı ─────────────────────────────────────
        IF @CalcMethod = 'ANUITE'
        BEGIN
            -- M = P * (r(1+r)^n) / ((1+r)^n - 1)
            SET @MonthlyPayment =
                CASE WHEN @MonthlyRate = 0
                     THEN @Principal / @TermMonths
                     ELSE @Principal * (@MonthlyRate * POWER(1 + @MonthlyRate, @TermMonths)) /
                          (POWER(1 + @MonthlyRate, @TermMonths) - 1)
                END;
            SET @EndDate = DATEADD(MONTH, @TermMonths, @StartDate);
        END
        ELSE IF @CalcMethod = 'EQUAL_PRINCIPAL'
        BEGIN
            -- Anapara payı sabit = P/n; faiz azalan bakiye üzerinden; MonthlyPayment 1. taksit referansı
            SET @MonthlyPayment = @Principal / @TermMonths + (@Principal * @MonthlyRate);
            SET @EndDate = DATEADD(MONTH, @TermMonths, @StartDate);
        END
        ELSE IF @CalcMethod = 'BALLOON'
        BEGIN
            -- İlk n-1 küçük + son taksit @BalloonAmount + kalan
            -- Aylık faiz: @Principal * @MonthlyRate; aylık anapara minimum (P - balon) / (n-1)
            SET @MonthlyPayment = (@Principal * @MonthlyRate) + ((@Principal - @BalloonAmount) / (@TermMonths - 1));
            SET @EndDate = DATEADD(MONTH, @TermMonths, @StartDate);
        END
        ELSE IF @CalcMethod = 'SPOT'
        BEGIN
            -- Tek taksit vade sonunda: P + (P * r * t/12)
            DECLARE @SpotInterest DECIMAL(18,2) = @Principal * (@InterestRate / 100.0) * (@TermMonths / 12.0);
            SET @MonthlyPayment = @Principal + @SpotInterest;
            SET @EndDate = DATEADD(MONTH, @TermMonths, @StartDate);
        END
        ELSE  -- ROTATIVE, KMH, DBS — taksit tablosu yok
        BEGIN
            SET @MonthlyPayment = 0;
            SET @EndDate = CASE WHEN @TermMonths IS NULL OR @TermMonths = 0
                                THEN DATEADD(YEAR, 1, @StartDate)
                                ELSE DATEADD(MONTH, @TermMonths, @StartDate)
                           END;
        END

        -- ── Loan başlık kaydı ─────────────────────────────────────────
        INSERT INTO Loan (
            Id, CompanyId, LoanNo, BankName, AccountId, LoanType, CalcMethod,
            Principal, InterestRate, TermMonths, StartDate, EndDate,
            MonthlyPayment, OutstandingBalance, GracePeriodMonths, BalloonAmount,
            Status, CreatedBy
        )
        VALUES (
            @NewLoanId, @CompanyId, @LoanNo, @BankName, @AccountId, 'COMMERCIAL', @CalcMethod,
            @Principal, @InterestRate, @TermMonths, @StartDate, @EndDate,
            @MonthlyPayment, @Principal, @GracePeriodMonths, @BalloonAmount,
            'ACTIVE', @UserId
        );

        -- ── Taksit takvimi üretimi (sadece anüite/eşit-anapara/balon/spot) ──
        IF @CalcMethod IN ('ANUITE','EQUAL_PRINCIPAL','BALLOON','SPOT')
        BEGIN
            DECLARE @i INT = 1;
            DECLARE @Bal DECIMAL(18,2) = @Principal;
            DECLARE @InterestThisMonth  DECIMAL(18,2);
            DECLARE @PrincipalThisMonth DECIMAL(18,2);
            DECLARE @TotalThisMonth     DECIMAL(18,2);

            WHILE @i <= @TermMonths
            BEGIN
                SET @InterestThisMonth = @Bal * @MonthlyRate;

                IF @i <= @GracePeriodMonths
                BEGIN
                    -- Ödemesiz dönem: sadece faiz ödenir, anapara değişmez
                    SET @PrincipalThisMonth = 0;
                    SET @TotalThisMonth     = @InterestThisMonth;
                END
                ELSE IF @CalcMethod = 'ANUITE'
                BEGIN
                    SET @PrincipalThisMonth = @MonthlyPayment - @InterestThisMonth;
                    SET @TotalThisMonth     = @MonthlyPayment;
                END
                ELSE IF @CalcMethod = 'EQUAL_PRINCIPAL'
                BEGIN
                    SET @PrincipalThisMonth = @Principal / @TermMonths;
                    SET @TotalThisMonth     = @PrincipalThisMonth + @InterestThisMonth;
                END
                ELSE IF @CalcMethod = 'BALLOON'
                BEGIN
                    IF @i < @TermMonths
                    BEGIN
                        -- Standart küçük taksit
                        SET @PrincipalThisMonth = (@Principal - @BalloonAmount) / (@TermMonths - 1);
                        SET @TotalThisMonth     = @PrincipalThisMonth + @InterestThisMonth;
                    END
                    ELSE
                    BEGIN
                        -- Son taksit: balon + kalan
                        SET @PrincipalThisMonth = @Bal;
                        SET @TotalThisMonth     = @PrincipalThisMonth + @InterestThisMonth;
                    END
                END
                ELSE IF @CalcMethod = 'SPOT'
                BEGIN
                    IF @i < @TermMonths
                    BEGIN
                        -- SPOT'ta tek taksit son ayda
                        SET @PrincipalThisMonth = 0;
                        SET @InterestThisMonth  = 0;
                        SET @TotalThisMonth     = 0;
                    END
                    ELSE
                    BEGIN
                        SET @PrincipalThisMonth = @Principal;
                        SET @InterestThisMonth  = @MonthlyPayment - @Principal;
                        SET @TotalThisMonth     = @MonthlyPayment;
                    END
                END

                INSERT INTO LoanPayment (
                    Id, LoanId, InstallmentNo, DueDate, PrincipalAmount, InterestAmount, TotalAmount
                )
                VALUES (
                    NEWID(), @NewLoanId, @i, DATEADD(MONTH, @i, @StartDate),
                    @PrincipalThisMonth, @InterestThisMonth, @TotalThisMonth
                );

                SET @Bal -= @PrincipalThisMonth;
                SET @i  += 1;
            END
        END
        -- ROTATIVE / KMH / DBS: taksit tablosu üretilmez; kullanım anında işlenir

        -- ── Kredi tutarı banka hesabına gelir olarak işlenir ──────────
        -- KMH ve ROTATIVE'de açılışta para gelmez; limit tahsisi sayılır
        IF @AccountId IS NOT NULL AND @CalcMethod NOT IN ('ROTATIVE','KMH','DBS')
        BEGIN
            INSERT INTO FinancialTransaction (
                Id, CompanyId, AccountId, TransactionDate, TransactionType,
                Amount, Currency, AmountTRY, Description,
                InstrumentType, InstrumentId, CreatedBy
            )
            VALUES (
                NEWID(), @CompanyId, @AccountId, @StartDate, 'INCOME',
                @Principal, 'TRY', @Principal,
                N'Kredi açılışı: ' + @LoanNo + N' (' + @CalcMethod + N')',
                'LOAN', @NewLoanId, @UserId
            );
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- M11 KREDİ: sp_PayLoanInstallment
-- Kredi taksidi öder, banka hesabından gider yazar, anapara bakiyesini düşürür
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PayLoanInstallment
    @PaymentId      UNIQUEIDENTIFIER,
    @PayDate        DATETIME2 = NULL,
    @FromAccountId  UNIQUEIDENTIFIER,
    @UserId         UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @LoanId UNIQUEIDENTIFIER, @Total DECIMAL(18,2),
                @InstNo INT, @CompanyId UNIQUEIDENTIFIER,
                @LoanNo NVARCHAR(50), @PrincipalPaid DECIMAL(18,2);

        SELECT @LoanId = LoanId, @Total = TotalAmount, @InstNo = InstallmentNo,
               @PrincipalPaid = PrincipalAmount
        FROM LoanPayment WHERE Id = @PaymentId AND IsPaid = 0;

        IF @LoanId IS NULL
            THROW 60010, N'Ödeme bulunamadı veya zaten ödenmiş.', 1;

        SELECT @CompanyId = CompanyId, @LoanNo = LoanNo FROM Loan WHERE Id = @LoanId;

        DECLARE @TxId UNIQUEIDENTIFIER = NEWID();
        INSERT INTO FinancialTransaction (
            Id, CompanyId, AccountId, TransactionDate, TransactionType,
            Amount, Currency, AmountTRY, Description, InstrumentType, InstrumentId,
            SourceDocType, SourceDocNo, CreatedBy
        )
        VALUES (
            @TxId, @CompanyId, @FromAccountId, ISNULL(@PayDate, GETUTCDATE()), 'EXPENSE',
            @Total, 'TRY', @Total,
            N'Kredi taksit ödemesi: ' + @LoanNo + N' / ' + CAST(@InstNo AS NVARCHAR(10)),
            'LOAN', @LoanId, 'LOAN_INSTALLMENT',
            @LoanNo + '/' + CAST(@InstNo AS NVARCHAR(10)), @UserId
        );

        UPDATE LoanPayment
        SET IsPaid                 = 1,
            PaidAmount             = @Total,
            PaidAt                 = GETUTCDATE(),
            FinancialTransactionId = @TxId
        WHERE Id = @PaymentId;

        UPDATE Loan
        SET OutstandingBalance = OutstandingBalance - @PrincipalPaid,
            UpdatedAt          = GETUTCDATE(),
            UpdatedBy          = @UserId
        WHERE Id = @LoanId;

        -- Tüm taksitler ödendiyse kredi CLOSED
        IF NOT EXISTS (SELECT 1 FROM LoanPayment WHERE LoanId = @LoanId AND IsPaid = 0)
            UPDATE Loan SET Status = 'CLOSED' WHERE Id = @LoanId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- tvf_AccountBalance — Şirkete ait hesap bakiyeleri (CompanyId parametreli iTVF)
-- v_AccountBalance view yerine geçti: CompanyId filtresi imzada zorunlu.
-- =============================================================================
IF OBJECT_ID('dbo.v_AccountBalance', 'V') IS NOT NULL DROP VIEW dbo.v_AccountBalance;
GO
CREATE OR ALTER FUNCTION dbo.tvf_AccountBalance
(
    @CompanyId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        a.Id AS AccountId,
        a.Code,
        a.Name,
        a.AccountType,
        a.Currency,
        a.OpeningBalance
            + ISNULL(SUM(CASE
                  WHEN t.TransactionType IN ('INCOME', 'TRANSFER_IN')     THEN  t.AmountTRY
                  WHEN t.TransactionType IN ('EXPENSE', 'TRANSFER_OUT')   THEN -t.AmountTRY
                  ELSE 0 END), 0) AS Balance,
        MAX(t.TransactionDate) AS LastMovementDate,
        COUNT(t.Id) AS TransactionCount
    FROM FinancialAccount a
    LEFT JOIN FinancialTransaction t
        ON t.AccountId = a.Id AND t.IsDeleted = 0
    WHERE a.CompanyId = @CompanyId AND a.IsDeleted = 0
    GROUP BY a.Id, a.Code, a.Name, a.AccountType, a.Currency, a.OpeningBalance
);
GO

-- =============================================================================
-- tvf_PaymentPlanAging — Yaşlandırma (alacak/borç), CompanyId parametreli iTVF
-- v_PaymentPlanAging view yerine geçti: CompanyId filtresi imzada zorunlu.
-- =============================================================================
IF OBJECT_ID('dbo.v_PaymentPlanAging', 'V') IS NOT NULL DROP VIEW dbo.v_PaymentPlanAging;
GO
CREATE OR ALTER FUNCTION dbo.tvf_PaymentPlanAging
(
    @CompanyId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        pp.PartnerId,
        p.Name AS PartnerName,
        pp.Direction,
        SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) <= 0
                 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS NotDue,
        SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) BETWEEN 1 AND 30
                 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS Days1_30,
        SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) BETWEEN 31 AND 60
                 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS Days31_60,
        SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) BETWEEN 61 AND 90
                 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS Days61_90,
        SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) > 90
                 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS Over90,
        SUM(pp.Amount - pp.PaidAmount) AS TotalOpen
    FROM PaymentPlan pp
    JOIN Partner p ON p.Id = pp.PartnerId
    WHERE pp.CompanyId = @CompanyId
      AND pp.Status IN ('OPEN', 'PARTIAL', 'OVERDUE') AND pp.IsDeleted = 0
    GROUP BY pp.PartnerId, p.Name, pp.Direction
);
GO

-- =============================================================================
-- WIRE: sp_ReceivingPost (override) — orijinal sp + maliyet güncelleme
-- ItemCost.AvgCost'u her POSTED Receiving sonrasinda Moving Avg ile guncelle
-- StockMovement.UnitCost da kaydedilir (gecmise donuk maliyet izi).
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ReceivingPost
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @WarehouseId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50),
                @Status NVARCHAR(20), @POId UNIQUEIDENTIFIER;

        SELECT @WarehouseId = WarehouseId, @DocNo = DocNo,
               @Status = Status, @POId = PurchaseOrderId
        FROM ReceivingHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId;

        IF @WarehouseId IS NULL THROW 50001, N'Mal kabul belgesi bulunamadı.', 1;

        EXEC dbo.sp_ValidateStatusTransition @CompanyId, 'RECEIVING', @Status, 'POSTED', @UserId;

        DECLARE @ReceivingBinId UNIQUEIDENTIFIER;
        SELECT TOP 1 @ReceivingBinId = Id
        FROM Bin WHERE WarehouseId = @WarehouseId AND IsReceivingArea = 1;

        -- StockMovement: her satira PO'dan UnitCost ata
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, LotNo,
             UnitCost, CreatedBy)
        SELECT
            @CompanyId, @WarehouseId, @ReceivingBinId, rl.ItemId, 'RECEIPT',
            rl.QtyBase, rl.UomId, rl.QtyBase, 'RECEIVING', @HeaderId, @DocNo, rl.LotNo,
            ISNULL(pol.Price, 0), @UserId
        FROM ReceivingLine rl
        LEFT JOIN PurchaseOrderLine pol ON pol.Id = rl.PurchaseOrderLineId
        WHERE rl.HeaderId = @HeaderId;

        UPDATE pol
        SET pol.QtyReceived = pol.QtyReceived + rl.QtyBase
        FROM PurchaseOrderLine pol
        JOIN ReceivingLine rl ON rl.PurchaseOrderLineId = pol.Id
        WHERE rl.HeaderId = @HeaderId;

        IF @POId IS NOT NULL
           AND NOT EXISTS (
               SELECT 1 FROM PurchaseOrderLine
               WHERE HeaderId = @POId AND QtyReceived < QtyOrdered
           )
            UPDATE PurchaseOrderHeader SET Status = 'RECEIVED' WHERE Id = @POId;

        UPDATE ReceivingHeader
        SET Status = 'POSTED', UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @HeaderId;

        -- WIRE: Her urun icin maliyet guncelle (Moving Avg)
        DECLARE @ItemId UNIQUEIDENTIFIER, @Qty DECIMAL(18,6), @UC DECIMAL(18,4);
        DECLARE c_lines CURSOR LOCAL FAST_FORWARD FOR
            SELECT rl.ItemId, SUM(rl.QtyBase),
                   ISNULL(AVG(CAST(pol.Price AS DECIMAL(18,4))), 0)
            FROM ReceivingLine rl
            LEFT JOIN PurchaseOrderLine pol ON pol.Id = rl.PurchaseOrderLineId
            WHERE rl.HeaderId = @HeaderId
            GROUP BY rl.ItemId;
        OPEN c_lines;
        FETCH NEXT FROM c_lines INTO @ItemId, @Qty, @UC;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            EXEC dbo.sp_UpdateItemCostMovingAvg
                @CompanyId = @CompanyId, @ItemId = @ItemId,
                @WarehouseId = @WarehouseId, @Qty = @Qty,
                @UnitCost = @UC, @MovementType = 'RECEIPT';
            FETCH NEXT FROM c_lines INTO @ItemId, @Qty, @UC;
        END
        CLOSE c_lines; DEALLOCATE c_lines;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'c_lines') >= -1 BEGIN CLOSE c_lines; DEALLOCATE c_lines; END
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- WIRE: sp_ShippingPost (override) — orijinal sp + otomatik fatura
-- Parameter.InvoiceMode='INSTANT' ise sp_GenerateSalesInvoiceFromShipping cagrilir
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ShippingPost
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @WarehouseId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50), @Status NVARCHAR(20);
        SELECT @WarehouseId = WarehouseId, @DocNo = DocNo, @Status = Status
        FROM ShippingHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId;

        IF @WarehouseId IS NULL THROW 50001, N'Sevkiyat belgesi bulunamadı.', 1;
        EXEC dbo.sp_ValidateStatusTransition @CompanyId, 'SHIPMENT', @Status, 'POSTED', @UserId;

        DECLARE @PickingBinId UNIQUEIDENTIFIER;
        SELECT TOP 1 @PickingBinId = Id
        FROM Bin WHERE WarehouseId = @WarehouseId AND IsPickingArea = 1;

        -- Stok hareketi: her satir icin ISSUE (eksi tabanlanan QtyBase)
        -- UnitCost ItemCost.AvgCost'tan alinir (satış maliyeti / COGS)
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, LotNo,
             UnitCost, CreatedBy)
        SELECT
            @CompanyId, @WarehouseId, ISNULL(sl.BinId, @PickingBinId), sl.ItemId, 'ISSUE',
            -sl.QtyBase, sl.UomId, -sl.QtyBase, 'SHIPMENT', @HeaderId, @DocNo, sl.LotNo,
            ISNULL(ic.AvgCost, 0), @UserId
        FROM ShippingLine sl
        LEFT JOIN ItemCost ic ON ic.CompanyId = @CompanyId AND ic.ItemId = sl.ItemId
        WHERE sl.HeaderId = @HeaderId;

        -- SO satirlarini guncelle
        UPDATE sol
        SET sol.QtyShipped = sol.QtyShipped + sl.QtyBase
        FROM SalesOrderLine sol
        JOIN ShippingLine sl ON sl.SalesOrderLineId = sol.Id
        WHERE sl.HeaderId = @HeaderId;

        -- Shipping POSTED
        UPDATE ShippingHeader
        SET Status = 'POSTED', PostedAt = GETUTCDATE(),
            UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @HeaderId;

        -- WIRE: ItemCost OnHandQty dusur
        DECLARE @ItemId UNIQUEIDENTIFIER, @Qty DECIMAL(18,6);
        DECLARE c_lines CURSOR LOCAL FAST_FORWARD FOR
            SELECT ItemId, SUM(QtyBase) FROM ShippingLine WHERE HeaderId = @HeaderId
            GROUP BY ItemId;
        OPEN c_lines;
        FETCH NEXT FROM c_lines INTO @ItemId, @Qty;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            EXEC dbo.sp_UpdateItemCostMovingAvg
                @CompanyId = @CompanyId, @ItemId = @ItemId,
                @WarehouseId = @WarehouseId, @Qty = @Qty,
                @UnitCost = NULL, @MovementType = 'ISSUE';
            FETCH NEXT FROM c_lines INTO @ItemId, @Qty;
        END
        CLOSE c_lines; DEALLOCATE c_lines;

        -- WIRE: Parameter.InvoiceMode = INSTANT ise otomatik fatura
        DECLARE @InvoiceMode NVARCHAR(50);
        SELECT @InvoiceMode = Value FROM Parameter
        WHERE CompanyId = @CompanyId AND Code = 'InvoiceMode';

        IF @InvoiceMode = 'INSTANT'
        BEGIN
            DECLARE @NewInvoiceId UNIQUEIDENTIFIER;
            EXEC dbo.sp_GenerateSalesInvoiceFromShipping
                @ShippingId = @HeaderId, @UserId = NULL,
                @NewInvoiceId = @NewInvoiceId OUTPUT;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'c_lines') >= -1 BEGIN CLOSE c_lines; DEALLOCATE c_lines; END
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- WIRE: sp_GeneratePaymentPlanFromPO
-- PO POSTED olunca tedarikciye odeme planı uretilir
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_GeneratePaymentPlanFromPO
    @PoHeaderId UNIQUEIDENTIFIER,
    @UserId     UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CompanyId UNIQUEIDENTIFIER, @PartnerId UNIQUEIDENTIFIER,
                @OrderNo NVARCHAR(50), @OrderDate DATETIME2,
                @PaymentTermDays INT, @Installments INT, @TotalAmount DECIMAL(18,2);

        SELECT @CompanyId = h.CompanyId, @PartnerId = h.PartnerId,
               @OrderNo = h.OrderNo, @OrderDate = h.OrderDate,
               @PaymentTermDays = ISNULL(h.PaymentTermDays, ISNULL(p.PaymentTermDays, 30)),
               @Installments = ISNULL(h.PaymentInstallments, 1),
               @TotalAmount = (SELECT SUM(QtyOrdered * Price) FROM PurchaseOrderLine WHERE HeaderId = @PoHeaderId)
        FROM PurchaseOrderHeader h
        JOIN Partner p ON p.Id = h.PartnerId
        WHERE h.Id = @PoHeaderId;

        IF @CompanyId IS NULL THROW 71001, N'Satınalma siparişi bulunamadı.', 1;
        IF @TotalAmount IS NULL OR @TotalAmount = 0 THROW 71002, N'Sipariş toplamı sıfır.', 1;

        -- Mevcut PaymentPlan kayıtları varsa sil
        DELETE FROM PaymentPlan
        WHERE SourceDocType = 'PURCHASE_ORDER' AND SourceDocId = @PoHeaderId;

        -- Taksit bazli olusum
        DECLARE @InstallmentAmount DECIMAL(18,2) = @TotalAmount / @Installments;
        DECLARE @i INT = 1;
        DECLARE @DueDate DATE;

        WHILE @i <= @Installments
        BEGIN
            SET @DueDate = DATEADD(DAY, @PaymentTermDays * @i / @Installments, CAST(@OrderDate AS DATE));

            INSERT INTO PaymentPlan
                (Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
                 PartnerId, Direction, InstallmentNo, TotalInstallments,
                 DueDate, Amount, Status)
            VALUES
                (NEWID(), @CompanyId, 'PURCHASE_ORDER', @PoHeaderId, @OrderNo,
                 @PartnerId, 'PAYABLE', @i, @Installments,
                 @DueDate, @InstallmentAmount, 'OPEN');

            SET @i += 1;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION; THROW;
    END CATCH
END
GO

-- =============================================================================
-- WIRE: sp_PoPost — PO'yu POSTED yapar + PaymentPlan uretir
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PoPost
    @PoHeaderId UNIQUEIDENTIFIER,
    @CompanyId  UNIQUEIDENTIFIER,
    @UserId     UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20);
        SELECT @Status = Status FROM PurchaseOrderHeader WITH (UPDLOCK)
        WHERE Id = @PoHeaderId AND CompanyId = @CompanyId;

        IF @Status IS NULL THROW 72001, N'Satınalma siparişi bulunamadı.', 1;

        EXEC dbo.sp_ValidateStatusTransition @CompanyId, 'PURCHASE_ORDER', @Status, 'POSTED', @UserId;

        UPDATE PurchaseOrderHeader
        SET Status = 'POSTED', PostedAt = GETUTCDATE(),
            UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @PoHeaderId;

        EXEC dbo.sp_GeneratePaymentPlanFromPO @PoHeaderId = @PoHeaderId, @UserId = @UserId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION; THROW;
    END CATCH
END
GO

-- =============================================================================
-- M11 OTOMATİK KAPATMA: sp_AutoClosePayments
-- Yeni FinancialTransaction (INCOME/EXPENSE) ile aynı Partner'ın açık
-- PaymentPlan kayıtlarını FIFO (vadeye göre) eşleştirerek kapatır.
-- Kullanıcı manuel kapatma da yapabilir; bu SP otomatik mod içindir.
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_AutoClosePayments
    @CompanyId    UNIQUEIDENTIFIER,
    @PartnerId    UNIQUEIDENTIFIER,
    @Direction    NVARCHAR(10),         -- RECEIVABLE (tahsilat) / PAYABLE (ödeme)
    @Amount       DECIMAL(18,2),
    @TransactionId UNIQUEIDENTIFIER,    -- referans: FinancialTransaction.Id
    @UserId       UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Remaining DECIMAL(18,2) = @Amount;
        DECLARE @PpId UNIQUEIDENTIFIER, @PpRemaining DECIMAL(18,2), @PpAmount DECIMAL(18,2), @PpPaid DECIMAL(18,2);

        -- FIFO: en eski vade ilk kapatılır
        DECLARE c_pp CURSOR LOCAL FAST_FORWARD FOR
            SELECT Id, Amount - PaidAmount, Amount, PaidAmount
            FROM PaymentPlan
            WHERE CompanyId = @CompanyId
              AND PartnerId = @PartnerId
              AND Direction = @Direction
              AND Status IN ('OPEN', 'PARTIAL', 'OVERDUE')
              AND IsDeleted = 0
            ORDER BY DueDate ASC, CreatedAt ASC;

        OPEN c_pp;
        FETCH NEXT FROM c_pp INTO @PpId, @PpRemaining, @PpAmount, @PpPaid;

        WHILE @@FETCH_STATUS = 0 AND @Remaining > 0
        BEGIN
            DECLARE @ApplyAmount DECIMAL(18,2) =
                CASE WHEN @Remaining >= @PpRemaining THEN @PpRemaining ELSE @Remaining END;

            UPDATE PaymentPlan
            SET PaidAmount             = @PpPaid + @ApplyAmount,
                Status                 = CASE WHEN (@PpPaid + @ApplyAmount) >= @PpAmount THEN 'PAID' ELSE 'PARTIAL' END,
                PaidAt                 = CASE WHEN (@PpPaid + @ApplyAmount) >= @PpAmount THEN GETUTCDATE() ELSE PaidAt END,
                FinancialTransactionId = COALESCE(FinancialTransactionId, @TransactionId),
                UpdatedAt              = GETUTCDATE()
            WHERE Id = @PpId;

            SET @Remaining -= @ApplyAmount;

            FETCH NEXT FROM c_pp INTO @PpId, @PpRemaining, @PpAmount, @PpPaid;
        END

        CLOSE c_pp;
        DEALLOCATE c_pp;

        -- Kalan tutar varsa avans olarak kaydedilir (bekleyen ödeme planı yok)
        IF @Remaining > 0
        BEGIN
            INSERT INTO PaymentPlan (
                Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
                PartnerId, Direction, InstallmentNo, TotalInstallments,
                DueDate, Amount, PaidAmount, Status, PaidAt, FinancialTransactionId
            )
            VALUES (
                NEWID(), @CompanyId, 'PREPAYMENT', @TransactionId,
                'AVANS-' + CAST(YEAR(GETUTCDATE()) AS NVARCHAR(4)) + '-' + CONVERT(NVARCHAR(8), GETUTCDATE(), 112),
                @PartnerId, @Direction, 1, 1,
                CAST(GETUTCDATE() AS DATE), @Remaining, @Remaining, 'PAID', GETUTCDATE(), @TransactionId
            );
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local','c_pp') >= -1
        BEGIN
            CLOSE c_pp;
            DEALLOCATE c_pp;
        END
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- M11 OTOMATİK KAPATMA: sp_RecordPaymentAndAutoClose
-- Yeni FinancialTransaction ekler + PaymentPlan'lara FIFO dağıtır.
-- Partner.AutoClosePayments=1 ise otomatik, =0 ise manuel kapatma için yalın hareket.
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_RecordPaymentAndAutoClose
    @CompanyId    UNIQUEIDENTIFIER,
    @AccountId    UNIQUEIDENTIFIER,
    @PartnerId    UNIQUEIDENTIFIER,
    @Amount       DECIMAL(18,2),
    @Currency     NVARCHAR(10) = 'TRY',
    @TxType       NVARCHAR(20),         -- INCOME (tahsilat) / EXPENSE (ödeme)
    @Description  NVARCHAR(500) = NULL,
    @InstrumentType NVARCHAR(20) = 'CASH',
    @InstrumentId UNIQUEIDENTIFIER = NULL,
    @UserId       UNIQUEIDENTIFIER = NULL,
    @NewTxId      UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        SET @NewTxId = NEWID();

        INSERT INTO FinancialTransaction (
            Id, CompanyId, AccountId, TransactionDate, TransactionType,
            Amount, Currency, AmountTRY, PartnerId, Description,
            InstrumentType, InstrumentId, CreatedBy
        )
        VALUES (
            @NewTxId, @CompanyId, @AccountId, GETUTCDATE(), @TxType,
            @Amount, @Currency, @Amount, @PartnerId, @Description,
            @InstrumentType, @InstrumentId, @UserId
        );

        -- Auto-close: sadece partner bayrağı açıksa otomatik dağıt
        DECLARE @AutoFlag BIT = 0;
        SELECT @AutoFlag = ISNULL(AutoClosePayments, 0) FROM Partner WHERE Id = @PartnerId;

        IF @AutoFlag = 1
        BEGIN
            DECLARE @Direction NVARCHAR(10) =
                CASE WHEN @TxType = 'INCOME' THEN 'RECEIVABLE' ELSE 'PAYABLE' END;

            EXEC dbo.sp_AutoClosePayments
                @CompanyId    = @CompanyId,
                @PartnerId    = @PartnerId,
                @Direction    = @Direction,
                @Amount       = @Amount,
                @TransactionId = @NewTxId,
                @UserId       = @UserId;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- v_PartnerVadeAnalysis — Ortalama vade analizi
-- Müşteri: kesilen fatura vadesi vs gerçekleşen tahsilat tarihi farkı (gün ortalama)
-- Tedarikçi: alınan fatura vadesi vs gerçekleşen ödeme tarihi farkı
-- =============================================================================
CREATE OR ALTER VIEW dbo.v_PartnerVadeAnalysis AS
SELECT
    pp.CompanyId,
    pp.PartnerId,
    p.Name AS PartnerName,
    pp.Direction,
    COUNT(*) AS PaidCount,
    AVG(CAST(DATEDIFF(DAY, pp.DueDate, pp.PaidAt) AS DECIMAL(10, 2))) AS AvgDelayDays,
    -- pozitif = geç ödendi, negatif = erken ödendi/tahsil edildi
    AVG(CAST(pp.Amount AS DECIMAL(18, 2))) AS AvgInvoiceAmount,
    SUM(pp.Amount) AS TotalPaidAmount,
    MIN(pp.DueDate) AS FirstDue,
    MAX(pp.PaidAt)  AS LastPayment
FROM PaymentPlan pp
JOIN Partner p ON p.Id = pp.PartnerId
WHERE pp.Status = 'PAID' AND pp.PaidAt IS NOT NULL AND pp.IsDeleted = 0
GROUP BY pp.CompanyId, pp.PartnerId, p.Name, pp.Direction;
GO

-- =============================================================================
-- tvf_LoanSummaryAsOf — Kredi kapama tablosu (tarih bazlı snapshot)
-- Belirtilen tarihe kadar her kredi için:
--   • Vadesi gelmiş taksitlerden ödenmiş / ödenmemiş anapara ve faiz
--   • Kalan anapara (OutstandingPrincipal)
--   • Sonraki vade tarihi ve tutarı
--   • Geciken (ödenmemiş + vadesi geçmiş) taksit adedi
-- =============================================================================
CREATE OR ALTER FUNCTION dbo.tvf_LoanSummaryAsOf
(
    @CompanyId  UNIQUEIDENTIFIER,
    @AsOfDate   DATE
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        l.Id                 AS LoanId,
        l.LoanNo,
        l.BankName,
        l.CalcMethod,
        l.Status,
        l.Principal,
        l.InterestRate,
        l.StartDate,
        l.EndDate,
        l.TermMonths,
        l.MonthlyPayment,

        -- Vadesi gelen taksit sayısı (@AsOfDate dahil)
        (SELECT COUNT(*) FROM LoanPayment lp
            WHERE lp.LoanId = l.Id AND lp.DueDate <= @AsOfDate)              AS DueInstallmentCount,

        -- Vadesi gelen ve ödenen
        (SELECT COUNT(*) FROM LoanPayment lp
            WHERE lp.LoanId = l.Id AND lp.DueDate <= @AsOfDate AND lp.IsPaid = 1) AS PaidInstallmentCount,

        -- Vadesi gelen ve ödenmemiş (gecikme)
        (SELECT COUNT(*) FROM LoanPayment lp
            WHERE lp.LoanId = l.Id AND lp.DueDate <= @AsOfDate AND lp.IsPaid = 0) AS OverdueInstallmentCount,

        -- Tüm taksit sayısı
        (SELECT COUNT(*) FROM LoanPayment lp WHERE lp.LoanId = l.Id)         AS TotalInstallmentCount,

        -- Bugüne kadar ödenen anapara
        ISNULL((SELECT SUM(lp.PrincipalAmount) FROM LoanPayment lp
                WHERE lp.LoanId = l.Id AND lp.IsPaid = 1), 0)                AS PaidPrincipal,

        -- Bugüne kadar ödenen faiz
        ISNULL((SELECT SUM(lp.InterestAmount) FROM LoanPayment lp
                WHERE lp.LoanId = l.Id AND lp.IsPaid = 1), 0)                AS PaidInterest,

        -- Kalan anapara borcu (Loan.OutstandingBalance senkron tutuluyor)
        l.OutstandingBalance                                                  AS OutstandingPrincipal,

        -- Kalan faiz (ödenmemiş tüm taksitlerin faiz toplamı)
        ISNULL((SELECT SUM(lp.InterestAmount) FROM LoanPayment lp
                WHERE lp.LoanId = l.Id AND lp.IsPaid = 0), 0)                AS RemainingInterest,

        -- Toplam kalan borç (anapara + faiz)
        l.OutstandingBalance +
        ISNULL((SELECT SUM(lp.InterestAmount) FROM LoanPayment lp
                WHERE lp.LoanId = l.Id AND lp.IsPaid = 0), 0)                AS TotalRemainingDebt,

        -- Vadesi geçmiş gecikme tutarı (ödenmemiş taksitlerin tutarı, AsOfDate'e kadar)
        ISNULL((SELECT SUM(lp.TotalAmount) FROM LoanPayment lp
                WHERE lp.LoanId = l.Id AND lp.IsPaid = 0 AND lp.DueDate <= @AsOfDate), 0)
                                                                              AS OverdueAmount,

        -- Sonraki ödeme tarihi + tutarı
        (SELECT TOP 1 lp.DueDate FROM LoanPayment lp
            WHERE lp.LoanId = l.Id AND lp.IsPaid = 0
            ORDER BY lp.DueDate)                                              AS NextDueDate,

        (SELECT TOP 1 lp.TotalAmount FROM LoanPayment lp
            WHERE lp.LoanId = l.Id AND lp.IsPaid = 0
            ORDER BY lp.DueDate)                                              AS NextDueAmount

    FROM Loan l
    WHERE l.CompanyId = @CompanyId AND l.IsDeleted = 0
);
GO

-- =============================================================================
-- tvf_FinancialPosition — Mali durum kapama tablosu (bilanço benzeri snapshot)
-- @AsOfDate tarihinde:
--   Pozitif (varlık): hesap bakiyeleri (kasa+banka), müşteri alacakları, çek portföyü
--   Negatif (yükümlülük): kredi kalan anapara+faiz, tedarikçi borçları, kredi kartı kullanılan limit
--   Net pozisyon = sum(varlık) - sum(yükümlülük)
-- =============================================================================
CREATE OR ALTER FUNCTION dbo.tvf_FinancialPosition
(
    @CompanyId  UNIQUEIDENTIFIER,
    @AsOfDate   DATE
)
RETURNS @Result TABLE
(
    Category    NVARCHAR(50),         -- ASSET / LIABILITY
    GroupName   NVARCHAR(100),        -- Kasa, Banka, Kredi, vb.
    ItemCode    NVARCHAR(100),        -- Hesap kodu / kredi no / cari kodu
    ItemName    NVARCHAR(300),
    Currency    NVARCHAR(10),
    Amount      DECIMAL(18,2),        -- TRY karşılığı; ASSET pozitif, LIABILITY pozitif (görsel negatif)
    SortOrder   INT
)
AS
BEGIN
    -- VARLIK 1: Kasa hesapları
    INSERT INTO @Result
    SELECT 'ASSET', N'Kasa', a.Code, a.Name, a.Currency,
           a.OpeningBalance + ISNULL(
               (SELECT SUM(CASE WHEN ft.TransactionType IN ('INCOME','TRANSFER_IN') THEN ft.AmountTRY
                                WHEN ft.TransactionType IN ('EXPENSE','TRANSFER_OUT') THEN -ft.AmountTRY
                                ELSE 0 END)
                FROM FinancialTransaction ft
                WHERE ft.AccountId = a.Id AND ft.IsDeleted = 0
                  AND CAST(ft.TransactionDate AS DATE) <= @AsOfDate), 0),
           10
    FROM FinancialAccount a
    WHERE a.CompanyId = @CompanyId AND a.AccountType = 'CASH' AND a.IsDeleted = 0;

    -- VARLIK 2: Banka hesapları
    INSERT INTO @Result
    SELECT 'ASSET', N'Banka', a.Code, a.Name, a.Currency,
           a.OpeningBalance + ISNULL(
               (SELECT SUM(CASE WHEN ft.TransactionType IN ('INCOME','TRANSFER_IN') THEN ft.AmountTRY
                                WHEN ft.TransactionType IN ('EXPENSE','TRANSFER_OUT') THEN -ft.AmountTRY
                                ELSE 0 END)
                FROM FinancialTransaction ft
                WHERE ft.AccountId = a.Id AND ft.IsDeleted = 0
                  AND CAST(ft.TransactionDate AS DATE) <= @AsOfDate), 0),
           20
    FROM FinancialAccount a
    WHERE a.CompanyId = @CompanyId AND a.AccountType = 'BANK' AND a.IsDeleted = 0;

    -- VARLIK 3: Çek portföyü (PORTFOLIO + IN_BANK, tahsil edilmemiş)
    INSERT INTO @Result
    SELECT 'ASSET', N'Çek Portföyü', c.ChequeNo,
           N'Çek ' + c.BankName + N' — ' + ISNULL(c.DrawerName, ''),
           c.Currency, c.Amount, 30
    FROM Cheque c
    WHERE c.CompanyId = @CompanyId AND c.IsDeleted = 0
      AND c.Direction = 'RECEIVED'
      AND c.Status IN ('PORTFOLIO','IN_BANK')
      AND c.ChequeDate <= @AsOfDate;

    -- VARLIK 4: Senet portföyü (alınan)
    INSERT INTO @Result
    SELECT 'ASSET', N'Senet Portföyü', n.NoteNo,
           N'Senet — ' + ISNULL(n.DrawerName, ''),
           n.Currency, n.Amount, 35
    FROM PromissoryNote n
    WHERE n.CompanyId = @CompanyId AND n.IsDeleted = 0
      AND n.Direction = 'RECEIVED'
      AND n.Status IN ('PORTFOLIO','IN_BANK')
      AND n.IssueDate <= @AsOfDate;

    -- VARLIK 5: Müşteri alacakları (PaymentPlan RECEIVABLE açık)
    INSERT INTO @Result
    SELECT 'ASSET', N'Müşteri Alacakları', p.Code, p.Name, 'TRY',
           SUM(pp.Amount - pp.PaidAmount), 40
    FROM PaymentPlan pp
    JOIN Partner p ON p.Id = pp.PartnerId
    WHERE pp.CompanyId = @CompanyId AND pp.IsDeleted = 0
      AND pp.Direction = 'RECEIVABLE'
      AND pp.Status IN ('OPEN','PARTIAL','OVERDUE')
      AND CAST(pp.CreatedAt AS DATE) <= @AsOfDate
    GROUP BY p.Code, p.Name
    HAVING SUM(pp.Amount - pp.PaidAmount) > 0;

    -- YÜKÜMLÜLÜK 1: Kredi kalan anapara + faiz
    INSERT INTO @Result
    SELECT 'LIABILITY', N'Banka Kredileri', l.LoanNo,
           l.BankName + N' (' + l.CalcMethod + N')', 'TRY',
           l.OutstandingPrincipal + l.RemainingInterest, 110
    FROM dbo.tvf_LoanSummaryAsOf(@CompanyId, @AsOfDate) l
    WHERE l.Status = 'ACTIVE';

    -- YÜKÜMLÜLÜK 2: Verilen çek (henüz ödenmemiş)
    INSERT INTO @Result
    SELECT 'LIABILITY', N'Verilen Çekler', c.ChequeNo,
           N'Çek ' + c.BankName + N' → ' + ISNULL(c.DrawerName, ''),
           c.Currency, c.Amount, 120
    FROM Cheque c
    WHERE c.CompanyId = @CompanyId AND c.IsDeleted = 0
      AND c.Direction = 'ISSUED'
      AND c.Status NOT IN ('PAID','RETURNED')
      AND c.ChequeDate <= @AsOfDate;

    -- YÜKÜMLÜLÜK 3: Verilen senet
    INSERT INTO @Result
    SELECT 'LIABILITY', N'Verilen Senetler', n.NoteNo,
           N'Senet → ' + ISNULL(n.DrawerName, ''),
           n.Currency, n.Amount, 125
    FROM PromissoryNote n
    WHERE n.CompanyId = @CompanyId AND n.IsDeleted = 0
      AND n.Direction = 'ISSUED'
      AND n.Status NOT IN ('PAID','RETURNED')
      AND n.IssueDate <= @AsOfDate;

    -- YÜKÜMLÜLÜK 4: Tedarikçi borçları (PaymentPlan PAYABLE açık)
    INSERT INTO @Result
    SELECT 'LIABILITY', N'Tedarikçi Borçları', p.Code, p.Name, 'TRY',
           SUM(pp.Amount - pp.PaidAmount), 130
    FROM PaymentPlan pp
    JOIN Partner p ON p.Id = pp.PartnerId
    WHERE pp.CompanyId = @CompanyId AND pp.IsDeleted = 0
      AND pp.Direction = 'PAYABLE'
      AND pp.Status IN ('OPEN','PARTIAL','OVERDUE')
      AND CAST(pp.CreatedAt AS DATE) <= @AsOfDate
    GROUP BY p.Code, p.Name
    HAVING SUM(pp.Amount - pp.PaidAmount) > 0;

    -- YÜKÜMLÜLÜK 5: Kredi kartı kullanılan limit (FinancialAccount Type=CREDIT_CARD üzerinden)
    INSERT INTO @Result
    SELECT 'LIABILITY', N'Kredi Kartı Kullanımı', a.Code, a.Name, a.Currency,
           CASE WHEN ISNULL(
                    (SELECT SUM(CASE WHEN ft.TransactionType = 'EXPENSE' THEN ft.AmountTRY
                                     WHEN ft.TransactionType = 'INCOME' THEN -ft.AmountTRY
                                     ELSE 0 END)
                     FROM FinancialTransaction ft
                     WHERE ft.AccountId = a.Id AND ft.IsDeleted = 0
                       AND CAST(ft.TransactionDate AS DATE) <= @AsOfDate), 0) > 0
                THEN ISNULL(
                    (SELECT SUM(CASE WHEN ft.TransactionType = 'EXPENSE' THEN ft.AmountTRY
                                     WHEN ft.TransactionType = 'INCOME' THEN -ft.AmountTRY
                                     ELSE 0 END)
                     FROM FinancialTransaction ft
                     WHERE ft.AccountId = a.Id AND ft.IsDeleted = 0
                       AND CAST(ft.TransactionDate AS DATE) <= @AsOfDate), 0)
                ELSE 0 END,
           140
    FROM FinancialAccount a
    WHERE a.CompanyId = @CompanyId AND a.AccountType = 'CREDIT_CARD' AND a.IsDeleted = 0;

    RETURN;
END
GO

-- =============================================================================
-- v_ChequePortfolio — Çek portföyü görünümü (durum + vade)
-- =============================================================================
CREATE OR ALTER VIEW dbo.v_ChequePortfolio AS
SELECT
    c.Id, c.CompanyId,
    c.Direction, c.ChequeNo,
    c.BankName, c.BranchName,
    c.DrawerName, c.Amount, c.Currency,
    c.ChequeDate, c.DueDate,
    c.Status, c.PartnerId,
    p.Name AS PartnerName,
    DATEDIFF(DAY, GETUTCDATE(), c.DueDate) AS DaysToDue,
    CASE
        WHEN c.Status = 'COLLECTED' THEN 'TAHSİL EDİLDİ'
        WHEN c.Status = 'RETURNED'  THEN 'KARŞILIKSIZ'
        WHEN c.Status = 'PAID'      THEN 'ÖDENDİ'
        WHEN DATEDIFF(DAY, GETUTCDATE(), c.DueDate) < 0  THEN 'GECİKMİŞ'
        WHEN DATEDIFF(DAY, GETUTCDATE(), c.DueDate) <= 7 THEN 'YAKINDA VADELİ'
        ELSE 'VADELİ'
    END AS DisplayStatus
FROM Cheque c
LEFT JOIN Partner p ON p.Id = c.PartnerId
WHERE c.IsDeleted = 0;
GO
