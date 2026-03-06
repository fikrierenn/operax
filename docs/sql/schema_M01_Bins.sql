-- M01 — Master Data: Bin (Hücre) Schema
-- Bu dosya M01 ana şemasını hücre yönetimi ile genişletir.

-- Bin (Hücre) Tablosu (Zaten M01'de var ama detaylandırıyoruz)
-- WarehouseId: Hangi depoda?
-- Code: Hücre adresi (Örn: A-01-01)
-- Zone: Bölge (Örn: Soğuk Hava, Kuru Alan)
-- IsPickingArea: Buradan sevkiyat için toplama yapılabilir mi?
-- IsReceivingArea: Mal kabulde ürünler buraya mı iner? (Staging)
-- IsActive: Hücre kullanımda mı?

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Bin')
BEGIN
    CREATE TABLE Bin (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        WarehouseId UNIQUEIDENTIFIER NOT NULL,
        Code NVARCHAR(100) NOT NULL,
        Zone NVARCHAR(50),
        SortNo INT DEFAULT 0,
        IsPickingArea BIT DEFAULT 1,
        IsReceivingArea BIT DEFAULT 0,
        IsActive BIT DEFAULT 1,
        IsDeleted BIT DEFAULT 0,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_Bin_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id)
    );
    CREATE INDEX IX_Bin_Warehouse_Code ON Bin(WarehouseId, Code) WHERE IsDeleted = 0;
END
GO

-- Seed: Örnek Hücreler (Zorunlu değil ama test için faydalı)
-- Main Warehouse için bir Mal Kabul (RCV) ve bir normal raf (A-01-01)
/*
DECLARE @WhId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Warehouse WHERE Code = 'MAIN');
IF @WhId IS NOT EXISTS
BEGIN
    INSERT INTO Bin (WarehouseId, Code, Zone, IsPickingArea, IsReceivingArea)
    VALUES (@WhId, 'RCV-AREA', 'STAGING', 0, 1),
           (@WhId, 'A-01-01', 'ZONE-A', 1, 0);
END
*/
