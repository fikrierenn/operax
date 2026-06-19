# M02 — Maliyetlendirme: Hareketli Ortalama + FIFO + Standart

> Sürüm: v1 · Tarih: 2026-05-28
> Önkoşul: `schema_M02_Costing.sql` (ItemCost, PriceVariance, StockMovement.UnitCost)

---

## 1. Çoklu Maliyet Yöntemi Mimarisi

Operax üç maliyet yöntemini destekler. Hangi yöntem aktif olacağı `Parameter` tablosunda set edilir:
- `CostingMethod = 'MOVING_AVG'`  → hareketli ağırlıklı ortalama (varsayılan)
- `CostingMethod = 'FIFO'`        → ilk giren ilk çıkar
- `CostingMethod = 'STANDARD'`    → standart maliyet (varyans ayrı hesaba yazılır)

Parametre Item başına da override edilebilir: `Item.CostingMethod NULL = sistem geneli`.

---

## 2. Hareketli Ortalama (Moving Average)

Mevcut `ItemCost.AvgCost` kolonu zaten bunun için tasarlandı. Mantık:

```
Yeni AvgCost = (OnHandQty × OldAvgCost + ReceivedQty × ReceivedUnitCost)
               / (OnHandQty + ReceivedQty)
```

### `sp_UpdateItemCostMovingAvg`

```sql
CREATE OR ALTER PROCEDURE sp_UpdateItemCostMovingAvg
    @CompanyId UNIQUEIDENTIFIER, @ItemId UNIQUEIDENTIFIER,
    @WarehouseId UNIQUEIDENTIFIER = NULL,
    @ReceivedQty DECIMAL(18,6), @ReceivedUnitCost DECIMAL(18,4),
    @MovementType NVARCHAR(20)              -- RECEIPT, ISSUE, ADJUSTMENT
AS
BEGIN
    SET XACT_ABORT ON;
    -- ItemCost satırı yoksa oluştur
    IF NOT EXISTS (SELECT 1 FROM ItemCost WHERE CompanyId = @CompanyId AND ItemId = @ItemId
                   AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL)))
    BEGIN
        INSERT INTO ItemCost (Id, CompanyId, ItemId, WarehouseId, AvgCost, OnHandQty)
        VALUES (NEWID(), @CompanyId, @ItemId, @WarehouseId, 0, 0);
    END

    DECLARE @OldAvg DECIMAL(18,4), @OldQty DECIMAL(18,6);
    SELECT @OldAvg = AvgCost, @OldQty = OnHandQty
    FROM ItemCost WHERE CompanyId = @CompanyId AND ItemId = @ItemId
                  AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL));

    IF @MovementType = 'RECEIPT'
    BEGIN
        DECLARE @NewQty DECIMAL(18,6) = @OldQty + @ReceivedQty;
        DECLARE @NewAvg DECIMAL(18,4) =
            CASE WHEN @NewQty > 0
                 THEN (@OldQty * @OldAvg + @ReceivedQty * @ReceivedUnitCost) / @NewQty
                 ELSE @ReceivedUnitCost
            END;
        UPDATE ItemCost
        SET AvgCost = @NewAvg, OnHandQty = @NewQty,
            LastReceiptDate = GETUTCDATE(), UpdatedAt = GETUTCDATE()
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId
          AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL));
    END
    ELSE IF @MovementType = 'ISSUE'
    BEGIN
        -- ISSUE'da AvgCost değişmez; sadece OnHandQty düşer
        UPDATE ItemCost
        SET OnHandQty = OnHandQty - @ReceivedQty, UpdatedAt = GETUTCDATE()
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId
          AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL));
    END
END
GO
```

`sp_ReceivingPost` her satır için bu SP'yi çağırır. `sp_ShippingPost` ISSUE için çağırır, StockMovement.UnitCost'a o anki `ItemCost.AvgCost` yazılır (COGS için).

---

## 3. FIFO Yöntemi

**Mantık:** Her RECEIPT bir maliyet lot'u açar (gerçek lot değil, maliyet katmanı). ISSUE en eski katmandan başlar tüketir.

