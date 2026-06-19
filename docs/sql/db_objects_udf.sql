-- =============================================================================
-- UDF (Plan 34) — Faz 0 backfill + seed
-- 1) Mevcut Item.Description JSON çantasını gerçek kolonlara + AdditionalFields'a taşır.
-- 2) Volume/Weight/TempRange için seed UDF tanımları ekler.
-- İdempotent: yalnız JSON ('{' ile başlayan) Description satırları işlenir; tekrar çalışınca
-- Description düz metne indiği için yeniden işlenmez.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ─── 1. Backfill: Description JSON → MinStockLevel/MaxStockLevel + AdditionalFields ──
-- Not: Tek UPDATE içinde tüm RHS ifadeleri satırın ESKİ Description değerini okur
-- (set-based), bu yüzden Description'ı en son ActualDescription'a indirmek güvenli.
UPDATE Item
SET
    -- NULLIF: boş string ('') gürültüsü NULL'a indirilir (anahtar/kolon boş kalmaz)
    MinStockLevel = TRY_CAST(NULLIF(JSON_VALUE(Description, '$.MinQty'), '') AS DECIMAL(18,4)),
    MaxStockLevel = TRY_CAST(NULLIF(JSON_VALUE(Description, '$.MaxQty'), '') AS DECIMAL(18,4)),
    -- Tüketeni olmayan ekstra alanlar dinamik UDF çantasına taşınır (NULL/boş değer → anahtar atlanır)
    AdditionalFields = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(
        '{}',
        '$.Volume',    NULLIF(JSON_VALUE(Description, '$.Volume'), '')),
        '$.Weight',    NULLIF(JSON_VALUE(Description, '$.Weight'), '')),
        '$.TempRange', NULLIF(JSON_VALUE(Description, '$.TempRange'), '')),
    -- En son: gerçek açıklama çantadan çıkarılır. COALESCE: ActualDescription yoksa Description
    -- HAM bırakılır (veri kaybı yok). Geçersiz JSON kalan satır ikinci geçişte ISJSON guard'ıyla elenir.
    Description = COALESCE(JSON_VALUE(Description, '$.ActualDescription'), Description)
WHERE Description IS NOT NULL
  AND LEFT(LTRIM(Description), 1) = '{'
  AND ISJSON(Description) = 1;

PRINT 'UDF backfill tamamlandi (Item.Description JSON -> kolon + AdditionalFields).';
GO

-- ─── 2. Seed UDF tanımları (her şirket için Volume/Weight/TempRange) ──────────
-- Hacim (NUMBER)
INSERT INTO UserFieldDefinition (Id, CompanyId, EntityName, FieldName, LabelText, FieldType, OrderNo, IsRequired)
SELECT NEWID(), c.Id, 'Item', 'Volume', N'Hacim (m³)', 'NUMBER', 10, 0
FROM Company c
WHERE NOT EXISTS (SELECT 1 FROM UserFieldDefinition u
                  WHERE u.CompanyId = c.Id AND u.EntityName = 'Item' AND u.FieldName = 'Volume' AND u.IsDeleted = 0);
GO
-- Ağırlık (NUMBER)
INSERT INTO UserFieldDefinition (Id, CompanyId, EntityName, FieldName, LabelText, FieldType, OrderNo, IsRequired)
SELECT NEWID(), c.Id, 'Item', 'Weight', N'Ağırlık (kg)', 'NUMBER', 20, 0
FROM Company c
WHERE NOT EXISTS (SELECT 1 FROM UserFieldDefinition u
                  WHERE u.CompanyId = c.Id AND u.EntityName = 'Item' AND u.FieldName = 'Weight' AND u.IsDeleted = 0);
GO
-- Sıcaklık aralığı (SELECT/STATIC)
INSERT INTO UserFieldDefinition (Id, CompanyId, EntityName, FieldName, LabelText, FieldType, DataSourceType, DataSourceKey, DefaultValue, OrderNo, IsRequired)
SELECT NEWID(), c.Id, 'Item', 'TempRange', N'Sıcaklık Aralığı', 'SELECT', 'STATIC', N'Normal,Soğuk,Donmuş', N'Normal', 30, 0
FROM Company c
WHERE NOT EXISTS (SELECT 1 FROM UserFieldDefinition u
                  WHERE u.CompanyId = c.Id AND u.EntityName = 'Item' AND u.FieldName = 'TempRange' AND u.IsDeleted = 0);
GO

PRINT 'UDF seed tanimlari eklendi (Volume/Weight/TempRange).';
GO
