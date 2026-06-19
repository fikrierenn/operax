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

-- Açık satınalma siparişleri — dropdown için (onaylı = POSTED; eski APPROVED veri toleransı)
CREATE OR ALTER VIEW dbo.vw_OpenPurchaseOrders
AS
SELECT Id, CompanyId, OrderNo AS Code, '' AS Name
FROM PurchaseOrderHeader
WHERE Status IN ('POSTED', 'APPROVED') AND IsDeleted = 0;
GO

-- Açık satış siparişleri — dropdown için (onaylı = POSTED; eski APPROVED veri toleransı)
CREATE OR ALTER VIEW dbo.vw_OpenSalesOrders
AS
SELECT Id, CompanyId, OrderNo AS Code, '' AS Name
FROM SalesOrderHeader
WHERE Status IN ('POSTED', 'APPROVED') AND IsDeleted = 0;
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
    -- Mal kabul ekranı: açık siparişi tedarikçiye göre filtrelemek + ekranda göstermek için
    -- PartnerId/Name (tedarikçi adı) ve OrderDate de döndürülür.
    SELECT po.Id,
           po.OrderNo     AS Code,
           p.Name         AS Name,
           po.PartnerId   AS PartnerId,
           po.WarehouseId AS WarehouseId,
           po.OrderDate   AS OrderDate
    FROM PurchaseOrderHeader po
    JOIN Partner p ON p.Id = po.PartnerId
    WHERE po.CompanyId = @CompanyId
      AND po.Status    IN ('POSTED', 'APPROVED')
      AND po.IsDeleted = 0
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
      AND Status    IN ('POSTED', 'APPROVED')
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
        (c.MaxQty - ISNULL(inv.QtyBalance, 0))  AS NeededQty,
        -- Plan 32: tercih edilen tedarikçi (satınalmacı kimden/ne sürede sipariş verecek)
        sup.PartnerId        AS PreferredSupplierId,
        psup.Name            AS PreferredSupplierName,
        sup.SupplierItemCode AS SupplierItemCode,
        sup.LeadTimeDays     AS LeadTimeDays,
        sup.MinOrderQty      AS SupplierMinOrderQty
    FROM ItemBinConfig c
    JOIN Item i  ON i.Id  = c.ItemId AND i.CompanyId = @CompanyId AND i.IsDeleted = 0
    JOIN Bin  pb ON pb.Id = c.BinId
    LEFT JOIN dbo.tvf_InventoryBalance(@CompanyId) inv
           ON inv.ItemId = c.ItemId
          AND inv.BinId  = c.BinId
    -- Tercih edilen tedarikçi (ürüne tek; filtered unique garanti eder)
    LEFT JOIN SupplierItem sup
           ON sup.CompanyId   = @CompanyId
          AND sup.ItemId      = c.ItemId
          AND sup.IsPreferred = 1
          AND sup.IsDeleted   = 0
    LEFT JOIN Partner psup ON psup.Id = sup.PartnerId AND psup.CompanyId = @CompanyId
    WHERE c.CompanyId                  = @CompanyId
      AND ISNULL(inv.QtyBalance, 0)    < c.MinQty
);
GO

-- ============================================================
-- STORED PROCEDURES
-- ============================================================

-- =============================================================================
-- sp_ValidateStatusTransition — Durum geçiş kuralını doğrular
-- NE YAPAR: StatusTransition tablosunda eşleşen aktif geçiş kuralı yoksa THROW fırlatır.
-- PARAMETRELERİ:
--   @CompanyId    UNIQUEIDENTIFIER — şirket kimliği
--   @DocumentType NVARCHAR(100)    — evrak tipi (örn. 'RECEIVING', 'SHIPMENT')
--   @FromStatus   NVARCHAR(100)    — mevcut durum kodu
--   @ToStatus     NVARCHAR(100)    — hedef durum kodu
--   @UserId       NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: Yalnızca okuma; hiçbir tabloya INSERT/UPDATE yazmaz.
-- THROW: 51000 — geçersiz veya tanımsız durum geçişi
-- BAĞIMLILIK: —
-- =============================================================================
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

