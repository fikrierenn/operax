-- Plan 05 Faz 1: Belge zinciri altyapı şema değişiklikleri
-- Idempotent (IF COL_LENGTH IS NULL korumalı)

-- ExpenseInvoice: kaynak Receiving bağlantısı
IF COL_LENGTH('ExpenseInvoice', 'ReceivingId') IS NULL
    ALTER TABLE ExpenseInvoice ADD ReceivingId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseInvoice_Receiving')
    ALTER TABLE ExpenseInvoice
    ADD CONSTRAINT FK_ExpenseInvoice_Receiving
    FOREIGN KEY (ReceivingId) REFERENCES ReceivingHeader(Id);

-- ShippingHeader: kaynak SO bağlantısı (zaten var mı kontrol)
IF COL_LENGTH('ShippingHeader', 'SalesOrderId') IS NULL
    ALTER TABLE ShippingHeader ADD SalesOrderId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ShippingHeader_SalesOrder')
    ALTER TABLE ShippingHeader
    ADD CONSTRAINT FK_ShippingHeader_SalesOrder
    FOREIGN KEY (SalesOrderId) REFERENCES SalesOrderHeader(Id);
