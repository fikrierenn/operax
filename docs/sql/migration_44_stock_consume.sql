-- ============================================================
-- Plan 44 Faz 1 — Stock consume primitive altyapısı (schema)
-- Idempotent: kolon + index NOT EXISTS guard'lı. Additive (drop güvenli, veri silmez).
-- ============================================================
SET XACT_ABORT ON;
SET NOCOUNT ON;

-- 1) Kaynak SATIR kimliği — forensic iz + idempotency anahtarı için (audit High: yoktu)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('StockMovement') AND name = 'SourceLineId')
    ALTER TABLE StockMovement ADD SourceLineId UNIQUEIDENTIFIER NULL;
GO

-- 2) Consume destek index'i — HOLDLOCK key-range kilidi + bakiye SUM kapsaması.
--    Filtered (IsCancelled=0): bakiye yalnız iptal-edilmemiş hareketlerden; tarihsel iptaller dışta.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockMovement_Consume' AND object_id = OBJECT_ID('StockMovement'))
    CREATE INDEX IX_StockMovement_Consume
        ON StockMovement (CompanyId, ItemId, WarehouseId, BinId, LotNo)
        INCLUDE (QtyBase)
        WHERE IsCancelled = 0;
GO

-- 3) Idempotency unique index — aynı kaynak satır + hareket tipi iki kez işlenemez.
--    Filtered: yalnız primitive üzerinden yazılan (SourceLineId dolu) + iptal-edilmemiş satırlar.
--    Tarihsel satırlar (SourceLineId NULL) hariç → mevcut veri çakışmaz.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_StockMovement_Idempotency' AND object_id = OBJECT_ID('StockMovement'))
    CREATE UNIQUE INDEX UX_StockMovement_Idempotency
        ON StockMovement (SourceDocType, SourceDocId, SourceLineId, MovementType)
        WHERE SourceLineId IS NOT NULL AND IsCancelled = 0;
GO
