-- =============================================================================
-- OPERAX DEMO SEED DATA
-- Şirket: Operax Demo LTD (d1e1b1a5-0000-0000-0000-000000000001)
-- Tüm sorgular idempotent (IF NOT EXISTS / IF EXISTS koruması var)
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CompanyId  UNIQUEIDENTIFIER = 'd1e1b1a5-0000-0000-0000-000000000001';
DECLARE @WH001      UNIQUEIDENTIFIER = 'BEF08E7E-8869-4980-A084-5FB8D21A0F6C'; -- WH-001
DECLARE @WH002      UNIQUEIDENTIFIER = '93BF43EB-C1E6-4D4A-BCDD-77CBE259FF2A'; -- WH-002

-- Items
DECLARE @HAM001     UNIQUEIDENTIFIER = '2D089444-516A-482B-967C-D7547F62AE02';
DECLARE @HAM002     UNIQUEIDENTIFIER = 'B21905F7-3652-42F2-ACAD-ED3AE51CE609';
DECLARE @PRD001     UNIQUEIDENTIFIER = 'F780133C-F8F5-4B12-A3D4-749500E50125';
DECLARE @PRD002     UNIQUEIDENTIFIER = '9D732E3B-FEB0-49D1-9686-CC8B1C7496F9';
DECLARE @PRD003     UNIQUEIDENTIFIER = 'AD7E22C3-77A6-41B1-9082-8228364976E7';

-- Partners
DECLARE @SUP001     UNIQUEIDENTIFIER = '68EC1270-3DEF-4D6C-A1CF-ADDF492EBC27'; -- SUP-001
DECLARE @SUP002     UNIQUEIDENTIFIER = 'F3FEA396-222A-4679-AE9C-9AC21FD62A1A'; -- SUP-002
DECLARE @CUS001     UNIQUEIDENTIFIER = '3FC8974C-E2B0-443D-AB56-9775ACBC9E29'; -- CUS-001
DECLARE @CUS002     UNIQUEIDENTIFIER = '3D2EC5A8-4EB1-4C7A-BDDC-17AFD4DD49D6'; -- CUS-002

-- UOMs
DECLARE @UOM_ADET   UNIQUEIDENTIFIER = 'C29E2FC2-E8D7-486A-90CE-B45E6039EE42'; -- ADET
DECLARE @UOM_KG     UNIQUEIDENTIFIER = 'A935A202-E6B8-46B8-B4C0-6EE8AA9D009F'; -- KG
DECLARE @UOM_MT     UNIQUEIDENTIFIER = '86C09707-AC97-4B4C-AD57-36F11BE9A77A'; -- MT

-- Bins WH-001
DECLARE @GRS01      UNIQUEIDENTIFIER = '32E9FBDE-68DE-4299-AD3D-5C2E7C27C404'; -- GRS-01
DECLARE @CIK01      UNIQUEIDENTIFIER = 'B14C27C1-BD48-4E07-8498-E2C452D084D7'; -- CIK-01
DECLARE @RA01       UNIQUEIDENTIFIER = 'EF5E5804-8FAA-4A64-B277-6A624C1EE83F'; -- R-A-01
DECLARE @RA02       UNIQUEIDENTIFIER = '81F5B334-A865-4929-9C35-E233806BCD8D'; -- R-A-02
DECLARE @RA03       UNIQUEIDENTIFIER = 'EA8A2D07-D233-489E-953D-06C04B4C1AA5'; -- R-A-03
DECLARE @RB01       UNIQUEIDENTIFIER = '7B6D19E9-A38C-4991-99C8-E8A92F0FA18A'; -- R-B-01

-- Bins WH-002
DECLARE @UREGRS     UNIQUEIDENTIFIER = '92853AB4-2DA6-491D-BBB5-D72913CD795B'; -- URE-GRS
DECLARE @UREA01     UNIQUEIDENTIFIER = 'BEF3B305-EFEF-41EA-BBA0-5010C326BFB7'; -- URE-A-01
DECLARE @UREA02     UNIQUEIDENTIFIER = 'C8FB1DC4-BA93-454A-B807-2E9B62899CBF'; -- URE-A-02

