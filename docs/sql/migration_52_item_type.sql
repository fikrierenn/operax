-- ============================================================
-- Plan 52 Faz 1 — Item taksonomisi sadeleştir: CONSUMABLE kaldır
-- "Sarf" bir KULLANIM (MaterialIssue evrağı), ürün DOĞASI değil; aynı mal hem satılır
-- hem sarf olur (streç film) → mutually-exclusive CONSUMABLE tipi yanlış kategoriydi.
-- ItemType kapalı sistem-kümesi (SP'ler branch'ler) → CHECK constraint'li.
-- Idempotent.
-- ============================================================
SET XACT_ABORT ON;
SET NOCOUNT ON;

-- Eski CONSUMABLE item'ları STOCK'a taşı (canlıda 0 beklenir; defensif — fresh-install + eski kurulum)
UPDATE Item SET ItemType = 'STOCK', UpdatedAt = GETUTCDATE() WHERE ItemType = 'CONSUMABLE';
GO

-- Kapalı küme CHECK: yalnız STOCK / SERVICE / FIXED_ASSET. Sözlük-driven DEĞİL (reason'dan farklı) —
-- bu değerler SP/iş-mantığında branch'lenir, admin keyfi ekleyemez.
-- Mevcut DB'de migration_41 eski CONSUMABLE'lı CHECK'i kurmuş olabilir → DROP + yeniden kur (idempotent).
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Item_ItemType')
    ALTER TABLE Item DROP CONSTRAINT CK_Item_ItemType;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Item_ItemType')
    ALTER TABLE Item WITH CHECK ADD CONSTRAINT CK_Item_ItemType
        CHECK (ItemType IN ('STOCK', 'SERVICE', 'FIXED_ASSET'));
GO