### Yeni Tablo: `ItemCostLayer`

```sql
CREATE TABLE ItemCostLayer (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    WarehouseId UNIQUEIDENTIFIER NULL,
    ReceiptDate DATETIME2 NOT NULL,
    SourceMovementId UNIQUEIDENTIFIER NOT NULL,   -- RECEIPT StockMovement.Id
    QtyIn DECIMAL(18,6) NOT NULL,
    QtyRemaining DECIMAL(18,6) NOT NULL,
    UnitCost DECIMAL(18,4) NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);
CREATE INDEX IX_ItemCostLayer_Fifo ON ItemCostLayer(CompanyId, ItemId, WarehouseId, ReceiptDate)
    WHERE QtyRemaining > 0;
```

### `sp_PostFifoIssue`

```sql
CREATE OR ALTER PROCEDURE sp_PostFifoIssue
    @CompanyId UNIQUEIDENTIFIER, @ItemId UNIQUEIDENTIFIER,
    @WarehouseId UNIQUEIDENTIFIER = NULL, @IssueQty DECIMAL(18,6),
    @MovementId UNIQUEIDENTIFIER,
    @WeightedUnitCost DECIMAL(18,4) OUTPUT
AS
BEGIN
    DECLARE @Remaining DECIMAL(18,6) = @IssueQty;
    DECLARE @TotalCost DECIMAL(18,4) = 0;
    DECLARE @LayerId UNIQUEIDENTIFIER, @LayerQty DECIMAL(18,6), @LayerCost DECIMAL(18,4);

    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT Id, QtyRemaining, UnitCost FROM ItemCostLayer
    WHERE CompanyId = @CompanyId AND ItemId = @ItemId
      AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL))
      AND QtyRemaining > 0
    ORDER BY ReceiptDate;

    OPEN cur; FETCH NEXT FROM cur INTO @LayerId, @LayerQty, @LayerCost;
    WHILE @Remaining > 0 AND @@FETCH_STATUS = 0
    BEGIN
        DECLARE @TakeQty DECIMAL(18,6) = CASE WHEN @LayerQty <= @Remaining THEN @LayerQty ELSE @Remaining END;
        UPDATE ItemCostLayer SET QtyRemaining = QtyRemaining - @TakeQty WHERE Id = @LayerId;
        SET @TotalCost += @TakeQty * @LayerCost;
        SET @Remaining -= @TakeQty;
        IF @Remaining > 0 FETCH NEXT FROM cur INTO @LayerId, @LayerQty, @LayerCost;
    END
    CLOSE cur; DEALLOCATE cur;

    IF @Remaining > 0
        THROW 50100, 'Yetersiz FIFO katmanı — negatif stok için maliyet hesaplanamadı.', 1;

    SET @WeightedUnitCost = @TotalCost / @IssueQty;
    UPDATE StockMovement SET UnitCost = @WeightedUnitCost WHERE Id = @MovementId;
END
GO
```

---

## 4. Standart Maliyet

**Mantık:** `Item.StandardCost` sabit değerdir. RECEIPT veya ISSUE bu sabit ile yapılır. Gerçek satınalma fiyatı farklıysa **fark** ayrı muhasebe kalemine yazılır.

```sql
ALTER TABLE Item ADD
    StandardCost DECIMAL(18,4) NULL,
    StandardCostValidFrom DATE NULL;
```

### Maliyet Varyansı

`PriceVariance` tablosu zaten var. Standart maliyet yönteminde:
- ReceivedUnitCost > StandardCost → ALICI YANINDA ZARAR (fark + olarak yazılır)
- ReceivedUnitCost < StandardCost → ALICI YANINDA KAZANÇ

`sp_PostStandardCostVariance` mal kabul SP'sinde otomatik çağrılır.

---

## 5. Üretim Maliyeti (M10 ile entegre)

Üretim çıktısı (PRODUCTION StockMovement) için UnitCost şöyle hesaplanır:

```
ProductUnitCost = (ToplamHammaddeMaliyeti + ToplamİşçilikMaliyeti + ToplamMakineMaliyeti) / ÜretilenAdet
```