PRINT '=== DEMO SEED BASLADI ===';

-- =============================================================================
-- 1. PURCHASE ORDERS
-- =============================================================================

DECLARE @PO001 UNIQUEIDENTIFIER = 'A1000001-0000-0000-0000-000000000001';
DECLARE @PO002 UNIQUEIDENTIFIER = 'A1000001-0000-0000-0000-000000000002';

IF NOT EXISTS (SELECT 1 FROM PurchaseOrderHeader WHERE Id = @PO001)
BEGIN
    INSERT INTO PurchaseOrderHeader (Id, CompanyId, WarehouseId, PartnerId, OrderNo, OrderDate, Status, Notes, CreatedAt, IsDeleted)
    VALUES (@PO001, @CompanyId, @WH001, @SUP001, 'PO-2026-001', '2026-03-01', 'APPROVED',
            'Ilk siparis - hammadde tedariki', GETUTCDATE(), 0);
    PRINT 'PO-2026-001 olusturuldu.';
END

DECLARE @PO001L1 UNIQUEIDENTIFIER = 'B1000001-0000-0000-0000-000000000001';
DECLARE @PO001L2 UNIQUEIDENTIFIER = 'B1000001-0000-0000-0000-000000000002';
DECLARE @PO001L3 UNIQUEIDENTIFIER = 'B1000001-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM PurchaseOrderLine WHERE Id = @PO001L1)
    INSERT INTO PurchaseOrderLine (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReceived, Price, Currency, CreatedAt)
    VALUES (@PO001L1, @PO001, @HAM001, @UOM_ADET, 500, 0, 12.50, 'TRY', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM PurchaseOrderLine WHERE Id = @PO001L2)
    INSERT INTO PurchaseOrderLine (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReceived, Price, Currency, CreatedAt)
    VALUES (@PO001L2, @PO001, @HAM002, @UOM_KG, 200, 0, 45.00, 'TRY', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM PurchaseOrderLine WHERE Id = @PO001L3)
    INSERT INTO PurchaseOrderLine (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReceived, Price, Currency, CreatedAt)
    VALUES (@PO001L3, @PO001, @PRD001, @UOM_ADET, 100, 0, 250.00, 'TRY', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM PurchaseOrderHeader WHERE Id = @PO002)
BEGIN
    INSERT INTO PurchaseOrderHeader (Id, CompanyId, WarehouseId, PartnerId, OrderNo, OrderDate, Status, Notes, CreatedAt, IsDeleted)
    VALUES (@PO002, @CompanyId, @WH001, @SUP002, 'PO-2026-002', '2026-03-05', 'DRAFT',
            'Ikinci siparis - taslak', GETUTCDATE(), 0);
    PRINT 'PO-2026-002 olusturuldu.';
END

DECLARE @PO002L1 UNIQUEIDENTIFIER = 'B1000002-0000-0000-0000-000000000001';
DECLARE @PO002L2 UNIQUEIDENTIFIER = 'B1000002-0000-0000-0000-000000000002';

IF NOT EXISTS (SELECT 1 FROM PurchaseOrderLine WHERE Id = @PO002L1)
    INSERT INTO PurchaseOrderLine (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReceived, Price, Currency, CreatedAt)
    VALUES (@PO002L1, @PO002, @PRD002, @UOM_ADET, 300, 0, 180.00, 'TRY', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM PurchaseOrderLine WHERE Id = @PO002L2)
    INSERT INTO PurchaseOrderLine (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReceived, Price, Currency, CreatedAt)
    VALUES (@PO002L2, @PO002, @PRD003, @UOM_ADET, 150, 0, 320.00, 'TRY', GETUTCDATE());

