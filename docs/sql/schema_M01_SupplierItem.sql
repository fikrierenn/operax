-- ═══════════════════════════════════════════════════════════════════
-- Plan 32 — Tedarikçi-Ürün Kataloğu (SupplierItem)
-- Bir tedarikçinin sattığı ürünler: tedarikçi ürün kodu, teslim süresi,
-- asgari sipariş (MOQ), son alış fiyatı (BİLGİLENDİRİCİ — resolver'a girmez),
-- tercih edilen tedarikçi. Odoo product.supplierinfo deseni.
-- AYRIM: katalog = eşleme/lojistik; bağlayıcı fiyat PriceList'te (Plan 30).
-- ═══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SupplierItem')
BEGIN
    CREATE TABLE SupplierItem (
        Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId        UNIQUEIDENTIFIER NOT NULL,
        PartnerId        UNIQUEIDENTIFIER NOT NULL,         -- tedarikçi (cari)
        ItemId           UNIQUEIDENTIFIER NOT NULL,         -- ürün
        SupplierItemCode NVARCHAR(100) NULL,                -- tedarikçinin kendi ürün kodu
        SupplierItemName NVARCHAR(500) NULL,                -- tedarikçinin ürün adı
        LeadTimeDays     INT NULL,                          -- teslim süresi (gün)
        MinOrderQty      DECIMAL(18,6) NOT NULL DEFAULT 0,  -- asgari sipariş (MOQ)
        LastPrice        DECIMAL(18,4) NULL,                -- son alış fiyatı (BİLGİLENDİRİCİ)
        Currency         NVARCHAR(10) NOT NULL DEFAULT 'TRY',
        IsPreferred      BIT NOT NULL DEFAULT 0,            -- tercih edilen tedarikçi
        IsDeleted        BIT NOT NULL DEFAULT 0,
        CreatedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy        NVARCHAR(450) NULL,
        UpdatedAt        DATETIME2 NULL,
        UpdatedBy        NVARCHAR(450) NULL,
        CONSTRAINT FK_SupplierItem_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_SupplierItem_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id),
        CONSTRAINT FK_SupplierItem_Item    FOREIGN KEY (ItemId)    REFERENCES Item(Id)
    );
    PRINT 'SupplierItem tablosu olusturuldu.';
END
GO

-- Bir tedarikçi-ürün tek satır (silinmemişler arası)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SupplierItem_PartnerItem')
BEGIN
    CREATE UNIQUE INDEX UX_SupplierItem_PartnerItem
        ON SupplierItem(CompanyId, PartnerId, ItemId) WHERE IsDeleted = 0;
    PRINT 'UX_SupplierItem_PartnerItem index olusturuldu.';
END
GO

-- Bir ürüne yalnız BİR tercih edilen tedarikçi (filtered unique)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SupplierItem_Preferred')
BEGIN
    CREATE UNIQUE INDEX UX_SupplierItem_Preferred
        ON SupplierItem(CompanyId, ItemId) WHERE IsPreferred = 1 AND IsDeleted = 0;
    PRINT 'UX_SupplierItem_Preferred index olusturuldu.';
END
GO

-- Ürüne göre tedarikçi okuma (Item kartı grid) için kapsayan index
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupplierItem_Item')
BEGIN
    CREATE INDEX IX_SupplierItem_Item
        ON SupplierItem(CompanyId, ItemId) WHERE IsDeleted = 0;
    PRINT 'IX_SupplierItem_Item index olusturuldu.';
END
GO

-- Toplu giriş TVP (grid bir ürünün TÜM tedarikçilerini yönetir; @ItemId ayrı parametre)
IF NOT EXISTS (SELECT 1 FROM sys.types WHERE name = 'SupplierItemTVP' AND is_table_type = 1)
BEGIN
    CREATE TYPE dbo.SupplierItemTVP AS TABLE (
        RowNo            INT,
        PartnerId        UNIQUEIDENTIFIER,
        SupplierItemCode NVARCHAR(100),
        SupplierItemName NVARCHAR(500),
        LeadTimeDays     INT,
        MinOrderQty      DECIMAL(18,6),
        LastPrice        DECIMAL(18,4),
        Currency         NVARCHAR(10),
        IsPreferred      BIT
    );
    PRINT 'SupplierItemTVP table type olusturuldu.';
END
GO
