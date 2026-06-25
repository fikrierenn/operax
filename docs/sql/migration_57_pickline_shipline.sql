-- =============================================================================
-- Migration 57 — Pick→Ship Ledger Handoff (Plan 57)
-- PickTaskLine'a kaynak sevkiyat satırı bağı (ShipLineId).
--   ShipLineId : bu toplama satırının türediği ShippingLine.Id. Çoklu-bin allocation'da
--                bir ShippingLine birden çok PickTaskLine üretir → hepsi aynı ShipLineId'yi taşır.
--                sp_ShippingPost pick-driven dalında ledger'ı PickTaskLine gerçeğine bağlamak için ZORUNLU.
-- İdempotent: kolon yoksa ekle + arama indeksi.
-- =============================================================================

IF COL_LENGTH('dbo.PickTaskLine', 'ShipLineId') IS NULL
    ALTER TABLE dbo.PickTaskLine ADD ShipLineId UNIQUEIDENTIFIER NULL;
GO

-- ShipLineId üzerinden POST consume sorgusu (ship line başına toplanan toplam) için indeks
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PickTaskLine_ShipLineId' AND object_id = OBJECT_ID('dbo.PickTaskLine'))
    CREATE INDEX IX_PickTaskLine_ShipLineId ON dbo.PickTaskLine(ShipLineId) WHERE ShipLineId IS NOT NULL;
GO
