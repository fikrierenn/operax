-- =============================================================================
-- schema_M04_ShipmentInvoiceMerge.sql — Plan 21: İrsaliye↔Fatura N:1 (M-F2.1/E1)
-- =============================================================================
-- Additive + geri uyumlu. İrsaliye=StockMovement, Fatura=AccountMovement (belge ayrımı).
-- InvoicedQty miktar-tabanlı (ERPNext billed_amt + Odoo qty_invoiced); boolean DEĞİL.
-- SourceShipmentLineId satır bazlı bağ (ERPNext dn_detail); N:1 birleştirme için.
-- IssueDate düzenleme tarihi (VUK md.231/5 7-gün; e-Fatura imza zamanı).
-- =============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ShippingLine.InvoicedQty — bu sevkiyat satırından ne kadar faturalandı (kısmi/N:1)
IF NOT EXISTS (SELECT 1 FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id
               WHERE t.name='ShippingLine' AND c.name='InvoicedQty')
    ALTER TABLE dbo.ShippingLine ADD InvoicedQty DECIMAL(18,6) NOT NULL DEFAULT 0;
GO

-- SalesInvoiceLine.SourceShipmentLineId — fatura satırının kaynağı (irsaliye satırı)
-- NULL = direkt fatura (irsaliyesiz; mallıysa StockMovement yazar, hizmetse yazmaz)
IF NOT EXISTS (SELECT 1 FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id
               WHERE t.name='SalesInvoiceLine' AND c.name='SourceShipmentLineId')
    ALTER TABLE dbo.SalesInvoiceLine ADD SourceShipmentLineId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_SalesInvoiceLine_ShipmentLine')
   AND EXISTS (SELECT 1 FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id
               WHERE t.name='SalesInvoiceLine' AND c.name='SourceShipmentLineId')
    ALTER TABLE dbo.SalesInvoiceLine
        ADD CONSTRAINT FK_SalesInvoiceLine_ShipmentLine
        FOREIGN KEY (SourceShipmentLineId) REFERENCES dbo.ShippingLine(Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SalesInvoiceLine_ShipmentLine')
    CREATE NONCLUSTERED INDEX IX_SalesInvoiceLine_ShipmentLine
        ON dbo.SalesInvoiceLine(SourceShipmentLineId) WHERE SourceShipmentLineId IS NOT NULL;
GO

-- SalesInvoice.IssueDate — düzenleme tarihi (VUK md.231/5; e-Fatura imza). InvoiceDate korunur.
IF NOT EXISTS (SELECT 1 FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id
               WHERE t.name='SalesInvoice' AND c.name='IssueDate')
    ALTER TABLE dbo.SalesInvoice ADD IssueDate DATETIME2 NULL;
GO

-- =============================================================================
-- BACKFILL: mevcut faturalı sevkiyatların InvoicedQty (çift-sayım önleme)
-- 1:1 dönüşmüş faturalar (SalesInvoice.ShippingId dolu) → o sevkiyatın tüm satırları faturalı.
-- Idempotent: yalnızca InvoicedQty=0 olan satırlara QtyBase yazar.
-- =============================================================================
UPDATE sl
SET sl.InvoicedQty = sl.QtyBase
FROM dbo.ShippingLine sl
JOIN dbo.SalesInvoice si ON si.ShippingId = sl.HeaderId
WHERE si.IsDeleted = 0 AND si.Status <> 'CANCELLED'
  AND sl.InvoicedQty = 0;
GO

-- IssueDate backfill: mevcut faturalarda InvoiceDate = IssueDate (geriye uyum)
UPDATE dbo.SalesInvoice SET IssueDate = InvoiceDate WHERE IssueDate IS NULL;
GO
