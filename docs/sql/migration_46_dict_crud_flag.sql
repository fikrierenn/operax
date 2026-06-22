-- ============================================================
-- Plan 46 M0 Faz 1 — DictionaryType.AllowValueCrud (ADR-02 kod-çıpalı vs dinamik)
-- Kod-çıpalı tipler (SP/ledger CODE'a göre dallanır): label düzenlenir ama değer EKLE/SİL YOK
--   (kullanıcı "POSTED"i silse sp_ValidateStatusTransition/Post SP kırılır).
-- Dinamik tipler (iş-mantığı dallandırmaz): tam CRUD.
-- IsSystem tüm tiplerde 1 olduğu için ayrım YAPAMAZ → ayrı flag (REFERENCE_INVENTORY sınıflandırması).
-- Idempotent: kolon NOT EXISTS guard + UPDATE ile flag seed.
-- ============================================================
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DictionaryType') AND name = 'AllowValueCrud')
    ALTER TABLE DictionaryType ADD AllowValueCrud BIT NOT NULL DEFAULT 0;
GO

-- DictionaryValue zorunlu audit kolonu UpdatedAt eksikti (sql-conventions §1) → CRUD UPDATE'leri için ekle.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DictionaryValue') AND name = 'UpdatedAt')
    ALTER TABLE DictionaryValue ADD UpdatedAt DATETIME2 NULL;
GO

-- Varsayılan: 0 (kod-çıpalı, güvenli). Dinamik tipler 1'e çekilir (kullanıcı tam yönetir).
UPDATE DictionaryType SET AllowValueCrud = 1
WHERE Code IN (
    'UOM', 'TAX_RATE', 'CURRENCY', 'PAYMENT_METHOD', 'PAYMENT_TERM',
    'PARTNER_CATEGORY', 'WITHHOLDING', 'BRAND',
    -- sektörel öznitelikler (kullanıcı-genişletilebilir, iş-mantığı dallandırmaz)
    'SIZE', 'COLOR', 'SEASON', 'FABRIC', 'PUBLISHER', 'LANGUAGE', 'BINDING', 'GENRE',
    'ALLERGEN', 'STORAGE', 'CERTIFICATE'
);
GO
