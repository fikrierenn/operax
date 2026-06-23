DECLARE @CompId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
DECLARE @CatElek UNIQUEIDENTIFIER = NEWID();
DECLARE @CatHam UNIQUEIDENTIFIER = NEWID();

-- KATEGORİLERİ EKLE
INSERT INTO Category (Id, CompanyId, Code, Name) VALUES 
(@CatElek, @CompId, 'CAT-ELEK', 'Elektronik Ürünler'),
(@CatHam, @CompId, 'CAT-HAM', 'Ham Maddeler');

-- ÜRÜNLERİ GÜNCELLE
UPDATE Item SET CategoryId = @CatElek, TaxRate = 20.0 WHERE Code IN ('PRD-001', 'PRD-002');
UPDATE Item SET CategoryId = @CatHam, TaxRate = 10.0 WHERE Code IN ('PRD-003');

PRINT 'Kategori ve KDV verileri (Seed) tamamlandı.';