-- =============================================================================
-- sp_ReceivingPost — Mal kabul belgesini onaylar
-- NE YAPAR: ReceivingLine satırları için RECEIPT hareketi yazar, PO satır miktarlarını
--           günceller, PO tamamen teslim alındıysa Status=RECEIVED yapar,
--           her satır için moving average maliyeti günceller.
-- PARAMETRELERİ:
--   @HeaderId  UNIQUEIDENTIFIER — onaylanacak mal kabul başlık ID'si
--   @CompanyId UNIQUEIDENTIFIER — şirket kimliği
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: StockMovement (INSERT), PurchaseOrderLine.QtyReceived (UPDATE),
--               PurchaseOrderHeader.Status (UPDATE), ReceivingHeader.Status (UPDATE),
--               ItemCost (UPDATE — sp_UpdateItemCostMovingAvg üzerinden)
-- THROW: 50001 — belge bulunamadı; 51000 — geçersiz durum geçişi
-- BAĞIMLILIK: sp_ValidateStatusTransition, sp_GuardPeriodOpen, sp_UpdateItemCostMovingAvg
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
             SourceDocType, SourceDocId, SourceDocNo, LotNo, CreatedBy, BranchId)
        SELECT
            @CompanyId, @WarehouseId, @ReceivingBinId, rl.ItemId, 'RECEIPT',
            rl.QtyBase, rl.UomId, rl.QtyBase, ISNULL(pol.Price, 0),
            'RECEIVING', @HeaderId, @DocNo, rl.LotNo, @UserId,
            ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @WarehouseId), dbo.fn_DefaultBranchId(@CompanyId))
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

-- =============================================================================
-- sp_ShippingPost — Sevkiyat belgesini onaylar
-- NE YAPAR: ShippingLine satırları için ISSUE hareketi (eksi miktar) yazar,
--           ilişkili SO satırlarının QtyShipped değerini günceller.
-- PARAMETRELERİ:
--   @HeaderId  UNIQUEIDENTIFIER — onaylanacak sevkiyat başlık ID'si
--   @CompanyId UNIQUEIDENTIFIER — şirket kimliği
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: StockMovement (INSERT), SalesOrderLine.QtyShipped (UPDATE),
--               ShippingHeader.Status (UPDATE)
-- THROW: 50001 — belge bulunamadı; 51000 — geçersiz durum geçişi
-- BAĞIMLILIK: sp_ValidateStatusTransition, sp_GuardPeriodOpen
-- =============================================================================
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
         QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, LotNo, CreatedBy, BranchId)
    SELECT
        @CompanyId, @WarehouseId, @PickingBinId, sl.ItemId, 'ISSUE',
        -sl.QtyBase, sl.UomId, sl.QtyBase, 'SHIPPING', @HeaderId, @DocNo, sl.LotNo, @UserId,
        ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @WarehouseId), dbo.fn_DefaultBranchId(@CompanyId))
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

-- =============================================================================
-- sp_ShippingCreatePickTask — Sevkiyat için pick task oluşturur
-- NE YAPAR: Yeterli stoğu olan satırlar için FIFO raf seçimiyle PickTask ve
--           PickTaskLine kayıtları açar; stok yetersiz satırlar için DRAFT
--           üretim emri (ProductionOrder) oluşturur. TaskId döner.
-- PARAMETRELERİ:
--   @HeaderId  UNIQUEIDENTIFIER — sevkiyat başlık ID'si
--   @CompanyId UNIQUEIDENTIFIER — şirket kimliği
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: PickTask (INSERT), PickTaskLine (INSERT),
--               ProductionOrder (INSERT — stok yetersiz satırlar için)
-- THROW: 50001 — belge bulunamadı
-- BAĞIMLILIK: —
-- =============================================================================
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

