-- M09 — Seri No (Serial Number) Schema

CREATE TABLE ItemSerial (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    SerialNo NVARCHAR(100) NOT NULL, -- Tekil Seri Numarası
    
    Status NVARCHAR(20) DEFAULT 'IN_STOCK', -- IN_STOCK, SHIPPED, SCRAPPED, QUARANTINE
    
    CurrentWarehouseId UNIQUEIDENTIFIER,
    CurrentBinId UNIQUEIDENTIFIER,
    CurrentLpnId UNIQUEIDENTIFIER,
    
    LotNo NVARCHAR(100), -- Eğer ürün hem Lot hem Seri takipliyse
    
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    
    CONSTRAINT FK_ItemSerial_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_ItemSerial_Item FOREIGN KEY (ItemId) REFERENCES Item(Id),
    CONSTRAINT UQ_ItemSerial_No UNIQUE (CompanyId, ItemId, SerialNo)
);

CREATE INDEX IX_ItemSerial_No ON ItemSerial(CompanyId, SerialNo);
GO

-- StockMovement tablosunda SerialNo zaten var. 
-- Bu tablo her bir tekil cihazın "Yaşam Döngüsünü" takip eder.
