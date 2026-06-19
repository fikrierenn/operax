-- =============================================================================
-- STARTER paketi için Item, Partner, ShippingHeader tablolarına eksik kolonlar
-- Hepsi idempotent (IF NOT EXISTS koruması) — birden çok kez güvenle çalışır
-- =============================================================================
SET NOCOUNT ON;

-- ─── Item: satış fiyatı, KDV, tip ──────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'SalesPrice' AND Object_ID = OBJECT_ID('Item'))
BEGIN
    ALTER TABLE Item ADD SalesPrice DECIMAL(18,4) NULL;
    PRINT 'Item.SalesPrice kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'TaxRate' AND Object_ID = OBJECT_ID('Item'))
BEGIN
    ALTER TABLE Item ADD TaxRate DECIMAL(8,4) DEFAULT 20;
    PRINT 'Item.TaxRate kolonu eklendi (varsayilan %20).';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'ItemType' AND Object_ID = OBJECT_ID('Item'))
BEGIN
    ALTER TABLE Item ADD ItemType NVARCHAR(20) NOT NULL DEFAULT 'STOCK';
    -- STOCK (stoklu urun), SERVICE (hizmet), FIXED_ASSET (sabit kiymet)
    PRINT 'Item.ItemType kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CostingMethod' AND Object_ID = OBJECT_ID('Item'))
BEGIN
    ALTER TABLE Item ADD CostingMethod NVARCHAR(20) NULL;
    -- NULL = sirket geneli parametre, override edilecekse MOVING_AVG/FIFO/STANDARD
    PRINT 'Item.CostingMethod kolonu eklendi.';
END
GO
-- Description: ürün açıklaması (fresh-install'da eksikti — schema_all/M01 Item'da yok;
-- db_objects_udf.sql backfill + Items/Details buna bağlı). Plan 34 sonrası gerçek açıklama kolonu.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Description' AND Object_ID = OBJECT_ID('Item'))
BEGIN
    ALTER TABLE Item ADD Description NVARCHAR(MAX) NULL;
    PRINT 'Item.Description kolonu eklendi.';
END
GO

-- ─── Partner: ödeme vadesi, kredi limiti, vergi numarası ───────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PaymentTermDays' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD PaymentTermDays INT DEFAULT 30;
    PRINT 'Partner.PaymentTermDays kolonu eklendi (varsayilan 30 gun).';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CreditLimit' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD CreditLimit DECIMAL(18,2) DEFAULT 0;
    -- 0 = sinirsiz
    PRINT 'Partner.CreditLimit kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'BlockOnLimitExceed' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD BlockOnLimitExceed BIT DEFAULT 0;
    PRINT 'Partner.BlockOnLimitExceed kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'AutoClosePayments' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD AutoClosePayments BIT DEFAULT 1;
    -- 1 = gelen tahsilat / yapilan odeme PaymentPlan'lara FIFO otomatik dagilir
    -- 0 = kullanici manuel kapatma yapmali
    PRINT 'Partner.AutoClosePayments kolonu eklendi (varsayilan 1).';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'TaxNumber' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD TaxNumber NVARCHAR(50) NULL;
    PRINT 'Partner.TaxNumber kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'TaxOffice' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD TaxOffice NVARCHAR(100) NULL;
    PRINT 'Partner.TaxOffice kolonu eklendi (vergi dairesi).';
END
GO

-- ─── ShippingHeader: müşteri referansı + post zaman damgası ────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PartnerId' AND Object_ID = OBJECT_ID('ShippingHeader'))
BEGIN
    ALTER TABLE ShippingHeader ADD PartnerId UNIQUEIDENTIFIER NULL;
    -- Multi-SO sevkiyat: ilk satirdan turetilir; tek-SO sevkiyat: zorunlu
    PRINT 'ShippingHeader.PartnerId kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'SalesOrderId' AND Object_ID = OBJECT_ID('ShippingHeader'))
BEGIN
    ALTER TABLE ShippingHeader ADD SalesOrderId UNIQUEIDENTIFIER NULL;
    PRINT 'ShippingHeader.SalesOrderId kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PostedAt' AND Object_ID = OBJECT_ID('ShippingHeader'))
