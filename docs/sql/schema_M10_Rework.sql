-- M10 — Esnek Yeniden İşlem (Rework) ve Parça Değişimi

-- 1. Rework Emirleri (Inspection FAIL sonrası oluşur)
CREATE TABLE ProductionRework (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ProductionOrderId UNIQUEIDENTIFIER NOT NULL,
    InspectionId UNIQUEIDENTIFIER NOT NULL,
    
    Status NVARCHAR(20) DEFAULT 'OPEN', -- OPEN, IN_PROGRESS, COMPLETED
    
    ReworkStepId UNIQUEIDENTIFIER, -- Hangi aşamaya geri dönecek? (Örn: Montaj)
    RequiresAdditionalMaterial BIT DEFAULT 0,
    
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    
    CONSTRAINT FK_Rework_Order FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
    CONSTRAINT FK_Rework_Insp FOREIGN KEY (InspectionId) REFERENCES ProductionInspection(Id)
);

-- 2. Rework Ek Sarfiyat (Ayna, Ayak, Vida değişimi için)
CREATE TABLE ProductionReworkMaterial (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ReworkId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL, -- Değişen/Eklenen parça
    Qty DECIMAL(18,6) NOT NULL,
    
    ActionType NVARCHAR(20) DEFAULT 'ADDITION', -- REPLACEMENT, ADDITION
    Reason NVARCHAR(200),
    
    CONSTRAINT FK_ReworkMat_Rework FOREIGN KEY (ReworkId) REFERENCES ProductionRework(Id),
    CONSTRAINT FK_ReworkMat_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

CREATE INDEX IX_ProductionRework_Order ON ProductionRework(ProductionOrderId);
GO