`ProductionOrder.ActualMaterialCost + ActualResourceCost` zaten hesaplanıyor (M10 mevcut). Production tamamlandığında:
1. PRODUCTION StockMovement açılır
2. UnitCost = (ActualMaterialCost + ActualResourceCost) / QtyProduced
3. ItemCost güncellenir (Moving Avg ise Avg, FIFO ise yeni katman, Standard ise varyans hesabı)

---

## 6. Hammadde Sarfiyat Maliyeti

Üretimde hammadde tüketildiğinde:
1. CONSUMPTION StockMovement açılır (negatif qty)
2. UnitCost = O anki ItemCost.AvgCost (veya FIFO katmanından çek)
3. `ProductionOrder.ActualMaterialCost += UnitCost * ConsumedQty`

---

## 7. Aylık Maliyet Yeniden Hesaplama

Veri tutarlılığı için ay sonu mutabakat job'u (`Hangfire`): `sp_RecalculateItemCost`

Tüm ItemCost'u dökerek başa al, tüm StockMovement'ları sırayla işle, yeniden hesapla. Olası hesap kaymalarını düzeltir.

---

## 8. Raporlar

| Rapor | View / TVF | UI Yol |
|---|---|---|
| Stok değer raporu (anlık) | `v_InventoryValuation` | `/inventory/valuation` |
| Satılan ürün maliyeti (COGS) | `v_CogsReport` | `/sales/cogs` |
| Maliyet katmanları (FIFO için) | `tvf_FifoLayers` | `/inventory/cost-layers` |
| Standart vs fiili varyans | `v_StandardCostVariance` | `/inventory/cost-variance` |
| Aylık maliyet trendi | `tvf_MonthlyCostTrend` | `/inventory/cost-trend` |

```sql
CREATE OR ALTER VIEW v_InventoryValuation AS
SELECT
    ic.CompanyId, ic.ItemId, i.Code, i.NameTr,
    ic.WarehouseId, w.Name AS WarehouseName,
    ic.OnHandQty, ic.AvgCost,
    ic.OnHandQty * ic.AvgCost AS TotalValue
FROM ItemCost ic
JOIN Item i ON i.Id = ic.ItemId
LEFT JOIN Warehouse w ON w.Id = ic.WarehouseId
WHERE ic.OnHandQty > 0;
```

---

## 9. Test Senaryoları

1. **Moving Avg basit:** 10 adet @ 10₺ alındı (AvgCost=10), sonra 10 @ 12₺ daha — AvgCost = 11₺.
2. **Moving Avg satış:** Yukarıdaki durumda 5 adet sevkiyat → COGS satırı 11×5 = 55₺. AvgCost değişmez.
3. **FIFO basit:** 10 adet @ 10₺ + 10 adet @ 12₺ alındı. 15 adet sevkiyat → COGS = (10×10 + 5×12) = 160₺ / 15 = 10.67₺ WeightedUnitCost.
4. **FIFO katmanları:** İlk katman tükendiğinde QtyRemaining = 0 olur, ikinci katmandan devam eder.
5. **Standard varyans:** StandardCost=10, gerçek alış 12 → PriceVariance kaydı 2₺ fark olarak yazılır.
6. **Üretim maliyeti:** 10 adet ürün üretildi, hammadde 200₺ + işçilik 100₺ + makine 50₺ → ProductUnitCost = 35₺.
7. **Negatif stok hatası:** FIFO modda 5 adet satılırken sadece 3 adet katman var → THROW.

---

## 10. Performans Notları

- `ItemCostLayer` tablosunda FIFO sorgusu için `IX_ItemCostLayer_Fifo` filtered index zorunludur (`WHERE QtyRemaining > 0`).
- `sp_PostFifoIssue` cursor kullanır ama her satırda küçük setlerle çalışır (ürün başına ortalama 5-20 katman). Cursor LOCAL FAST_FORWARD ile minimum overhead.
- Aylık mutabakat job'u tablo lock'u almamak için her ürün için ayrı transaction içinde döner.
