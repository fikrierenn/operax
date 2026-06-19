-- Plan 28 Faz A — Sipariş kontrollü mal kabul: tedarikçi irsaliye + mod + iade alanı miktarı.
-- Idempotent. Mevzuat (mali-evrak): tedarikçi sevk irsaliyesi (VUK md.230) + 3-way match.

-- ReceivingHeader: kabul modu (SINGLE_PO / BULK_SUPPLIER / FREE)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='ReceivingMode' AND Object_ID=OBJECT_ID('ReceivingHeader'))
BEGIN
    ALTER TABLE ReceivingHeader ADD ReceivingMode NVARCHAR(20) NOT NULL DEFAULT 'SINGLE_PO';
    PRINT 'ReceivingHeader.ReceivingMode eklendi.';
END
GO

-- Tedarikçi sevk irsaliyesi no (VUK md.230) — toplu kabulde birden çok olabilir; başlık birincil referans
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='SupplierWaybillNo' AND Object_ID=OBJECT_ID('ReceivingHeader'))
BEGIN
    ALTER TABLE ReceivingHeader ADD SupplierWaybillNo NVARCHAR(50) NULL;
    PRINT 'ReceivingHeader.SupplierWaybillNo eklendi.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='SupplierWaybillDate' AND Object_ID=OBJECT_ID('ReceivingHeader'))
BEGIN
    ALTER TABLE ReceivingHeader ADD SupplierWaybillDate DATE NULL;
    PRINT 'ReceivingHeader.SupplierWaybillDate eklendi.';
END
GO

-- ReceivingLine: iade/karantina alanına ayrılan fazla miktar (sipariş aşımı)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='ReturnQty' AND Object_ID=OBJECT_ID('ReceivingLine'))
BEGIN
    ALTER TABLE ReceivingLine ADD ReturnQty DECIMAL(18,6) NOT NULL DEFAULT 0;
    PRINT 'ReceivingLine.ReturnQty eklendi (iade alanına ayrılan fazla).';
END
GO

-- İade/karantina alanı bin flag'i (sipariş fazlası mal buraya iner)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='IsReturnArea' AND Object_ID=OBJECT_ID('Bin'))
BEGIN
    ALTER TABLE Bin ADD IsReturnArea BIT NOT NULL DEFAULT 0;
    PRINT 'Bin.IsReturnArea eklendi.';
END
GO

-- İade alanı bin'i olmayan her depoya 'IADE' bin'i (idempotent)
INSERT INTO Bin (Id, WarehouseId, Code, Zone, SortNo, IsPickingArea, IsReceivingArea, IsReturnArea, IsActive, IsDeleted)
SELECT NEWID(), w.Id, N'IADE', N'IADE', 99, 0, 0, 1, 1, 0
FROM Warehouse w
WHERE w.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Bin b WHERE b.WarehouseId = w.Id AND b.IsReturnArea = 1 AND b.IsDeleted = 0);
GO
