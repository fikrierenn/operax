# Plan 57 — Pick→Ship Ledger Handoff (toplanan-gerçek stok düşümü)

**Durum:** ✅ TAMAMLANDI 2026-06-25 (commit d87b198) · **Tier:** 3 · **Plan:** 56 Picking devamı (sql-sp-reviewer IMP-3)

> **Çıktı:** sql-sp-reviewer CRITICAL yok · fresh-DB 0 fail · E2E smoke multi-bin(5000→2563+2437)+short(200→150)+skip(200→0) → çıkış -5150 (drift yok), ShippingLine reconcile doğru. Done criteria tümü ✓.

## Problem

Toplama (pick) katmanı şu an **tamamen tavsiye niteliğinde**; ledger ondan beslenmiyor:

1. `sp_ShippingCreatePickTask` çoklu-bin FIFO allocation yapıyor ama `PickTaskLine`'a **`ShipLineId` yazmıyor** (CTE'de `sl.Id AS ShipLineId` var, INSERT'e girmiyor) → toplama satırı sevkiyat satırına bağlı değil.
2. `sp_ShippingPost` stok düşerken `ShippingLine.QtyBase`'i (planlanan) `ISNULL(sl.BinId, PickingBin)`'den düşüyor — pick'in topladığı **gerçek bin'leri (`TargetBinId`) ve gerçek miktarı (`QtyPickedBase`) hiç kullanmıyor**.

**Sonuç (drift senaryoları):**
- **Short-pick:** operatör az topladı (`QtyPickedBase < QtyRequestedBase`, `ExceptionNote='SHORT'`) ama POST tam `QtyBase` düşer → fiziksel ≠ ledger.
- **SKIP/DAMAGED:** kalem toplanmadı ama POST yine düşmeye çalışır.
- **Bin uyumsuzluğu:** pick depo rafından (A/B) topladı, POST `PickingBin`'den düşmeye çalışır → ya yanlış bin ya "yetersiz stok" THROW.

## Scope

**Dahil:** `PickTaskLine.ShipLineId` kolonu · `sp_ShippingCreatePickTask` (ShipLineId yaz) · `sp_ShippingPost` (pick-driven dalı: PickTaskLine başına gerçek consume + ShippingLine reconcile + SO.QtyShipped gerçekten + ItemCost gerçekten).
**Hariç:** Pick olmayan doğrudan sevkiyat akışı (mevcut ShippingLine-based consume korunur) · UI değişikliği (gerek yok) · Üretim emri akışı (deficit zaten ayrı).

## Çözüm

**Anahtar içgörü:** Pick task yalnız **mevcut stok** için `PickTaskLine` üretir (deficit → ProductionOrder). Yani `SUM(QtyPickedBase)` doğal olarak "fiilen sevk edilebilen" miktardır. Pick-driven sevkiyatta POST'u PickTaskLine'a dayandırmak short-pick + kısmi-stok + multi-bin'i **tek mekanizmayla** çözer.

### Faz 1 — Şema + allocation bağı
- `migration_57_pickline_shipline.sql`: `ALTER TABLE PickTaskLine ADD ShipLineId UNIQUEIDENTIFIER NULL` + index. Migrate listesine wire.
- `sp_ShippingCreatePickTask`: `INSERT INTO PickTaskLine (... ShipLineId)` + `SELECT ... a.ShipLineId`.

### Faz 2 — sp_ShippingPost pick-aware
- POST başında: `@HasPick = EXISTS(PickTask WHERE ShipmentId=@HeaderId AND Status<>'CANCELLED')`.
- **@HasPick = 1** (pick-driven dal):
  - Consume cursor `PickTaskLine` üzerinden: `QtyPickedBase > 0` satırlar (SKIP/DAMAGED/0 atlanır), `@BinId = TargetBinId`, `@QtyBase = QtyPickedBase`, `@SourceLineId = ShipLineId`, lot = ShippingLine.LotNo. Idempotency anahtarı `(SourceLineId, tip, bin, lot)` multi-bin'i zaten destekler.
  - `ShippingLine.QtyBase` → ship line başına `SUM(QtyPickedBase)` reconcile (sevkiyat gerçeği = toplanan). 0 toplanan satır 0'a iner.
  - `SO.QtyShipped += SUM(QtyPickedBase)` ship line başına (gerçek; kalan SO açık).
  - ItemCost OnHand: ürün başına `SUM(QtyPickedBase)`.
- **@HasPick = 0** (doğrudan): mevcut ShippingLine-based consume aynen.
- ShippingLine reconcile öncesi 0-qty satır consume cursor'da atlanır (`QtyBase>0`).

### Faz 3 — Review + fresh-DB + smoke
- `sql-sp-reviewer` (opus) — ledger atomiklik, idempotency, immutability.
- Fresh-DB migrate ritüeli (§3.5) — 0 fail + ShipLineId kolon mevcut.
- E2E smoke: ship oluştur → pick (1 satır SHORT, 1 SKIP, 1 tam) → POST → doğrula: StockMovement = toplanan toplam, ShippingLine = toplanan, SO.QtyShipped = toplanan (kalan açık), net drift = 0.

## Alternatifler (reddedilen)
- **A — ShippingLine'ı sp_PickConfirm'de reconcile et:** POST'tan önce ShippingLine düşür. Red: bin uyumsuzluğunu çözmez (POST hâlâ ShippingLine.BinId'den düşer); multi-bin gerçek tüketim kaybolur.
- **B — Guard-only (short varsa POST blokla):** En ucuz ama otomasyon yok, kullanıcı elle düzeltir; multi-bin disconnect kalır. Red: yarım çözüm.

## Riskler
- 🔴 **Contrarian:** Pick-driven dalda eski testler kırılır mı? → mevcut Faz D smoke pick-li POST'u zaten kapsamamış olabilir; fresh smoke şart.
- 🔵 **First Principles:** "Sevk = toplanan" doğru mu? Evet — fiilen toplanmayan sevk edilemez; kalan SO'da açık kalır (kısmi sevk standart).
- 🟢 **Expansionist:** ShipLineId ileride iade/parça-sevk izine de yarar.
- ⚪ **Outsider:** Multi-bin'de aynı ürün 2 ShippingLine'da → ShipLineId ile artık ayrışır (eski ItemId-eşleşme belirsizliği biter).
- 🟡 **Executor:** Faz 1 migration + allocation tek satır; Faz 2 cursor mantığı asıl iş.

## Done criteria
- [ ] PickTaskLine.ShipLineId dolu (yeni pick task'larda)
- [ ] Pick-driven POST: ledger = `SUM(QtyPickedBase)`, ShippingLine + SO.QtyShipped = toplanan
- [ ] Short/SKIP satır POST'u patlatmaz; kalan SO açık
- [ ] Doğrudan (pick'siz) sevkiyat davranışı değişmedi
- [ ] sql-sp-reviewer CRITICAL yok · fresh-DB 0 fail · E2E smoke net drift = 0

## Rollback
- migration geri: kolon NULLABLE eklenir, eski SP'ler kolonu görmezse çalışır (geri uyumlu). SP'ler `CREATE OR ALTER` — önceki sürüme revert + migrate.

## Adımlar (sıra)
1. Faz 1 şema + allocation (migration_57 + sp_ShippingCreatePickTask)
2. fresh-DB ara-test (kolon eklendi mi)
3. Faz 2 sp_ShippingPost pick-aware dal
4. Faz 3 review + fresh-DB + E2E smoke
5. Commit (faz başına) + plan arşiv
