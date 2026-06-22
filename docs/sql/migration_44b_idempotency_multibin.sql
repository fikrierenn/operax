-- ============================================================
-- Plan 44 Faz 3 — Idempotency index'i multi-bin split'e aç (BinId + LotNo ekle)
-- Bir kaynak satır birden çok bin'e/lot'a bölünebilir (FEFO allocation) → her (satır,bin,lot)
-- tekil. Eski (satır,tip) tekilliği split'i engelliyordu. Idempotent: drop + recreate.
-- ============================================================
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_StockMovement_Idempotency' AND object_id = OBJECT_ID('StockMovement'))
    DROP INDEX UX_StockMovement_Idempotency ON StockMovement;
GO

CREATE UNIQUE INDEX UX_StockMovement_Idempotency
    ON StockMovement (SourceDocType, SourceDocId, SourceLineId, MovementType, BinId, LotNo)
    WHERE SourceLineId IS NOT NULL AND IsCancelled = 0;
GO
