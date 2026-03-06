DECLARE @CompId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
DECLARE @WhId UNIQUEIDENTIFIER = NEWID();
DECLARE @BinId UNIQUEIDENTIFIER = NEWID();
DECLARE @PartId UNIQUEIDENTIFIER = NEWID();
DECLARE @UomAdet UNIQUEIDENTIFIER = 'c29e2fc2-e8d7-486a-90ce-b45e6039ee42';
DECLARE @UomKg UNIQUEIDENTIFIER = 'a935a202-e6b8-46b8-b4c0-6ee8aa9d009f';

-- 1. TEMİZLİK (DOĞRU SIRA - Foreign Key Uyumlu)
DELETE FROM StockMovement WHERE CompanyId = @CompId;
DELETE FROM ProductionOrder WHERE CompanyId = @CompId;
DELETE FROM PurchaseOrderHeader WHERE CompanyId = @CompId;
DELETE FROM ShippingHeader WHERE CompanyId = @CompId;
DELETE FROM Bin WHERE WarehouseId IN (SELECT Id FROM Warehouse WHERE CompanyId = @CompId);
DELETE FROM Warehouse WHERE CompanyId = @CompId;
DELETE FROM Partner WHERE CompanyId = @CompId;

-- 2. TEMEL VERILER (Partner, Warehouse, Bin)
INSERT INTO Partner (Id, CompanyId, Code, Name, Type, IsActive, IsDeleted)
VALUES (@PartId, @CompId, 'P-001', 'ACME Global Cari', 'BOTH', 1, 0);

INSERT INTO Warehouse (Id, CompanyId, Code, Name, IsActive, IsDeleted)
VALUES (@WhId, @CompId, 'W01', 'Ana Depo', 1, 0);

INSERT INTO Bin (Id, Code, WarehouseId, IsActive, IsDeleted)
VALUES (@BinId, 'A-01-01', @WhId, 1, 0);

-- 3. ÜRÜN ID'LERİNİ AL
DECLARE @PRD1 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Item WHERE Code = 'PRD-001');
DECLARE @PRD2 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Item WHERE Code = 'PRD-002');
DECLARE @PRD3 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Item WHERE Code = 'PRD-003');

-- 4. SAHTE SEVKIYATLAR
INSERT INTO ShippingHeader (Id, CompanyId, DocNo, Status, CreatedAt, IsDeleted, WarehouseId)
VALUES 
(NEWID(), @CompId, 'SHP-001', 'PENDING', GETDATE(), 0, @WhId),
(NEWID(), @CompId, 'SHP-002', 'NEW', DATEADD(HOUR, -2, GETDATE()), 0, @WhId);

-- 5. SAHTE SATINALMALAR
INSERT INTO PurchaseOrderHeader (Id, CompanyId, OrderNo, Status, CreatedAt, IsDeleted, WarehouseId, PartnerId)
VALUES 
(NEWID(), @CompId, 'PO-101', 'PENDING', GETDATE(), 0, @WhId, @PartId),
(NEWID(), @CompId, 'PO-102', 'APPROVED', DATEADD(HOUR, -5, GETDATE()), 0, @WhId, @PartId);

-- 6. SAHTE URETIM EMIRLERI
INSERT INTO ProductionOrder (Id, CompanyId, DocNo, ItemId, Status, QtyTarget, DueDate, CreatedAt)
VALUES 
(NEWID(), @CompId, 'WO-001', @PRD1, 'IN_PROGRESS', 100, GETDATE(), GETDATE()),
(NEWID(), @CompId, 'WO-002', @PRD2, 'NEW', 50, GETDATE(), GETDATE());

-- 7. STOK HAREKETLERI
INSERT INTO StockMovement (Id, CompanyId, ItemId, MovementType, QtyBase, QtyOriginal, CreatedAt, IsCancelled, WarehouseId, BinId, UomId)
VALUES 
(NEWID(), @CompId, @PRD1, 'RECEIPT', 500, 500, DATEADD(MINUTE, -40, GETDATE()), 0, @WhId, @BinId, @UomAdet),
(NEWID(), @CompId, @PRD2, 'RECEIPT', 250, 250, DATEADD(MINUTE, -30, GETDATE()), 0, @WhId, @BinId, @UomAdet),
(NEWID(), @CompId, @PRD3, 'RECEIPT', 100, 100, DATEADD(MINUTE, -20, GETDATE()), 0, @WhId, @BinId, @UomKg),
(NEWID(), @CompId, @PRD1, 'ISSUE', 10, 10, DATEADD(MINUTE, -10, GETDATE()), 0, @WhId, @BinId, @UomAdet),
(NEWID(), @CompId, @PRD2, 'ISSUE', 5, 5, DATEADD(MINUTE, -5, GETDATE()), 0, @WhId, @BinId, @UomAdet);

PRINT 'Final Canlı Veri Simülasyonu (FK & PartnerType Fix) Başarıyla Tamamlandı.';
