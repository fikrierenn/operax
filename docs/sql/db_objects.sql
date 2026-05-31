-- ============================================================
-- OPERAX — DB Objects: Functions, Views, Stored Procedures
-- Idempotent: CREATE OR ALTER — her zaman çalıştırılabilir
-- CLI: operax-cli script docs/sql/db_objects.sql
--      veya: operax-cli migrate (schema_all.sql sonrası otomatik)
-- ============================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ============================================================
-- SCHEMA MIGRATIONS (idempotent — CREATE OR ALTER ile uyumlu)
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ProductionActivity' AND COLUMN_NAME = 'RouteStepId'
)
    ALTER TABLE dbo.ProductionActivity ADD RouteStepId UNIQUEIDENTIFIER NULL;
GO

-- ============================================================
-- SCALAR FUNCTIONS
-- ============================================================

-- UOM Dönüşüm Oranı: Ürünün temel birimi ile verilen birim arası oran döner.
-- Temel birimse 1 döner, tanımsızsa 1 döner.
CREATE OR ALTER FUNCTION dbo.fn_GetConversionRate
(
    @ItemId UNIQUEIDENTIFIER,
    @UomId  UNIQUEIDENTIFIER
)
RETURNS DECIMAL(18,6)
AS
BEGIN
    DECLARE @Rate DECIMAL(18,6);

    SELECT @Rate = CASE
                      WHEN i.BaseUomId = @UomId THEN 1.0
                      ELSE ISNULL(u.ConversionRate, 1.0)
                   END
    FROM Item i
    LEFT JOIN ItemUOM u ON u.ItemId = i.Id AND u.UomId = @UomId
    WHERE i.Id = @ItemId;

    RETURN ISNULL(@Rate, 1.0);
END
GO

-- ============================================================
-- VIEWS
-- ============================================================

-- Anlık stok bakiyesi: şirket + depo + raf + madde bazında
-- IsCancelled=0 olan hareketlerin toplamı; sıfır bakiyeler hariç
CREATE OR ALTER VIEW dbo.vw_InventoryBalance
AS
SELECT
    sm.CompanyId,
    sm.WarehouseId,
    sm.BinId,
    sm.ItemId,
    sm.UomId,
    SUM(sm.QtyBase) AS QtyBalance
FROM StockMovement sm
WHERE sm.IsCancelled = 0
GROUP BY sm.CompanyId, sm.WarehouseId, sm.BinId, sm.ItemId, sm.UomId
HAVING SUM(sm.QtyBase) <> 0;
GO

-- Açık satınalma siparişleri — dropdown için (Status=APPROVED)
CREATE OR ALTER VIEW dbo.vw_OpenPurchaseOrders
AS
SELECT Id, CompanyId, OrderNo AS Code, '' AS Name
FROM PurchaseOrderHeader
WHERE Status = 'APPROVED' AND IsDeleted = 0;
GO

-- Açık satış siparişleri — dropdown için (Status=APPROVED)
CREATE OR ALTER VIEW dbo.vw_OpenSalesOrders
AS
SELECT Id, CompanyId, OrderNo AS Code, '' AS Name
FROM SalesOrderHeader
WHERE Status = 'APPROVED' AND IsDeleted = 0;
GO

-- ============================================================
-- INLINE TABLE-VALUED FUNCTIONS (iTVF)
-- View'ın parameterized versiyonu — CompanyId ile yalıtılmış, optimizer dostu
-- Kullanım: SELECT * FROM dbo.tvf_InventoryBalance(@CompanyId)
-- ============================================================

-- Şirkete ait anlık stok bakiyesi — tek şirket, SARGABLE
CREATE OR ALTER FUNCTION dbo.tvf_InventoryBalance
(
    @CompanyId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        sm.WarehouseId,
        sm.BinId,
        sm.ItemId,
        sm.UomId,
        SUM(sm.QtyBase) AS QtyBalance
    FROM StockMovement sm
    WHERE sm.CompanyId   = @CompanyId
      AND sm.IsCancelled = 0
    GROUP BY sm.WarehouseId, sm.BinId, sm.ItemId, sm.UomId
    HAVING SUM(sm.QtyBase) <> 0
);
GO

