-- M10 — Üretim Kalite Kontrol (Inspection) ve Hata Yönetimi

-- 1. Hata Kodları (Defect Codes) - Dictionary tablosuna da eklenebilir
CREATE TABLE DefectCode (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    Code NVARCHAR(50) NOT NULL, -- Örn: CAM-KIRIK, PROFIL-CIZIK
    Description NVARCHAR(200),
    Severity NVARCHAR(20) DEFAULT 'MINOR', -- MINOR, MAJOR, CRITICAL
    
    CONSTRAINT FK_Defect_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
);

-- 2. Kalite Kontrol Kayıtları
CREATE TABLE ProductionInspection (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ProductionOrderId UNIQUEIDENTIFIER NOT NULL,
    InspectorUserId UNIQUEIDENTIFIER NOT NULL,
    
    InspectionDate DATETIME2 DEFAULT GETUTCDATE(),
    QtyInspected DECIMAL(18,6) NOT NULL,
    QtyPassed DECIMAL(18,6) NOT NULL,
    QtyFailed AS (QtyInspected - QtyPassed),
    
    Result NVARCHAR(20) NOT NULL, -- 'PASS', 'FAIL'
    FailAction NVARCHAR(20), -- 'REWORK', 'SCRAP', 'CANCEL_ORDER'
    
    DefectCodeId UNIQUEIDENTIFIER,
    Notes NVARCHAR(MAX),
    
    CONSTRAINT FK_Insp_Order FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
    CONSTRAINT FK_Insp_Defect FOREIGN KEY (DefectCodeId) REFERENCES DefectCode(Id)
);

CREATE INDEX IX_ProductionInspection_Order ON ProductionInspection(ProductionOrderId);
GO
