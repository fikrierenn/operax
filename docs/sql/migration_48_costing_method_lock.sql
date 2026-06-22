-- ============================================================
-- Plan 47 M1 Faz 3 — CostingMethod yalnız MOVING_AVG (FIFO/STANDARD motoru yok)
-- Bulgu: CostingMethod parametresi/Item kolonu hiçbir kod tarafından OKUNMUYOR
--   (sp_UpdateItemCostMovingAvg method'a bakmaz) → "sessiz yanlış sonuç" teorik.
--   Tek maruziyet: yanıltıcı Description ("MOVING_AVG, FIFO, STANDARD") generic Parameters
--   ekranında admin'e FIFO çalışıyormuş izlenimi verir.
-- Çözüm: Description'ı düzelt + savunmacı: MOVING_AVG dışı değer geri çekilir.
-- Idempotent: WHERE guard tekrar koşumda no-op.
-- FIFO/STANDARD motoru = ayrı gelecek plan (Plan 47 kapsam dışı).
-- ============================================================
SET XACT_ABORT ON;
SET NOCOUNT ON;

DECLARE @desc NVARCHAR(400) = N'Maliyetlendirme yöntemi: yalnızca MOVING_AVG uygulanır (FIFO/STANDARD motoru henüz yok).';

UPDATE Parameter
SET Value       = 'MOVING_AVG',
    Description  = @desc,
    UpdatedAt    = GETUTCDATE()
WHERE Code = 'CostingMethod' AND ModuleCode = 'M02'
  AND (Value <> 'MOVING_AVG' OR ISNULL(Description, '') <> @desc);
GO

-- Item.CostingMethod kolonu da okunmuyor; yanlış izlenim vermesin diye MOVING_AVG dışı değerleri temizle.
UPDATE Item
SET CostingMethod = NULL, UpdatedAt = GETUTCDATE()
WHERE CostingMethod IS NOT NULL AND CostingMethod <> 'MOVING_AVG';
GO