-- Şirkete ait onaylı satınalma siparişleri — dropdown için
CREATE OR ALTER FUNCTION dbo.tvf_OpenPurchaseOrders
(
    @CompanyId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT Id, OrderNo AS Code, '' AS Name
    FROM PurchaseOrderHeader
    WHERE CompanyId = @CompanyId
      AND Status    = 'APPROVED'
      AND IsDeleted = 0
);
GO

-- Şirkete ait onaylı satış siparişleri — dropdown için
CREATE OR ALTER FUNCTION dbo.tvf_OpenSalesOrders
(
    @CompanyId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT Id, OrderNo AS Code, '' AS Name
    FROM SalesOrderHeader
    WHERE CompanyId = @CompanyId
      AND Status    = 'APPROVED'
      AND IsDeleted = 0
);
GO

-- MinQty altına düşmüş toplama rafları için yenileme önerileri
-- tvf_InventoryBalance ile nested iTVF — planlayıcı tarafından düzleştirilir
CREATE OR ALTER FUNCTION dbo.tvf_ReplenishmentSuggestions
(
    @CompanyId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        c.ItemId,
        i.Code  AS ItemCode,
        i.Name  AS ItemName,
        c.BinId AS PickingBinId,
        pb.Code AS PickingBinCode,
        c.MinQty,
        c.MaxQty,
        ISNULL(inv.QtyBalance, 0)               AS CurrentQty,
        (c.MaxQty - ISNULL(inv.QtyBalance, 0))  AS NeededQty
    FROM ItemBinConfig c
    JOIN Item i  ON i.Id  = c.ItemId
    JOIN Bin  pb ON pb.Id = c.BinId
    LEFT JOIN dbo.tvf_InventoryBalance(@CompanyId) inv
           ON inv.ItemId = c.ItemId
          AND inv.BinId  = c.BinId
    WHERE c.CompanyId                  = @CompanyId
      AND ISNULL(inv.QtyBalance, 0)    < c.MinQty
);
GO

-- ============================================================
-- STORED PROCEDURES
-- ============================================================

-- Durum Geçiş Doğrulama Motoru (StatusTransition Engine)
-- Belirtilen şirket, evrak tipi ve durum geçişi için aktif bir kural olup olmadığını kontrol eder.
-- Geçiş yetkisi ve kuralı yoksa hata fırlatır.
CREATE OR ALTER PROCEDURE dbo.sp_ValidateStatusTransition
    @CompanyId    UNIQUEIDENTIFIER,
    @DocumentType NVARCHAR(100),
    @FromStatus   NVARCHAR(100),
    @ToStatus     NVARCHAR(100),
    @UserId       NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;

    -- Eğer transition kuralı yoksa hata fırlat
    IF NOT EXISTS (
        SELECT 1 
        FROM StatusTransition 
        WHERE CompanyId = @CompanyId 
          AND DocumentType = @DocumentType 
          AND FromStatusCode = @FromStatus 
          AND ToStatusCode = @ToStatus 
          AND IsActive = 1 
          AND IsDeleted = 0
    )
    BEGIN
        DECLARE @ErrMsg NVARCHAR(500);
        SET @ErrMsg = N'Gecersiz durum gecisi: ' + ISNULL(@FromStatus, 'NULL') + N' -> ' + ISNULL(@ToStatus, 'NULL') + N' (' + @DocumentType + N')';
        THROW 51000, @ErrMsg, 1;
    END
END
GO

