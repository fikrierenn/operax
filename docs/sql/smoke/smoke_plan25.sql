-- Smoke Plan 25: sarf fişi → Post → stok düşer → Reverse → stok geri
DECLARE @C  UNIQUEIDENTIFIER = 'd1e1b1a5-0000-0000-0000-000000000001';
DECLARE @W  UNIQUEIDENTIFIER = 'bef08e7e-8869-4980-a084-5fb8d21a0f6c';
DECLARE @It UNIQUEIDENTIFIER = 'f780133c-f8f5-4b12-a3d4-749500e50125';
DECLARE @U  UNIQUEIDENTIFIER = '992FACC3-4A6E-4526-A8FD-3110CA52B47A';
DECLARE @Uom UNIQUEIDENTIFIER = 'a935a202-e6b8-46b8-b4c0-6ee8aa9d009f';

-- Başlangıç bakiye
SELECT 'BASLANGIC' AS Adim, QtyBalance FROM tvf_InventoryBalance(@C) WHERE WarehouseId=@W AND ItemId=@It;

-- Sarf fişi oluştur (100 adet sarf)
DECLARE @H UNIQUEIDENTIFIER = NEWID();
INSERT INTO MaterialIssueHeader (Id, CompanyId, WarehouseId, DocNo, Status, CreatedBy)
VALUES (@H, @C, @W, 'SARF-SMOKE-001', 'DRAFT', @U);
INSERT INTO MaterialIssueLine (HeaderId, ItemId, UomId, Qty, QtyBase)
VALUES (@H, @It, @Uom, 100, 100);

-- Onayla → stok düşmeli
EXEC sp_MaterialIssuePost @HeaderId=@H, @CompanyId=@C, @UserId=@U;
SELECT 'POST SONRASI' AS Adim, QtyBalance FROM tvf_InventoryBalance(@C) WHERE WarehouseId=@W AND ItemId=@It;

-- İptal → stok geri gelmeli (flag-only)
EXEC sp_MaterialIssueReverse @HeaderId=@H, @CompanyId=@C, @UserId=@U;
SELECT 'REVERSE SONRASI' AS Adim, QtyBalance FROM tvf_InventoryBalance(@C) WHERE WarehouseId=@W AND ItemId=@It;

-- AccountMovement YAZILMAMIŞ olmalı (iç tüketim)
SELECT 'AM kayit (0 olmali)' AS Adim, COUNT(*) AS c FROM AccountMovement WHERE SourceDocId=@H;

-- Temizlik
DELETE FROM StockMovement WHERE SourceDocId=@H;
DELETE FROM MaterialIssueLine WHERE HeaderId=@H;
DELETE FROM MaterialIssueHeader WHERE Id=@H;
SELECT 'TEMIZLENDI' AS Adim, QtyBalance FROM tvf_InventoryBalance(@C) WHERE WarehouseId=@W AND ItemId=@It;
