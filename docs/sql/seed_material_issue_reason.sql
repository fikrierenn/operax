-- Baseline: Sarf/Zayiat nedeni sözlüğü (MATERIAL_ISSUE_REASON) — her şirket için.
-- Vokabüler veri-driven (admin yönetir); KDV davranışı RequiresKdvAdjustment flag'inde (mevzuat md.30/c).
-- Code İngilizce (sistem-çapa), NameTr Türkçe (kullanıcı görünür). Idempotent (NOT EXISTS).

-- 1) Tip — eksik olan her şirkete
INSERT INTO DictionaryType (Id, CompanyId, Code, NameTr, NameEn, IsSystem)
SELECT NEWID(), c.Id, 'MATERIAL_ISSUE_REASON', N'Sarf/Zayiat Nedeni', 'Material Issue Reason', 1
FROM Company c
WHERE c.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM DictionaryType dt WHERE dt.Code = 'MATERIAL_ISSUE_REASON' AND dt.CompanyId = c.Id);
GO

-- 2) Değerler — tip başına 4 neden + KDV flag (DAMAGE/FIRE/SCRAP=düzeltme, NORMAL_FIRE=nötr)
;WITH r AS (
    SELECT 'DAMAGE'      AS Code, N'Hasar / Bozulma'        AS NameTr, 1 AS Kdv, 1 AS OrderNo
    UNION ALL SELECT 'FIRE',        N'Yangın',                1, 2
    UNION ALL SELECT 'SCRAP',       N'Hurda / İmha',          1, 3
    UNION ALL SELECT 'NORMAL_FIRE', N'Normal Fire (üretim)',  0, 4
)
INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, OrderNo, IsActive, RequiresKdvAdjustment)
SELECT NEWID(), dt.CompanyId, dt.Id, r.Code, r.NameTr, r.OrderNo, 1, r.Kdv
FROM DictionaryType dt
CROSS JOIN r
WHERE dt.Code = 'MATERIAL_ISSUE_REASON'
  AND NOT EXISTS (SELECT 1 FROM DictionaryValue dv WHERE dv.TypeId = dt.Id AND dv.Code = r.Code AND dv.IsDeleted = 0);
GO
