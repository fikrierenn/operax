-- M07 — Transfer (Extensions: Replenishment & Config)

-- ItemBinConfig: Hangi ürün hangi rafta durmalı? Min/Max seviyeleri neler?
CREATE TABLE ItemBinConfig (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    BinId UNIQUEIDENTIFIER NOT NULL, -- Bu ürünün "Picking" (Toplama) rafı
    
    MinQty DECIMAL(18,6) DEFAULT 0, -- Bu miktarın altına düşerse Besleme (Replenishment) tetiklenir
    MaxQty DECIMAL(18,6) DEFAULT 0, -- Besleme yapılırken bu miktara tamamlanır
    
    Priority INT DEFAULT 10,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_ItemBin_Item FOREIGN KEY (ItemId) REFERENCES Item(Id),
    CONSTRAINT FK_ItemBin_Bin FOREIGN KEY (BinId) REFERENCES Bin(Id)
);

CREATE INDEX IX_ItemBinConfig_Item ON ItemBinConfig(ItemId);
GO
