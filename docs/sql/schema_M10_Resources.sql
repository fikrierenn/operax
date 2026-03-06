-- M10 — Üretim Operasyonları ve Kaynak Takibi (Labor, Machine, Energy)

-- 1. İş Merkezi / Makine Tanımları
CREATE TABLE WorkCenter (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    Code NVARCHAR(50) NOT NULL, -- Örn: CNC-01, MONTAJ-HATTI
    Name NVARCHAR(200) NOT NULL,
    
    -- Maliyet Parametreleri (Saniye başına)
    LaborRatePerSec DECIMAL(18,6) DEFAULT 0,
    MachineRatePerSec DECIMAL(18,6) DEFAULT 0,
    ElectricityRatePerSec DECIMAL(18,6) DEFAULT 0,
    
    IsActive BIT DEFAULT 1,
    CONSTRAINT FK_WC_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
);

-- 2. Üretim Operasyon Girişleri (Start/Stop Logları)
CREATE TABLE ProductionActivity (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductionOrderId UNIQUEIDENTIFIER NOT NULL,
    WorkCenterId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL, -- Operatör
    
    ActivityType NVARCHAR(20) DEFAULT 'PRODUCTION', -- SETUP, PRODUCTION, DOWNTIME
    
    StartTime DATETIME2 NOT NULL,
    EndTime DATETIME2, -- Bittiğinde doldurulur
    
    QtyProduced DECIMAL(18,6) DEFAULT 0, -- Bu aktivitede biten adet
    
    -- Bittiğinde hesaplanan süre (Saniye)
    DurationSec AS DATEDIFF(SECOND, StartTime, EndTime),
    
    -- Otomatik Maliyet Hesaplama (Bittiğinde tetiklenebilir veya raporlanır)
    CalculatedLaborCost DECIMAL(18,4) DEFAULT 0,
    CalculatedEnergyCost DECIMAL(18,4) DEFAULT 0,
    
    Notes NVARCHAR(MAX),
    
    CONSTRAINT FK_Activity_Order FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
    CONSTRAINT FK_Activity_WC FOREIGN KEY (WorkCenterId) REFERENCES WorkCenter(Id)
);

CREATE INDEX IX_ProductionActivity_Order ON ProductionActivity(ProductionOrderId);
GO