PRINT 'PO satirlari olusturuldu.';

-- =============================================================================
-- 2. RECEIVING (PO-2026-001 icin irsaliye)
-- =============================================================================

DECLARE @RCV001 UNIQUEIDENTIFIER = 'C1000001-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM ReceivingHeader WHERE Id = @RCV001)
BEGIN
    INSERT INTO ReceivingHeader (Id, CompanyId, WarehouseId, PartnerId, PurchaseOrderId, DocNo, DocDate, Status, Notes, CreatedAt, IsDeleted)
    VALUES (@RCV001, @CompanyId, @WH001, @SUP001, @PO001, 'RCV-2026-001', '2026-03-03', 'POSTED',
            'PO-2026-001 kismi teslim', GETUTCDATE(), 0);
    PRINT 'RCV-2026-001 olusturuldu.';
END

DECLARE @RCV001L1 UNIQUEIDENTIFIER = 'D1000001-0000-0000-0000-000000000001';
DECLARE @RCV001L2 UNIQUEIDENTIFIER = 'D1000001-0000-0000-0000-000000000002';

IF NOT EXISTS (SELECT 1 FROM ReceivingLine WHERE Id = @RCV001L1)
    INSERT INTO ReceivingLine (Id, HeaderId, PurchaseOrderLineId, ItemId, UomId, QtyOriginal, QtyBase, BinId, CreatedAt)
    VALUES (@RCV001L1, @RCV001, @PO001L1, @HAM001, @UOM_ADET, 300, 300, @GRS01, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM ReceivingLine WHERE Id = @RCV001L2)
    INSERT INTO ReceivingLine (Id, HeaderId, PurchaseOrderLineId, ItemId, UomId, QtyOriginal, QtyBase, BinId, CreatedAt)
    VALUES (@RCV001L2, @RCV001, @PO001L2, @HAM002, @UOM_KG, 120, 120, @GRS01, GETUTCDATE());

UPDATE PurchaseOrderLine SET QtyReceived = 300 WHERE Id = @PO001L1;
UPDATE PurchaseOrderLine SET QtyReceived = 120 WHERE Id = @PO001L2;

-- StockMovement RECEIPT
IF NOT EXISTS (SELECT 1 FROM StockMovement WHERE SourceDocId = @RCV001 AND ItemId = @HAM001)
    INSERT INTO StockMovement (Id, CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedAt)
    VALUES (NEWID(), @CompanyId, @WH001, @GRS01, @HAM001, 'RECEIPT', 300, @UOM_ADET, 300, 'RECEIVING', @RCV001, 'RCV-2026-001', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM StockMovement WHERE SourceDocId = @RCV001 AND ItemId = @HAM002)
    INSERT INTO StockMovement (Id, CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedAt)
    VALUES (NEWID(), @CompanyId, @WH001, @GRS01, @HAM002, 'RECEIPT', 120, @UOM_KG, 120, 'RECEIVING', @RCV001, 'RCV-2026-001', GETUTCDATE());

PRINT 'RCV-2026-001 stok hareketleri olusturuldu.';

-- =============================================================================
-- 3. STOCK TRANSFER (GRS-01 raflara)
-- =============================================================================

DECLARE @TRF001 UNIQUEIDENTIFIER = 'E1000001-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM StockTransfer WHERE Id = @TRF001)
BEGIN
    INSERT INTO StockTransfer (Id, CompanyId, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes, CreatedAt, PostedAt)
    VALUES (@TRF001, @CompanyId, 'TRF-2026-001', 'POSTED', 'BIN_TO_BIN', @WH001, @WH001,
            'Giris bolgesinden raflara yerlestirme', GETUTCDATE(), GETUTCDATE());
    PRINT 'TRF-2026-001 olusturuldu.';
END

