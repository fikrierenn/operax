-- M18 — Gider Yönetimi (Expense Management)

-- 1. Gider Merkezleri (Cost Centers)
CREATE TABLE CostCenter (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    Code NVARCHAR(50) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Type NVARCHAR(50), -- PRODUCTION, ADMIN, SALES, LOGISTICS
    ParentId UNIQUEIDENTIFIER NULL, -- Hiyerarşik yapı için
    IsActive BIT DEFAULT 1,
    
    CONSTRAINT FK_CostCenter_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_CostCenter_Parent FOREIGN KEY (ParentId) REFERENCES CostCenter(Id)
);

-- 2. Gider Kategorileri / Tipleri (Expense Types)
CREATE TABLE ExpenseType (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    Code NVARCHAR(50) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    UnitOfMeasure NVARCHAR(20), -- kWh, m3, LT, Adet, Ay vb.
    Description NVARCHAR(500),
    
    CONSTRAINT FK_ExpenseType_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
);

-- 3. Gider Faturaları (Expense Invoices)
CREATE TABLE ExpenseInvoice (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    PartnerId UNIQUEIDENTIFIER NOT NULL, -- Tedarikçi
    DocNo NVARCHAR(50) NOT NULL,
    InvoiceDate DATE NOT NULL,
    DueDate DATE,
    TotalAmount DECIMAL(18,4) NOT NULL,
    Currency NVARCHAR(10) DEFAULT 'TRY',
    Status NVARCHAR(50) DEFAULT 'DRAFT', -- DRAFT, POSTED, PAID, CANCELLED
    
    CONSTRAINT FK_ExpenseInv_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_ExpenseInv_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id)
);

-- 4. Gider Fatura Satırları (Expense Invoice Lines)
CREATE TABLE ExpenseInvoiceLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ExpenseInvoiceId UNIQUEIDENTIFIER NOT NULL,
    ExpenseTypeId UNIQUEIDENTIFIER NOT NULL,
    CostCenterId UNIQUEIDENTIFIER NOT NULL,
    Quantity DECIMAL(18,6) NOT NULL, -- kWh miktarı, m3 miktarı vb.
    UnitPrice DECIMAL(18,4) NOT NULL,
    Amount DECIMAL(18,4) NOT NULL,
    TaxRate DECIMAL(5,2) DEFAULT 20.00,
    TaxAmount AS (Amount * TaxRate / 100),
    TotalAmount AS (Amount + (Amount * TaxRate / 100)),
    
    CONSTRAINT FK_ExpInvLine_Inv FOREIGN KEY (ExpenseInvoiceId) REFERENCES ExpenseInvoice(Id),
    CONSTRAINT FK_ExpInvLine_Type FOREIGN KEY (ExpenseTypeId) REFERENCES ExpenseType(Id),
    CONSTRAINT FK_ExpInvLine_CC FOREIGN KEY (CostCenterId) REFERENCES CostCenter(Id)
);
GO
