-- M10 — Üretim Rotaları ve Operasyonel Akış (Routing)

-- 1. Rota Tanımı (Örn: Standart Duşakabin Rotası)
CREATE TABLE ProductRoute (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    Code NVARCHAR(50) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    IsActive BIT DEFAULT 1,
    
    CONSTRAINT FK_Route_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
);

-- 2. Rota Adımları (Operasyonlar: Kesim -> Montaj -> Paketleme)
CREATE TABLE ProductRouteStep (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductRouteId UNIQUEIDENTIFIER NOT NULL,
    StepOrder INT NOT NULL, -- İşlem sırası (10, 20, 30...)
    
    OperationCode NVARCHAR(50) NOT NULL, -- KESIM, MONTAJ, PAKETLEME
    OperationName NVARCHAR(200) NOT NULL,
    
    WorkCenterId UNIQUEIDENTIFIER NOT NULL, -- Bu operasyonun yapılacağı makine/istasyon
    
    StandardDurationSec INT DEFAULT 0, -- Planlanan standart süre
    StandardLaborCost DECIMAL(18,4) DEFAULT 0,
    StandardMachineCost DECIMAL(18,4) DEFAULT 0,
    
    CONSTRAINT FK_Step_Route FOREIGN KEY (ProductRouteId) REFERENCES ProductRoute(Id),
    CONSTRAINT FK_Step_WC FOREIGN KEY (WorkCenterId) REFERENCES WorkCenter(Id)
);

-- 3. Üretim Emrine Rota Bilgisi Ekleme
-- ProductionOrder tablosuna RouteId ve CurrentStepId eklenecek.
ALTER TABLE ProductionOrder ADD RouteId UNIQUEIDENTIFIER;
ALTER TABLE ProductionOrder ADD CurrentRouteStepId UNIQUEIDENTIFIER;
GO