-- Mal Kabul Onay
-- Stok hareketi (RECEIPT) yazar, PO satırlarını günceller,
-- PO tamamen teslim alındıysa PO durumunu RECEIVED yapar.
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

        -- Dönem kilidi: onay anının dönemi açık olmalı (Plan 14)
        DECLARE @nowR DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowR, @UserId;

        -- Belgeyi kilitle ve bilgilerini al
        DECLARE @WarehouseId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50),
                @Status NVARCHAR(20), @POId UNIQUEIDENTIFIER;

        SELECT @WarehouseId = WarehouseId, @DocNo = DocNo,
               @Status = Status, @POId = PurchaseOrderId
        FROM ReceivingHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId;

        IF @WarehouseId IS NULL
            THROW 50001, N'Mal kabul belgesi bulunamadı.', 1;

        -- Durum Geçiş Motoru doğrulaması
        EXEC dbo.sp_ValidateStatusTransition @CompanyId, 'RECEIVING', @Status, 'POSTED', @UserId;

        -- Alım alanı bin
        DECLARE @ReceivingBinId UNIQUEIDENTIFIER;
        SELECT TOP 1 @ReceivingBinId = Id
        FROM Bin WHERE WarehouseId = @WarehouseId AND IsReceivingArea = 1;

        -- İş kuralı: Her satır için maliyetli StockMovement INSERT
        -- UnitCost: PurchaseOrderLine.Price (varsa); yoksa 0 (PO bağlantısız satırda)
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal, UnitCost,
             SourceDocType, SourceDocId, SourceDocNo, LotNo, CreatedBy)
        SELECT
            @CompanyId, @WarehouseId, @ReceivingBinId, rl.ItemId, 'RECEIPT',
            rl.QtyBase, rl.UomId, rl.QtyBase, ISNULL(pol.Price, 0),
            'RECEIVING', @HeaderId, @DocNo, rl.LotNo, @UserId
        FROM ReceivingLine rl
        LEFT JOIN PurchaseOrderLine pol ON pol.Id = rl.PurchaseOrderLineId
        WHERE rl.HeaderId = @HeaderId;

        -- PO satırlarını güncelle
        UPDATE pol
        SET pol.QtyReceived = pol.QtyReceived + rl.QtyBase
        FROM PurchaseOrderLine pol
        JOIN ReceivingLine rl ON rl.PurchaseOrderLineId = pol.Id
        WHERE rl.HeaderId = @HeaderId;

        -- PO tamamen alındıysa kapat
        IF @POId IS NOT NULL
           AND NOT EXISTS (
               SELECT 1 FROM PurchaseOrderLine
               WHERE HeaderId = @POId AND QtyReceived < QtyOrdered
           )
            UPDATE PurchaseOrderHeader SET Status = 'RECEIVED' WHERE Id = @POId;

        -- ──────────────────────────────────────────────────────────────
        -- M02 Maliyet: Her satır için Moving Average güncellemesi
        -- (cursor — toplu UPDATE'in atomik olmamasını engelliyor)
        -- ──────────────────────────────────────────────────────────────
        DECLARE @rItemId UNIQUEIDENTIFIER, @rQty DECIMAL(18,6), @rUnitCost DECIMAL(18,4);

        DECLARE cur_lines CURSOR LOCAL FAST_FORWARD FOR
            SELECT rl.ItemId, rl.QtyBase, ISNULL(pol.Price, 0)
            FROM ReceivingLine rl
            LEFT JOIN PurchaseOrderLine pol ON pol.Id = rl.PurchaseOrderLineId
            WHERE rl.HeaderId = @HeaderId;

        OPEN cur_lines;
        FETCH NEXT FROM cur_lines INTO @rItemId, @rQty, @rUnitCost;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            EXEC dbo.sp_UpdateItemCostMovingAvg
                @CompanyId    = @CompanyId,
                @ItemId       = @rItemId,
                @WarehouseId  = @WarehouseId,
                @Qty          = @rQty,
                @UnitCost     = @rUnitCost,
                @MovementType = 'RECEIPT';

            FETCH NEXT FROM cur_lines INTO @rItemId, @rQty, @rUnitCost;
        END

        CLOSE cur_lines;
        DEALLOCATE cur_lines;

        -- Mal kabul evrağı kapat
        UPDATE ReceivingHeader
        SET Status = 'POSTED', UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @HeaderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'cur_lines') >= -1
        BEGIN
            CLOSE cur_lines;
            DEALLOCATE cur_lines;
        END
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Sevkiyat Onay
-- Picking alanından stok hareketi (ISSUE, eksi) yazar, SO satırlarını günceller.
CREATE OR ALTER PROCEDURE dbo.sp_ShippingPost
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Dönem kilidi: onay anının dönemi açık olmalı (Plan 14)
    DECLARE @nowS DATETIME2 = GETUTCDATE();
    EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowS, @UserId;

    DECLARE @WarehouseId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50), @Status NVARCHAR(20);

    SELECT @WarehouseId = WarehouseId, @DocNo = DocNo, @Status = Status
    FROM ShippingHeader WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @HeaderId AND CompanyId = @CompanyId;

    IF @WarehouseId IS NULL
        THROW 50001, 'Sevkiyat belgesi bulunamadı.', 1;

    -- Durum Geçiş Motoru (StatusTransition Engine) doğrulaması
    EXEC dbo.sp_ValidateStatusTransition @CompanyId, 'SHIPMENT', @Status, 'POSTED', @UserId;


    -- Picking alanı bin
    DECLARE @PickingBinId UNIQUEIDENTIFIER;
    SELECT TOP 1 @PickingBinId = Id
    FROM Bin WHERE WarehouseId = @WarehouseId AND IsPickingArea = 1;

    -- Stok hareketi: ISSUE (eksi miktar)
    INSERT INTO StockMovement
        (CompanyId, WarehouseId, BinId, ItemId, MovementType,
         QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, LotNo, CreatedBy)
    SELECT
        @CompanyId, @WarehouseId, @PickingBinId, sl.ItemId, 'ISSUE',
        -sl.QtyBase, sl.UomId, sl.QtyBase, 'SHIPPING', @HeaderId, @DocNo, sl.LotNo, @UserId
    FROM ShippingLine sl
    WHERE sl.HeaderId = @HeaderId;

    -- SO satırları güncelle
    UPDATE sol
    SET sol.QtyShipped = sol.QtyShipped + sl.QtyBase
    FROM SalesOrderLine sol
    JOIN ShippingLine sl ON sl.SalesOrderLineId = sol.Id
    WHERE sl.HeaderId = @HeaderId;

    UPDATE ShippingHeader
    SET Status = 'POSTED', UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
    WHERE Id = @HeaderId;

    COMMIT TRANSACTION;
