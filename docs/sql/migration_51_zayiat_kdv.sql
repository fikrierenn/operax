-- ============================================================
-- Plan 51 — Zayiat KDV ön-muhasebe (DATA-DRIVEN: sözlük + flag)
-- Sarf/zayiat nedeni vokabüleri DictionaryType 'MATERIAL_ISSUE_REASON' altında
-- (admin yönetir, NameTr merkezi). KDV davranışı DictionaryValue.RequiresKdvAdjustment
-- flag'inde (mevzuat-bağlı; DictRefCols UnEceCode/IsWholeNumber pattern'i).
-- MaterialIssueHeader'a mücbir-sebep + hesaplanan ilave-KDV + yasal belge ref.
-- Idempotent.
-- ============================================================
SET XACT_ABORT ON;
SET NOCOUNT ON;

-- Mücbir sebep (Maliye-ilan afet: deprem/sel/yangın → KDV korunur; herhangi KDV-reason'da geçerli)
IF COL_LENGTH('MaterialIssueHeader', 'IsForceMajeure') IS NULL
    ALTER TABLE MaterialIssueHeader ADD IsForceMajeure BIT NOT NULL DEFAULT 0;
GO

-- POST'ta hesaplanan ilave-edilecek KDV (türev snapshot; KDV-nötr/mücbir → 0)
IF COL_LENGTH('MaterialIssueHeader', 'KdvAdjustmentAmount') IS NULL
    ALTER TABLE MaterialIssueHeader ADD KdvAdjustmentAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
GO

-- Yasal belge ref (takdir komisyonu karar / itfaiye-sigorta rapor no)
IF COL_LENGTH('MaterialIssueHeader', 'LegalDocRef') IS NULL
    ALTER TABLE MaterialIssueHeader ADD LegalDocRef NVARCHAR(100) NULL;
GO

-- Sözlük flag: bu zayiat nedeni indirilen KDV düzeltmesini (md.30/c) gerektirir mi?
-- Yalnız MATERIAL_ISSUE_REASON tipi doldurur (DictRefCols pattern). NULL=ilgisiz/KDV-nötr.
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='DictionaryValue' AND COLUMN_NAME='RequiresKdvAdjustment')
    ALTER TABLE DictionaryValue ADD RequiresKdvAdjustment BIT NULL;
GO

-- Eski hardcoded WASTE → SCRAP veri map (Plan 49→51 taksonomi netleşmesi)
UPDATE MaterialIssueHeader SET ReasonCode = 'SCRAP' WHERE ReasonCode = 'WASTE';
GO

-- ReasonCode artık sözlük-driven (admin yeni neden ekleyebilir) → sabit-küme CHECK KALDIRILIR.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_MaterialIssue_ReasonCode')
    ALTER TABLE MaterialIssueHeader DROP CONSTRAINT CK_MaterialIssue_ReasonCode;
GO
