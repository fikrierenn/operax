-- Mevcut UOM'lardan ADET'i bul
DECLARE @UOM_ADET UNIQUEIDENTIFIER;
DECLARE @UOM_KG UNIQUEIDENTIFIER;
DECLARE @UOM_MT UNIQUEIDENTIFIER;
DECLARE @UOM_LT UNIQUEIDENTIFIER;
DECLARE @CId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Company);

SELECT TOP 1 @UOM_ADET = Id FROM DictionaryValue 
WHERE TypeId = (SELECT Id FROM DictionaryType WHERE Code = 'UOM') AND Code = 'ADET';

SELECT TOP 1 @UOM_KG = Id FROM DictionaryValue 
WHERE TypeId = (SELECT Id FROM DictionaryType WHERE Code = 'UOM') AND Code = 'KG';

SELECT TOP 1 @UOM_MT = Id FROM DictionaryValue 
WHERE TypeId = (SELECT Id FROM DictionaryType WHERE Code = 'UOM') AND Code = 'MT';

-- Eğer UOM yoksa ilk UOM'u kullan
IF @UOM_ADET IS NULL
    SELECT TOP 1 @UOM_ADET = Id FROM DictionaryValue 
    WHERE TypeId = (SELECT Id FROM DictionaryType WHERE Code = 'UOM');

IF @UOM_KG IS NULL SET @UOM_KG = @UOM_ADET;
IF @UOM_MT IS NULL SET @UOM_MT = @UOM_ADET;

-- Mevcut örnek ürünleri temizle (tekrar çalıştırılabilir olması için)
DELETE FROM Item WHERE Code IN ('FIN-KABLO-3M','VLV-BASINC-112','PCB-KONTROL-V2','HMD-CEL-001','GRE-YAG-50','DK-8080','CAM-80','PROF-ALU');

-- Örnek ürünler ekle
INSERT INTO Item (Id, CompanyId, Code, Name, BaseUomId, IsLotTracked, IsSerialTracked, IsActive, LastPurchasePrice)
VALUES
(NEWID(), @CId, 'FIN-KABLO-3M',   'Finyal Kablo 3 Metre',           @UOM_ADET, 1, 0, 1, 142.50),
(NEWID(), @CId, 'VLV-BASINC-112', 'Basınç Valfi 1/2"',               @UOM_ADET, 0, 1, 1, 387.00),
(NEWID(), @CId, 'PCB-KONTROL-V2', 'Kontrol Kartı PCB V2',            @UOM_ADET, 0, 1, 1, 2140.00),
(NEWID(), @CId, 'HMD-CEL-001',    'Çelik Profil 40x40',              @UOM_MT,   0, 0, 0, 48.20),
(NEWID(), @CId, 'GRE-YAG-50',     'Endüstriyel Gres Yağı 50kg',     @UOM_KG,   1, 0, 1, 31.80),
(NEWID(), @CId, 'DK-8080',        'Duşakabin 80x80 Kare',            @UOM_ADET, 1, 0, 1, 1100.00),
(NEWID(), @CId, 'CAM-80',         'Temperli Cam 80cm',               @UOM_ADET, 0, 0, 1, 400.00),
(NEWID(), @CId, 'PROF-ALU',       'Alüminyum Yan Profil',            @UOM_MT,   0, 0, 1, 150.00);

SELECT Code, Name, IsActive FROM Item ORDER BY Code;
PRINT 'Örnek ürünler eklendi!';
