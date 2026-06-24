-- =============================================================================
-- Migration 56 — Picking Workflow (Plan 56 Faz B)
-- PickTaskLine'a pick-path sıra (PickSeq) + istisna notu (ExceptionNote) kolonları.
--   PickSeq      : serpentine sıra (task açılışında ROW_NUMBER OVER Zone,SortNo ile dondurulur)
--   ExceptionNote: short-pick / atla / hasar gerekçe izi (Faz C terminal istisna akışı)
-- İdempotent: kolon yoksa ekle.
-- =============================================================================

IF COL_LENGTH('dbo.PickTaskLine', 'PickSeq') IS NULL
    ALTER TABLE dbo.PickTaskLine ADD PickSeq INT NULL;
GO

IF COL_LENGTH('dbo.PickTaskLine', 'ExceptionNote') IS NULL
    ALTER TABLE dbo.PickTaskLine ADD ExceptionNote NVARCHAR(500) NULL;
GO
