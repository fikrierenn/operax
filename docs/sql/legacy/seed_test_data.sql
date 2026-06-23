-- Operax Demo LTD test verisi
-- CompanyId: d1e1b1a5-0000-0000-0000-000000000001

DECLARE @CompId UNIQUEIDENTIFIER = 'd1e1b1a5-0000-0000-0000-000000000001';
DECLARE @UomAdet UNIQUEIDENTIFIER = 'c29e2fc2-e8d7-486a-90ce-b45e6039ee42';
DECLARE @UomKg   UNIQUEIDENTIFIER = 'a935a202-e6b8-46b8-b4c0-6ee8aa9d009f';
DECLARE @UomMt   UNIQUEIDENTIFIER = '86c09707-ac97-4b4c-ad57-36f11be9a77a';
DECLARE @CatElek UNIQUEIDENTIFIER = '7d83a9eb-955c-4613-8512-c4a9cec75f7d';
DECLARE @CatHam  UNIQUEIDENTIFIER = '04ed30cb-733e-4c89-b817-80443a70c2cd';

-- Yinelenen PRD-001 kayıtlarını temizle (en son kalanı sakla)
DELETE FROM Item
WHERE CompanyId = @CompId
  AND Code = 'PRD-001'
  AND Id NOT IN (
      SELECT TOP 1 Id FROM Item
      WHERE CompanyId = @CompId AND Code = 'PRD-001'
      ORDER BY CreatedAt DESC
  );

PRINT 'Yinelenen PRD-001 temizlendi.';

-- Mevcut PRD-001 güncelle
UPDATE Item
SET Name = 'Laptop Dell XPS 15', TaxRate = 20.00
WHERE CompanyId = @CompId AND Code = 'PRD-001';

-- Ek ürünler ekle (yoksa)
IF NOT EXISTS (SELECT 1 FROM Item WHERE CompanyId = @CompId AND Code = 'PRD-002')
    INSERT INTO Item (Id, CompanyId, Code, Name, CategoryId, BaseUomId, IsActive, IsLotTracked, IsSerialTracked, CreatedAt, IsDeleted, TaxRate)
    VALUES (NEWID(), @CompId, 'PRD-002', 'Kablosuz Mouse', @CatElek, @UomAdet, 1, 0, 0, GETUTCDATE(), 0, 20.00);

IF NOT EXISTS (SELECT 1 FROM Item WHERE CompanyId = @CompId AND Code = 'PRD-003')
    INSERT INTO Item (Id, CompanyId, Code, Name, CategoryId, BaseUomId, IsActive, IsLotTracked, IsSerialTracked, CreatedAt, IsDeleted, TaxRate)
    VALUES (NEWID(), @CompId, 'PRD-003', 'USB-C Hub 7 Port', @CatElek, @UomAdet, 1, 0, 0, GETUTCDATE(), 0, 20.00);

IF NOT EXISTS (SELECT 1 FROM Item WHERE CompanyId = @CompId AND Code = 'HAM-001')
    INSERT INTO Item (Id, CompanyId, Code, Name, CategoryId, BaseUomId, IsActive, IsLotTracked, IsSerialTracked, CreatedAt, IsDeleted, TaxRate)
    VALUES (NEWID(), @CompId, 'HAM-001', 'Alüminyum Profil 6063-T5', @CatHam, @UomMt, 1, 1, 0, GETUTCDATE(), 0, 10.00);

IF NOT EXISTS (SELECT 1 FROM Item WHERE CompanyId = @CompId AND Code = 'HAM-002')
    INSERT INTO Item (Id, CompanyId, Code, Name, CategoryId, BaseUomId, IsActive, IsLotTracked, IsSerialTracked, CreatedAt, IsDeleted, TaxRate)
    VALUES (NEWID(), @CompId, 'HAM-002', 'Çelik Levha 3mm', @CatHam, @UomKg, 1, 1, 0, GETUTCDATE(), 0, 10.00);

PRINT '5 ürün hazır.';