END
GO

-- Sevkiyat Pick Task Oluştur
-- Stok olanlar için FIFO raf rezervasyonu ile pick task açar;
-- stok olmayanlar için üretim emri (DRAFT) oluşturur.
-- Sonuç: oluşturulan TaskId döner (NULL = tümü üretime gitti).
CREATE OR ALTER PROCEDURE dbo.sp_ShippingCreatePickTask
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @WarehouseId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50), @Status NVARCHAR(20);

    SELECT @WarehouseId = WarehouseId, @DocNo = DocNo, @Status = Status
    FROM ShippingHeader WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @HeaderId AND CompanyId = @CompanyId;

    IF @WarehouseId IS NULL
        THROW 50001, 'Sevkiyat belgesi bulunamadı.', 1;

    DECLARE @TaskId   UNIQUEIDENTIFIER = NEWID();
    DECLARE @TaskDocNo NVARCHAR(50)    = 'PCK-' + @DocNo;

    -- Pick Task Header
    INSERT INTO PickTask (Id, CompanyId, DocNo, ShipmentId, Status, Priority, CreatedBy)
    VALUES (@TaskId, @CompanyId, @TaskDocNo, @HeaderId, 'DRAFT', 10, @UserId);

    -- Pick Task Lines — yalnızca yeterli stok olan satırlar (FIFO bin)
    INSERT INTO PickTaskLine
        (PickTaskId, ItemId, UomId, TargetWarehouseId, TargetBinId, QtyRequested, QtyRequestedBase)
    SELECT
        @TaskId,
        sl.ItemId,
        sl.UomId,
        @WarehouseId,
        COALESCE(
            -- FIFO: en eski stoklu raf
            (SELECT TOP 1 BinId
             FROM StockMovement
             WHERE ItemId = sl.ItemId AND CompanyId = @CompanyId
               AND WarehouseId = @WarehouseId AND IsCancelled = 0
             GROUP BY BinId HAVING SUM(QtyBase) > 0
             ORDER BY MIN(CreatedAt)),
            -- Fallback: picking alanı
            (SELECT TOP 1 Id FROM Bin WHERE WarehouseId = @WarehouseId AND IsPickingArea = 1)
        ),
        sl.QtyOriginal,
        sl.QtyBase
    FROM ShippingLine sl
    WHERE sl.HeaderId = @HeaderId
      AND (
          SELECT ISNULL(SUM(QtyBase), 0)
          FROM StockMovement
          WHERE ItemId = sl.ItemId AND CompanyId = @CompanyId AND IsCancelled = 0
      ) >= sl.QtyBase;

    -- Stok yetersiz satırlar için üretim emri oluştur
    INSERT INTO ProductionOrder
        (Id, CompanyId, DocNo, ItemId, QtyTarget, Status, SourceDocType, SourceDocId, CreatedBy)
    SELECT
        NEWID(),
        @CompanyId,
        'PRD-' + i.Code + '-' + FORMAT(GETDATE(), 'MMdd'),
        sl.ItemId,
        sl.QtyBase - ISNULL((
            SELECT SUM(QtyBase) FROM StockMovement
            WHERE ItemId = sl.ItemId AND CompanyId = @CompanyId AND IsCancelled = 0
        ), 0),
        'DRAFT',
        'SHIPPING',
        @HeaderId,
        @UserId
    FROM ShippingLine sl
    JOIN Item i ON i.Id = sl.ItemId
    WHERE sl.HeaderId = @HeaderId
      AND (
          SELECT ISNULL(SUM(QtyBase), 0)
          FROM StockMovement
          WHERE ItemId = sl.ItemId AND CompanyId = @CompanyId AND IsCancelled = 0
      ) < sl.QtyBase;

    -- Hiç picking satırı oluşmadıysa task'ı sil
    IF NOT EXISTS (SELECT 1 FROM PickTaskLine WHERE PickTaskId = @TaskId)
    BEGIN
        DELETE FROM PickTask WHERE Id = @TaskId;
        SET @TaskId = NULL;
    END

    COMMIT TRANSACTION;

    SELECT @TaskId AS TaskId;
