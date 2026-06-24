-- =============================================================================
-- OPERAX WMS/ERP — MASTER DATABASE SCHEMA
-- Tüm modüllerin tek konsolide scripti
-- Versiyon: 2.0 (Konsolide + Hatalar Düzeltildi)
-- Oluşturulma: 2026-03-06
--
-- ÇALIŞTIRILMA SIRASI (bağımlılık sırasına göre):
--   1. Platform & Identity   (M00 + ASP.NET Identity)
--   2. Master Data           (M01)
--   3. Inventory             (M02)
--   4. Procurement           (M03)
--   5. Receiving             (M04)
--   6. Sales Order           (M05)
--   7. Shipping              (M06)
--   8. Picking               (M06 - PickTask)
--   9. Transfer              (M07)
--  10. Cycle Count           (M08)
--  11. Traceability / BOM    (M09)
--  12. Manufacturing         (M10)
--  13. Expenses & Budget     (M18 / M19)
--  14. Dashboard Views       (M15)
--  15. Seed Data
--
-- DÜZELTİLEN HATALAR:
--   - ProductionOrderLine çift tanımlıydı (M09 + M10) → tek tanım, M09'da kaldı
--   - PickTask.SourceDocId eksikti → eklendi (üretim kaynaklı pick task'lar için)
--   - PurchaseOrderHeader M03.sql + M03_PurchaseOrder.sql'de çift tanımlıydı → tek kaldı
--   - Bin tablosunda CreatedAt eksikti → eklendi
--   - Dashboard view'larında sm.Qty → sm.QtyBase düzeltildi
--   - SalesOrderLine.RequestedQty → QtyOrdered düzeltildi
--   - seed_core.sql sonundaki syntax hatası () → kaldırıldı
-- =============================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
GO

-- =============================================================================
-- BÖLÜM 1: PLATFORM CORE (M00)
-- =============================================================================

-- Company
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Company')
BEGIN
    CREATE TABLE Company (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Name        NVARCHAR(200) NOT NULL,
        TaxNumber   NVARCHAR(50),
        IsActive    BIT DEFAULT 1,
        CreatedAt   DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy   UNIQUEIDENTIFIER,
        UpdatedAt   DATETIME2,
        UpdatedBy   UNIQUEIDENTIFIER,
        IsDeleted   BIT DEFAULT 0,
        DeletedAt   DATETIME2,
        DeletedBy   UNIQUEIDENTIFIER
    );
    PRINT 'Company tablosu olusturuldu.';
END
GO

-- DictionaryType
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DictionaryType')
BEGIN
    CREATE TABLE DictionaryType (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        Code        NVARCHAR(100) NOT NULL,
        NameTr      NVARCHAR(200) NOT NULL,
        NameEn      NVARCHAR(200),
        IsSystem    BIT DEFAULT 0,
        CreatedAt   DATETIME2 DEFAULT GETUTCDATE(),
        IsDeleted   BIT DEFAULT 0,
        CONSTRAINT FK_DictionaryType_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    PRINT 'DictionaryType tablosu olusturuldu.';
END
GO

-- DictionaryValue
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DictionaryValue')
BEGIN
    CREATE TABLE DictionaryValue (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        TypeId      UNIQUEIDENTIFIER NOT NULL,
        Code        NVARCHAR(100) NOT NULL,
        NameTr      NVARCHAR(200) NOT NULL,
        NameEn      NVARCHAR(200),
        OrderNo     INT DEFAULT 0,
        IsActive    BIT DEFAULT 1,
        CreatedAt   DATETIME2 DEFAULT GETUTCDATE(),
        IsDeleted   BIT DEFAULT 0,
        CONSTRAINT FK_DictionaryValue_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_DictionaryValue_Type    FOREIGN KEY (TypeId)    REFERENCES DictionaryType(Id)
    );
    CREATE INDEX IX_DictionaryValue_Type ON DictionaryValue(CompanyId, TypeId) WHERE IsDeleted = 0;
    PRINT 'DictionaryValue tablosu olusturuldu.';
END
GO

-- Parameter
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Parameter')
BEGIN
    CREATE TABLE Parameter (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        ModuleCode  NVARCHAR(50) NOT NULL,
        Code        NVARCHAR(100) NOT NULL,
        Value       NVARCHAR(MAX),
        Description NVARCHAR(500),
        CreatedAt   DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt   DATETIME2,
        IsDeleted   BIT DEFAULT 0,
        CONSTRAINT FK_Parameter_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    CREATE INDEX IX_Parameter_Module ON Parameter(CompanyId, ModuleCode) WHERE IsDeleted = 0;
    PRINT 'Parameter tablosu olusturuldu.';
END
GO

-- Module
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Module')
BEGIN
    CREATE TABLE Module (
        Id      UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Code    NVARCHAR(50) UNIQUE NOT NULL,
        NameTr  NVARCHAR(200) NOT NULL,
        NameEn  NVARCHAR(200),
        IsActive BIT DEFAULT 1
    );
    PRINT 'Module tablosu olusturuldu.';
END
GO

-- CompanyModule
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CompanyModule')
BEGIN
    CREATE TABLE CompanyModule (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        ModuleId    UNIQUEIDENTIFIER NOT NULL,
        IsActive    BIT DEFAULT 1,
        ActivatedAt DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_CompanyModule_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_CompanyModule_Module  FOREIGN KEY (ModuleId)  REFERENCES Module(Id)
    );
    PRINT 'CompanyModule tablosu olusturuldu.';
END
GO

-- AuditLog
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AuditLog')
BEGIN
    CREATE TABLE AuditLog (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        UserId      UNIQUEIDENTIFIER NULL,
        UserName    NVARCHAR(256) NOT NULL DEFAULT '',
        Action      NVARCHAR(100) NOT NULL,   -- CREATE, UPDATE, DELETE, POST, LOGIN, LOGOUT
        EntityType  NVARCHAR(200) NOT NULL,   -- ReceivingHeader, SalesOrder vb.
        EntityId    UNIQUEIDENTIFIER NULL,
        Details     NVARCHAR(MAX) NULL,
        IpAddress   NVARCHAR(50) NULL,
        CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_AuditLog_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    CREATE INDEX IX_AuditLog_Company_Date ON AuditLog(CompanyId, CreatedAt DESC);
    CREATE INDEX IX_AuditLog_Entity       ON AuditLog(CompanyId, EntityType, EntityId) WHERE EntityId IS NOT NULL;
    PRINT 'AuditLog tablosu olusturuldu.';
END
GO

-- StatusTransition
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StatusTransition')
BEGIN
    CREATE TABLE StatusTransition (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId       UNIQUEIDENTIFIER NOT NULL,
        DocumentType    NVARCHAR(100) NOT NULL,  -- RECEIVING, SHIPMENT, COUNT vb.
        FromStatusCode  NVARCHAR(100) NOT NULL,
        ToStatusCode    NVARCHAR(100) NOT NULL,
        RoleId          NVARCHAR(450),
        ActionNameTr    NVARCHAR(200),
        ActionNameEn    NVARCHAR(200),
        OrderNo         INT DEFAULT 0,
        IsActive        BIT DEFAULT 1,
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        IsDeleted       BIT DEFAULT 0,
        CONSTRAINT FK_StatusTransition_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    CREATE INDEX IX_StatusTransition_Doc ON StatusTransition(CompanyId, DocumentType) WHERE IsDeleted = 0;
    PRINT 'StatusTransition tablosu olusturuldu.';
END
GO

-- =============================================================================
-- BÖLÜM 2: ASP.NET CORE IDENTITY
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetRoles')
BEGIN
    CREATE TABLE [dbo].[AspNetRoles] (
        [Id]               NVARCHAR(450) NOT NULL,
        [Name]             NVARCHAR(256) NULL,
        [NormalizedName]   NVARCHAR(256) NULL,
        [ConcurrencyStamp] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE UNIQUE NONCLUSTERED INDEX [RoleNameIndex] ON [dbo].[AspNetRoles]([NormalizedName] ASC)
        WHERE ([NormalizedName] IS NOT NULL);
    PRINT 'AspNetRoles tablosu olusturuldu.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUsers')
BEGIN
    CREATE TABLE [dbo].[AspNetUsers] (
        [Id]                   NVARCHAR(450)     NOT NULL,
        [UserName]             NVARCHAR(256)     NULL,
        [NormalizedUserName]   NVARCHAR(256)     NULL,
        [Email]                NVARCHAR(256)     NULL,
        [NormalizedEmail]      NVARCHAR(256)     NULL,
        [EmailConfirmed]       BIT               NOT NULL,
        [PasswordHash]         NVARCHAR(MAX)     NULL,
        [SecurityStamp]        NVARCHAR(MAX)     NULL,
        [ConcurrencyStamp]     NVARCHAR(MAX)     NULL,
        [PhoneNumber]          NVARCHAR(MAX)     NULL,
        [PhoneNumberConfirmed] BIT               NOT NULL,
        [TwoFactorEnabled]     BIT               NOT NULL,
        [LockoutEnd]           DATETIMEOFFSET(7) NULL,
        [LockoutEnabled]       BIT               NOT NULL,
        [AccessFailedCount]    INT               NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE INDEX [EmailIndex] ON [dbo].[AspNetUsers]([NormalizedEmail] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [UserNameIndex] ON [dbo].[AspNetUsers]([NormalizedUserName] ASC)
        WHERE ([NormalizedUserName] IS NOT NULL);
    PRINT 'AspNetUsers tablosu olusturuldu.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetRoleClaims')
BEGIN
    CREATE TABLE [dbo].[AspNetRoleClaims] (
        [Id]         INT            IDENTITY(1,1) NOT NULL,
        [RoleId]     NVARCHAR(450) NOT NULL,
        [ClaimType]  NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId]
            FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims]([RoleId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUserClaims')
BEGIN
    CREATE TABLE [dbo].[AspNetUserClaims] (
        [Id]         INT            IDENTITY(1,1) NOT NULL,
        [UserId]     NVARCHAR(450) NOT NULL,
        [ClaimType]  NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims]([UserId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUserLogins')
BEGIN
    CREATE TABLE [dbo].[AspNetUserLogins] (
        [LoginProvider]       NVARCHAR(450) NOT NULL,
        [ProviderKey]         NVARCHAR(450) NOT NULL,
        [ProviderDisplayName] NVARCHAR(MAX) NULL,
        [UserId]              NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY CLUSTERED ([LoginProvider] ASC, [ProviderKey] ASC),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins]([UserId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUserRoles')
BEGIN
    CREATE TABLE [dbo].[AspNetUserRoles] (
        [UserId] NVARCHAR(450) NOT NULL,
        [RoleId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY CLUSTERED ([UserId] ASC, [RoleId] ASC),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId]
            FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles]([RoleId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUserTokens')
BEGIN
    CREATE TABLE [dbo].[AspNetUserTokens] (
        [UserId]        NVARCHAR(450) NOT NULL,
        [LoginProvider] NVARCHAR(450) NOT NULL,
        [Name]          NVARCHAR(450) NOT NULL,
        [Value]         NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY CLUSTERED ([UserId] ASC, [LoginProvider] ASC, [Name] ASC),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
    );
END
GO

-- =============================================================================
-- BÖLÜM 3: MASTER DATA (M01)
-- =============================================================================

-- Category (FIX: schema_fix_step1 içeriği buraya taşındı)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Category')
BEGIN
    CREATE TABLE Category (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        Code        NVARCHAR(50)  NOT NULL,
        Name        NVARCHAR(255) NOT NULL,
        ParentId    UNIQUEIDENTIFIER NULL,
        IsActive    BIT DEFAULT 1,
        IsDeleted   BIT DEFAULT 0,
        CreatedAt   DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_Category_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_Category_Parent  FOREIGN KEY (ParentId)  REFERENCES Category(Id)
    );
    PRINT 'Category tablosu olusturuldu.';
END
GO

-- Item
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Item')
BEGIN
    CREATE TABLE Item (
        Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId        UNIQUEIDENTIFIER NOT NULL,
        Code             NVARCHAR(100) NOT NULL,
        Name             NVARCHAR(500) NOT NULL,
        Barcode          NVARCHAR(100),
        CategoryId       UNIQUEIDENTIFIER,
        BaseUomId        UNIQUEIDENTIFIER NOT NULL,
        TaxRate          DECIMAL(18,2) DEFAULT 20.0,
        IsActive         BIT DEFAULT 1,
        IsLotTracked     BIT DEFAULT 0,
        IsSerialTracked  BIT DEFAULT 0,
        CreatedAt        DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy        UNIQUEIDENTIFIER,
        IsDeleted        BIT DEFAULT 0,
        CONSTRAINT FK_Item_Company  FOREIGN KEY (CompanyId)  REFERENCES Company(Id),
        CONSTRAINT FK_Item_Category FOREIGN KEY (CategoryId) REFERENCES Category(Id)
    );
    CREATE UNIQUE INDEX IX_Item_Code ON Item(CompanyId, Code) WHERE IsDeleted = 0;
    PRINT 'Item tablosu olusturuldu.';
END
ELSE
BEGIN
    -- TaxRate kolonu yoksa ekle (migration uyumluluğu)
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Item') AND name = 'TaxRate')
        ALTER TABLE Item ADD TaxRate DECIMAL(18,2) DEFAULT 20.0;
END
GO

-- Item.Code mükerrer engeli (Plan 31): mevcut DB'de IX_Item_Code non-unique ise
-- UNIQUE'e yükselt. Filtre IsDeleted=0 → soft-delete edilmiş kayıtlar kapsam dışı.
-- ÖN KOŞUL: yükseltmeden önce within-(CompanyId,Code) mükerrerler temizlenmiş olmalı.
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_Item_Code' AND object_id = OBJECT_ID('Item') AND is_unique = 0)
BEGIN
    DROP INDEX IX_Item_Code ON Item;
    CREATE UNIQUE INDEX IX_Item_Code ON Item(CompanyId, Code) WHERE IsDeleted = 0;
    PRINT 'IX_Item_Code UNIQUE yapildi (mukerrer urun kodu engellendi).';
END
GO

-- ItemUOM (UOM dönüşümleri)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ItemUOM')
BEGIN
    CREATE TABLE ItemUOM (
        Id             UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ItemId         UNIQUEIDENTIFIER NOT NULL,
        UomId          UNIQUEIDENTIFIER NOT NULL,
        ConversionRate DECIMAL(18,6) NOT NULL,
        CreatedAt      DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_ItemUOM_Item FOREIGN KEY (ItemId) REFERENCES Item(Id),
        CONSTRAINT FK_ItemUOM_Uom  FOREIGN KEY (UomId)  REFERENCES DictionaryValue(Id)
    );
    PRINT 'ItemUOM tablosu olusturuldu.';
END
GO

-- ItemBarcode
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ItemBarcode')
BEGIN
    CREATE TABLE ItemBarcode (
        Id        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ItemId    UNIQUEIDENTIFIER NOT NULL,
        UomId     UNIQUEIDENTIFIER NOT NULL,
        Barcode   NVARCHAR(100) NOT NULL,
        IsActive  BIT DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_ItemBarcode_Item FOREIGN KEY (ItemId) REFERENCES Item(Id),
        CONSTRAINT FK_ItemBarcode_Uom  FOREIGN KEY (UomId)  REFERENCES DictionaryValue(Id)
    );
    CREATE UNIQUE INDEX IX_ItemBarcode_Barcode ON ItemBarcode(Barcode) WHERE IsActive = 1;
    PRINT 'ItemBarcode tablosu olusturuldu.';
END
GO

-- Warehouse
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Warehouse')
BEGIN
    CREATE TABLE Warehouse (
        Id        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId UNIQUEIDENTIFIER NOT NULL,
        Code      NVARCHAR(50)  NOT NULL,
        Name      NVARCHAR(200) NOT NULL,
        IsActive  BIT DEFAULT 1,
        IsDeleted BIT DEFAULT 0,
        CONSTRAINT FK_Warehouse_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    PRINT 'Warehouse tablosu olusturuldu.';
END
GO

-- Bin (FIX: CreatedAt eklendi, M01_Bins ile birleştirildi)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Bin')
BEGIN
    CREATE TABLE Bin (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        WarehouseId     UNIQUEIDENTIFIER NOT NULL,
        Code            NVARCHAR(100) NOT NULL,
        Zone            NVARCHAR(50),
        SortNo          INT DEFAULT 0,
        IsPickingArea   BIT DEFAULT 1,
        IsReceivingArea BIT DEFAULT 0,
        IsStorageArea   BIT NOT NULL DEFAULT 1,
        IsActive        BIT DEFAULT 1,
        IsDeleted       BIT DEFAULT 0,
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_Bin_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id)
    );
    CREATE INDEX IX_Bin_Code      ON Bin(WarehouseId, Code) WHERE IsDeleted = 0;
    CREATE INDEX IX_Bin_Warehouse ON Bin(WarehouseId) WHERE IsActive = 1 AND IsDeleted = 0;
    PRINT 'Bin tablosu olusturuldu.';
END
GO

-- City
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'City')
BEGIN
    CREATE TABLE City (
        Id        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Name      NVARCHAR(100) NOT NULL,
        Code      NVARCHAR(20)  NULL,
        IsActive  BIT DEFAULT 1,
        IsDeleted BIT DEFAULT 0
    );
    PRINT 'City tablosu olusturuldu.';
END
GO

-- Partner
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Partner')
BEGIN
    CREATE TABLE Partner (
        Id        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId UNIQUEIDENTIFIER NOT NULL,
        Code      NVARCHAR(100) NOT NULL,
        Name      NVARCHAR(500) NOT NULL,
        Type      NVARCHAR(20)  NOT NULL, -- VENDOR, CUSTOMER, BOTH
        TaxNumber NVARCHAR(50),
        Email     NVARCHAR(200),
        CityId    UNIQUEIDENTIFIER NULL,
        City      NVARCHAR(100),
        IsActive  BIT DEFAULT 1,
        IsDeleted BIT DEFAULT 0,
        CONSTRAINT FK_Partner_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_Partner_City FOREIGN KEY (CityId) REFERENCES City(Id)
    );
    CREATE INDEX IX_Partner_Code ON Partner(CompanyId, Code) WHERE IsDeleted = 0;
    PRINT 'Partner tablosu olusturuldu.';
END
GO

-- PriceList
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PriceList')
BEGIN
    CREATE TABLE PriceList (
        Id        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId UNIQUEIDENTIFIER NOT NULL,
        Code      NVARCHAR(50)  NOT NULL,
        Name      NVARCHAR(200) NOT NULL,
        Currency  NVARCHAR(10)  DEFAULT 'TRY',
        IsActive  BIT DEFAULT 1,
        CONSTRAINT FK_PriceList_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    CREATE INDEX IX_PriceList_Code ON PriceList(CompanyId, Code);
    PRINT 'PriceList tablosu olusturuldu.';
END
GO

-- PriceListLine
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PriceListLine')
BEGIN
    CREATE TABLE PriceListLine (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PriceListId UNIQUEIDENTIFIER NOT NULL,
        ItemId      UNIQUEIDENTIFIER NOT NULL,
        UnitPrice   DECIMAL(18,4) NOT NULL,
        MinQty      DECIMAL(18,6) DEFAULT 0,
        CONSTRAINT FK_PriceLine_List FOREIGN KEY (PriceListId) REFERENCES PriceList(Id),
        CONSTRAINT FK_PriceLine_Item FOREIGN KEY (ItemId)      REFERENCES Item(Id)
    );
    PRINT 'PriceListLine tablosu olusturuldu.';
END
GO

-- =============================================================================
-- BÖLÜM 4: INVENTORY LEDGER (M02)
-- =============================================================================

-- StockMovement
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StockMovement')
BEGIN
    CREATE TABLE StockMovement (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId     UNIQUEIDENTIFIER NOT NULL,
        WarehouseId   UNIQUEIDENTIFIER NOT NULL,
        BinId         UNIQUEIDENTIFIER NOT NULL,
        ItemId        UNIQUEIDENTIFIER NOT NULL,
        MovementType  NVARCHAR(50)  NOT NULL, -- RECEIPT, ISSUE, TRANSFER, COUNT_ADJ
        QtyBase       DECIMAL(18,6) NOT NULL, -- Her zaman Base Unit. Giriş: Pozitif, Çıkış: Negatif
        UomId         UNIQUEIDENTIFIER NOT NULL,
        QtyOriginal   DECIMAL(18,6) NOT NULL,
        SourceDocType NVARCHAR(50),           -- RECEIVING, SHIPPING, TRANSFER, ADJUSTMENT
        SourceDocId   UNIQUEIDENTIFIER,
        SourceDocNo   NVARCHAR(100),
        LpnId         UNIQUEIDENTIFIER,
        LotNo         NVARCHAR(100),
        SerialNo      NVARCHAR(100),
        ExpiryDate    DATETIME2,
        CreatedAt     DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy     NVARCHAR(450),
        IsCancelled   BIT DEFAULT 0,
        CancelledAt   DATETIME2,
        CancelledBy   UNIQUEIDENTIFIER,
        CONSTRAINT FK_StockMovement_Company   FOREIGN KEY (CompanyId)   REFERENCES Company(Id),
        CONSTRAINT FK_StockMovement_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id),
        CONSTRAINT FK_StockMovement_Bin       FOREIGN KEY (BinId)       REFERENCES Bin(Id),
        CONSTRAINT FK_StockMovement_Item      FOREIGN KEY (ItemId)      REFERENCES Item(Id)
    );
    CREATE INDEX IX_StockMovement_Item     ON StockMovement(CompanyId, ItemId, CreatedAt);
    CREATE INDEX IX_StockMovement_Bin      ON StockMovement(CompanyId, BinId, ItemId);
    CREATE INDEX IX_StockMovement_Source   ON StockMovement(SourceDocId);
    CREATE INDEX IX_StockMovement_Filtered ON StockMovement(CompanyId, IsCancelled, ItemId);
    PRINT 'StockMovement tablosu olusturuldu.';
END
GO

-- vw_InventoryBalance
CREATE OR ALTER VIEW vw_InventoryBalance AS
    SELECT
        CompanyId, WarehouseId, BinId, LpnId, ItemId, LotNo,
        SUM(QtyBase) AS QtyOnHand
    FROM StockMovement
    WHERE IsCancelled = 0
    GROUP BY CompanyId, WarehouseId, BinId, LpnId, ItemId, LotNo
    HAVING SUM(QtyBase) <> 0;
GO

-- vw_InventoryBalanceByBin
CREATE OR ALTER VIEW vw_InventoryBalanceByBin AS
SELECT
    m.CompanyId,
    m.WarehouseId,
    w.Name                    AS WarehouseName,
    m.BinId,
    ISNULL(b.Code, 'UNASSIGNED') AS BinCode,
    m.ItemId,
    i.Code                    AS ItemCode,
    i.Name                    AS ItemName,
    SUM(m.QtyBase)            AS OnHandQty,
    dv.Code                   AS BaseUomCode
FROM StockMovement m
JOIN Item           i  ON i.Id  = m.ItemId
JOIN Warehouse      w  ON w.Id  = m.WarehouseId
LEFT JOIN Bin       b  ON b.Id  = m.BinId
JOIN DictionaryValue dv ON dv.Id = i.BaseUomId
WHERE m.IsCancelled = 0
GROUP BY
    m.CompanyId, m.WarehouseId, w.Name,
    m.BinId, b.Code, m.ItemId, i.Code, i.Name, dv.Code;
GO

-- =============================================================================
-- BÖLÜM 5: PROCUREMENT / PURCHASE ORDER (M03)
-- FIX: schema_M03.sql ve schema_M03_PurchaseOrder.sql aynıydı → tek tanım
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchaseOrderHeader')
BEGIN
    CREATE TABLE PurchaseOrderHeader (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        WarehouseId UNIQUEIDENTIFIER NOT NULL,
        PartnerId   UNIQUEIDENTIFIER NOT NULL,
        OrderNo     NVARCHAR(100) UNIQUE NOT NULL, -- PO-2026-00001
        OrderDate   DATETIME2 DEFAULT GETUTCDATE(),
        Status      NVARCHAR(50) DEFAULT 'DRAFT',  -- DRAFT, APPROVED, RECEIVED, CANCELLED
        Notes       NVARCHAR(MAX),
        CreatedAt   DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy   UNIQUEIDENTIFIER,
        UpdatedAt   DATETIME2,
        UpdatedBy   UNIQUEIDENTIFIER,
        IsDeleted   BIT DEFAULT 0,
        CONSTRAINT FK_PurchaseOrderHeader_Company   FOREIGN KEY (CompanyId)   REFERENCES Company(Id),
        CONSTRAINT FK_PurchaseOrderHeader_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id),
        CONSTRAINT FK_PurchaseOrderHeader_Partner   FOREIGN KEY (PartnerId)   REFERENCES Partner(Id)
    );
    CREATE INDEX IX_PurchaseOrder_OrderNo ON PurchaseOrderHeader(CompanyId, OrderNo);
    CREATE INDEX IX_PurchaseOrder_Vendor  ON PurchaseOrderHeader(CompanyId, PartnerId);
    CREATE INDEX IX_PurchaseOrder_Status  ON PurchaseOrderHeader(CompanyId, Status) WHERE IsDeleted = 0;
    PRINT 'PurchaseOrderHeader tablosu olusturuldu.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchaseOrderLine')
BEGIN
    CREATE TABLE PurchaseOrderLine (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        HeaderId    UNIQUEIDENTIFIER NOT NULL,
        ItemId      UNIQUEIDENTIFIER NOT NULL,
        UomId       UNIQUEIDENTIFIER NOT NULL,
        QtyOrdered  DECIMAL(18,6) NOT NULL,
        QtyReceived DECIMAL(18,6) DEFAULT 0,
        Price       DECIMAL(18,4),
        Currency    NVARCHAR(10),
        CreatedAt   DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_PurchaseOrderLine_Header FOREIGN KEY (HeaderId) REFERENCES PurchaseOrderHeader(Id),
        CONSTRAINT FK_PurchaseOrderLine_Item   FOREIGN KEY (ItemId)   REFERENCES Item(Id)
    );
    CREATE INDEX IX_PurchaseOrderLine_Header ON PurchaseOrderLine(HeaderId);
    PRINT 'PurchaseOrderLine tablosu olusturuldu.';
END
GO

-- =============================================================================
-- BÖLÜM 6: RECEIVING (M04)
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceivingHeader')
BEGIN
    CREATE TABLE ReceivingHeader (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId       UNIQUEIDENTIFIER NOT NULL,
        WarehouseId     UNIQUEIDENTIFIER NOT NULL,
        PartnerId       UNIQUEIDENTIFIER NOT NULL,
        PurchaseOrderId UNIQUEIDENTIFIER,
        DocNo           NVARCHAR(100) UNIQUE NOT NULL, -- RCV-2026-00001
        DocDate         DATETIME2 DEFAULT GETUTCDATE(),
        Status          NVARCHAR(50) DEFAULT 'DRAFT',  -- DRAFT, POSTED, CANCELLED
        Notes           NVARCHAR(MAX),
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy       UNIQUEIDENTIFIER,
        UpdatedAt       DATETIME2,
        UpdatedBy       UNIQUEIDENTIFIER,
        IsDeleted       BIT DEFAULT 0,
        CONSTRAINT FK_ReceivingHeader_Company FOREIGN KEY (CompanyId)       REFERENCES Company(Id),
        CONSTRAINT FK_ReceivingHeader_WH      FOREIGN KEY (WarehouseId)     REFERENCES Warehouse(Id),
        CONSTRAINT FK_ReceivingHeader_Partner FOREIGN KEY (PartnerId)       REFERENCES Partner(Id),
        CONSTRAINT FK_ReceivingHeader_PO      FOREIGN KEY (PurchaseOrderId) REFERENCES PurchaseOrderHeader(Id)
    );
    CREATE INDEX IX_Receiving_DocNo ON ReceivingHeader(CompanyId, DocNo);
    CREATE INDEX IX_Receiving_PO    ON ReceivingHeader(PurchaseOrderId);
    CREATE INDEX IX_Receiving_Status ON ReceivingHeader(CompanyId, Status) WHERE IsDeleted = 0;
    PRINT 'ReceivingHeader tablosu olusturuldu.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceivingLine')
BEGIN
    CREATE TABLE ReceivingLine (
        Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        HeaderId            UNIQUEIDENTIFIER NOT NULL,
        PurchaseOrderLineId UNIQUEIDENTIFIER,
        ItemId              UNIQUEIDENTIFIER NOT NULL,
        UomId               UNIQUEIDENTIFIER NOT NULL,
        QtyOriginal         DECIMAL(18,6) NOT NULL,
        QtyBase             DECIMAL(18,6) NOT NULL, -- QtyOriginal * ConversionRate
        LotNo               NVARCHAR(100),
        SerialNo            NVARCHAR(100),
        ExpiryDate          DATETIME2,
        BinId               UNIQUEIDENTIFIER,
        CreatedAt           DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_ReceivingLine_Header FOREIGN KEY (HeaderId)            REFERENCES ReceivingHeader(Id),
        CONSTRAINT FK_ReceivingLine_Item   FOREIGN KEY (ItemId)              REFERENCES Item(Id),
        CONSTRAINT FK_ReceivingLine_POLine FOREIGN KEY (PurchaseOrderLineId) REFERENCES PurchaseOrderLine(Id)
    );
    CREATE INDEX IX_Receiving_Line_Header ON ReceivingLine(HeaderId);
    PRINT 'ReceivingLine tablosu olusturuldu.';
END
GO

-- =============================================================================
-- BÖLÜM 7: SALES ORDER (M05)
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SalesOrderHeader')
BEGIN
    CREATE TABLE SalesOrderHeader (
        Id                    UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId             UNIQUEIDENTIFIER NOT NULL,
        WarehouseId           UNIQUEIDENTIFIER NOT NULL,
        PartnerId             UNIQUEIDENTIFIER NOT NULL,
        OrderNo               NVARCHAR(100) UNIQUE NOT NULL, -- SO-2026-00001
        OrderDate             DATETIME2 DEFAULT GETUTCDATE(),
        RequestedDeliveryDate DATETIME2,
        Status                NVARCHAR(50) DEFAULT 'DRAFT', -- DRAFT, APPROVED, SHIPPED, CANCELLED
        Notes                 NVARCHAR(MAX),
        CreatedAt             DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy             UNIQUEIDENTIFIER,
        UpdatedAt             DATETIME2,
        UpdatedBy             UNIQUEIDENTIFIER,
        IsDeleted             BIT DEFAULT 0,
        CONSTRAINT FK_SalesOrderHeader_Company   FOREIGN KEY (CompanyId)   REFERENCES Company(Id),
        CONSTRAINT FK_SalesOrderHeader_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id),
        CONSTRAINT FK_SalesOrderHeader_Partner   FOREIGN KEY (PartnerId)   REFERENCES Partner(Id)
    );
    CREATE INDEX IX_SalesOrder_OrderNo ON SalesOrderHeader(CompanyId, OrderNo);
    CREATE INDEX IX_SalesOrder_Customer ON SalesOrderHeader(CompanyId, PartnerId);
    CREATE INDEX IX_SalesOrder_Status   ON SalesOrderHeader(CompanyId, Status) WHERE IsDeleted = 0;
    PRINT 'SalesOrderHeader tablosu olusturuldu.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SalesOrderLine')
BEGIN
    CREATE TABLE SalesOrderLine (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        HeaderId    UNIQUEIDENTIFIER NOT NULL,
        ItemId      UNIQUEIDENTIFIER NOT NULL,
        UomId       UNIQUEIDENTIFIER NOT NULL,
        QtyOrdered  DECIMAL(18,6) NOT NULL,
        QtyReserved DECIMAL(18,6) DEFAULT 0,
        QtyShipped  DECIMAL(18,6) DEFAULT 0,
        Price       DECIMAL(18,4),
        Currency    NVARCHAR(10),
        CreatedAt   DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_SalesOrderLine_Header FOREIGN KEY (HeaderId) REFERENCES SalesOrderHeader(Id),
        CONSTRAINT FK_SalesOrderLine_Item   FOREIGN KEY (ItemId)   REFERENCES Item(Id)
    );
    CREATE INDEX IX_SalesOrderLine_Header ON SalesOrderLine(HeaderId);
    PRINT 'SalesOrderLine tablosu olusturuldu.';
END
GO

-- =============================================================================
-- BÖLÜM 8: SHIPPING (M06)
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ShippingHeader')
BEGIN
    CREATE TABLE ShippingHeader (
        Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId    UNIQUEIDENTIFIER NOT NULL,
        WarehouseId  UNIQUEIDENTIFIER NOT NULL,
        DocNo        NVARCHAR(100) UNIQUE NOT NULL, -- SHP-2026-00001
        DocDate      DATETIME2 DEFAULT GETUTCDATE(),
        Status       NVARCHAR(50) DEFAULT 'DRAFT',  -- DRAFT, POSTED, CANCELLED
        CarrierName  NVARCHAR(200),
        VehiclePlate NVARCHAR(50),
        Notes        NVARCHAR(MAX),
        CreatedAt    DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy    UNIQUEIDENTIFIER,
        UpdatedAt    DATETIME2,
        UpdatedBy    UNIQUEIDENTIFIER,
        IsDeleted    BIT DEFAULT 0,
        CONSTRAINT FK_ShippingHeader_Company   FOREIGN KEY (CompanyId)   REFERENCES Company(Id),
        CONSTRAINT FK_ShippingHeader_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id)
    );
    CREATE INDEX IX_Shipping_DocNo  ON ShippingHeader(CompanyId, DocNo);
    CREATE INDEX IX_Shipping_Status ON ShippingHeader(CompanyId, Status) WHERE IsDeleted = 0;
    PRINT 'ShippingHeader tablosu olusturuldu.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ShippingLine')
BEGIN
    CREATE TABLE ShippingLine (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        HeaderId        UNIQUEIDENTIFIER NOT NULL,
        SalesOrderLineId UNIQUEIDENTIFIER NOT NULL,
        ItemId          UNIQUEIDENTIFIER NOT NULL,
        UomId           UNIQUEIDENTIFIER NOT NULL,
        QtyOriginal     DECIMAL(18,6) NOT NULL,
        QtyBase         DECIMAL(18,6) NOT NULL,
        LotNo           NVARCHAR(100),
        BinId           UNIQUEIDENTIFIER,
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_ShippingLine_Header FOREIGN KEY (HeaderId)         REFERENCES ShippingHeader(Id),
        CONSTRAINT FK_ShippingLine_SOLine FOREIGN KEY (SalesOrderLineId) REFERENCES SalesOrderLine(Id),
        CONSTRAINT FK_ShippingLine_Item   FOREIGN KEY (ItemId)           REFERENCES Item(Id)
    );
    CREATE INDEX IX_ShippingLine_Header ON ShippingLine(HeaderId);
    CREATE INDEX IX_ShippingLine_SOLine ON ShippingLine(SalesOrderLineId);
    PRINT 'ShippingLine tablosu olusturuldu.';
END
GO

-- =============================================================================
-- BÖLÜM 9: PICKING (M06 - PickTask)
-- FIX: SourceDocId eklendi (üretim emirlerinden açılan pick task'lar için)
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PickTask')
BEGIN
    CREATE TABLE PickTask (
        Id             UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId      UNIQUEIDENTIFIER NOT NULL,
        DocNo          NVARCHAR(50) NOT NULL,          -- PCK-2026-0001
        ShipmentId     UNIQUEIDENTIFIER,               -- Sevkiyat kaynaklı ise
        SourceDocId    UNIQUEIDENTIFIER,               -- Üretim emri kaynaklı ise (ProductionOrder.Id)
        SourceDocType  NVARCHAR(50),                   -- SHIPMENT, PRODUCTION
        Status         NVARCHAR(20) DEFAULT 'DRAFT',   -- DRAFT, ASSIGNED, IN_PROGRESS, COMPLETED, CANCELLED
        AssignedUserId NVARCHAR(450),
        Priority       INT DEFAULT 10,
        CreatedAt      DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy      UNIQUEIDENTIFIER,
        StartedAt      DATETIME2,
        CompletedAt    DATETIME2,
        CONSTRAINT FK_PickTask_Company  FOREIGN KEY (CompanyId)  REFERENCES Company(Id),
        CONSTRAINT FK_PickTask_Shipment FOREIGN KEY (ShipmentId) REFERENCES ShippingHeader(Id),
        CONSTRAINT CK_PickTask_Source   CHECK (ShipmentId IS NOT NULL OR SourceDocId IS NOT NULL)
    );
    CREATE INDEX IX_PickTask_DocNo  ON PickTask(CompanyId, DocNo);
    CREATE INDEX IX_PickTask_Source ON PickTask(SourceDocId);
    CREATE INDEX IX_PickTask_Status ON PickTask(CompanyId, Status);
    PRINT 'PickTask tablosu olusturuldu.';
END
ELSE
BEGIN
    -- SourceDocId & SourceDocType mevcut değilse ekle (migration)
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PickTask') AND name = 'SourceDocId')
    BEGIN
        ALTER TABLE PickTask ADD SourceDocId   UNIQUEIDENTIFIER;
        ALTER TABLE PickTask ADD SourceDocType NVARCHAR(50);
        PRINT 'PickTask.SourceDocId ve SourceDocType eklendi.';
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PickTaskLine')
BEGIN
    CREATE TABLE PickTaskLine (
        Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PickTaskId          UNIQUEIDENTIFIER NOT NULL,
        ItemId              UNIQUEIDENTIFIER NOT NULL,
        UomId               UNIQUEIDENTIFIER NOT NULL,
        TargetWarehouseId   UNIQUEIDENTIFIER NOT NULL,
        TargetBinId         UNIQUEIDENTIFIER NOT NULL,
        QtyRequested        DECIMAL(18,6) NOT NULL,
        QtyRequestedBase    DECIMAL(18,6) NOT NULL,
        PickedBinId         UNIQUEIDENTIFIER,
        QtyPicked           DECIMAL(18,6) DEFAULT 0,
        QtyPickedBase       DECIMAL(18,6) DEFAULT 0,
        PickedByUserId      NVARCHAR(450),
        PickedAt            DATETIME2,
        IsCancelled         BIT DEFAULT 0,
        CONSTRAINT FK_PickTaskLine_Task  FOREIGN KEY (PickTaskId)  REFERENCES PickTask(Id),
        CONSTRAINT FK_PickTaskLine_Item  FOREIGN KEY (ItemId)      REFERENCES Item(Id),
        CONSTRAINT FK_PickTaskLine_TBin  FOREIGN KEY (TargetBinId) REFERENCES Bin(Id),
        CONSTRAINT FK_PickTaskLine_PBin  FOREIGN KEY (PickedBinId) REFERENCES Bin(Id)
    );
    CREATE INDEX IX_PickTaskLine_Task ON PickTaskLine(PickTaskId);
    PRINT 'PickTaskLine tablosu olusturuldu.';
END
GO

-- =============================================================================
-- BÖLÜM 10: TRANSFER (M07)
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StockTransfer')
BEGIN
    CREATE TABLE StockTransfer (
        Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId        UNIQUEIDENTIFIER NOT NULL,
        DocNo            NVARCHAR(50) NOT NULL,
        Status           NVARCHAR(20) DEFAULT 'DRAFT',  -- DRAFT, POSTED, CANCELLED
        TransferType     NVARCHAR(20) NOT NULL,          -- BIN_TO_BIN, WH_TO_WH
        FromWarehouseId  UNIQUEIDENTIFIER NOT NULL,
        ToWarehouseId    UNIQUEIDENTIFIER NOT NULL,
        Notes            NVARCHAR(MAX),
        CreatedAt        DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy        UNIQUEIDENTIFIER,
        PostedAt         DATETIME2,
        CONSTRAINT FK_Transfer_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    CREATE INDEX IX_StockTransfer_DocNo ON StockTransfer(CompanyId, DocNo);
    PRINT 'StockTransfer tablosu olusturuldu.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StockTransferLine')
BEGIN
    CREATE TABLE StockTransferLine (
        Id         UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TransferId UNIQUEIDENTIFIER NOT NULL,
        ItemId     UNIQUEIDENTIFIER NOT NULL,
        UomId      UNIQUEIDENTIFIER NOT NULL,
        FromBinId  UNIQUEIDENTIFIER,
        ToBinId    UNIQUEIDENTIFIER,
        Qty        DECIMAL(18,6) NOT NULL,
        QtyBase    DECIMAL(18,6) NOT NULL,
        LotNo      NVARCHAR(50),
        CONSTRAINT FK_TLine_Header FOREIGN KEY (TransferId) REFERENCES StockTransfer(Id),
        CONSTRAINT FK_TLine_Item   FOREIGN KEY (ItemId)     REFERENCES Item(Id)
    );
    CREATE INDEX IX_StockTransferLine_Header ON StockTransferLine(TransferId);
    PRINT 'StockTransferLine tablosu olusturuldu.';
END
GO

-- ItemBinConfig (Min/Max & Replenishment)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ItemBinConfig')
BEGIN
    CREATE TABLE ItemBinConfig (
        Id        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId UNIQUEIDENTIFIER NOT NULL,
        ItemId    UNIQUEIDENTIFIER NOT NULL,
        BinId     UNIQUEIDENTIFIER NOT NULL,
        MinQty    DECIMAL(18,6) DEFAULT 0,
        MaxQty    DECIMAL(18,6) DEFAULT 0,
        Priority  INT DEFAULT 10,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_ItemBin_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_ItemBin_Item    FOREIGN KEY (ItemId)    REFERENCES Item(Id),
        CONSTRAINT FK_ItemBin_Bin     FOREIGN KEY (BinId)     REFERENCES Bin(Id)
    );
    CREATE INDEX IX_ItemBinConfig_Item ON ItemBinConfig(ItemId);
    PRINT 'ItemBinConfig tablosu olusturuldu.';
END
GO

-- =============================================================================
-- BÖLÜM 11: CYCLE COUNT (M08)
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CycleCount')
BEGIN
    CREATE TABLE CycleCount (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        DocNo       NVARCHAR(50) NOT NULL,            -- CNT-2026-001
        Status      NVARCHAR(20) DEFAULT 'DRAFT',     -- DRAFT, COUNTING, COMPLETED, CANCELLED
        WarehouseId UNIQUEIDENTIFIER NOT NULL,
        Description NVARCHAR(MAX),
        PlannedDate DATETIME2,
        CreatedAt   DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy   UNIQUEIDENTIFIER,
        PostedAt    DATETIME2,
        IsDeleted   BIT DEFAULT 0,
        CONSTRAINT FK_CC_Company   FOREIGN KEY (CompanyId)   REFERENCES Company(Id),
        CONSTRAINT FK_CC_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id)
    );
    CREATE INDEX IX_CycleCount_DocNo ON CycleCount(CompanyId, DocNo);
    PRINT 'CycleCount tablosu olusturuldu.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CycleCountLine')
BEGIN
    CREATE TABLE CycleCountLine (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CycleCountId  UNIQUEIDENTIFIER NOT NULL,
        BinId         UNIQUEIDENTIFIER NOT NULL,
        ItemId        UNIQUEIDENTIFIER NOT NULL,
        LpnId         UNIQUEIDENTIFIER,
        LotNo         NVARCHAR(100),
        QtySystem     DECIMAL(18,6) DEFAULT 0,
        QtyCounted    DECIMAL(18,6) DEFAULT 0,
        QtyDifference AS (QtyCounted - QtySystem),
        CountedAt     DATETIME2,
        CountedBy     NVARCHAR(450),
        IsDeleted     BIT DEFAULT 0,
        CONSTRAINT FK_CCLine_Header FOREIGN KEY (CycleCountId) REFERENCES CycleCount(Id),
        CONSTRAINT FK_CCLine_Bin    FOREIGN KEY (BinId)        REFERENCES Bin(Id),
        CONSTRAINT FK_CCLine_Item   FOREIGN KEY (ItemId)       REFERENCES Item(Id)
    );
    CREATE INDEX IX_CycleCountLine_Header ON CycleCountLine(CycleCountId);
    PRINT 'CycleCountLine tablosu olusturuldu.';
END
GO

-- =============================================================================
-- BÖLÜM 12: TRACEABILITY (M09) — LPN, LOT, SERIAL, BOM
-- =============================================================================

-- LPN (Palet / Kap)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LPN')
BEGIN
    CREATE TABLE LPN (
        Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId        UNIQUEIDENTIFIER NOT NULL,
        Code             NVARCHAR(50) NOT NULL UNIQUE,
        Status           NVARCHAR(20) DEFAULT 'AVAILABLE', -- AVAILABLE, IN_USE, LOADED, SHIPPED
        LpnType          NVARCHAR(20) DEFAULT 'PALLET',    -- PALLET, BOX, CARTON
        CurrentWarehouseId UNIQUEIDENTIFIER,
        CurrentBinId     UNIQUEIDENTIFIER,
        ParentLpnId      UNIQUEIDENTIFIER,
        Notes            NVARCHAR(MAX),
        CreatedAt        DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy        UNIQUEIDENTIFIER,
        CONSTRAINT FK_LPN_Company FOREIGN KEY (CompanyId)       REFERENCES Company(Id),
        CONSTRAINT FK_LPN_WH      FOREIGN KEY (CurrentWarehouseId) REFERENCES Warehouse(Id),
        CONSTRAINT FK_LPN_Bin     FOREIGN KEY (CurrentBinId)    REFERENCES Bin(Id),
        CONSTRAINT FK_LPN_Parent  FOREIGN KEY (ParentLpnId)     REFERENCES LPN(Id)
    );
    CREATE INDEX IX_LPN_Code ON LPN(CompanyId, Code);
    PRINT 'LPN tablosu olusturuldu.';
END
GO

-- ItemLot
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ItemLot')
BEGIN
    CREATE TABLE ItemLot (
        Id             UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId      UNIQUEIDENTIFIER NOT NULL,
        ItemId         UNIQUEIDENTIFIER NOT NULL,
        LotNo          NVARCHAR(100) NOT NULL,
        ProductionDate DATETIME2,
        ExpiryDate     DATETIME2,
        Status         NVARCHAR(20) DEFAULT 'AVAILABLE', -- AVAILABLE, QUARANTINE, BLOCKED
        Notes          NVARCHAR(MAX),
        CreatedAt      DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy      UNIQUEIDENTIFIER,
        IsDeleted      BIT DEFAULT 0,
        CONSTRAINT FK_ItemLot_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_ItemLot_Item    FOREIGN KEY (ItemId)    REFERENCES Item(Id),
        CONSTRAINT UQ_ItemLot_No      UNIQUE (CompanyId, ItemId, LotNo)
    );
    CREATE INDEX IX_ItemLot_Expiry ON ItemLot(CompanyId, ItemId, ExpiryDate);
    PRINT 'ItemLot tablosu olusturuldu.';
END
GO

-- ItemSerial
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ItemSerial')
BEGIN
    CREATE TABLE ItemSerial (
        Id                 UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId          UNIQUEIDENTIFIER NOT NULL,
        ItemId             UNIQUEIDENTIFIER NOT NULL,
        SerialNo           NVARCHAR(100) NOT NULL,
        Status             NVARCHAR(20) DEFAULT 'IN_STOCK', -- IN_STOCK, SHIPPED, SCRAPPED, QUARANTINE
        CurrentWarehouseId UNIQUEIDENTIFIER,
        CurrentBinId       UNIQUEIDENTIFIER,
        CurrentLpnId       UNIQUEIDENTIFIER,
        LotNo              NVARCHAR(100),
        CreatedAt          DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy          UNIQUEIDENTIFIER,
        IsDeleted          BIT DEFAULT 0,
        CONSTRAINT FK_ItemSerial_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_ItemSerial_Item    FOREIGN KEY (ItemId)    REFERENCES Item(Id),
        CONSTRAINT UQ_ItemSerial_No      UNIQUE (CompanyId, ItemId, SerialNo)
    );
    CREATE INDEX IX_ItemSerial_No ON ItemSerial(CompanyId, SerialNo);
    PRINT 'ItemSerial tablosu olusturuldu.';
END
GO

-- ItemBOM (Ürün Reçetesi - Statik)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ItemBOM')
BEGIN
    CREATE TABLE ItemBOM (
        Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ParentItemId UNIQUEIDENTIFIER NOT NULL, -- Üretilecek mamül
        ChildItemId  UNIQUEIDENTIFIER NOT NULL, -- Kullanılacak hammadde/sarf
        QtyRequired  DECIMAL(18,6) NOT NULL,    -- Gereken miktar (Base Unit)
        IsActive     BIT DEFAULT 1,
        CONSTRAINT FK_BOM_Parent FOREIGN KEY (ParentItemId) REFERENCES Item(Id),
        CONSTRAINT FK_BOM_Child  FOREIGN KEY (ChildItemId)  REFERENCES Item(Id)
    );
    CREATE INDEX IX_ItemBOM_Parent ON ItemBOM(ParentItemId) WHERE IsActive = 1;
    PRINT 'ItemBOM tablosu olusturuldu.';
END
GO

-- =============================================================================
-- BÖLÜM 13: MANUFACTURING (M10)
-- =============================================================================

-- ProductionOrder
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductionOrder')
BEGIN
    CREATE TABLE ProductionOrder (
        Id                   UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId            UNIQUEIDENTIFIER NOT NULL,
        DocNo                NVARCHAR(50) NOT NULL,           -- PRD-2026-0001
        ItemId               UNIQUEIDENTIFIER NOT NULL,
        QtyTarget            DECIMAL(18,6) NOT NULL,
        QtyProduced          DECIMAL(18,6) DEFAULT 0,
        Status               NVARCHAR(20) DEFAULT 'DRAFT',    -- DRAFT, RELEASED, IN_PROGRESS, COMPLETED, CANCELLED
        SourceDocType        NVARCHAR(50),                    -- SHIPPING (sevkiyattan tetiklendiyse)
        SourceDocId          UNIQUEIDENTIFIER,
        TargetWarehouseId    UNIQUEIDENTIFIER,
        TargetBinId          UNIQUEIDENTIFIER,
        SourceBinId          UNIQUEIDENTIFIER,
        Priority             INT DEFAULT 10,
        DueDate              DATETIME2,
        PlanDate             DATETIME2,
        PickStatus           NVARCHAR(20) DEFAULT 'PENDING',  -- PENDING, PICKING, PICKED
        RouteId              UNIQUEIDENTIFIER,
        CurrentRouteStepId   UNIQUEIDENTIFIER,
        PlannedMaterialCost  DECIMAL(18,4) DEFAULT 0,
        PlannedResourceCost  DECIMAL(18,4) DEFAULT 0,
        ActualMaterialCost   DECIMAL(18,4) DEFAULT 0,
        ActualResourceCost   DECIMAL(18,4) DEFAULT 0,
        CumulativeLaborCost  DECIMAL(18,4) DEFAULT 0,
        CumulativeEnergyCost DECIMAL(18,4) DEFAULT 0,
        CreatedAt            DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy            UNIQUEIDENTIFIER,
        CompletedAt          DATETIME2,
        CONSTRAINT FK_Production_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_Production_Item    FOREIGN KEY (ItemId)    REFERENCES Item(Id)
    );
    CREATE INDEX IX_ProductionOrder_DocNo  ON ProductionOrder(CompanyId, DocNo);
    CREATE INDEX IX_ProductionOrder_Source ON ProductionOrder(SourceDocId);
    CREATE INDEX IX_ProductionOrder_Status ON ProductionOrder(CompanyId, Status);
    PRINT 'ProductionOrder tablosu olusturuldu.';
END
GO

-- ProductionOrderLine
-- FIX: M10_DynamicBOM.sql'de de tanımlıydı → kaldırıldı, sadece burada tek tanım
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductionOrderLine')
BEGIN
    CREATE TABLE ProductionOrderLine (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ProductionOrderId UNIQUEIDENTIFIER NOT NULL,
        ItemId          UNIQUEIDENTIFIER NOT NULL,
        QtyRequired     DECIMAL(18,6) NOT NULL,
        QtyIssued       DECIMAL(18,6) DEFAULT 0,
        PickTaskId      UNIQUEIDENTIFIER,            -- Bu hammadde için açılan toplama emri
        CONSTRAINT FK_POLine_Order FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
        CONSTRAINT FK_POLine_Item  FOREIGN KEY (ItemId)            REFERENCES Item(Id)
    );
    CREATE INDEX IX_ProductionOrderLine_Order ON ProductionOrderLine(ProductionOrderId);
    PRINT 'ProductionOrderLine tablosu olusturuldu.';
END
GO

-- WorkCenter (İş Merkezi / Makine)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WorkCenter')
BEGIN
    CREATE TABLE WorkCenter (
        Id                   UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId            UNIQUEIDENTIFIER NOT NULL,
        Code                 NVARCHAR(50)  NOT NULL,
        Name                 NVARCHAR(200) NOT NULL,
        LaborRatePerSec      DECIMAL(18,6) DEFAULT 0,
        MachineRatePerSec    DECIMAL(18,6) DEFAULT 0,
        ElectricityRatePerSec DECIMAL(18,6) DEFAULT 0,
        IsActive             BIT DEFAULT 1,
        CONSTRAINT FK_WC_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    PRINT 'WorkCenter tablosu olusturuldu.';
END
GO

-- ProductionActivity (Operasyon Girişleri)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductionActivity')
BEGIN
    CREATE TABLE ProductionActivity (
        Id                   UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ProductionOrderId    UNIQUEIDENTIFIER NOT NULL,
        WorkCenterId         UNIQUEIDENTIFIER NOT NULL,
        UserId               UNIQUEIDENTIFIER NOT NULL,
        ActivityType         NVARCHAR(20) DEFAULT 'PRODUCTION', -- SETUP, PRODUCTION, DOWNTIME
        StartTime            DATETIME2 NOT NULL,
        EndTime              DATETIME2,
        QtyProduced          DECIMAL(18,6) DEFAULT 0,
        DurationSec          AS DATEDIFF(SECOND, StartTime, EndTime),
        CalculatedLaborCost  DECIMAL(18,4) DEFAULT 0,
        CalculatedEnergyCost DECIMAL(18,4) DEFAULT 0,
        Notes                NVARCHAR(MAX),
        CONSTRAINT FK_Activity_Order FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
        CONSTRAINT FK_Activity_WC    FOREIGN KEY (WorkCenterId)      REFERENCES WorkCenter(Id)
    );
    CREATE INDEX IX_ProductionActivity_Order ON ProductionActivity(ProductionOrderId);
    PRINT 'ProductionActivity tablosu olusturuldu.';
END
GO

-- ProductRoute (Rota Tanımı)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductRoute')
BEGIN
    CREATE TABLE ProductRoute (
        Id        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId UNIQUEIDENTIFIER NOT NULL,
        Code      NVARCHAR(50)  NOT NULL,
        Name      NVARCHAR(200) NOT NULL,
        IsActive  BIT DEFAULT 1,
        CONSTRAINT FK_Route_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    PRINT 'ProductRoute tablosu olusturuldu.';
END
GO

-- ProductRouteStep (Rota Adımları)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductRouteStep')
BEGIN
    CREATE TABLE ProductRouteStep (
        Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ProductRouteId      UNIQUEIDENTIFIER NOT NULL,
        StepOrder           INT NOT NULL,
        OperationCode       NVARCHAR(50)  NOT NULL,
        OperationName       NVARCHAR(200) NOT NULL,
        WorkCenterId        UNIQUEIDENTIFIER NOT NULL,
        StandardDurationSec INT DEFAULT 0,
        StandardLaborCost   DECIMAL(18,4) DEFAULT 0,
        StandardMachineCost DECIMAL(18,4) DEFAULT 0,
        CONSTRAINT FK_Step_Route FOREIGN KEY (ProductRouteId) REFERENCES ProductRoute(Id),
        CONSTRAINT FK_Step_WC    FOREIGN KEY (WorkCenterId)   REFERENCES WorkCenter(Id)
    );
    PRINT 'ProductRouteStep tablosu olusturuldu.';
END
GO

-- ProductModel (Dinamik / Parametrik BOM Şablonu)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductModel')
BEGIN
    CREATE TABLE ProductModel (
        Id         UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId  UNIQUEIDENTIFIER NOT NULL,
        Code       NVARCHAR(50)  NOT NULL,
        Name       NVARCHAR(200) NOT NULL,
        BaseItemId UNIQUEIDENTIFIER,
        CreatedAt  DATETIME2 DEFAULT GETUTCDATE(),
        IsActive   BIT DEFAULT 1,
        CONSTRAINT FK_Model_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    PRINT 'ProductModel tablosu olusturuldu.';
END
GO

-- ProductModelParameter
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductModelParameter')
BEGIN
    CREATE TABLE ProductModelParameter (
        Id             UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ProductModelId UNIQUEIDENTIFIER NOT NULL,
        Code           NVARCHAR(50)  NOT NULL,
        Name           NVARCHAR(100) NOT NULL,
        DataType       NVARCHAR(20) DEFAULT 'NUMBER', -- NUMBER, STRING, OPTION
        DefaultValue   NVARCHAR(100),
        Unit           NVARCHAR(20),
        CONSTRAINT FK_Param_Model FOREIGN KEY (ProductModelId) REFERENCES ProductModel(Id)
    );
    PRINT 'ProductModelParameter tablosu olusturuldu.';
END
GO

-- ProductModelBOM (Formüllü Dinamik BOM)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductModelBOM')
BEGIN
    CREATE TABLE ProductModelBOM (
        Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ProductModelId   UNIQUEIDENTIFIER NOT NULL,
        ComponentItemId  UNIQUEIDENTIFIER NOT NULL,
        QtyFormula       NVARCHAR(500) NOT NULL,  -- Örn: "[WIDTH] * [HEIGHT] / 1000000"
        WastePercentage  DECIMAL(5,2) DEFAULT 0,
        ConditionFormula NVARCHAR(500),           -- Örn: "[GLASS_TYPE] == 'FROSTED'"
        CONSTRAINT FK_ModelBOM_Model FOREIGN KEY (ProductModelId)  REFERENCES ProductModel(Id),
        CONSTRAINT FK_ModelBOM_Item  FOREIGN KEY (ComponentItemId) REFERENCES Item(Id)
    );
    PRINT 'ProductModelBOM tablosu olusturuldu.';
END
GO

-- ProductionOrderConfig (Seçilen Parametre Değerleri)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductionOrderConfig')
BEGIN
    CREATE TABLE ProductionOrderConfig (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ProductionOrderId UNIQUEIDENTIFIER NOT NULL,
        ParameterId     UNIQUEIDENTIFIER NOT NULL,
        Value           NVARCHAR(100) NOT NULL,
        CONSTRAINT FK_Config_Order FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
        CONSTRAINT FK_Config_Param FOREIGN KEY (ParameterId)       REFERENCES ProductModelParameter(Id)
    );
    PRINT 'ProductionOrderConfig tablosu olusturuldu.';
END
GO

-- ProductionConsumption (Fiili Sarfiyat)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductionConsumption')
BEGIN
    CREATE TABLE ProductionConsumption (
        Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId           UNIQUEIDENTIFIER NOT NULL,
        ProductionOrderId   UNIQUEIDENTIFIER NOT NULL,
        ProductionActivityId UNIQUEIDENTIFIER,
        RouteStepId         UNIQUEIDENTIFIER,
        ItemId              UNIQUEIDENTIFIER NOT NULL,
        LotNo               NVARCHAR(100),
        SerialNo            NVARCHAR(100),
        LpnId               UNIQUEIDENTIFIER,
        QtyConsumed         DECIMAL(18,6) NOT NULL,
        UnitPrice           DECIMAL(18,4),
        TotalCost           AS (QtyConsumed * UnitPrice),
        CreatedAt           DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy           UNIQUEIDENTIFIER,
        CONSTRAINT FK_Cons_Order FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
        CONSTRAINT FK_Cons_Item  FOREIGN KEY (ItemId)            REFERENCES Item(Id)
    );
    CREATE INDEX IX_ProductionConsumption_Order ON ProductionConsumption(ProductionOrderId);
    PRINT 'ProductionConsumption tablosu olusturuldu.';
END
GO

-- DefectCode (Hata Kodları)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DefectCode')
BEGIN
    CREATE TABLE DefectCode (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        Code        NVARCHAR(50)  NOT NULL,
        Description NVARCHAR(200),
        Severity    NVARCHAR(20) DEFAULT 'MINOR', -- MINOR, MAJOR, CRITICAL
        CONSTRAINT FK_Defect_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    PRINT 'DefectCode tablosu olusturuldu.';
END
GO

-- ProductionInspection (Kalite Kontrol)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductionInspection')
BEGIN
    CREATE TABLE ProductionInspection (
        Id                UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId         UNIQUEIDENTIFIER NOT NULL,
        ProductionOrderId UNIQUEIDENTIFIER NOT NULL,
        InspectorUserId   UNIQUEIDENTIFIER NOT NULL,
        InspectionDate    DATETIME2 DEFAULT GETUTCDATE(),
        QtyInspected      DECIMAL(18,6) NOT NULL,
        QtyPassed         DECIMAL(18,6) NOT NULL,
        QtyFailed         AS (QtyInspected - QtyPassed),
        Result            NVARCHAR(20) NOT NULL, -- PASS, FAIL
        FailAction        NVARCHAR(20),          -- REWORK, SCRAP, CANCEL_ORDER
        DefectCodeId      UNIQUEIDENTIFIER,
        Notes             NVARCHAR(MAX),
        CONSTRAINT FK_Insp_Order  FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
        CONSTRAINT FK_Insp_Defect FOREIGN KEY (DefectCodeId)      REFERENCES DefectCode(Id)
    );
    CREATE INDEX IX_ProductionInspection_Order ON ProductionInspection(ProductionOrderId);
    PRINT 'ProductionInspection tablosu olusturuldu.';
END
GO

-- ProductionRework (Yeniden İşlem)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductionRework')
BEGIN
    CREATE TABLE ProductionRework (
        Id                       UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId                UNIQUEIDENTIFIER NOT NULL,
        ProductionOrderId        UNIQUEIDENTIFIER NOT NULL,
        InspectionId             UNIQUEIDENTIFIER NOT NULL,
        Status                   NVARCHAR(20) DEFAULT 'OPEN', -- OPEN, IN_PROGRESS, COMPLETED
        ReworkStepId             UNIQUEIDENTIFIER,
        RequiresAdditionalMaterial BIT DEFAULT 0,
        CreatedAt                DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy                UNIQUEIDENTIFIER,
        CONSTRAINT FK_Rework_Order FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
        CONSTRAINT FK_Rework_Insp  FOREIGN KEY (InspectionId)      REFERENCES ProductionInspection(Id)
    );
    CREATE INDEX IX_ProductionRework_Order ON ProductionRework(ProductionOrderId);
    PRINT 'ProductionRework tablosu olusturuldu.';
END
GO

-- ProductionReworkMaterial
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductionReworkMaterial')
BEGIN
    CREATE TABLE ProductionReworkMaterial (
        Id         UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ReworkId   UNIQUEIDENTIFIER NOT NULL,
        ItemId     UNIQUEIDENTIFIER NOT NULL,
        Qty        DECIMAL(18,6) NOT NULL,
        ActionType NVARCHAR(20) DEFAULT 'ADDITION', -- REPLACEMENT, ADDITION
        Reason     NVARCHAR(200),
        CONSTRAINT FK_ReworkMat_Rework FOREIGN KEY (ReworkId) REFERENCES ProductionRework(Id),
        CONSTRAINT FK_ReworkMat_Item   FOREIGN KEY (ItemId)   REFERENCES Item(Id)
    );
    PRINT 'ProductionReworkMaterial tablosu olusturuldu.';
END
GO

-- =============================================================================
-- BÖLÜM 14: EXPENSES & BUDGET (M18 / M19)
-- =============================================================================

-- CostCenter
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CostCenter')
BEGIN
    CREATE TABLE CostCenter (
        Id        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId UNIQUEIDENTIFIER NOT NULL,
        Code      NVARCHAR(50)  NOT NULL,
        Name      NVARCHAR(200) NOT NULL,
        Type      NVARCHAR(50),   -- PRODUCTION, ADMIN, SALES, LOGISTICS
        ParentId  UNIQUEIDENTIFIER NULL,
        IsActive  BIT DEFAULT 1,
        CONSTRAINT FK_CostCenter_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_CostCenter_Parent  FOREIGN KEY (ParentId)  REFERENCES CostCenter(Id)
    );
    PRINT 'CostCenter tablosu olusturuldu.';
END
GO

-- ExpenseType
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ExpenseType')
BEGIN
    CREATE TABLE ExpenseType (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId     UNIQUEIDENTIFIER NOT NULL,
        Code          NVARCHAR(50)  NOT NULL,
        Name          NVARCHAR(200) NOT NULL,
        UnitOfMeasure NVARCHAR(20),
        Description   NVARCHAR(500),
        CONSTRAINT FK_ExpenseType_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    PRINT 'ExpenseType tablosu olusturuldu.';
END
GO

-- ExpenseInvoice
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ExpenseInvoice')
BEGIN
    CREATE TABLE ExpenseInvoice (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        PartnerId   UNIQUEIDENTIFIER NOT NULL,
        DocNo       NVARCHAR(50) NOT NULL,
        InvoiceDate DATE NOT NULL,
        DueDate     DATE,
        TotalAmount DECIMAL(18,4) NOT NULL,
        Currency    NVARCHAR(10) DEFAULT 'TRY',
        Status      NVARCHAR(50) DEFAULT 'DRAFT', -- DRAFT, POSTED, PAID, CANCELLED
        CONSTRAINT FK_ExpenseInv_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_ExpenseInv_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id)
    );
    PRINT 'ExpenseInvoice tablosu olusturuldu.';
END
GO

-- ExpenseInvoiceLine
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ExpenseInvoiceLine')
BEGIN
    CREATE TABLE ExpenseInvoiceLine (
        Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ExpenseInvoiceId UNIQUEIDENTIFIER NOT NULL,
        ExpenseTypeId    UNIQUEIDENTIFIER NOT NULL,
        CostCenterId     UNIQUEIDENTIFIER NOT NULL,
        Quantity         DECIMAL(18,6) NOT NULL,
        UnitPrice        DECIMAL(18,4) NOT NULL,
        Amount           DECIMAL(18,4) NOT NULL,
        TaxRate          DECIMAL(5,2)  DEFAULT 20.00,
        TaxAmount        AS (Amount * TaxRate / 100),
        TotalAmount      AS (Amount + (Amount * TaxRate / 100)),
        CONSTRAINT FK_ExpInvLine_Inv  FOREIGN KEY (ExpenseInvoiceId) REFERENCES ExpenseInvoice(Id),
        CONSTRAINT FK_ExpInvLine_Type FOREIGN KEY (ExpenseTypeId)    REFERENCES ExpenseType(Id),
        CONSTRAINT FK_ExpInvLine_CC   FOREIGN KEY (CostCenterId)     REFERENCES CostCenter(Id)
    );
    PRINT 'ExpenseInvoiceLine tablosu olusturuldu.';
END
GO

-- Budget
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Budget')
BEGIN
    CREATE TABLE Budget (
        Id        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId UNIQUEIDENTIFIER NOT NULL,
        Year      INT NOT NULL,
        Code      NVARCHAR(50)  NOT NULL,
        Name      NVARCHAR(200) NOT NULL,
        Type      NVARCHAR(50) DEFAULT 'OPERATIONAL', -- OPERATIONAL, CASH_FLOW, INVESTMENT
        Status    NVARCHAR(50) DEFAULT 'DRAFT',        -- DRAFT, APPROVED, CLOSED
        CONSTRAINT FK_Budget_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    PRINT 'Budget tablosu olusturuldu.';
END
GO

-- BudgetLine
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BudgetLine')
BEGIN
    CREATE TABLE BudgetLine (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        BudgetId      UNIQUEIDENTIFIER NOT NULL,
        AccrualDate   DATE NOT NULL,
        CashDate      DATE NOT NULL,
        Direction     NVARCHAR(10) DEFAULT 'OUT', -- IN, OUT
        CostCenterId  UNIQUEIDENTIFIER NULL,
        ExpenseTypeId UNIQUEIDENTIFIER NULL,
        AmountPlanned DECIMAL(18,4) NOT NULL DEFAULT 0,
        AmountActual  DECIMAL(18,4) NOT NULL DEFAULT 0,
        CONSTRAINT FK_BudgetLine_Budget FOREIGN KEY (BudgetId)     REFERENCES Budget(Id),
        CONSTRAINT FK_BudgetLine_CC     FOREIGN KEY (CostCenterId) REFERENCES CostCenter(Id)
    );
    PRINT 'BudgetLine tablosu olusturuldu.';
END
GO

-- CashFlowForecast
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CashFlowForecast')
BEGIN
    CREATE TABLE CashFlowForecast (
        Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId    UNIQUEIDENTIFIER NOT NULL,
        ForecastDate DATE NOT NULL,
        Type         NVARCHAR(10) NOT NULL,  -- IN, OUT
        SourceType   NVARCHAR(50),           -- SALES_ORDER, PURCHASE_ORDER, EXPENSE_INVOICE vb.
        SourceId     UNIQUEIDENTIFIER NULL,
        Amount       DECIMAL(18,4) NOT NULL,
        Probability  DECIMAL(5,2) DEFAULT 100.00,
        Notes        NVARCHAR(500),
        CONSTRAINT FK_CashFlow_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    PRINT 'CashFlowForecast tablosu olusturuldu.';
END
GO

-- =============================================================================
-- BÖLÜM 15: DASHBOARD VIEWS (M15)
-- FIX: sm.Qty → sm.QtyBase, SalesOrderLine.RequestedQty → QtyOrdered
-- =============================================================================

CREATE OR ALTER VIEW View_ProductionEfficiency AS
SELECT
    po.DocNo,
    i.Name                     AS ItemName,
    po.QtyTarget,
    po.QtyProduced,
    po.PlannedResourceCost,
    po.ActualResourceCost,
    (po.ActualResourceCost - po.PlannedResourceCost) AS CostVariance,
    CASE WHEN po.PlannedResourceCost > 0
         THEN (po.ActualResourceCost / po.PlannedResourceCost) * 100
         ELSE 0 END             AS EfficiencyIndex
FROM ProductionOrder po
JOIN Item i ON i.Id = po.ItemId
WHERE po.Status = 'COMPLETED';
GO

CREATE OR ALTER VIEW View_ManufacturingScrapRate AS
SELECT
    i.Name         AS ItemName,
    SUM(po.QtyProduced) AS GoodQty,
    ISNULL((
        SELECT SUM(pi2.QtyInspected - pi2.QtyPassed)
        FROM ProductionInspection pi2
        WHERE pi2.ProductionOrderId = po.Id
    ), 0)          AS ScrapQty
FROM ProductionOrder po
JOIN Item i ON i.Id = po.ItemId
GROUP BY i.Name, po.Id;
GO

-- FIX: sm.Qty → sm.QtyBase
CREATE OR ALTER VIEW View_InventoryHealth AS
SELECT
    i.Code AS SKU,
    i.Name,
    ISNULL(SUM(sm.QtyBase), 0) AS OnHand,
    (
        SELECT ISNULL(SUM(pol.QtyRequired), 0)
        FROM ProductionOrderLine pol
        JOIN ProductionOrder po2 ON po2.Id = pol.ProductionOrderId
        WHERE pol.ItemId = i.Id AND po2.Status IN ('DRAFT', 'RELEASED')
    ) AS FutureRequirement
FROM Item i
LEFT JOIN StockMovement sm ON sm.ItemId = i.Id AND sm.IsCancelled = 0
GROUP BY i.Id, i.Code, i.Name;
GO

-- FIX: sm.Qty → sm.QtyBase
CREATE OR ALTER VIEW View_WarehouseOccupation AS
SELECT
    w.Name AS WarehouseName,
    COUNT(b.Id) AS TotalBins,
    (
        SELECT COUNT(DISTINCT sm2.BinId)
        FROM StockMovement sm2
        WHERE sm2.WarehouseId = w.Id
          AND sm2.IsCancelled = 0
          AND sm2.QtyBase > 0
    ) AS OccupiedBins,
    CAST((
        SELECT COUNT(DISTINCT sm2.BinId)
        FROM StockMovement sm2
        WHERE sm2.WarehouseId = w.Id
          AND sm2.IsCancelled = 0
          AND sm2.QtyBase > 0
    ) AS FLOAT) / NULLIF(COUNT(b.Id), 0) * 100 AS OccupationRate
FROM Warehouse w
LEFT JOIN Bin b ON b.WarehouseId = w.Id AND b.IsDeleted = 0
GROUP BY w.Name, w.Id;
GO

CREATE OR ALTER VIEW View_OperatorPerformance AS
SELECT
    u.UserName                                           AS OperatorName,
    SUM(DATEDIFF(SECOND, a.StartTime, a.EndTime))        AS TotalWorkSeconds,
    COUNT(a.Id)                                          AS TotalActivities,
    ISNULL(SUM(a.QtyProduced), 0)                        AS TotalUnitsProduced
FROM AspNetUsers u
JOIN ProductionActivity a ON CAST(a.UserId AS NVARCHAR(450)) = u.Id
WHERE a.EndTime IS NOT NULL
GROUP BY u.UserName;
GO

-- FIX: SalesOrderLine.RequestedQty → QtyOrdered
CREATE OR ALTER VIEW View_SalesVsProduction AS
SELECT
    i.Name AS ItemName,
    SUM(sol.QtyOrdered) AS OrderedQty,
    ISNULL((
        SELECT SUM(po.QtyProduced)
        FROM ProductionOrder po
        WHERE po.ItemId = i.Id AND po.Status = 'COMPLETED'
    ), 0) AS ProducedQty
FROM SalesOrderLine sol
JOIN Item i ON i.Id = sol.ItemId
GROUP BY i.Name, i.Id;
GO

CREATE OR ALTER VIEW View_AccrualBudgetReport AS
SELECT
    b.Year,
    MONTH(bl.AccrualDate) AS BudgetMonth,
    cc.Name               AS CostCenter,
    bl.AmountPlanned,
    bl.AmountActual,
    (bl.AmountPlanned - bl.AmountActual) AS Variance
FROM BudgetLine bl
JOIN Budget b ON b.Id = bl.BudgetId
LEFT JOIN CostCenter cc ON cc.Id = bl.CostCenterId;
GO

CREATE OR ALTER VIEW View_CashFlowCalendar AS
SELECT
    bl.CashDate,
    bl.Direction,
    cc.Name AS CostCenter,
    bl.AmountPlanned AS ForecastAmount,
    CASE WHEN bl.Direction = 'IN' THEN bl.AmountPlanned ELSE -bl.AmountPlanned END AS NetFlow
FROM BudgetLine bl
LEFT JOIN CostCenter cc ON cc.Id = bl.CostCenterId;
GO

-- =============================================================================
-- BÖLÜM 16: SEED DATA
-- =============================================================================

-- Modüller
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M00')
BEGIN
    INSERT INTO Module (Code, NameTr, NameEn) VALUES
    ('M00', 'Platform Çekirdek',       'Platform Core'),
    ('M01', 'Ana Veri Yönetimi',       'Master Data'),
    ('M02', 'Envanter Hareketleri',    'Inventory Ledger'),
    ('M03', 'Satınalma & Mal Kabul',   'Procurement & Receiving'),
    ('M04', 'Satış Siparişi',          'Sales Order'),
    ('M05', 'Sevkiyat',                'Shipping'),
    ('M06', 'Toplama (Picking)',        'Picking'),
    ('M07', 'Transfer',                'Transfer'),
    ('M08', 'Sayım (Cycle Count)',     'Cycle Count'),
    ('M09', 'İzlenebilirlik',          'Traceability'),
    ('M10', 'Üretim Yönetimi',         'Manufacturing'),
    ('M12', 'Servis Yönetimi',         'Service'),
    ('M13', 'Proje Yönetimi',          'Project'),
    ('M15', 'Dashboard & Analiz',      'Dashboards'),
    ('M16', 'Entegrasyon Köprüsü',     'Integration Bridge'),
    ('M18', 'Gider Yönetimi',          'Expense Management'),
    ('M19', 'Bütçe & Nakit Akışı',     'Budgeting & Cash Flow');
    PRINT 'Modüller eklendi.';
END
GO

-- Dictionary Tipleri
DECLARE @SysComp UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- Sistem şirket yoksa oluştur
IF NOT EXISTS (SELECT 1 FROM Company WHERE Id = @SysComp)
    INSERT INTO Company (Id, Name, IsActive) VALUES (@SysComp, 'SYSTEM', 1);

IF NOT EXISTS (SELECT 1 FROM DictionaryType WHERE Code = 'UOM' AND CompanyId = @SysComp)
BEGIN
    DECLARE @UomTypeId   UNIQUEIDENTIFIER = NEWID();
    DECLARE @StatTypeId  UNIQUEIDENTIFIER = NEWID();
    DECLARE @MovTypeId   UNIQUEIDENTIFIER = NEWID();
    DECLARE @TaxTypeId   UNIQUEIDENTIFIER = NEWID();

    INSERT INTO DictionaryType (Id, CompanyId, Code, NameTr, NameEn, IsSystem) VALUES
    (@UomTypeId,  @SysComp, 'UOM',      'Ölçü Birimi',   'Unit of Measure',   1),
    (@StatTypeId, @SysComp, 'STATUS',   'Belge Durumu',  'Document Status',   1),
    (@MovTypeId,  @SysComp, 'MOV_TYPE', 'Hareket Tipi',  'Movement Type',     1),
    (@TaxTypeId,  @SysComp, 'TAX_RATE', 'KDV Oranı',     'Tax Rate',          1);

    -- UOM değerleri (sabit ID'ler — seed_test_data ile uyumlu)
    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE Id = 'c29e2fc2-e8d7-486a-90ce-b45e6039ee42')
        INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES ('c29e2fc2-e8d7-486a-90ce-b45e6039ee42', @SysComp, @UomTypeId, 'ADET', 'Adet', 'Each', 10);

    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE Id = 'a935a202-e6b8-46b8-b4c0-6ee8aa9d009f')
        INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES ('a935a202-e6b8-46b8-b4c0-6ee8aa9d009f', @SysComp, @UomTypeId, 'KG', 'Kilogram', 'Kilogram', 20);

    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE Id = '86c09707-ac97-4b4c-ad57-36f11be9a77a')
        INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES ('86c09707-ac97-4b4c-ad57-36f11be9a77a', @SysComp, @UomTypeId, 'MT', 'Metre', 'Meter', 30);

    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE CompanyId = @SysComp AND TypeId = @UomTypeId AND Code = 'KUTU')
        INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES (NEWID(), @SysComp, @UomTypeId, 'KUTU', 'Kutu', 'Box', 40);

    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE CompanyId = @SysComp AND TypeId = @UomTypeId AND Code = 'LT')
        INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES (NEWID(), @SysComp, @UomTypeId, 'LT', 'Litre', 'Liter', 50);

    -- STATUS değerleri
    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE CompanyId = @SysComp AND TypeId = @StatTypeId AND Code = 'DRAFT')
        INSERT INTO DictionaryValue (CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES (@SysComp, @StatTypeId, 'DRAFT', 'Taslak', 'Draft', 10);

    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE CompanyId = @SysComp AND TypeId = @StatTypeId AND Code = 'APPROVED')
        INSERT INTO DictionaryValue (CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES (@SysComp, @StatTypeId, 'APPROVED', 'Onaylandı', 'Approved', 20);

    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE CompanyId = @SysComp AND TypeId = @StatTypeId AND Code = 'POSTED')
        INSERT INTO DictionaryValue (CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES (@SysComp, @StatTypeId, 'POSTED', 'Tamamlandı', 'Posted', 30);

    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE CompanyId = @SysComp AND TypeId = @StatTypeId AND Code = 'CANCELLED')
        INSERT INTO DictionaryValue (CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES (@SysComp, @StatTypeId, 'CANCELLED', 'İptal', 'Cancelled', 40);

    -- KDV değerleri
    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE CompanyId = @SysComp AND TypeId = @TaxTypeId AND Code = '0')
        INSERT INTO DictionaryValue (CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES (@SysComp, @TaxTypeId, '0', '%0', '0%', 1);

    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE CompanyId = @SysComp AND TypeId = @TaxTypeId AND Code = '1')
        INSERT INTO DictionaryValue (CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES (@SysComp, @TaxTypeId, '1', '%1', '1%', 2);

    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE CompanyId = @SysComp AND TypeId = @TaxTypeId AND Code = '10')
        INSERT INTO DictionaryValue (CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES (@SysComp, @TaxTypeId, '10', '%10', '10%', 3);

    IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE CompanyId = @SysComp AND TypeId = @TaxTypeId AND Code = '20')
        INSERT INTO DictionaryValue (CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
        VALUES (@SysComp, @TaxTypeId, '20', '%20', '20%', 4);

    PRINT 'Dictionary tipleri ve değerleri eklendi.';
END
GO

-- ASP.NET Identity tabloları
IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Email = 'admin@operax.com')
BEGIN
    DECLARE @AdminId NVARCHAR(450) = NEWID();
    INSERT INTO AspNetUsers
        (Id, UserName, NormalizedUserName, Email, NormalizedEmail,
         EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
         PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
    VALUES
        (@AdminId, 'admin', 'ADMIN', 'admin@operax.com', 'ADMIN@OPERAX.COM',
         1, 'AQAAAAIAAYagAAAAEPlaceholderHash==', NEWID(), NEWID(),
         0, 0, 1, 0);

    IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = 'Administrator')
    BEGIN
        DECLARE @RoleId NVARCHAR(450) = NEWID();
        INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
        VALUES (@RoleId, 'Administrator', 'ADMINISTRATOR', NEWID());
        INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@AdminId, @RoleId);
    END
    PRINT 'Admin kullanıcısı olusturuldu.';
END
GO

-- Demo şirket & company claim
DECLARE @DemoCompId UNIQUEIDENTIFIER = 'd1e1b1a5-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM Company WHERE Id = @DemoCompId)
BEGIN
    INSERT INTO Company (Id, Name, TaxNumber, IsActive)
    VALUES (@DemoCompId, 'Operax Demo Ltd. Şti.', '1234567890', 1);
    PRINT 'Demo şirket olusturuldu.';
END

-- Admin kullanıcısına company claim ekle
DECLARE @AdminUserId NVARCHAR(450) = (SELECT Id FROM AspNetUsers WHERE NormalizedEmail = 'ADMIN@OPERAX.COM');
IF @AdminUserId IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM AspNetUserClaims WHERE UserId = @AdminUserId AND ClaimType = 'company'
)
BEGIN
    INSERT INTO AspNetUserClaims (UserId, ClaimType, ClaimValue)
    VALUES (@AdminUserId, 'company', CAST(@DemoCompId AS NVARCHAR(36)));
    PRINT 'Admin kullanıcısına company claim eklendi.';
END
GO

PRINT '=== OPERAX MASTER SCHEMA TAMAMLANDI ===';
GO
