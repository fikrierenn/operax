-- =============================================================================
-- UDF (Dinamik Kullanıcı Tanımlı Alanlar) — Plan 34
-- UserFieldDefinition metadata tablosu + Item ek kolonları (AdditionalFields,
-- MinStockLevel, MaxStockLevel). Hepsi idempotent (IF NOT EXISTS) — birden çok
-- kez güvenle çalışır.
-- =============================================================================
SET NOCOUNT ON;

-- ─── UserFieldDefinition: hangi entity'de hangi dinamik alan tanımlı ─────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserFieldDefinition')
BEGIN
    CREATE TABLE UserFieldDefinition (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId       UNIQUEIDENTIFIER NOT NULL,
        EntityName      NVARCHAR(100) NOT NULL,    -- 'Item' | 'Partner' | ...
        FieldName       NVARCHAR(100) NOT NULL,    -- JSON anahtarı (örn. 'Volume')
        LabelText       NVARCHAR(200) NOT NULL,    -- UI etiketi (örn. 'Hacim')
        FieldType       NVARCHAR(20)  NOT NULL,    -- TEXT | NUMBER | SELECT (Faz 2: DATE, BOOLEAN)
        DataSourceType  NVARCHAR(20)  NULL,        -- STATIC (Faz 2: DICTIONARY, TABLE)
        DataSourceKey   NVARCHAR(1000) NULL,       -- STATIC için virgüllü değerler
        DefaultValue    NVARCHAR(500) NULL,
        OrderNo         INT NOT NULL DEFAULT 0,
        IsRequired      BIT NOT NULL DEFAULT 0,
        IsActive        BIT NOT NULL DEFAULT 1,
        IsDeleted       BIT NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy       NVARCHAR(450) NULL,
        UpdatedAt       DATETIME2 NULL,
        UpdatedBy       NVARCHAR(450) NULL,
        CONSTRAINT FK_UDF_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    -- Aynı şirket+entity+alan adı tekil (silinmemiş kayıtlar arasında)
    CREATE UNIQUE INDEX UX_UDF_Field ON UserFieldDefinition(CompanyId, EntityName, FieldName) WHERE IsDeleted = 0;
    -- Render sorgusu: şirket+entity bazlı aktif tanımlar
    CREATE INDEX IX_UDF_Entity ON UserFieldDefinition(CompanyId, EntityName) WHERE IsDeleted = 0 AND IsActive = 1;
    PRINT 'UserFieldDefinition tablosu olusturuldu.';
END
GO

-- ─── Item: dinamik alan çantası + ürün-seviyesi emniyet stok kolonları ──────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'AdditionalFields' AND Object_ID = OBJECT_ID('Item'))
BEGIN
    ALTER TABLE Item ADD AdditionalFields NVARCHAR(MAX) NULL;  -- Dinamik UDF JSON çantası
    PRINT 'Item.AdditionalFields kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'MinStockLevel' AND Object_ID = OBJECT_ID('Item'))
BEGIN
    ALTER TABLE Item ADD MinStockLevel DECIMAL(18,4) NULL;  -- Ürün emniyet stok alt limiti (CriticalSku KPI)
    PRINT 'Item.MinStockLevel kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'MaxStockLevel' AND Object_ID = OBJECT_ID('Item'))
BEGIN
    ALTER TABLE Item ADD MaxStockLevel DECIMAL(18,4) NULL;  -- Ürün stok üst limiti
    PRINT 'Item.MaxStockLevel kolonu eklendi.';
END
GO
-- Zorunlu audit kolonları (sql-conventions §1) — Item'da eksikti, UPDATE bunları yazdığı için edit kırılıyordu
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'UpdatedAt' AND Object_ID = OBJECT_ID('Item'))
BEGIN
    ALTER TABLE Item ADD UpdatedAt DATETIME2 NULL;
    PRINT 'Item.UpdatedAt kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'UpdatedBy' AND Object_ID = OBJECT_ID('Item'))
BEGIN
    ALTER TABLE Item ADD UpdatedBy NVARCHAR(450) NULL;
    PRINT 'Item.UpdatedBy kolonu eklendi.';
END
GO
