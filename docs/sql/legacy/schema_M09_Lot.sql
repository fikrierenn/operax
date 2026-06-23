-- M09 — Lot (Parti) Takibi Schema

CREATE TABLE ItemLot (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    LotNo NVARCHAR(100) NOT NULL, -- Parti Numarası
    
    ProductionDate DATETIME2,
    ExpiryDate DATETIME2, -- SKT
    
    Status NVARCHAR(20) DEFAULT 'AVAILABLE', -- AVAILABLE, QUARANTINE, BLOCKED
    Notes NVARCHAR(MAX),
    
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    
    CONSTRAINT FK_ItemLot_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_ItemLot_Item FOREIGN KEY (ItemId) REFERENCES Item(Id),
    CONSTRAINT UQ_ItemLot_No UNIQUE (CompanyId, ItemId, LotNo)
);

CREATE INDEX IX_ItemLot_Expiry ON ItemLot(CompanyId, ItemId, ExpiryDate);
GO

-- StockMovement tablosu zaten LotNo kolonu içeriyor (M02'den). 
-- Ancak bu tablo lot bazlı ekstra meta-data (SKT vb) tutmak için master data işlevi görür.