DECLARE @TRF001L1 UNIQUEIDENTIFIER = 'F1000001-0000-0000-0000-000000000001';
DECLARE @TRF001L2 UNIQUEIDENTIFIER = 'F1000001-0000-0000-0000-000000000002';
DECLARE @TRF001L3 UNIQUEIDENTIFIER = 'F1000001-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM StockTransferLine WHERE Id = @TRF001L1)
    INSERT INTO StockTransferLine (Id, TransferId, ItemId, UomId, FromBinId, ToBinId, Qty, QtyBase)
    VALUES (@TRF001L1, @TRF001, @HAM001, @UOM_ADET, @GRS01, @RA01, 150, 150);

IF NOT EXISTS (SELECT 1 FROM StockTransferLine WHERE Id = @TRF001L2)
    INSERT INTO StockTransferLine (Id, TransferId, ItemId, UomId, FromBinId, ToBinId, Qty, QtyBase)
    VALUES (@TRF001L2, @TRF001, @HAM001, @UOM_ADET, @GRS01, @RA02, 150, 150);

IF NOT EXISTS (SELECT 1 FROM StockTransferLine WHERE Id = @TRF001L3)
    INSERT INTO StockTransferLine (Id, TransferId, ItemId, UomId, FromBinId, ToBinId, Qty, QtyBase)
    VALUES (@TRF001L3, @TRF001, @HAM002, @UOM_KG, @GRS01, @RB01, 120, 120);

-- Transfer StockMovements
IF NOT EXISTS (SELECT 1 FROM StockMovement WHERE SourceDocId = @TRF001 AND BinId = @GRS01 AND ItemId = @HAM001)
BEGIN
    INSERT INTO StockMovement (Id, CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedAt)
    VALUES (NEWID(), @CompanyId, @WH001, @GRS01, @HAM001, 'TRANSFER', -300, @UOM_ADET, -300, 'TRANSFER', @TRF001, 'TRF-2026-001', GETUTCDATE());
    INSERT INTO StockMovement (Id, CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedAt)
    VALUES (NEWID(), @CompanyId, @WH001, @RA01, @HAM001, 'TRANSFER', 150, @UOM_ADET, 150, 'TRANSFER', @TRF001, 'TRF-2026-001', GETUTCDATE());
    INSERT INTO StockMovement (Id, CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedAt)
    VALUES (NEWID(), @CompanyId, @WH001, @RA02, @HAM001, 'TRANSFER', 150, @UOM_ADET, 150, 'TRANSFER', @TRF001, 'TRF-2026-001', GETUTCDATE());
END

IF NOT EXISTS (SELECT 1 FROM StockMovement WHERE SourceDocId = @TRF001 AND BinId = @GRS01 AND ItemId = @HAM002)
BEGIN
    INSERT INTO StockMovement (Id, CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedAt)
    VALUES (NEWID(), @CompanyId, @WH001, @GRS01, @HAM002, 'TRANSFER', -120, @UOM_KG, -120, 'TRANSFER', @TRF001, 'TRF-2026-001', GETUTCDATE());
    INSERT INTO StockMovement (Id, CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedAt)
    VALUES (NEWID(), @CompanyId, @WH001, @RB01, @HAM002, 'TRANSFER', 120, @UOM_KG, 120, 'TRANSFER', @TRF001, 'TRF-2026-001', GETUTCDATE());
END

PRINT 'TRF-2026-001 stok hareketleri olusturuldu.';

-- =============================================================================
-- 4. SALES ORDERS
-- =============================================================================

DECLARE @SO001 UNIQUEIDENTIFIER = 'AA000001-0000-0000-0000-000000000001';
DECLARE @SO002 UNIQUEIDENTIFIER = 'AA000001-0000-0000-0000-000000000002';

