-- =============================================================================
-- M04 — Satis Faturasi (SalesInvoice + Line)
-- Shipping POSTED'da Parameter InvoiceMode=INSTANT ise otomatik uretilir.
-- =============================================================================
SET NOCOUNT ON;

-- ─── SalesInvoice ──────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SalesInvoice')
BEGIN
    CREATE TABLE SalesInvoice (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId       UNIQUEIDENTIFIER NOT NULL,
        InvoiceNo       NVARCHAR(50)  NOT NULL,            -- INV-2026-0001
        InvoiceDate     DATETIME2 DEFAULT GETUTCDATE(),
        DueDate         DATE NULL,
        PartnerId       UNIQUEIDENTIFIER NOT NULL,         -- musteri
        ShippingId      UNIQUEIDENTIFIER NULL,             -- kaynak sevkiyat (varsa)
        SalesOrderId    UNIQUEIDENTIFIER NULL,             -- kaynak satis siparisi (yedek)
        Subtotal        DECIMAL(18,2) NOT NULL,
        TaxAmount       DECIMAL(18,2) NOT NULL,
        GrandTotal      DECIMAL(18,2) NOT NULL,
        PaidAmount      DECIMAL(18,2) DEFAULT 0,
        Currency        NVARCHAR(10) DEFAULT 'TRY',
        Status          NVARCHAR(20) DEFAULT 'DRAFT',      -- DRAFT, POSTED, PAID, CANCELLED
        EInvoiceUUID    NVARCHAR(50) NULL,                 -- e-Fatura UUID (Phase 2)
        Notes           NVARCHAR(MAX) NULL,
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy       UNIQUEIDENTIFIER,
        UpdatedAt       DATETIME2 NULL,
        UpdatedBy       UNIQUEIDENTIFIER,
        IsDeleted       BIT DEFAULT 0,
        CONSTRAINT FK_SalesInvoice_Company  FOREIGN KEY (CompanyId)    REFERENCES Company(Id),
        CONSTRAINT FK_SalesInvoice_Partner  FOREIGN KEY (PartnerId)    REFERENCES Partner(Id),
        CONSTRAINT FK_SalesInvoice_Shipping FOREIGN KEY (ShippingId)   REFERENCES ShippingHeader(Id),
        CONSTRAINT FK_SalesInvoice_SO       FOREIGN KEY (SalesOrderId) REFERENCES SalesOrderHeader(Id)
    );
    CREATE UNIQUE INDEX UX_SalesInvoice_No ON SalesInvoice(CompanyId, InvoiceNo) WHERE IsDeleted = 0;
    CREATE INDEX IX_SalesInvoice_Status   ON SalesInvoice(CompanyId, Status, InvoiceDate) WHERE IsDeleted = 0;
    CREATE INDEX IX_SalesInvoice_Partner  ON SalesInvoice(CompanyId, PartnerId);
    PRINT 'SalesInvoice tablosu olusturuldu.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SalesInvoiceLine')
BEGIN
    CREATE TABLE SalesInvoiceLine (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        InvoiceId       UNIQUEIDENTIFIER NOT NULL,
        ItemId          UNIQUEIDENTIFIER NOT NULL,
        UomId           UNIQUEIDENTIFIER NOT NULL,
        Description     NVARCHAR(500) NULL,
        Qty             DECIMAL(18,6) NOT NULL,
        UnitPrice       DECIMAL(18,4) NOT NULL,
        DiscountPercent DECIMAL(8,4)  DEFAULT 0,
        DiscountAmount  DECIMAL(18,2) DEFAULT 0,
        LineSubtotal    DECIMAL(18,2) NOT NULL,             -- (Qty*UnitPrice) - Discount
        TaxRatePercent  DECIMAL(8,4)  DEFAULT 20,
        TaxAmount       DECIMAL(18,2) NOT NULL,
        LineTotal       DECIMAL(18,2) NOT NULL,             -- LineSubtotal + TaxAmount
        UnitCost        DECIMAL(18,4) NULL,                 -- maliyet (COGS hesabi icin)
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_SIL_Invoice FOREIGN KEY (InvoiceId) REFERENCES SalesInvoice(Id),
        CONSTRAINT FK_SIL_Item    FOREIGN KEY (ItemId)    REFERENCES Item(Id)
    );
    CREATE INDEX IX_SIL_Invoice ON SalesInvoiceLine(InvoiceId);
    PRINT 'SalesInvoiceLine tablosu olusturuldu.';
END
GO
