-- =============================================================================
-- M02 — Maliyetlendirme (Item Cost — Moving Average)
-- StockMovement.UnitCost kolonu eklenir; her Receiving POSTED sonrasi
-- ItemCost guncellenir, Shipping ISSUE'da satis maliyeti yazilir.
-- =============================================================================
SET NOCOUNT ON;

-- ─── ItemCost ──────────────────────────────────────────────────────
-- Her urun icin (warehouse bazli) gecerli ortalama maliyet ve eldeki adet
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ItemCost')
BEGIN
    CREATE TABLE ItemCost (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        ItemId      UNIQUEIDENTIFIER NOT NULL,
        WarehouseId UNIQUEIDENTIFIER NULL,            -- NULL = sirket geneli (basit mod)
        AvgCost     DECIMAL(18,4) NOT NULL DEFAULT 0,
        OnHandQty   DECIMAL(18,6) NOT NULL DEFAULT 0,
        LastReceiptDate DATETIME2 NULL,
        UpdatedAt   DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_ItemCost_Company   FOREIGN KEY (CompanyId)   REFERENCES Company(Id),
        CONSTRAINT FK_ItemCost_Item      FOREIGN KEY (ItemId)      REFERENCES Item(Id),
        CONSTRAINT FK_ItemCost_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id)
    );
    CREATE UNIQUE INDEX UX_ItemCost_Key ON ItemCost(CompanyId, ItemId, WarehouseId);
    PRINT 'ItemCost tablosu olusturuldu.';
END
GO

-- ─── StockMovement.UnitCost kolonu ─────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'UnitCost' AND Object_ID = OBJECT_ID('StockMovement'))
BEGIN
    ALTER TABLE StockMovement ADD UnitCost DECIMAL(18,4) NULL;
    PRINT 'StockMovement.UnitCost kolonu eklendi.';
END
GO

-- ─── PriceVariance (Fiyat Farki) ───────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PriceVariance')
BEGIN
    CREATE TABLE PriceVariance (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId       UNIQUEIDENTIFIER NOT NULL,
        SourceDocType   NVARCHAR(50) NOT NULL,        -- PURCHASE_ORDER, SALES_ORDER
        SourceDocId     UNIQUEIDENTIFIER NOT NULL,
        SourceLineId    UNIQUEIDENTIFIER NOT NULL,
        ItemId          UNIQUEIDENTIFIER NOT NULL,
        PartnerId       UNIQUEIDENTIFIER NULL,
        ExpectedPrice   DECIMAL(18,4) NOT NULL,        -- fiyat listesinden gelen
        ActualPrice     DECIMAL(18,4) NOT NULL,        -- kullanicinin girdigi
        Variance        DECIMAL(18,4) NOT NULL,        -- ActualPrice - ExpectedPrice
        VariancePercent DECIMAL(8,4) NOT NULL,
        Status          NVARCHAR(20) DEFAULT 'DRAFT', -- DRAFT, APPROVED, REJECTED
        ApprovedBy      UNIQUEIDENTIFIER NULL,
        ApprovedAt      DATETIME2 NULL,
        Notes           NVARCHAR(MAX) NULL,
        IsDeleted       BIT DEFAULT 0,
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy       UNIQUEIDENTIFIER,
        CONSTRAINT FK_PriceVariance_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_PriceVariance_Item    FOREIGN KEY (ItemId)    REFERENCES Item(Id),
        CONSTRAINT FK_PriceVariance_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id)
    );
    CREATE INDEX IX_PriceVariance_Status ON PriceVariance(CompanyId, Status) WHERE IsDeleted = 0;
    CREATE INDEX IX_PriceVariance_Source ON PriceVariance(SourceDocType, SourceDocId);
    PRINT 'PriceVariance tablosu olusturuldu.';
END
GO

-- Defense-in-depth (sql-sp-reviewer IMP-2): kaynak satır başına TEK aktif fiyat farkı.
-- POST/Correct yolunda mükerrer DRAFT variance birikmesini DB seviyesinde imkânsız kılar.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PriceVariance_Source' AND object_id = OBJECT_ID('PriceVariance'))
    CREATE UNIQUE INDEX UX_PriceVariance_Source
        ON PriceVariance(SourceDocType, SourceDocId, SourceLineId) WHERE IsDeleted = 0;
GO

-- ─── PriceList genisleme: PartnerId + ValidFrom/To ─────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PartnerId' AND Object_ID = OBJECT_ID('PriceList'))
BEGIN
    ALTER TABLE PriceList ADD PartnerId UNIQUEIDENTIFIER NULL;      -- NULL = genel liste
    ALTER TABLE PriceList ADD ValidFrom DATE NULL;
    ALTER TABLE PriceList ADD ValidTo   DATE NULL;
    ALTER TABLE PriceList ADD Direction NVARCHAR(10) DEFAULT 'SALES'; -- SALES (musteri), PURCHASE (tedarikci)
    PRINT 'PriceList tablosu Partner+Tarih+Direction kolonlari ile genisletildi.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Currency' AND Object_ID = OBJECT_ID('PriceListLine'))
BEGIN
    ALTER TABLE PriceListLine ADD Currency NVARCHAR(10) DEFAULT 'TRY';
    PRINT 'PriceListLine.Currency kolonu eklendi.';
END
GO