IF NOT EXISTS (SELECT 1 FROM SalesOrderHeader WHERE Id = @SO001)
BEGIN
    INSERT INTO SalesOrderHeader (Id, CompanyId, WarehouseId, PartnerId, OrderNo, OrderDate, RequestedDeliveryDate, Status, Notes, CreatedAt, IsDeleted)
    VALUES (@SO001, @CompanyId, @WH001, @CUS001, 'SO-2026-001', '2026-03-04', '2026-03-15', 'APPROVED',
            'Ilk satis siparisi', GETUTCDATE(), 0);
    PRINT 'SO-2026-001 olusturuldu.';
END

DECLARE @SO001L1 UNIQUEIDENTIFIER = 'AB000001-0000-0000-0000-000000000001';
DECLARE @SO001L2 UNIQUEIDENTIFIER = 'AB000001-0000-0000-0000-000000000002';

IF NOT EXISTS (SELECT 1 FROM SalesOrderLine WHERE Id = @SO001L1)
    INSERT INTO SalesOrderLine (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReserved, QtyShipped, Price, Currency, CreatedAt)
    VALUES (@SO001L1, @SO001, @HAM001, @UOM_ADET, 50, 50, 0, 25.00, 'TRY', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM SalesOrderLine WHERE Id = @SO001L2)
    INSERT INTO SalesOrderLine (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReserved, QtyShipped, Price, Currency, CreatedAt)
    VALUES (@SO001L2, @SO001, @HAM002, @UOM_KG, 30, 30, 0, 90.00, 'TRY', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM SalesOrderHeader WHERE Id = @SO002)
BEGIN
    INSERT INTO SalesOrderHeader (Id, CompanyId, WarehouseId, PartnerId, OrderNo, OrderDate, RequestedDeliveryDate, Status, Notes, CreatedAt, IsDeleted)
    VALUES (@SO002, @CompanyId, @WH001, @CUS002, 'SO-2026-002', '2026-03-06', '2026-03-20', 'DRAFT',
            'Ikinci siparis - taslak', GETUTCDATE(), 0);
    PRINT 'SO-2026-002 olusturuldu.';
END

DECLARE @SO002L1 UNIQUEIDENTIFIER = 'AB000002-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM SalesOrderLine WHERE Id = @SO002L1)
    INSERT INTO SalesOrderLine (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReserved, QtyShipped, Price, Currency, CreatedAt)
    VALUES (@SO002L1, @SO002, @PRD001, @UOM_ADET, 20, 0, 0, 450.00, 'TRY', GETUTCDATE());

PRINT 'Satis siparisleri olusturuldu.';

-- =============================================================================
-- 5. SHIPPING (SO-2026-001 icin)
-- =============================================================================

DECLARE @SHP001 UNIQUEIDENTIFIER = 'AC000001-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM ShippingHeader WHERE Id = @SHP001)
BEGIN
    INSERT INTO ShippingHeader (Id, CompanyId, WarehouseId, DocNo, DocDate, Status, CarrierName, VehiclePlate, Notes, CreatedAt, IsDeleted)
    VALUES (@SHP001, @CompanyId, @WH001, 'SHP-2026-001', '2026-03-07', 'DRAFT',
            'Aras Kargo', '34 ABC 123', 'SO-2026-001 sevkiyati', GETUTCDATE(), 0);
    PRINT 'SHP-2026-001 olusturuldu.';
END

IF NOT EXISTS (SELECT 1 FROM ShippingLine WHERE HeaderId = @SHP001 AND ItemId = @HAM001)
    INSERT INTO ShippingLine (Id, HeaderId, SalesOrderLineId, ItemId, UomId, QtyOriginal, QtyBase, BinId, CreatedAt)
    VALUES (NEWID(), @SHP001, @SO001L1, @HAM001, @UOM_ADET, 50, 50, @CIK01, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM ShippingLine WHERE HeaderId = @SHP001 AND ItemId = @HAM002)
    INSERT INTO ShippingLine (Id, HeaderId, SalesOrderLineId, ItemId, UomId, QtyOriginal, QtyBase, BinId, CreatedAt)
    VALUES (NEWID(), @SHP001, @SO001L2, @HAM002, @UOM_KG, 30, 30, @CIK01, GETUTCDATE());

