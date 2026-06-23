-- M19 — Bütçe Yönetimi & Nakit Akışı (Budgeting & Cash Flow)

-- 1. Bütçe Tanımları (Budgets)
CREATE TABLE Budget (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    Year INT NOT NULL,
    Code NVARCHAR(50) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Type NVARCHAR(50) DEFAULT 'OPERATIONAL', -- OPERATIONAL, CASH_FLOW, INVESTMENT
    Status NVARCHAR(50) DEFAULT 'DRAFT', -- DRAFT, APPROVED, CLOSED
    
    CONSTRAINT FK_Budget_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
);

-- 2. Bütçe Satırları / Tahakkuk & Nakit Akış Planı (Budget Lines)
CREATE TABLE BudgetLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    BudgetId UNIQUEIDENTIFIER NOT NULL,
    AccrualDate DATE NOT NULL, -- Bütçede hangi ayda görünecek? (Tahakkuk - Fatura Tarihi)
    CashDate DATE NOT NULL,    -- Nakit akışında hangi gün görünecek? (Ödeme/Tahsilat Tarihi)
    Direction NVARCHAR(10) DEFAULT 'OUT', -- IN (Gelir/Tahsilat), OUT (Gider/Ödeme)
    CostCenterId UNIQUEIDENTIFIER NULL, 
    ExpenseTypeId UNIQUEIDENTIFIER NULL,
    AmountPlanned DECIMAL(18,4) NOT NULL DEFAULT 0,
    AmountActual DECIMAL(18,4) NOT NULL DEFAULT 0,
    
    CONSTRAINT FK_BudgetLine_Budget FOREIGN KEY (BudgetId) REFERENCES Budget(Id),
    CONSTRAINT FK_BudgetLine_CC FOREIGN KEY (CostCenterId) REFERENCES CostCenter(Id)
);

-- 3. Nakit Akış Beklentileri (Cash Flow Forecast)
-- Bu tablo bütçeden bağımsız olarak gelecek ödeme/tahsilat tahminlerini tutar.
CREATE TABLE CashFlowForecast (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ForecastDate DATE NOT NULL,
    Type NVARCHAR(10) NOT NULL, -- IN (Tahsilat/Gelir), OUT (Ödeme/Gider)
    SourceType NVARCHAR(50), -- SALES_ORDER, PURCHASE_ORDER, EXPENSE_INVOICE, LOAN, ETC
    SourceId UNIQUEIDENTIFIER NULL,
    Amount DECIMAL(18,4) NOT NULL,
    Probability DECIMAL(5,2) DEFAULT 100.00, -- Tahmin olasılığı
    Notes NVARCHAR(500),
    
    CONSTRAINT FK_CashFlow_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
);

-- 4. Bütçe (Tahakkuk) Bazlı Performans Görünümü
CREATE VIEW View_AccrualBudgetReport AS
SELECT 
    b.Year,
    MONTH(bl.AccrualDate) as BudgetMonth,
    cc.Name as CostCenter,
    bl.AmountPlanned,
    bl.AmountActual,
    (bl.AmountPlanned - bl.AmountActual) as Variance
FROM BudgetLine bl
JOIN Budget b ON b.Id = bl.BudgetId
LEFT JOIN CostCenter cc ON cc.Id = bl.CostCenterId;
GO

-- 5. Nakit Akışı (Ödeme/Tahsilat) Takvimi
CREATE VIEW View_CashFlowCalendar AS
SELECT 
    bl.CashDate,
    bl.Direction,
    cc.Name as CostCenter,
    bl.AmountPlanned as ForecastAmount,
    CASE WHEN bl.Direction = 'IN' THEN bl.AmountPlanned ELSE -bl.AmountPlanned END as NetFlow
FROM BudgetLine bl
LEFT JOIN CostCenter cc ON cc.Id = bl.CostCenterId;
GO
