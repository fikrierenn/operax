-- ============================================================
-- Plan 47 M1 Faz 1 — ItemCost grain: per-depo → ŞİRKET-GENEL (WarehouseId NULL)
-- Sorun: ItemCost per-depo (WarehouseId SET) saklanıyordu ama 2 okuyucu (PO öneri,
--   fiyat listesi) WarehouseId filtresiz şirket-genel okuyor → çoklu-depo ürününde
--   çoklu-satır JOIN belirsizliği. Ayrıca OnHandQty snapshot reverse/transfer/sayımda
--   güncellenmiyor → drift (canlıda OnHandQty=-54 gözlendi).
-- Çözüm: per-depo satırları şirket-genel'e collapse. AvgCost = pozitif-qty ağırlıklı
--   ortalama (yoksa MAX). OnHandQty = şirket-genel ledger SUM (türev, IsCancelled=0).
-- UX_ItemCost_Key (CompanyId,ItemId,WarehouseId): NULL warehouse tekil → company+item
--   başına TEK şirket-genel satır garantisi (SQL Server UNIQUE NULL'ları eşit sayar).
-- Idempotent: per-depo satır kalmadıysa no-op.
-- ============================================================
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM ItemCost WHERE WarehouseId IS NOT NULL)
BEGIN
    -- 1) Per-depo satırlardan şirket-genel AvgCost türet (pozitif-miktar ağırlıklı; yoksa MAX fallback)
    ;WITH Merged AS (
        SELECT CompanyId, ItemId,
               CASE WHEN SUM(CASE WHEN OnHandQty > 0 THEN OnHandQty ELSE 0 END) > 0
                    THEN SUM(AvgCost * CASE WHEN OnHandQty > 0 THEN OnHandQty ELSE 0 END)
                         / SUM(CASE WHEN OnHandQty > 0 THEN OnHandQty ELSE 0 END)
                    ELSE MAX(AvgCost) END AS MergedAvg
        FROM ItemCost
        WHERE WarehouseId IS NOT NULL
        GROUP BY CompanyId, ItemId
    )
    MERGE ItemCost AS t
    USING Merged AS s
       ON t.CompanyId = s.CompanyId AND t.ItemId = s.ItemId AND t.WarehouseId IS NULL
    WHEN MATCHED THEN
        UPDATE SET AvgCost = s.MergedAvg, UpdatedAt = GETUTCDATE()
    WHEN NOT MATCHED THEN
        INSERT (Id, CompanyId, ItemId, WarehouseId, AvgCost, OnHandQty)
        VALUES (NEWID(), s.CompanyId, s.ItemId, NULL, s.MergedAvg, 0);

    -- 2) Per-depo satırları sil (artık şirket-genel'de toplandı)
    DELETE FROM ItemCost WHERE WarehouseId IS NOT NULL;
END
GO

-- 3) Şirket-genel satırların OnHandQty'sini ledger'a senkronla (türev — tek doğruluk kaynağı).
--    Drift'i sıfırlar; bundan sonra sp_UpdateItemCostMovingAvg her harekette ledger'dan yeniler.
-- İade (RECEIVING_RETURN) maliyet tabanına girmez (sp_UpdateItemCostMovingAvg ile aynı tanım) → dışla.
UPDATE ic
SET OnHandQty = ISNULL((
        SELECT SUM(sm.QtyBase) FROM StockMovement sm
        WHERE sm.CompanyId = ic.CompanyId AND sm.ItemId = ic.ItemId AND sm.IsCancelled = 0
          AND ISNULL(sm.SourceDocType, '') <> 'RECEIVING_RETURN'), 0),
    UpdatedAt = GETUTCDATE()
FROM ItemCost ic
WHERE ic.WarehouseId IS NULL;
GO
