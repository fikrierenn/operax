-- Plan 05 Faz 1: Belge zinciri altyapı şema değişiklikleri
-- Idempotent (IF COL_LENGTH IS NULL korumalı)

-- ExpenseInvoice: kaynak Receiving bağlantısı
IF COL_LENGTH('ExpenseInvoice', 'ReceivingId') IS NULL
    ALTER TABLE ExpenseInvoice ADD ReceivingId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseInvoice_Receiving')
    ALTER TABLE ExpenseInvoice
    ADD CONSTRAINT FK_ExpenseInvoice_Receiving
    FOREIGN KEY (ReceivingId) REFERENCES ReceivingHeader(Id);

-- ExpenseInvoice: ReceivingId başına en fazla 1 aktif fatura (çift-belge koruması)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'UIX_ExpenseInvoice_Receiving_Active'
)
    CREATE UNIQUE INDEX UIX_ExpenseInvoice_Receiving_Active
    ON ExpenseInvoice(ReceivingId)
    WHERE ReceivingId IS NOT NULL AND Status <> 'CANCELLED';

-- ShippingHeader: kaynak SO bağlantısı (zaten var mı kontrol)
IF COL_LENGTH('ShippingHeader', 'SalesOrderId') IS NULL
    ALTER TABLE ShippingHeader ADD SalesOrderId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ShippingHeader_SalesOrder')
    ALTER TABLE ShippingHeader
    ADD CONSTRAINT FK_ShippingHeader_SalesOrder
    FOREIGN KEY (SalesOrderId) REFERENCES SalesOrderHeader(Id);
