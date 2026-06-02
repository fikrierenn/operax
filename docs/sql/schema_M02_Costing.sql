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

-- ═══════════════════════════════════════════════════════════════════
-- Plan 30 — PriceList kapsam boyutları (şube + öncelik + zincir iskonto)
-- Karar: CARİ BASKIN (MatchScore = Partner×2 + Branch×1). Satış = Alış.
-- ═══════════════════════════════════════════════════════════════════

-- ─── Direction sertlestirme: eski satirlar NULL kalmis olabilir (ADD DEFAULT
-- nullable kolonu mevcut satirlara backfill etmez) → resolver Direction filtresi
-- bu satirlari sessizce kacirirdi. Backfill + NOT NULL + CHECK ile invariant garanti.
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Direction' AND Object_ID = OBJECT_ID('PriceList'))
   AND EXISTS (SELECT 1 FROM PriceList WHERE Direction IS NULL)
BEGIN
    UPDATE PriceList SET Direction = 'SALES' WHERE Direction IS NULL;
    PRINT 'PriceList.Direction NULL satirlari SALES olarak backfill edildi.';
END
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE Name = 'Direction' AND Object_ID = OBJECT_ID('PriceList') AND is_nullable = 1)
BEGIN
    ALTER TABLE PriceList ALTER COLUMN Direction NVARCHAR(10) NOT NULL;
    PRINT 'PriceList.Direction NOT NULL yapildi.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PriceList_Direction')
BEGIN
    ALTER TABLE PriceList ADD CONSTRAINT CK_PriceList_Direction
        CHECK (Direction IN ('SALES', 'PURCHASE'));
    PRINT 'CK_PriceList_Direction eklendi.';
END
GO

-- ─── PriceList genisleme: şube boyutu + öncelik + audit + soft-delete ──
-- BranchId NULL = tüm şubeler (genel). Priority = kullanıcı tanımlı tie-break (ERPNext deseni).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'BranchId' AND Object_ID = OBJECT_ID('PriceList'))
BEGIN
    ALTER TABLE PriceList ADD BranchId UNIQUEIDENTIFIER NULL;   -- NULL = tüm şubeler
    PRINT 'PriceList.BranchId kolonu eklendi.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Priority' AND Object_ID = OBJECT_ID('PriceList'))
BEGIN
    ALTER TABLE PriceList ADD Priority INT NOT NULL CONSTRAINT DF_PriceList_Priority DEFAULT 0;
    PRINT 'PriceList.Priority kolonu eklendi.';
END
GO

-- Audit kolonları (UpdatedBy zaten migration_add_updatedby ile var)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CreatedAt' AND Object_ID = OBJECT_ID('PriceList'))
BEGIN
    ALTER TABLE PriceList ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PriceList_CreatedAt DEFAULT GETUTCDATE();
    ALTER TABLE PriceList ADD CreatedBy NVARCHAR(450) NULL;
    ALTER TABLE PriceList ADD UpdatedAt DATETIME2 NULL;
    PRINT 'PriceList audit kolonlari (CreatedAt/By, UpdatedAt) eklendi.';
END
GO

-- Soft-delete (CRUD ekrani icin gerekli, sql-conventions zorunlu kolon)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsDeleted' AND Object_ID = OBJECT_ID('PriceList'))
BEGIN
    ALTER TABLE PriceList ADD IsDeleted BIT NOT NULL CONSTRAINT DF_PriceList_IsDeleted DEFAULT 0;
    PRINT 'PriceList.IsDeleted kolonu eklendi.';
END
GO

-- BranchId FK (kolon eklendikten sonra)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PriceList_Branch')
   AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Branch')
BEGIN
    ALTER TABLE PriceList ADD CONSTRAINT FK_PriceList_Branch
        FOREIGN KEY (BranchId) REFERENCES Branch(Id);
    PRINT 'FK_PriceList_Branch eklendi.';
END
GO

-- ─── PriceListLine: satir tipi (sabit fiyat / sadece iskonto) + soft-delete ──
-- FIXED   = UnitPrice brüt baz fiyat; iskonto zinciri (varsa) üstüne uygulanır.
-- DISCOUNT= UnitPrice yine baz; bu tip sadece iskonto kademeleri tasiyan satir icin etiket.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'LineType' AND Object_ID = OBJECT_ID('PriceListLine'))
BEGIN
    ALTER TABLE PriceListLine ADD LineType NVARCHAR(10) NOT NULL
        CONSTRAINT DF_PriceListLine_LineType DEFAULT 'FIXED';
    PRINT 'PriceListLine.LineType kolonu eklendi.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PriceListLine_LineType')
BEGIN
    ALTER TABLE PriceListLine ADD CONSTRAINT CK_PriceListLine_LineType
        CHECK (LineType IN ('FIXED', 'DISCOUNT'));
    PRINT 'CK_PriceListLine_LineType eklendi.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsDeleted' AND Object_ID = OBJECT_ID('PriceListLine'))
BEGIN
    ALTER TABLE PriceListLine ADD IsDeleted BIT NOT NULL CONSTRAINT DF_PriceListLine_IsDeleted DEFAULT 0;
    PRINT 'PriceListLine.IsDeleted kolonu eklendi.';
END
GO

-- ─── Zincir iskonto kademeleri (normalize child tablo) ────────────────
-- Karar 3: denormalize Mikro-slot REDDEDİLDİ; sınırsız sıralı kademe.
-- "10+5+3" → 3 satır (Seq 1/2/3). Zincir ardışık uygulanır (toplanmaz):
-- 100 → 90 → 85,5 → 82,935. Hesap Faz B tvf'inde (recursive CTE, tam DECIMAL).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PriceListLineDiscount')
BEGIN
    CREATE TABLE PriceListLineDiscount (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        LineId      UNIQUEIDENTIFIER NOT NULL,
        Seq         INT NOT NULL,                 -- uygulama sırası (1'den başlar)
        Pct         DECIMAL(9,4) NOT NULL,        -- iskonto yüzdesi (0–100)
        CONSTRAINT FK_PLLineDiscount_Line FOREIGN KEY (LineId)
            REFERENCES PriceListLine(Id),
        CONSTRAINT CK_PLLineDiscount_Pct CHECK (Pct >= 0 AND Pct <= 100),
        CONSTRAINT UQ_PLLineDiscount_LineSeq UNIQUE (LineId, Seq)
    );
    PRINT 'PriceListLineDiscount tablosu olusturuldu.';
END
GO