-- Depo 1: Ana Depo
DECLARE @Wh1 UNIQUEIDENTIFIER = NEWID();
IF NOT EXISTS (SELECT 1 FROM Warehouse WHERE CompanyId = @CompId AND Code = 'WH-001')
BEGIN
    SET @Wh1 = NEWID();
    INSERT INTO Warehouse (Id, CompanyId, Code, Name, IsActive, IsDeleted)
    VALUES (@Wh1, @CompId, 'WH-001', 'Ana Depo', 1, 0);
    PRINT 'WH-001 Ana Depo oluşturuldu.';

    -- Ana Depo rafları
    INSERT INTO Bin (Id, WarehouseId, Code, Zone, SortNo, IsPickingArea, IsReceivingArea, IsActive, IsDeleted)
    VALUES
    (NEWID(), @Wh1, 'R-A-01', 'A', 1, 1, 0, 1, 0),
    (NEWID(), @Wh1, 'R-A-02', 'A', 2, 1, 0, 1, 0),
    (NEWID(), @Wh1, 'R-A-03', 'A', 3, 1, 0, 1, 0),
    (NEWID(), @Wh1, 'R-B-01', 'B', 4, 1, 0, 1, 0),
    (NEWID(), @Wh1, 'R-B-02', 'B', 5, 1, 0, 1, 0),
    (NEWID(), @Wh1, 'GRS-01', 'GRS', 6, 0, 1, 1, 0),  -- Giriş Bölgesi
    (NEWID(), @Wh1, 'CIK-01', 'CIK', 7, 0, 0, 1, 0);  -- Çıkış Bölgesi
    PRINT 'WH-001 için 7 raf oluşturuldu.';
END
ELSE
    SELECT @Wh1 = Id FROM Warehouse WHERE CompanyId = @CompId AND Code = 'WH-001';

-- Depo 2: Üretim Deposu
DECLARE @Wh2 UNIQUEIDENTIFIER = NEWID();
IF NOT EXISTS (SELECT 1 FROM Warehouse WHERE CompanyId = @CompId AND Code = 'WH-002')
BEGIN
    SET @Wh2 = NEWID();
    INSERT INTO Warehouse (Id, CompanyId, Code, Name, IsActive, IsDeleted)
    VALUES (@Wh2, @CompId, 'WH-002', 'Üretim Deposu', 1, 0);
    PRINT 'WH-002 Üretim Deposu oluşturuldu.';

    INSERT INTO Bin (Id, WarehouseId, Code, Zone, SortNo, IsPickingArea, IsReceivingArea, IsActive, IsDeleted)
    VALUES
    (NEWID(), @Wh2, 'URE-A-01', 'URE', 1, 1, 0, 1, 0),
    (NEWID(), @Wh2, 'URE-A-02', 'URE', 2, 1, 0, 1, 0),
    (NEWID(), @Wh2, 'URE-GRS',  'GRS', 3, 0, 1, 1, 0);
    PRINT 'WH-002 için 3 raf oluşturuldu.';
END

-- Partner (Tedarikçi ve Müşteri) ekle
IF NOT EXISTS (SELECT 1 FROM Partner WHERE CompanyId = @CompId AND Code = 'SUP-001')
    INSERT INTO Partner (Id, CompanyId, Code, Name, Type, IsActive, IsDeleted)
    VALUES (NEWID(), @CompId, 'SUP-001', 'Teknoloji Dağıtım A.Ş.', 'VENDOR', 1, 0);

IF NOT EXISTS (SELECT 1 FROM Partner WHERE CompanyId = @CompId AND Code = 'SUP-002')
    INSERT INTO Partner (Id, CompanyId, Code, Name, Type, IsActive, IsDeleted)
    VALUES (NEWID(), @CompId, 'SUP-002', 'Global Hammadde Ltd.', 'VENDOR', 1, 0);

IF NOT EXISTS (SELECT 1 FROM Partner WHERE CompanyId = @CompId AND Code = 'CUS-001')
    INSERT INTO Partner (Id, CompanyId, Code, Name, Type, IsActive, IsDeleted)
    VALUES (NEWID(), @CompId, 'CUS-001', 'ABC Bilişim Sistemleri', 'CUSTOMER', 1, 0);

IF NOT EXISTS (SELECT 1 FROM Partner WHERE CompanyId = @CompId AND Code = 'CUS-002')
    INSERT INTO Partner (Id, CompanyId, Code, Name, Type, IsActive, IsDeleted)
    VALUES (NEWID(), @CompId, 'CUS-002', 'XYZ Mühendislik Sanayi', 'CUSTOMER', 1, 0);

PRINT 'Partner kayıtları hazır.';
PRINT 'Seed tamamlandı!';
