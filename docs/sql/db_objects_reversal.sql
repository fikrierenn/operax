-- =============================================================================
-- REVERSAL SP'LERİ — Plan 14 (StockMovement immutability)
-- Cancel = ters hareket (MovementType='REVERSAL') + IsCancelled=1 flag.
-- Silme yok; bakiye = SUM(QtyBase WHERE IsCancelled=0).
-- THROW aralığı: 51300-51399.
-- =============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Mal Kabul İptali — ters RECEIPT hareketi yazar
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

        -- Mevcut hareketleri kapat
        UPDATE StockMovement
        SET IsCancelled = 1, CancelledAt = GETUTCDATE(), CancelledBy = @UserId
        WHERE SourceDocType = 'RECEIVING' AND SourceDocId = @HeaderId AND IsCancelled = 0;

        -- Ters hareket yaz (negatif QtyBase)
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal, UnitCost,
             SourceDocType, SourceDocId, SourceDocNo, LotNo, CreatedBy)
        SELECT
            CompanyId, WarehouseId, BinId, ItemId, 'REVERSAL',
            -QtyBase, UomId, QtyOriginal, UnitCost,
            'RECEIVING', @HeaderId, @DocNo, LotNo, @UserId
        FROM StockMovement
        WHERE SourceDocType = 'RECEIVING' AND SourceDocId = @HeaderId
          AND MovementType <> 'REVERSAL';

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

-- Sevkiyat İptali — ters ISSUE hareketi yazar
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

        UPDATE StockMovement
        SET IsCancelled = 1, CancelledAt = GETUTCDATE(), CancelledBy = @UserId
        WHERE SourceDocType = 'SHIPPING' AND SourceDocId = @HeaderId AND IsCancelled = 0;

        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal,
             SourceDocType, SourceDocId, SourceDocNo, LotNo, CreatedBy)
        SELECT
            CompanyId, WarehouseId, BinId, ItemId, 'REVERSAL',
            -QtyBase, UomId, QtyOriginal,
            'SHIPPING', @HeaderId, @DocNo, LotNo, @UserId
        FROM StockMovement
        WHERE SourceDocType = 'SHIPPING' AND SourceDocId = @HeaderId
          AND MovementType <> 'REVERSAL';

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

-- Transfer İptali — ters TRANSFER hareketi (çıkış+giriş ikisi birden) yazar
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

        UPDATE StockMovement
        SET IsCancelled = 1, CancelledAt = GETUTCDATE(), CancelledBy = @UserId
        WHERE SourceDocType = 'TRANSFER' AND SourceDocId = @HeaderId AND IsCancelled = 0;

        -- Çıkış ve giriş hareketi ikisi de ters çevrilir
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal,
             SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
        SELECT
            CompanyId, WarehouseId, BinId, ItemId, 'REVERSAL',
            -QtyBase, UomId, QtyOriginal,
            'TRANSFER', @HeaderId, @DocNo, @UserId
        FROM StockMovement
        WHERE SourceDocType = 'TRANSFER' AND SourceDocId = @HeaderId
          AND MovementType <> 'REVERSAL';

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

-- Sayım İptali — COUNT_ADJ hareketini ters çevirir
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

        UPDATE StockMovement
        SET IsCancelled = 1, CancelledAt = GETUTCDATE(), CancelledBy = @UserId
        WHERE SourceDocType = 'COUNT' AND SourceDocId = @HeaderId AND IsCancelled = 0;

        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal,
             SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
        SELECT
            CompanyId, WarehouseId, BinId, ItemId, 'REVERSAL',
            -QtyBase, UomId, QtyOriginal,
            'COUNT', @HeaderId, @DocNo, @UserId
        FROM StockMovement
        WHERE SourceDocType = 'COUNT' AND SourceDocId = @HeaderId
          AND MovementType <> 'REVERSAL';

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

-- Üretim Çıkışı İptali — PRODUCTION hareketi ters çevirir
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

        UPDATE StockMovement
        SET IsCancelled = 1, CancelledAt = GETUTCDATE(), CancelledBy = @UserId
        WHERE SourceDocType = 'PRODUCTION' AND SourceDocId = @OrderId AND IsCancelled = 0;

        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal,
             SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
        SELECT
            CompanyId, WarehouseId, BinId, ItemId, 'REVERSAL',
            -QtyBase, UomId, QtyOriginal,
            'PRODUCTION', @OrderId, @DocNo, @UserId
        FROM StockMovement
        WHERE SourceDocType = 'PRODUCTION' AND SourceDocId = @OrderId
          AND MovementType <> 'REVERSAL';

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

-- Satış Faturası İptali
-- POSTED → CANCELLED; AM Debit ters Credit (REVERSAL); tahsilat varsa REJECT.
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

        -- İmmutability: tahsil edilmiş ödeme varsa reject
        IF EXISTS (
            SELECT 1 FROM PaymentPlan
            WHERE SourceDocType = 'SALES_INVOICE' AND SourceDocId = @InvoiceId
              AND Status IN ('PAID','PARTIAL') AND FinancialTransactionId IS NOT NULL
        )
            THROW 51403, N'Bağlı tahsilat mevcut; önce tahsilatı iptal edin.', 1;

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

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Alış Faturası İptali
-- POSTED → CANCELLED; AM Credit satırlarını ters Debit (REVERSAL); ödeme varsa REJECT.
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

        -- İmmutability: ödenmiş PaymentPlan varsa reject
        IF EXISTS (
            SELECT 1 FROM PaymentPlan
            WHERE SourceDocType = 'PURCHASE_INVOICE' AND SourceDocId = @InvoiceId
              AND Status IN ('PAID','PARTIAL') AND FinancialTransactionId IS NOT NULL
        )
            THROW 51413, N'Bağlı ödeme mevcut; önce ödemeyi iptal edin.', 1;

        -- Fatura iptal
        UPDATE ExpenseInvoice
        SET Status = 'CANCELLED', UpdatedBy = @UserId
        WHERE Id = @InvoiceId;

        -- PaymentPlan açıkları iptal
        UPDATE PaymentPlan
        SET Status = 'CANCELLED'
        WHERE SourceDocType = 'PURCHASE_INVOICE' AND SourceDocId = @InvoiceId
          AND Status = 'OPEN';

        -- AM ters kayıt: satır bazlı Debit (ters Credit); REVERSAL ile unique
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit,
             NetAmount, TaxAmount, Currency,
             CostCenterId, ExpenseTypeId,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        SELECT
            NEWID(), @CompanyId, @PartnerId, @now,
            l.TotalAmount, 0,
            -l.Amount, -l.TaxAmount, 'TRY',
            l.CostCenterId, l.ExpenseTypeId,
            'REVERSAL', l.Id, @DocNo,
            N'Alış Faturası İptali: ' + @DocNo, @UserId
        FROM ExpenseInvoiceLine l
        WHERE l.ExpenseInvoiceId = @InvoiceId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Tahsilat/Ödeme İptali
-- FinancialTransaction soft-delete + AM ters kayıt + PaymentPlan geri aç.
-- INCOME iptali → Debit; EXPENSE iptali → Credit.
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

        -- İşlemi soft-delete
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
