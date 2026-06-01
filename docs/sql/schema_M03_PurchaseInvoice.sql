-- Plan 24 Faz A: Mal Alım Faturası (PurchaseInvoice) şema
-- Idempotent (IF NOT EXISTS / COL_LENGTH korumalı)
-- Filtered unique index için QUOTED_IDENTIFIER + ANSI_NULLS ON zorunlu
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ItemType değer kümesi genişletme: CONSUMABLE eklendi (kolon zaten var, yeni kolon yok)
-- Mevcut değerler: STOCK / SERVICE / FIXED_ASSET → + CONSUMABLE (sarf malzeme)
-- CHECK constraint yoksa eklemiyoruz (dropdown + UI katmanı doğrular); mevcut STOCK korunur.

-- Mal Alım Faturası başlık
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchaseInvoice')
BEGIN
    CREATE TABLE PurchaseInvoice (
        Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId           UNIQUEIDENTIFIER NOT NULL,
        PartnerId           UNIQUEIDENTIFIER NOT NULL,        -- Tedarikçi
        BranchId            UNIQUEIDENTIFIER NULL,            -- Plan 23 şube boyutu
        DocNo               NVARCHAR(50)     NOT NULL,        -- Operax iç sistem no (ALN-...)
        -- Tedarikçinin yasal belge kimliği (Mikro cha_belge_no/cha_belge_tarih/cha_uuid)
        SupplierInvoiceNo   NVARCHAR(50)     NOT NULL,        -- tedarikçi fatura no
        SupplierInvoiceDate DATE             NOT NULL,        -- tedarikçi belge tarihi (KDV/BA-BS)
        SupplierInvoiceUuid NVARCHAR(40)     NULL,            -- e-Fatura ETTN/UUID (M16 inbound, sonra)
        InvoiceDate         DATE             NOT NULL,        -- kayıt/işlem tarihi
        DueDate             DATE             NULL,
        Subtotal            DECIMAL(18,4)    NOT NULL DEFAULT 0,
        TaxAmount           DECIMAL(18,4)    NOT NULL DEFAULT 0,
        GrandTotal          DECIMAL(18,4)    NOT NULL DEFAULT 0,
        PaidAmount          DECIMAL(18,4)    NOT NULL DEFAULT 0,
        Currency            NVARCHAR(10)     NOT NULL DEFAULT 'TRY',
        ExchangeRate        DECIMAL(18,6)    NOT NULL DEFAULT 1,  -- dövizli faturada TL karşılığı kuru
        Status              NVARCHAR(20)     NOT NULL DEFAULT 'DRAFT',  -- DRAFT/POSTED/CANCELLED
        CreatedAt           DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy           UNIQUEIDENTIFIER NULL,
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           UNIQUEIDENTIFIER NULL,
        CONSTRAINT FK_PurchaseInvoice_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_PurchaseInvoice_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id),
        CONSTRAINT FK_PurchaseInvoice_Branch  FOREIGN KEY (BranchId)  REFERENCES Branch(Id)
    );
    CREATE INDEX IX_PurchaseInvoice_Company ON PurchaseInvoice(CompanyId, Status);
    CREATE INDEX IX_PurchaseInvoice_Partner ON PurchaseInvoice(CompanyId, PartnerId);
    -- Mükerrer tedarikçi faturası koruması (aynı tedarikçi + aynı belge no tek kayıt)
    CREATE UNIQUE INDEX UIX_PurchaseInvoice_SupplierNo
        ON PurchaseInvoice(CompanyId, PartnerId, SupplierInvoiceNo)
        WHERE SupplierInvoiceNo IS NOT NULL;
END

-- Mal Alım Faturası satır
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchaseInvoiceLine')
BEGIN
    CREATE TABLE PurchaseInvoiceLine (
        Id                   UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        InvoiceId            UNIQUEIDENTIFIER NOT NULL,
        ItemId               UNIQUEIDENTIFIER NOT NULL,
        UomId                UNIQUEIDENTIFIER NOT NULL,
        Qty                  DECIMAL(18,6)    NOT NULL,
        UnitPrice            DECIMAL(18,4)    NOT NULL DEFAULT 0,
        LineSubtotal         DECIMAL(18,4)    NOT NULL DEFAULT 0,
        TaxRatePercent       DECIMAL(5,2)     NOT NULL DEFAULT 20,
        TaxAmount            DECIMAL(18,4)    NOT NULL DEFAULT 0,
        LineTotal            DECIMAL(18,4)    NOT NULL DEFAULT 0,
        -- Satır-bazlı mal kabul bağı (N:1 — ay-sonu dönemsel fatura)
        SourceReceivingLineId UNIQUEIDENTIFIER NULL,
        SourceLinkType       NVARCHAR(20)     NOT NULL DEFAULT 'LINKED',  -- LINKED/UNLINKED
        CreatedAt            DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_PurchaseInvoiceLine_Invoice FOREIGN KEY (InvoiceId) REFERENCES PurchaseInvoice(Id),
        CONSTRAINT FK_PurchaseInvoiceLine_Item    FOREIGN KEY (ItemId)    REFERENCES Item(Id),
        CONSTRAINT FK_PurchaseInvoiceLine_RcvLine FOREIGN KEY (SourceReceivingLineId) REFERENCES ReceivingLine(Id)
    );
    CREATE INDEX IX_PurchaseInvoiceLine_Invoice ON PurchaseInvoiceLine(InvoiceId);
    CREATE INDEX IX_PurchaseInvoiceLine_RcvLine ON PurchaseInvoiceLine(SourceReceivingLineId);
END
