-- =============================================================================
-- M01 Partner genişletme — Plan 08 (Cari Kart Tablı Yapı)
-- Faz 0: Sorumlu temsilci (satış/satınalma) kolonları
-- Faz 2: PartnerContact / PartnerAddress / PartnerBankAccount / PartnerActivity
-- Tümü idempotent (IF NOT EXISTS koruması).
-- =============================================================================
SET NOCOUNT ON;

-- ─── Eksik temel kolonlar (önceki form genişletmesinde varsayılmış ama eklenmemiş) ──
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Phone' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD Phone NVARCHAR(40) NULL;
    PRINT 'Partner.Phone eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Address' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD Address NVARCHAR(500) NULL;
    PRINT 'Partner.Address eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Notes' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD Notes NVARCHAR(MAX) NULL;
    PRINT 'Partner.Notes eklendi.';
END
GO

-- ─── Standart audit kolonları (Partner eski tablo, eksikti — UPDATE UpdatedAt set ediyor) ──
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CreatedAt' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Partner_CreatedAt DEFAULT GETUTCDATE();
    PRINT 'Partner.CreatedAt eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CreatedBy' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD CreatedBy NVARCHAR(450) NULL;
    PRINT 'Partner.CreatedBy eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'UpdatedAt' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD UpdatedAt DATETIME2 NULL;
    PRINT 'Partner.UpdatedAt eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'UpdatedBy' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD UpdatedBy NVARCHAR(450) NULL;
    PRINT 'Partner.UpdatedBy eklendi.';
END
GO

-- ─── Faz 0: Sorumlu temsilci ──────────────────────────────────────
-- Satış temsilcisi: müşteri carisinin sorumlusu. Satınalma sorumlusu: tedarikçi carisinin sorumlusu.
-- İleride satır-seviyesi yetki temeli (kullanıcı sadece kendi carisini görür).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'SalesRepUserId' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD SalesRepUserId NVARCHAR(450) NULL;
    PRINT 'Partner.SalesRepUserId eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PurchaseRepUserId' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD PurchaseRepUserId NVARCHAR(450) NULL;
    PRINT 'Partner.PurchaseRepUserId eklendi.';
END
GO
-- FK'ler (AspNetUsers) — NO ACTION (SET NULL iki FK'de "multiple cascade paths" hatası verir).
-- Temsilci kullanıcı silinmek istenirse önce carideki atama kaldırılmalı (Identity'de hard-delete nadir).
-- Drop/create ile idempotent.
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Partner_SalesRep')
    ALTER TABLE Partner DROP CONSTRAINT FK_Partner_SalesRep;
GO
ALTER TABLE Partner ADD CONSTRAINT FK_Partner_SalesRep
    FOREIGN KEY (SalesRepUserId) REFERENCES AspNetUsers(Id);
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Partner_PurchaseRep')
    ALTER TABLE Partner DROP CONSTRAINT FK_Partner_PurchaseRep;
GO
ALTER TABLE Partner ADD CONSTRAINT FK_Partner_PurchaseRep
    FOREIGN KEY (PurchaseRepUserId) REFERENCES AspNetUsers(Id);
GO
