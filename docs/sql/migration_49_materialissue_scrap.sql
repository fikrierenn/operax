-- ============================================================
-- Plan 47 M1 Faz 4 — Zayiat (scrap): MaterialIssue reason-code
-- Sarf fişine başlık-seviye ReasonCode eklenir (NULL=normal tüketim, set=zayiat).
-- Zayiat onayında StockMovement.MovementType = 'SCRAP' yazılır (VUK zayiat ayrımı,
-- normal sarf 'ISSUE'). Yeni evrak YOK — MaterialIssue genişletilir (footprint-ladder).
-- Idempotent: COL_LENGTH + sys.check_constraints guard ile tekrar koşum no-op.
-- ============================================================
SET XACT_ABORT ON;
SET NOCOUNT ON;

-- Sarf nedeni: NULL → genel/idari tüketim (ISSUE) · DAMAGE/FIRE/WASTE → zayiat (SCRAP)
-- Kod değerleri İngilizce sabit (turkish-ui.md: Codes İNGİLİZCE); UI label'ı Türkçe (Hasar/Yangın/Hurda).
IF COL_LENGTH('MaterialIssueHeader', 'ReasonCode') IS NULL
    ALTER TABLE MaterialIssueHeader ADD ReasonCode NVARCHAR(20) NULL;
GO

-- İş kuralı: ReasonCode yalnız izinli zayiat kodları (veya NULL) olabilir
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_MaterialIssue_ReasonCode')
    ALTER TABLE MaterialIssueHeader ADD CONSTRAINT CK_MaterialIssue_ReasonCode
        CHECK (ReasonCode IS NULL OR ReasonCode IN ('DAMAGE', 'FIRE', 'WASTE'));
GO
