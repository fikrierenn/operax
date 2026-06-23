-- M10 — Üretim Yönetimi (Production Management) Schema
-- Bu modül stok yetersizliğinde otomatik veya manuel üretim emirlerini yönetir.

CREATE TABLE ProductionOrder (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    DocNo NVARCHAR(50) NOT NULL, -- Örn: PRD-2026-0001
    ItemId UNIQUEIDENTIFIER NOT NULL,
    QtyTarget DECIMAL(18,6) NOT NULL,
    QtyProduced DECIMAL(18,6) DEFAULT 0,
    Status NVARCHAR(20) DEFAULT 'DRAFT', -- DRAFT, RELEASED, IN_PROGRESS, COMPLETED, CANCELLED
    
    SourceDocType NVARCHAR(50), -- 'SHIPPING' (Eğer sevkiyattan tetiklendiyse)
    SourceDocId UNIQUEIDENTIFIER, -- Sevkiyat ID
    
    TargetWarehouseId UNIQUEIDENTIFIER, -- Üretilen malın gireceği depo
    TargetBinId UNIQUEIDENTIFIER, -- Üretilen malın gireceği raf (FG)
    SourceBinId UNIQUEIDENTIFIER, -- Hamaddelerin toplandığı üretim alanı rafı (WIP)
    
    Priority INT DEFAULT 10,
    DueDate DATETIME2,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    CompletedAt DATETIME2,
    
    CumulativeLaborCost DECIMAL(18,4) DEFAULT 0,
    CumulativeEnergyCost DECIMAL(18,4) DEFAULT 0,
    
    -- Planlanan Maliyetler
    PlannedMaterialCost DECIMAL(18,4) DEFAULT 0,
    PlannedResourceCost DECIMAL(18,4) DEFAULT 0,
    
    -- Fiili Maliyetler (Kümülatif toplanır)
    ActualMaterialCost DECIMAL(18,4) DEFAULT 0,
    ActualResourceCost DECIMAL(18,4) DEFAULT 0,
    
    CONSTRAINT FK_Production_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_Production_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

CREATE INDEX IX_ProductionOrder_DocNo ON ProductionOrder(CompanyId, DocNo);
CREATE INDEX IX_ProductionOrder_Source ON ProductionOrder(SourceDocId);
GO