BEGIN
    ALTER TABLE ShippingHeader ADD PostedAt DATETIME2 NULL;
    PRINT 'ShippingHeader.PostedAt kolonu eklendi.';
END
GO

-- ─── PurchaseOrderHeader: vade + döviz ─────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PaymentTermDays' AND Object_ID = OBJECT_ID('PurchaseOrderHeader'))
BEGIN
    ALTER TABLE PurchaseOrderHeader ADD PaymentTermDays INT DEFAULT 30;
    PRINT 'PurchaseOrderHeader.PaymentTermDays kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PaymentInstallments' AND Object_ID = OBJECT_ID('PurchaseOrderHeader'))
BEGIN
    ALTER TABLE PurchaseOrderHeader ADD PaymentInstallments INT DEFAULT 1;
    PRINT 'PurchaseOrderHeader.PaymentInstallments kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Currency' AND Object_ID = OBJECT_ID('PurchaseOrderHeader'))
BEGIN
    ALTER TABLE PurchaseOrderHeader ADD Currency NVARCHAR(10) DEFAULT 'TRY';
    PRINT 'PurchaseOrderHeader.Currency kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'ExchangeRate' AND Object_ID = OBJECT_ID('PurchaseOrderHeader'))
BEGIN
    ALTER TABLE PurchaseOrderHeader ADD ExchangeRate DECIMAL(18,6) DEFAULT 1;
    PRINT 'PurchaseOrderHeader.ExchangeRate kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PostedAt' AND Object_ID = OBJECT_ID('PurchaseOrderHeader'))
BEGIN
    ALTER TABLE PurchaseOrderHeader ADD PostedAt DATETIME2 NULL;
    PRINT 'PurchaseOrderHeader.PostedAt kolonu eklendi.';
END
GO

-- ─── SalesOrderHeader: aynı kolonlar ───────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PaymentTermDays' AND Object_ID = OBJECT_ID('SalesOrderHeader'))
BEGIN
    ALTER TABLE SalesOrderHeader ADD PaymentTermDays INT DEFAULT 30;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PaymentInstallments' AND Object_ID = OBJECT_ID('SalesOrderHeader'))
BEGIN
    ALTER TABLE SalesOrderHeader ADD PaymentInstallments INT DEFAULT 1;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Currency' AND Object_ID = OBJECT_ID('SalesOrderHeader'))
BEGIN
    ALTER TABLE SalesOrderHeader ADD Currency NVARCHAR(10) DEFAULT 'TRY';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PostedAt' AND Object_ID = OBJECT_ID('SalesOrderHeader'))
BEGIN
    ALTER TABLE SalesOrderHeader ADD PostedAt DATETIME2 NULL;
END
GO

-- ─── Parameter tablosunda InvoiceMode varsayılan ───────────────────
INSERT INTO Parameter (Id, CompanyId, ModuleCode, Code, Value, Description, CreatedAt)
SELECT NEWID(), c.Id, 'M04', 'InvoiceMode', 'INSTANT',
       N'Sevkiyat onaylaninca otomatik mi fatura kesilsin (INSTANT) yoksa gun sonu toplu mu (EOD_FROM_SHIPMENT)?',
       GETUTCDATE()
FROM Company c
WHERE NOT EXISTS (
    SELECT 1 FROM Parameter p
    WHERE p.CompanyId = c.Id AND p.Code = 'InvoiceMode'
);
GO

INSERT INTO Parameter (Id, CompanyId, ModuleCode, Code, Value, Description, CreatedAt)
SELECT NEWID(), c.Id, 'M02', 'CostingMethod', 'MOVING_AVG',
       N'Maliyetlendirme yontemi: MOVING_AVG (varsayilan), FIFO, STANDARD',
       GETUTCDATE()
FROM Company c
WHERE NOT EXISTS (
    SELECT 1 FROM Parameter p
    WHERE p.CompanyId = c.Id AND p.Code = 'CostingMethod'
);
GO

-- NOT (Plan 27): PriceTolerancePercent KALDIRILDI — fiyat farkında tolerans YOK ilkesi.
-- Liste/PO fiyatından her sapma variance üretir (satınalmacı gerekçelendirir). Param artık seed edilmez.