-- =============================================================================
-- sp_TransferPost — Depo transfer belgesini onaylar
-- NE YAPAR: Her satır için çift yönlü TRANSFER hareketi yazar; kaynak depoda
--           eksi (çıkış), hedef depoda artı (giriş) StockMovement kaydı açar.
-- PARAMETRELERİ:
--   @HeaderId  UNIQUEIDENTIFIER — onaylanacak transfer başlık ID'si
--   @CompanyId UNIQUEIDENTIFIER — şirket kimliği
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: StockMovement (INSERT ×2 per satır — çıkış ve giriş),
--               StockTransfer.Status (UPDATE)
-- THROW: 50001 — belge bulunamadı; 51000 — geçersiz durum geçişi
-- BAĞIMLILIK: sp_ValidateStatusTransition, sp_GuardPeriodOpen
-- =============================================================================
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


    -- Çıkış hareketi (kaynak depo, eksi) — BranchId kaynak depodan türetilir
    INSERT INTO StockMovement
        (CompanyId, WarehouseId, BinId, ItemId, MovementType,
         QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy, BranchId)
    SELECT
        @CompanyId, @FromWarehouseId, l.FromBinId, l.ItemId, 'TRANSFER',
        -l.QtyBase, l.UomId, l.QtyBase, 'TRANSFER', @HeaderId, @DocNo, @UserId,
        ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @FromWarehouseId), dbo.fn_DefaultBranchId(@CompanyId))
    FROM StockTransferLine l WHERE l.TransferId = @HeaderId;

    -- Giriş hareketi (hedef depo, artı) — BranchId hedef depodan türetilir
    INSERT INTO StockMovement
        (CompanyId, WarehouseId, BinId, ItemId, MovementType,
         QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy, BranchId)
    SELECT
        @CompanyId, @ToWarehouseId, l.ToBinId, l.ItemId, 'TRANSFER',
        l.QtyBase, l.UomId, l.QtyBase, 'TRANSFER', @HeaderId, @DocNo, @UserId,
        ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @ToWarehouseId), dbo.fn_DefaultBranchId(@CompanyId))
    FROM StockTransferLine l WHERE l.TransferId = @HeaderId;

    UPDATE StockTransfer
    SET Status = 'POSTED', PostedAt = GETUTCDATE(), UpdatedBy = @UserId
    WHERE Id = @HeaderId;

    COMMIT TRANSACTION;
END
GO

-- =============================================================================
-- sp_CycleCountPost — Döngüsel sayım belgesini onaylar
-- NE YAPAR: Sayılan ile sistemdeki miktar arasında fark olan satırlar için
--           COUNT_ADJ hareketi yazar (fark = QtyCounted - QtySystem); sayım
--           belgesini COMPLETED durumuna geçirir.
-- PARAMETRELERİ:
--   @HeaderId  UNIQUEIDENTIFIER — onaylanacak sayım başlık ID'si
--   @CompanyId UNIQUEIDENTIFIER — şirket kimliği
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: StockMovement (INSERT — yalnızca fark olan satırlar),
--               CycleCount.Status (UPDATE)
-- THROW: 50001 — belge bulunamadı; 51000 — geçersiz durum geçişi
-- BAĞIMLILIK: sp_ValidateStatusTransition, sp_GuardPeriodOpen
-- =============================================================================
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
         QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy, BranchId)
    SELECT
        @CompanyId, @WarehouseId, l.BinId, l.ItemId, 'COUNT_ADJ',
        l.QtyCounted - l.QtySystem,
        i.BaseUomId,
        ABS(l.QtyCounted - l.QtySystem),
        'COUNT', @HeaderId, @DocNo, @UserId,
        ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @WarehouseId), dbo.fn_DefaultBranchId(@CompanyId))
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

-- =============================================================================
-- sp_ProductionLoadBOM — Üretim emrine BOM satırlarını yükler
-- NE YAPAR: Ürünün aktif reçetesinden (ItemBOM) hammadde kalemlerini alır,
--           QtyRequired × QtyTarget çarpımıyla ProductionOrderLine satırlarını
--           yeniden oluşturur (önce mevcut satırları siler — idempotent).
-- PARAMETRELERİ:
--   @OrderId   UNIQUEIDENTIFIER — BOM yüklenecek üretim emri ID'si
--   @CompanyId UNIQUEIDENTIFIER — şirket kimliği
-- SIDE EFFECTS: ProductionOrderLine (DELETE + INSERT)
-- THROW: 50001 — üretim emri bulunamadı
-- BAĞIMLILIK: —
-- =============================================================================
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

