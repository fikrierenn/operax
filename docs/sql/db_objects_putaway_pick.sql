-- =============================================================================
-- Plan 33 Faz B — WMS terminal SP'leri (SQL-First; C# orkestrasyonu kaldırıldı)
-- AC1 sp_PutawayPost  : mal kabul rafından depolama rafına bin-to-bin yerleme
-- AC2 sp_PickConfirm   : el terminali barkod onayı + toplama satırı tamamlama
-- İki SP de TRY/CATCH + ROLLBACK + THROW (50000-59999) + dönem kilidi taşır.
-- =============================================================================

-- =============================================================================
-- sp_PutawayPost — Adresleme (Putaway): mal kabul alanından depolama rafına taşıma
-- NE YAPAR: Tek-satır bin-to-bin transfer; StockTransfer başlığı POSTED açar,
--           çıkış (kaynak raf, eksi) + giriş (hedef raf, artı) StockMovement yazar.
-- İŞ KURALI: Kaynak rafta yeterli stok yoksa REDDEDİLİR (negatif stok engeli).
-- THROW: 51570 yetersiz stok · 51571 geçersiz miktar · 51572 raf bulunamadı
-- BAĞIMLILIK: sp_GuardPeriodOpen, tvf_InventoryBalance, fn_DefaultBranchId
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PutawayPost
    @ItemId    UNIQUEIDENTIFIER,
    @FromBinId UNIQUEIDENTIFIER,
    @ToBinId   UNIQUEIDENTIFIER,
    @Qty       DECIMAL(18,4),
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Guard: miktar pozitif olmalı
        IF @Qty IS NULL OR @Qty <= 0
            THROW 51571, N'Yerleme miktarı sıfırdan büyük olmalı.', 1;

        -- Kaynak ve hedef rafın deposunu türet (raf → depo); raf yoksa hata
        DECLARE @FromWarehouseId UNIQUEIDENTIFIER, @ToWarehouseId UNIQUEIDENTIFIER, @BaseUomId UNIQUEIDENTIFIER;

        SELECT @FromWarehouseId = w.Id
        FROM Bin b JOIN Warehouse w ON w.Id = b.WarehouseId
        WHERE b.Id = @FromBinId AND w.CompanyId = @CompanyId;

        SELECT @ToWarehouseId = w.Id
        FROM Bin b JOIN Warehouse w ON w.Id = b.WarehouseId
        WHERE b.Id = @ToBinId AND w.CompanyId = @CompanyId;

        IF @FromWarehouseId IS NULL OR @ToWarehouseId IS NULL
            THROW 51572, N'Kaynak veya hedef raf bulunamadı.', 1;

        -- Ürünün temel birimi — ürün yoksa/birim tanımsızsa erken Türkçe hata (IMP-1)
        SELECT @BaseUomId = BaseUomId FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId;
        IF @BaseUomId IS NULL
            THROW 51573, N'Ürün bulunamadı veya temel birim tanımsız.', 1;

        -- İş kuralı: kaynak rafta yeterli stok yoksa yerleme reddedilir (negatif stok engeli)
        DECLARE @Available DECIMAL(18,4) = (
            SELECT ISNULL(SUM(QtyBalance), 0)
            FROM tvf_InventoryBalance(@CompanyId)
            WHERE ItemId = @ItemId AND BinId = @FromBinId);
        IF @Available < @Qty
            THROW 51570, N'Kaynak rafta yeterli stok yok.', 1;

        -- Dönem kilidi (onay anı)
        DECLARE @nowT DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowT, @UserId;

        DECLARE @TransferId UNIQUEIDENTIFIER = NEWID();
        DECLARE @DocNo NVARCHAR(50) = CONCAT('RPL-', FORMAT(@nowT, 'yyyyMMddHHmm'));

        -- Transfer başlığı: aynı depo içi bin-to-bin, doğrudan POSTED
        -- CreatedBy UNIQUEIDENTIFIER → @UserId NVARCHAR TRY_CAST ile (implicit cast riski, CRIT-1)
        INSERT INTO StockTransfer
            (Id, CompanyId, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes, CreatedBy)
        VALUES
            (@TransferId, @CompanyId, @DocNo, 'POSTED', 'BIN_TO_BIN',
             @FromWarehouseId, @ToWarehouseId, N'ADRESLEME (PUTAWAY)', TRY_CAST(@UserId AS UNIQUEIDENTIFIER));

        -- Çıkış hareketi (kaynak raf, eksi)
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy, BranchId)
        VALUES
            (@CompanyId, @FromWarehouseId, @FromBinId, @ItemId, 'TRANSFER',
             -@Qty, @BaseUomId, @Qty, 'TRANSFER', @TransferId, @DocNo, @UserId,
             ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @FromWarehouseId), dbo.fn_DefaultBranchId(@CompanyId)));

        -- Giriş hareketi (hedef raf, artı)
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy, BranchId)
        VALUES
            (@CompanyId, @ToWarehouseId, @ToBinId, @ItemId, 'TRANSFER',
             @Qty, @BaseUomId, @Qty, 'TRANSFER', @TransferId, @DocNo, @UserId,
             ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @ToWarehouseId), dbo.fn_DefaultBranchId(@CompanyId)));

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_PickConfirm — El terminali toplama barkod onayı (atomik durum geçişi)
-- NE YAPAR: Barkodu satır ürünüyle doğrular; eşleşirse satırı tamamlar
--           (QtyPickedBase = QtyRequestedBase), DRAFT görevi IN_PROGRESS'e geçirir,
--           tüm satırlar bitince görevi COMPLETED yapar.
-- NOT: Mevcut terminal davranışı birebir korunur — bu SP StockMovement YAZMAZ
--      (gerçek stok çıkışı sevkiyat/sp_PickLinePost yolunda; iki yol arası
--       stok-düşme semantiği ayrı domain kararı olarak Plan 33 DEBT'e işlendi).
-- THROW: 51580 satır bulunamadı · 51581 barkod eşleşmedi
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PickConfirm
    @TaskId    UNIQUEIDENTIFIER,
    @LineId    UNIQUEIDENTIFIER,
    @Barcode   NVARCHAR(100),
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Satır + ürün kodu (görev şirket filtresiyle — IDOR koruması)
        DECLARE @ItemId UNIQUEIDENTIFIER, @ItemCode NVARCHAR(100);
        SELECT @ItemId = l.ItemId, @ItemCode = i.Code
        FROM PickTaskLine l
        JOIN PickTask pt ON pt.Id = l.PickTaskId
        JOIN Item i ON i.Id = l.ItemId
        WHERE l.Id = @LineId AND l.PickTaskId = @TaskId AND pt.CompanyId = @CompanyId;

        IF @ItemId IS NULL
            THROW 51580, N'Toplama satırı bulunamadı.', 1;

        -- İş kuralı: barkod ürün koduyla VEYA ürünün barkod listesiyle eşleşmeli
        DECLARE @Match BIT = 0;
        IF UPPER(@Barcode) = UPPER(@ItemCode)
            SET @Match = 1;
        ELSE IF EXISTS (SELECT 1 FROM ItemBarcode WHERE ItemId = @ItemId AND Barcode = @Barcode)
            SET @Match = 1;

        IF @Match = 0
            THROW 51581, N'Barkod eşleşmedi! Beklenen ürün kodu farklı.', 1;

        -- Satırı tamamla
        UPDATE PickTaskLine
        SET QtyPickedBase = QtyRequestedBase
        WHERE Id = @LineId;

        -- İş kuralı: DRAFT toplama görevi ilk barkodla IN_PROGRESS'e geçer
        UPDATE PickTask
        SET Status = 'IN_PROGRESS', AssignedUserId = @UserId
        WHERE Id = @TaskId AND Status = 'DRAFT';

        -- Tüm satırlar tamamlandıysa görevi COMPLETED yap
        IF NOT EXISTS (
            SELECT 1 FROM PickTaskLine
            WHERE PickTaskId = @TaskId AND QtyPickedBase < QtyRequestedBase)
            UPDATE PickTask SET Status = 'COMPLETED' WHERE Id = @TaskId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
