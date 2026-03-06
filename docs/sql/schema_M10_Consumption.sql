-- M10 — Üretim Sarfiyat (Consumption) Tablosu

CREATE TABLE ProductionConsumption (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ProductionOrderId UNIQUEIDENTIFIER NOT NULL,
    ProductionActivityId UNIQUEIDENTIFIER, -- Hangi başlat/bitir seansında kullanıldı?
    RouteStepId UNIQUEIDENTIFIER, -- Hangi operasyonda (Kesim, Montaj?)
    
    ItemId UNIQUEIDENTIFIER NOT NULL,
    LotNo NVARCHAR(100),
    SerialNo NVARCHAR(100),
    LpnId UNIQUEIDENTIFIER,
    
    QtyConsumed DECIMAL(18,6) NOT NULL,
    UnitPrice DECIMAL(18,4), -- O anki stok maliyeti
    TotalCost AS (QtyConsumed * UnitPrice),
    
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    
    CONSTRAINT FK_Cons_Order FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
    CONSTRAINT FK_Cons_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

CREATE INDEX IX_ProductionConsumption_Order ON ProductionConsumption(ProductionOrderId);
GO
