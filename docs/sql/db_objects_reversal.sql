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
