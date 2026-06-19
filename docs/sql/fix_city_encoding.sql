-- Bozuk encoding (mojibake) City kayıtlarını düzelt + mükerrerleri temizle
-- Önce Partner.CityId referanslarını doğru (mojibake olmayan) kayda taşı, sonra mojibake satırı sil

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- İstanbul: mojibake 'Ä°stanbul' -> doğru kaydı bul/oluştur
DECLARE @GoodIst UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM City WHERE Name = N'İstanbul');
IF @GoodIst IS NULL
BEGIN
    -- Doğru kayıt yok; ASCII 'Istanbul' veya mojibake'i Türkçe'ye çevirerek tekilleştir
    UPDATE City SET Name = N'İstanbul' WHERE Name = 'Istanbul';
    SET @GoodIst = (SELECT TOP 1 Id FROM City WHERE Name = N'İstanbul');
END

-- Mojibake İstanbul satırlarındaki referansları doğru kayda taşı, sonra sil
UPDATE Partner SET CityId = @GoodIst WHERE CityId IN (SELECT Id FROM City WHERE Name = 'Ä°stanbul');
DELETE FROM City WHERE Name = 'Ä°stanbul';

-- İzmir: aynı mantık
DECLARE @GoodIzm UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM City WHERE Name = N'İzmir');
IF @GoodIzm IS NULL
BEGIN
    UPDATE City SET Name = N'İzmir' WHERE Name = 'Izmir';
    SET @GoodIzm = (SELECT TOP 1 Id FROM City WHERE Name = N'İzmir');
END
UPDATE Partner SET CityId = @GoodIzm WHERE CityId IN (SELECT Id FROM City WHERE Name = 'Ä°zmir');
DELETE FROM City WHERE Name = 'Ä°zmir';

COMMIT;

SELECT Id, Name FROM City ORDER BY Name;
