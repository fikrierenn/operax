-- =============================================================================
-- M01 Partner genişletme — Plan 08 (Cari Kart Tablı Yapı)
-- Faz 0: Sorumlu temsilci (satış/satınalma) kolonları
-- Faz 2: PartnerContact / PartnerAddress / PartnerBankAccount / PartnerActivity
-- Tümü idempotent (IF NOT EXISTS koruması).
-- =============================================================================
SET NOCOUNT ON;

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
