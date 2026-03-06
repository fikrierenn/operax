-- 1. KDV Dictionary Tipi
DECLARE @CompId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
DECLARE @TypeId UNIQUEIDENTIFIER = NEWID();

IF NOT EXISTS (SELECT * FROM DictionaryType WHERE Code = 'TAX_RATE')
BEGIN
    INSERT INTO DictionaryType (Id, CompanyId, Code, NameTr, NameEn, IsSystem)
    VALUES (@TypeId, @CompId, 'TAX_RATE', 'KDV Oranı', 'Tax Rate', 1);
END
ELSE
BEGIN
    SET @TypeId = (SELECT Id FROM DictionaryType WHERE Code = 'TAX_RATE');
END

-- 2. KDV Değerleri (Temizle ve Ekle)
DELETE FROM DictionaryValue WHERE TypeId = @TypeId;

INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive)
VALUES 
(NEWID(), @CompId, @TypeId, '0', '%0', '0%', 1, 1),
(NEWID(), @CompId, @TypeId, '1', '%1', '1%', 2, 1),
(NEWID(), @CompId, @TypeId, '10', '%10', '10%', 3, 1),
(NEWID(), @CompId, @TypeId, '20', '%20', '20%', 4, 1);

PRINT 'KDV Dictionary tanımları (%0, %1, %10, %20) tamamlandı.';
