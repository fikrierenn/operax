-- Plan 23 Faz F: Belge-seviye BranchId — evrak + ledger tabloları
-- Idempotent (IF COL_LENGTH IS NULL korumalı)
-- Kararlar:
--   NOT NULL YOK — iş/raporlama boyutu, mevzuat zorunluluğu değil
--   StockTransferHeader + CycleCountHeader HARIÇ (kaynak/hedef şube farklı olabilir; depodan türetilir)
--   Ledger (StockMovement, AccountMovement) — post SP'sinde Warehouse.BranchId'den türetilir

-- Satınalma Siparişi
IF COL_LENGTH('PurchaseOrderHeader', 'BranchId') IS NULL
    ALTER TABLE PurchaseOrderHeader ADD BranchId UNIQUEIDENTIFIER NULL;

-- Mal Kabul
IF COL_LENGTH('ReceivingHeader', 'BranchId') IS NULL
    ALTER TABLE ReceivingHeader ADD BranchId UNIQUEIDENTIFIER NULL;

-- Alış Faturası (gider)
IF COL_LENGTH('ExpenseInvoice', 'BranchId') IS NULL
    ALTER TABLE ExpenseInvoice ADD BranchId UNIQUEIDENTIFIER NULL;

-- Satış Siparişi
IF COL_LENGTH('SalesOrderHeader', 'BranchId') IS NULL
    ALTER TABLE SalesOrderHeader ADD BranchId UNIQUEIDENTIFIER NULL;

-- Sevkiyat
IF COL_LENGTH('ShippingHeader', 'BranchId') IS NULL
    ALTER TABLE ShippingHeader ADD BranchId UNIQUEIDENTIFIER NULL;

-- Satış Faturası
IF COL_LENGTH('SalesInvoice', 'BranchId') IS NULL
    ALTER TABLE SalesInvoice ADD BranchId UNIQUEIDENTIFIER NULL;

-- Stok Hareketi (Mikro subeno deseni: her satırda, post SP'sinde Warehouse.BranchId'den türetilir)
IF COL_LENGTH('StockMovement', 'BranchId') IS NULL
    ALTER TABLE StockMovement ADD BranchId UNIQUEIDENTIFIER NULL;

-- Cari Hesap Hareketi (Mikro cha_subeno deseni: her satırda, post SP'sinde başlıktan türetilir)
IF COL_LENGTH('AccountMovement', 'BranchId') IS NULL
    ALTER TABLE AccountMovement ADD BranchId UNIQUEIDENTIFIER NULL;

-- FK kısıtları (Branch tablosu var olduğundan ekleyebiliriz)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PurchaseOrderHeader_Branch')
    ALTER TABLE PurchaseOrderHeader ADD CONSTRAINT FK_PurchaseOrderHeader_Branch FOREIGN KEY (BranchId) REFERENCES Branch(Id);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ReceivingHeader_Branch')
    ALTER TABLE ReceivingHeader ADD CONSTRAINT FK_ReceivingHeader_Branch FOREIGN KEY (BranchId) REFERENCES Branch(Id);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseInvoice_Branch')
    ALTER TABLE ExpenseInvoice ADD CONSTRAINT FK_ExpenseInvoice_Branch FOREIGN KEY (BranchId) REFERENCES Branch(Id);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SalesOrderHeader_Branch')
    ALTER TABLE SalesOrderHeader ADD CONSTRAINT FK_SalesOrderHeader_Branch FOREIGN KEY (BranchId) REFERENCES Branch(Id);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ShippingHeader_Branch')
    ALTER TABLE ShippingHeader ADD CONSTRAINT FK_ShippingHeader_Branch FOREIGN KEY (BranchId) REFERENCES Branch(Id);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SalesInvoice_Branch')
    ALTER TABLE SalesInvoice ADD CONSTRAINT FK_SalesInvoice_Branch FOREIGN KEY (BranchId) REFERENCES Branch(Id);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_StockMovement_Branch')
    ALTER TABLE StockMovement ADD CONSTRAINT FK_StockMovement_Branch FOREIGN KEY (BranchId) REFERENCES Branch(Id);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AccountMovement_Branch')
    ALTER TABLE AccountMovement ADD CONSTRAINT FK_AccountMovement_Branch FOREIGN KEY (BranchId) REFERENCES Branch(Id);
