-- Plan 26 Faz A: Kırılımlı gider raporlama şema
-- Idempotent (COL_LENGTH / IF NOT EXISTS korumalı)

-- ExpenseType hiyerarşi: ParentId (ağaç — Mikro grup-kodu deseni)
IF COL_LENGTH('ExpenseType', 'ParentId') IS NULL
    ALTER TABLE ExpenseType ADD ParentId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseType_Parent')
    ALTER TABLE ExpenseType ADD CONSTRAINT FK_ExpenseType_Parent
    FOREIGN KEY (ParentId) REFERENCES ExpenseType(Id);

-- ExpenseInvoice başlığına default gider merkezi
IF COL_LENGTH('ExpenseInvoice', 'CostCenterId') IS NULL
    ALTER TABLE ExpenseInvoice ADD CostCenterId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseInvoice_CostCenter')
    ALTER TABLE ExpenseInvoice ADD CONSTRAINT FK_ExpenseInvoice_CostCenter
    FOREIGN KEY (CostCenterId) REFERENCES CostCenter(Id);

-- ExpenseInvoiceLine.CostCenterId NOT NULL → NULL (NULL = başlıktan miras)
-- Mevcut satırlar dolu kalır; COALESCE(Line, Header) raporda çözer
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('ExpenseInvoiceLine') AND name = 'CostCenterId' AND is_nullable = 0
)
    ALTER TABLE ExpenseInvoiceLine ALTER COLUMN CostCenterId UNIQUEIDENTIFIER NULL;
