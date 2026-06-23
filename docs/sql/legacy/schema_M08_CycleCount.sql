-- M08 — Sayım (Cycle Count) Schema
-- Fiziksel stok kontrollerini ve fark düzeltmelerini yönetir.

CREATE TABLE CycleCount (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    DocNo NVARCHAR(50) NOT NULL, -- Örn: CNT-2026-001
    Status NVARCHAR(20) DEFAULT 'DRAFT', -- DRAFT, COUNTING, COMPLETED, CANCELLED
    
    WarehouseId UNIQUEIDENTIFIER NOT NULL,
    Description NVARCHAR(MAX),
    
    PlannedDate DATETIME2,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    PostedAt DATETIME2,
    
    CONSTRAINT FK_CC_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_CC_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id)
);

CREATE TABLE CycleCountLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CycleCountId UNIQUEIDENTIFIER NOT NULL,
    BinId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    LpnId UNIQUEIDENTIFIER,
    LotNo NVARCHAR(100),
    
    -- Sayılmadan önceki sistem stok verisi (Snapshot)
    QtySystem DECIMAL(18,6) DEFAULT 0,
    
    -- Fiziksel sayılan miktar
    QtyCounted DECIMAL(18,6) DEFAULT 0,
    
    -- Aradaki fark (QtyCounted - QtySystem)
    QtyDifference AS (QtyCounted - QtySystem),
    
    CountedAt DATETIME2,
    CountedBy NVARCHAR(450),
    
    CONSTRAINT FK_CCLine_Header FOREIGN KEY (CycleCountId) REFERENCES CycleCount(Id),
    CONSTRAINT FK_CCLine_Bin FOREIGN KEY (BinId) REFERENCES Bin(Id),
    CONSTRAINT FK_CCLine_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

CREATE INDEX IX_CycleCount_DocNo ON CycleCount(CompanyId, DocNo);
CREATE INDEX IX_CycleCountLine_Header ON CycleCountLine(CycleCountId);
GO
