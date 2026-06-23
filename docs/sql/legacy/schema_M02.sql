-- M02 — Inventory Ledger Schema

CREATE TABLE StockMovement (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    WarehouseId UNIQUEIDENTIFIER NOT NULL,
    BinId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    MovementType NVARCHAR(50) NOT NULL, -- 'RECEIPT', 'ISSUE', 'TRANSFER', 'COUNT_ADJ'
    QtyBase DECIMAL(18,6) NOT NULL, -- Always in EACH/Base Unit. Positive for In, Negative for Out.
    UomId UNIQUEIDENTIFIER NOT NULL, -- Original UOM used in transaction
    QtyOriginal DECIMAL(18,6) NOT NULL, 
    SourceDocType NVARCHAR(50), -- 'RECEIVING', 'SHIPMENT', 'COUNT'
    SourceDocId UNIQUEIDENTIFIER,
    SourceDocNo NVARCHAR(100),
    LpnId UNIQUEIDENTIFIER, -- Palet/Kap ID (M09 LPN tablosuna referans)
    LotNo NVARCHAR(100),
    SerialNo NVARCHAR(100),
    ExpiryDate DATETIME2,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    IsCancelled BIT DEFAULT 0,
    CancelledAt DATETIME2,
    CancelledBy UNIQUEIDENTIFIER,
    CONSTRAINT FK_StockMovement_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_StockMovement_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id),
    CONSTRAINT FK_StockMovement_Bin FOREIGN KEY (BinId) REFERENCES Bin(Id),
    CONSTRAINT FK_StockMovement_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

-- Inventory Balance View (Calculated from StockMovement)
-- We don't use a physical table for balance to ensure data integrity, 
-- but indexed views or summary tables can be added for performance later.
GO

CREATE VIEW vw_InventoryBalance AS
    SELECT 
        CompanyId, WarehouseId, BinId, LpnId, ItemId, LotNo,
        SUM(QtyBase) as QtyOnHand
    FROM StockMovement
    WHERE IsCancelled = 0
    GROUP BY CompanyId, WarehouseId, BinId, LpnId, ItemId, LotNo
    HAVING SUM(QtyBase) <> 0;
GO

-- Indexing
CREATE INDEX IX_StockMovement_Item ON StockMovement(CompanyId, ItemId, CreatedAt);
CREATE INDEX IX_StockMovement_Bin ON StockMovement(CompanyId, BinId, ItemId);
CREATE INDEX IX_StockMovement_Source ON StockMovement(SourceDocId);
