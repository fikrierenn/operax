-- Plan 28 Faz C — Toplu (BULK) mal kabul satırlarının alış faturası aşamasında PO satırlarına tahsisi.
-- Idempotent. Skill denetimi: competitor (FIFO öner+override, SAP GRPO hizası) + mali-evrak (kısmi faturalama VUK ✓).

-- PurchaseInvoiceLine: fatura satırının tahsis edildiği PO satırı (3-way match + fiyat kaynağı).
-- ŞU AN YOK — yalnız SourceReceivingLineId vardı. Tahsis sonrası hangi siparişe bağlandığı buradan izlenir.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='PurchaseOrderLineId' AND Object_ID=OBJECT_ID('PurchaseInvoiceLine'))
BEGIN
    ALTER TABLE PurchaseInvoiceLine ADD PurchaseOrderLineId UNIQUEIDENTIFIER NULL;
    PRINT 'PurchaseInvoiceLine.PurchaseOrderLineId eklendi.';
END
GO

-- ReceivingLine: kümülatif faturalanan miktar (kısmi faturalama — 1 kabul → N alış faturası).
-- Plan 21 ShippingLine.InvoicedQty aynası. Validasyon: SUM InvoicedQty <= QtyBase.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='InvoicedQty' AND Object_ID=OBJECT_ID('ReceivingLine'))
BEGIN
    ALTER TABLE ReceivingLine ADD InvoicedQty DECIMAL(18,6) NOT NULL DEFAULT 0;
    PRINT 'ReceivingLine.InvoicedQty eklendi (kısmi faturalama).';
END
GO

-- GRNI faturasız mal kabul yaşlandırma eşiği (gün) — kod içinde sabit DEĞİL, parametrik.
-- Her şirkete varsayılan 30; Admin > Parametreler ekranından değiştirilebilir.
INSERT INTO Parameter (Id, CompanyId, ModuleCode, Code, Value, Description)
SELECT NEWID(), c.Id, 'M03', 'INVOICE_AGING_DAYS', '30',
       N'Faturasız mal kabul kaç gün sonra "geciken" sayılır (GRNI yaşlandırma eşiği).'
FROM Company c
WHERE NOT EXISTS (
    SELECT 1 FROM Parameter p
    WHERE p.CompanyId = c.Id AND p.Code = 'INVOICE_AGING_DAYS' AND p.IsDeleted = 0);
GO
