-- 1. VARSA ESKİLERİ TEMİZLE VE ŞİRKET EKLE (VARSAYILAN GUID)
DECLARE @CompId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

IF NOT EXISTS (SELECT 1 FROM Company WHERE Id = @CompId)
BEGIN
    INSERT INTO Company (Id, Name, TaxNumber, IsActive) 
    VALUES (@CompId, 'Operax Default Company', '0000000000', 1);
END

-- 2. DICTIONARY TYPE (UOM)
IF NOT EXISTS (SELECT 1 FROM DictionaryType WHERE Code = 'UOM')
BEGIN
    INSERT INTO DictionaryType (Id, CompanyId, Code, NameTr, NameEn, IsSystem)
    VALUES (NEWID(), @CompId, 'UOM', 'Ölçü Birimi', 'Unit of Measure', 1);
END

DECLARE @UomTypeId UNIQUEIDENTIFIER = (SELECT Id FROM DictionaryType WHERE Code = 'UOM');

-- 3. DICTIONARY VALUES (ADET, KG, MT)
IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE TypeId = @UomTypeId AND Code = 'ADET')
    INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, IsActive, OrderNo) VALUES (NEWID(), @CompId, @UomTypeId, 'ADET', 'Adet', 'Piece', 1, 1);

IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE TypeId = @UomTypeId AND Code = 'KG')
    INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, IsActive, OrderNo) VALUES (NEWID(), @CompId, @UomTypeId, 'KG', 'Kilogram', 'Kilogram', 1, 2);

IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE TypeId = @UomTypeId AND Code = 'MT')
    INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, IsActive, OrderNo) VALUES (NEWID(), @CompId, @UomTypeId, 'MT', 'Metre', 'Meter', 1, 3);

-- 4. ÜRÜNLERİ EKLE (Doğru ID'lerle)
DECLARE @ADET_ID UNIQUEIDENTIFIER = (SELECT Id FROM DictionaryValue WHERE TypeId = @UomTypeId AND Code = 'ADET');
DECLARE @KG_ID UNIQUEIDENTIFIER = (SELECT Id FROM DictionaryValue WHERE TypeId = @UomTypeId AND Code = 'KG');
DECLARE @MT_ID UNIQUEIDENTIFIER = (SELECT Id FROM DictionaryValue WHERE TypeId = @UomTypeId AND Code = 'MT');

-- Temizlik (Tüm itemları temizleyelim ki kirlilik olmasın)
DELETE FROM Item WHERE CompanyId = @CompId OR Code IN ('PRD-001', 'PRD-002', 'PRD-003', 'PRD-004', 'PRD-005');

INSERT INTO Item (Id, CompanyId, Code, Name, BaseUomId, IsLotTracked, IsSerialTracked, IsActive, LastPurchasePrice, CreatedAt, IsDeleted)
VALUES
(NEWID(), @CompId, 'PRD-001', 'Akıllı Telefon X1', @ADET_ID, 0, 1, 1, 15000, GETDATE(), 0),
(NEWID(), @CompId, 'PRD-002', 'Kablosuz Kulaklık Pro', @ADET_ID, 1, 0, 1, 2400, GETDATE(), 0),
(NEWID(), @CompId, 'PRD-003', 'Endüstriyel Alüminyum Levha', @KG_ID, 1, 0, 1, 450, GETDATE(), 0),
(NEWID(), @CompId, 'PRD-004', 'Fiber Optik Kablo', @MT_ID, 0, 0, 1, 85, GETDATE(), 0),
(NEWID(), @CompId, 'PRD-005', 'Eski Nesil Anakart (Pasif)', @ADET_ID, 0, 0, 0, 1200, GETDATE(), 0);

PRINT 'Seed verisi (Şirket, UOM ve Ürünler) varsayılan GUID ile eklendi.';