END
GO

-- Transfer Onay
-- İki yönlü stok hareketi yazar: kaynak depoda çıkış, hedef depoda giriş.
CREATE OR ALTER PROCEDURE dbo.sp_TransferPost
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Dönem kilidi: onay anının dönemi açık olmalı (Plan 14)
    DECLARE @nowT DATETIME2 = GETUTCDATE();
    EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowT, @UserId;

    DECLARE @FromWarehouseId UNIQUEIDENTIFIER, @ToWarehouseId UNIQUEIDENTIFIER,
            @DocNo NVARCHAR(50), @Status NVARCHAR(20);

    SELECT @FromWarehouseId = FromWarehouseId, @ToWarehouseId = ToWarehouseId,
           @DocNo = DocNo, @Status = Status
    FROM StockTransfer WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @HeaderId AND CompanyId = @CompanyId;

    IF @FromWarehouseId IS NULL
        THROW 50001, 'Transfer belgesi bulunamadı.', 1;

    -- Durum Geçiş Motoru (StatusTransition Engine) doğrulaması
    EXEC dbo.sp_ValidateStatusTransition @CompanyId, 'TRANSFER', @Status, 'POSTED', @UserId;


    -- Çıkış hareketi (kaynak depo, eksi)
    INSERT INTO StockMovement
        (CompanyId, WarehouseId, BinId, ItemId, MovementType,
         QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
    SELECT
        @CompanyId, @FromWarehouseId, l.FromBinId, l.ItemId, 'TRANSFER',
        -l.QtyBase, l.UomId, l.QtyBase, 'TRANSFER', @HeaderId, @DocNo, @UserId
    FROM StockTransferLine l WHERE l.TransferId = @HeaderId;

    -- Giriş hareketi (hedef depo, artı)
    INSERT INTO StockMovement
        (CompanyId, WarehouseId, BinId, ItemId, MovementType,
         QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
    SELECT
        @CompanyId, @ToWarehouseId, l.ToBinId, l.ItemId, 'TRANSFER',
        l.QtyBase, l.UomId, l.QtyBase, 'TRANSFER', @HeaderId, @DocNo, @UserId
    FROM StockTransferLine l WHERE l.TransferId = @HeaderId;

    UPDATE StockTransfer
    SET Status = 'POSTED', PostedAt = GETUTCDATE(), UpdatedBy = @UserId
    WHERE Id = @HeaderId;

    COMMIT TRANSACTION;
END
GO

-- Sayım Düzeltme Onay
-- Fark olan satırlar için COUNT_ADJ hareketi yazar, sayım durumunu COMPLETED yapar.
CREATE OR ALTER PROCEDURE dbo.sp_CycleCountPost
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Dönem kilidi: onay anının dönemi açık olmalı (Plan 14)
    DECLARE @nowC DATETIME2 = GETUTCDATE();
    EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowC, @UserId;

    DECLARE @WarehouseId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50), @Status NVARCHAR(20);

    SELECT @WarehouseId = WarehouseId, @DocNo = DocNo, @Status = Status
    FROM CycleCount WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @HeaderId AND CompanyId = @CompanyId;

    IF @WarehouseId IS NULL
        THROW 50001, 'Sayım belgesi bulunamadı.', 1;

    -- Durum Geçiş Motoru (StatusTransition Engine) doğrulaması
    EXEC dbo.sp_ValidateStatusTransition @CompanyId, 'CYCLE_COUNT', @Status, 'COMPLETED', @UserId;


    -- Fark olan satırlar için düzeltme hareketi
    INSERT INTO StockMovement
        (CompanyId, WarehouseId, BinId, ItemId, MovementType,
         QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
    SELECT
        @CompanyId, @WarehouseId, l.BinId, l.ItemId, 'COUNT_ADJ',
        l.QtyCounted - l.QtySystem,
        i.BaseUomId,
        ABS(l.QtyCounted - l.QtySystem),
        'COUNT', @HeaderId, @DocNo, @UserId
    FROM CycleCountLine l
    JOIN Item i ON i.Id = l.ItemId
    WHERE l.CycleCountId = @HeaderId
      AND l.QtyCounted <> l.QtySystem;

    UPDATE CycleCount
    SET Status = 'COMPLETED', PostedAt = GETUTCDATE(), UpdatedBy = @UserId
    WHERE Id = @HeaderId;

    COMMIT TRANSACTION;
END
GO

-- Üretim BOM Yükle
-- Ürünün reçetesinden (ItemBOM) hammadde ihtiyaçlarını üretim emrine kopyalar.
-- Mevcut satırları temizleyip yeniden yükler (idempotent).
CREATE OR ALTER PROCEDURE dbo.sp_ProductionLoadBOM
    @OrderId   UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @ItemId UNIQUEIDENTIFIER, @QtyTarget DECIMAL(18,4);

    SELECT @ItemId = ItemId, @QtyTarget = QtyTarget
    FROM ProductionOrder
    WHERE Id = @OrderId AND CompanyId = @CompanyId;

    IF @ItemId IS NULL
        THROW 50001, 'Üretim emri bulunamadı.', 1;

    -- Mevcut satırları temizle
    DELETE FROM ProductionOrderLine WHERE ProductionOrderId = @OrderId;

    -- BOM'dan hammadde ihtiyaçlarını hesapla
    INSERT INTO ProductionOrderLine (Id, ProductionOrderId, ItemId, QtyRequired)
    SELECT NEWID(), @OrderId, ChildItemId, QtyRequired * @QtyTarget
    FROM ItemBOM
    WHERE ParentItemId = @ItemId AND IsActive = 1;

    COMMIT TRANSACTION;
END
GO

-- Üretim Pick Task Oluştur
-- Üretim emri hammaddeleri için picking görev açar.
-- Sonuç: oluşturulan TaskId döner.
CREATE OR ALTER PROCEDURE dbo.sp_ProductionCreatePickTask
    @OrderId   UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @ItemId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50), @WarehouseId UNIQUEIDENTIFIER;

    SELECT @ItemId = ItemId, @DocNo = DocNo, @WarehouseId = TargetWarehouseId
    FROM ProductionOrder
    WHERE Id = @OrderId AND CompanyId = @CompanyId;

    IF @ItemId IS NULL
        THROW 50001, 'Üretim emri bulunamadı.', 1;

    DECLARE @TaskId    UNIQUEIDENTIFIER = NEWID();
    DECLARE @TaskDocNo NVARCHAR(50)     = 'PRD-PCK-' + @DocNo;

    -- Picking alanı bin
    DECLARE @PickingBinId UNIQUEIDENTIFIER;
    SELECT TOP 1 @PickingBinId = Id FROM Bin WHERE WarehouseId = @WarehouseId AND IsPickingArea = 1;

    INSERT INTO PickTask (Id, CompanyId, DocNo, SourceDocId, Status, Priority, CreatedBy)
    VALUES (@TaskId, @CompanyId, @TaskDocNo, @OrderId, 'DRAFT', 20, @UserId);

    INSERT INTO PickTaskLine
        (PickTaskId, ItemId, UomId, TargetWarehouseId, TargetBinId, QtyRequested, QtyRequestedBase)
    SELECT
        @TaskId, pol.ItemId, i.BaseUomId, @WarehouseId, @PickingBinId,
        pol.QtyRequired, pol.QtyRequired
    FROM ProductionOrderLine pol
    JOIN Item i ON i.Id = pol.ItemId
    WHERE pol.ProductionOrderId = @OrderId;

    COMMIT TRANSACTION;

    SELECT @TaskId AS TaskId;
END
GO

-- Üretim Tamamla
-- Mamul ürünü stoğa girer (PRODUCTION hareketi), emri COMPLETED yapar.
CREATE OR ALTER PROCEDURE dbo.sp_ProductionFinish
    @OrderId   UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @Qty       DECIMAL(18,4),
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Dönem kilidi: onay anının dönemi açık olmalı (Plan 14)
    DECLARE @nowP DATETIME2 = GETUTCDATE();
    EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowP, @UserId;

    DECLARE @ItemId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50),
            @WarehouseId UNIQUEIDENTIFIER, @BinId UNIQUEIDENTIFIER;

    SELECT @ItemId = ItemId, @DocNo = DocNo,
           @WarehouseId = TargetWarehouseId, @BinId = TargetBinId
    FROM ProductionOrder
    WHERE Id = @OrderId AND CompanyId = @CompanyId;

    IF @ItemId IS NULL
        THROW 50001, 'Üretim emri bulunamadı.', 1;

    -- Mamul stok girişi
    INSERT INTO StockMovement
        (CompanyId, WarehouseId, BinId, ItemId, MovementType,
         QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
    SELECT
        @CompanyId, @WarehouseId, @BinId, @ItemId, 'PRODUCTION',
        @Qty, i.BaseUomId, @Qty, 'PRODUCTION', @OrderId, @DocNo, @UserId
    FROM Item i WHERE i.Id = @ItemId;

    UPDATE ProductionOrder
    SET Status = 'COMPLETED', QtyProduced = @Qty, CompletedAt = GETUTCDATE(), UpdatedBy = @UserId
    WHERE Id = @OrderId;

    COMMIT TRANSACTION;
END
GO

-- Picking Satırı Tamamla
-- Satırı günceller, stok hareketi yazar, üretim sarfiyatını günceller,
-- tüm satırlar bittiyse görevi COMPLETED yapar.
CREATE OR ALTER PROCEDURE dbo.sp_PickLinePost
    @LineId    UNIQUEIDENTIFIER,
    @TaskId    UNIQUEIDENTIFIER,
    @Qty       DECIMAL(18,4),
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Satırı güncelle
    UPDATE PickTaskLine
    SET QtyPicked     = @Qty,
        QtyPickedBase = @Qty,
        PickedByUserId = @UserId,
        PickedAt      = GETUTCDATE(),
        PickedBinId   = TargetBinId
    WHERE Id = @LineId;

    -- Stok bilgilerini al
    DECLARE @WarehouseId UNIQUEIDENTIFIER, @BinId UNIQUEIDENTIFIER,
            @ItemId UNIQUEIDENTIFIER, @UomId UNIQUEIDENTIFIER,
            @DocNo NVARCHAR(50), @TaskDocNo NVARCHAR(50),
            @SourceDocId UNIQUEIDENTIFIER;

    SELECT
        @WarehouseId = ptl.TargetWarehouseId,
        @BinId       = ptl.TargetBinId,
        @ItemId      = ptl.ItemId,
        @UomId       = ptl.UomId,
        @DocNo       = pt.DocNo,
        @TaskDocNo   = pt.DocNo,
        @SourceDocId = ISNULL(pt.ShipmentId, pt.SourceDocId)
    FROM PickTaskLine ptl
    JOIN PickTask pt ON pt.Id = ptl.PickTaskId
    WHERE ptl.Id = @LineId;

    -- Stok hareketi: ISSUE
    INSERT INTO StockMovement
        (CompanyId, WarehouseId, BinId, ItemId, MovementType,
         QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
    VALUES
        (@CompanyId, @WarehouseId, @BinId, @ItemId, 'ISSUE',
         -@Qty, @UomId, @Qty, 'PICKING', @TaskId, @DocNo, @UserId);

    -- Üretim hammadde sarfiyatı güncelle (sadece PRD-PCK- görevleri)
    IF @TaskDocNo LIKE 'PRD-PCK-%'
    BEGIN
        DECLARE @PrdOrderId UNIQUEIDENTIFIER;
        SELECT @PrdOrderId = SourceDocId FROM PickTask WHERE Id = @TaskId;

        UPDATE ProductionOrderLine
        SET QtyIssued = QtyIssued + @Qty
        WHERE ProductionOrderId = @PrdOrderId AND ItemId = @ItemId;
    END

    -- Görev durumunu güncelle
    UPDATE PickTask
    SET Status     = 'IN_PROGRESS',
        StartedAt  = ISNULL(StartedAt, GETUTCDATE())
    WHERE Id = @TaskId;

    -- Tüm satırlar tamamlandıysa kapat
    IF NOT EXISTS (SELECT 1 FROM PickTaskLine WHERE PickTaskId = @TaskId AND QtyPicked = 0)
        UPDATE PickTask
        SET Status      = 'COMPLETED',
            CompletedAt = GETUTCDATE()
        WHERE Id = @TaskId;

    COMMIT TRANSACTION;
END
GO