PRINT 'SHP-2026-001 olusturuldu.';

-- =============================================================================
-- 6. CYCLE COUNT
-- =============================================================================

DECLARE @CC001 UNIQUEIDENTIFIER = 'AD000001-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM CycleCount WHERE Id = @CC001)
BEGIN
    INSERT INTO CycleCount (Id, CompanyId, DocNo, Status, WarehouseId, Description, PlannedDate, CreatedAt, IsDeleted)
    VALUES (@CC001, @CompanyId, 'SAY-2026-001', 'DRAFT', @WH001,
            'Mart 2026 aylik sayim', '2026-03-10', GETUTCDATE(), 0);
    PRINT 'SAY-2026-001 olusturuldu.';
END

IF NOT EXISTS (SELECT 1 FROM CycleCountLine WHERE CycleCountId = @CC001 AND BinId = @RA01 AND ItemId = @HAM001)
    INSERT INTO CycleCountLine (Id, CycleCountId, BinId, ItemId, QtySystem, QtyCounted, IsDeleted)
    VALUES (NEWID(), @CC001, @RA01, @HAM001, 150, 0, 0);

IF NOT EXISTS (SELECT 1 FROM CycleCountLine WHERE CycleCountId = @CC001 AND BinId = @RA02 AND ItemId = @HAM001)
    INSERT INTO CycleCountLine (Id, CycleCountId, BinId, ItemId, QtySystem, QtyCounted, IsDeleted)
    VALUES (NEWID(), @CC001, @RA02, @HAM001, 150, 0, 0);

IF NOT EXISTS (SELECT 1 FROM CycleCountLine WHERE CycleCountId = @CC001 AND BinId = @RB01 AND ItemId = @HAM002)
    INSERT INTO CycleCountLine (Id, CycleCountId, BinId, ItemId, QtySystem, QtyCounted, IsDeleted)
    VALUES (NEWID(), @CC001, @RB01, @HAM002, 120, 0, 0);

PRINT 'SAY-2026-001 sayim satirlari olusturuldu.';

-- =============================================================================
-- 7. WORK CENTERS
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM WorkCenter WHERE CompanyId = @CompanyId AND Code = 'CNC-01')
BEGIN
    INSERT INTO WorkCenter (Id, CompanyId, Code, Name, LaborRatePerSec, MachineRatePerSec, ElectricityRatePerSec, IsActive)
    VALUES (NEWID(), @CompanyId, 'CNC-01', 'CNC Isleme Merkezi', 0.008333, 0.016667, 0.002778, 1);
    PRINT 'WorkCenter CNC-01 olusturuldu.';
END

IF NOT EXISTS (SELECT 1 FROM WorkCenter WHERE CompanyId = @CompanyId AND Code = 'ASM-01')
BEGIN
    INSERT INTO WorkCenter (Id, CompanyId, Code, Name, LaborRatePerSec, MachineRatePerSec, ElectricityRatePerSec, IsActive)
    VALUES (NEWID(), @CompanyId, 'ASM-01', 'Montaj Hatti 1', 0.005556, 0.002778, 0.001389, 1);
    PRINT 'WorkCenter ASM-01 olusturuldu.';
END

IF NOT EXISTS (SELECT 1 FROM WorkCenter WHERE CompanyId = @CompanyId AND Code = 'QC-01')
BEGIN
    INSERT INTO WorkCenter (Id, CompanyId, Code, Name, LaborRatePerSec, MachineRatePerSec, ElectricityRatePerSec, IsActive)
    VALUES (NEWID(), @CompanyId, 'QC-01', 'Kalite Kontrol', 0.005556, 0.000000, 0.000000, 1);
    PRINT 'WorkCenter QC-01 olusturuldu.';
END

PRINT '=== DEMO SEED TAMAMLANDI ===';