-- =============================================================================
-- sp_ProductionCreatePickTask — Üretim için hammadde pick task oluşturur
-- NE YAPAR: ProductionOrderLine satırlarındaki hammaddeler için PickTask başlığı
--           ve PickTaskLine kayıtları açar; TaskId döner.
-- PARAMETRELERİ:
--   @OrderId   UNIQUEIDENTIFIER — kaynak üretim emri ID'si
--   @CompanyId UNIQUEIDENTIFIER — şirket kimliği
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: PickTask (INSERT), PickTaskLine (INSERT)
-- THROW: 50001 — üretim emri bulunamadı
-- BAĞIMLILIK: —
-- =============================================================================
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

-- =============================================================================
-- sp_ProductionFinish — Üretim emrini tamamlar ve mamulü stoğa girer
-- NE YAPAR: Üretilen mamul için PRODUCTION tipi StockMovement yazar,
--           ProductionOrder.QtyProduced ve Status=COMPLETED günceller.
-- PARAMETRELERİ:
--   @OrderId   UNIQUEIDENTIFIER — tamamlanacak üretim emri ID'si
--   @CompanyId UNIQUEIDENTIFIER — şirket kimliği
--   @Qty       DECIMAL(18,4)    — üretilen mamul miktarı
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: StockMovement (INSERT — PRODUCTION), ProductionOrder (UPDATE)
-- THROW: 50001 — üretim emri bulunamadı
-- BAĞIMLILIK: sp_GuardPeriodOpen
-- =============================================================================
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
         QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy, BranchId)
    SELECT
        @CompanyId, @WarehouseId, @BinId, @ItemId, 'PRODUCTION',
        @Qty, i.BaseUomId, @Qty, 'PRODUCTION', @OrderId, @DocNo, @UserId,
        ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @WarehouseId), dbo.fn_DefaultBranchId(@CompanyId))
    FROM Item i WHERE i.Id = @ItemId;

    UPDATE ProductionOrder
    SET Status = 'COMPLETED', QtyProduced = @Qty, CompletedAt = GETUTCDATE(), UpdatedBy = @UserId
    WHERE Id = @OrderId;

    COMMIT TRANSACTION;
END
GO

-- =============================================================================
-- sp_PickLinePost — Tek bir picking satırını tamamlar
-- NE YAPAR: PickTaskLine.QtyPicked'ı günceller, ISSUE StockMovement yazar;
--           görev PRD-PCK- ise ProductionOrderLine.QtyIssued artırır;
--           görevin tüm satırları bittiyse PickTask.Status=COMPLETED yapar.
-- PARAMETRELERİ:
--   @LineId    UNIQUEIDENTIFIER — tamamlanan pick satır ID'si
--   @TaskId    UNIQUEIDENTIFIER — bağlı pick task ID'si
--   @Qty       DECIMAL(18,4)    — toplanan miktar
--   @CompanyId UNIQUEIDENTIFIER — şirket kimliği
--   @UserId    NVARCHAR(450)    — işlemi yapan kullanıcı
-- SIDE EFFECTS: PickTaskLine (UPDATE), StockMovement (INSERT — ISSUE),
--               ProductionOrderLine.QtyIssued (UPDATE — PRD-PCK- görevlerinde),
--               PickTask.Status (UPDATE)
-- THROW: —
-- BAĞIMLILIK: —
-- =============================================================================
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
         QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy, BranchId)
    VALUES
        (@CompanyId, @WarehouseId, @BinId, @ItemId, 'ISSUE',
         -@Qty, @UomId, @Qty, 'PICKING', @TaskId, @DocNo, @UserId,
         ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @WarehouseId), dbo.fn_DefaultBranchId(@CompanyId)));

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
