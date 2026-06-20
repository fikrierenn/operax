-- ============================================================
-- Plan 35 Faz 1 — DictionaryValue referans kolonları
-- UnEceCode: UN/ECE Rec 20 birim kodu (e-Belge zorunlu, ör. Adet=C62) — yalnız UOM tipi doldurur
-- IsWholeNumber: tam sayı birim mi (Adet/Koli gibi bölünemez) — yalnız UOM tipi doldurur
-- Her iki kolon da nullable; diğer dict tipleri için NULL kalır.
-- Idempotent: IF NOT EXISTS korumalı. ALTER sonrası GO (batch ayrımı — DocChain dersi).
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='DictionaryValue' AND COLUMN_NAME='UnEceCode')
    ALTER TABLE DictionaryValue ADD UnEceCode NVARCHAR(10) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='DictionaryValue' AND COLUMN_NAME='IsWholeNumber')
    ALTER TABLE DictionaryValue ADD IsWholeNumber BIT NULL;
GO
