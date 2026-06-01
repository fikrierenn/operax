# Plan 25 — Sarf Tüketim (Consumption / Material Issue)

**Tarih:** 2026-06-01 · **Durum:** `Taslak (bekliyor — Plan 24 sonrası)` · **Modül:** M02 (Stok) · **Paket:** STARTER/MANUFACTURING sınırı (denetim: tartışmalı) · **Kaynak:** Plan 24'ten bölündü + competitor-analyst sarf-tüketim analizi (2026-06-01)

> **Bağımlılık:** Plan 24 (ItemType genişletme — CONSUMABLE değeri) önce bitmeli.

---

## 1. Problem

Sarf malzeme (`ItemType=CONSUMABLE`) alındığında stok girer (Plan 24 PurchaseInvoice). **Tüketildiğinde stok düşmeli** — ama genel/idari sarf (kırtasiye, temizlik, bakım) için belge yok. `ProductionConsumption` üretim-emri zorunlu (`ProductionOrderId NOT NULL`) — genel sarfı karşılamaz.

### Domain kararı (competitor-analyst 2026-06-01)
- Sarf tüketimi = **sadece StockMovement ISSUE + maliyet** (anlık Moving Avg). **AccountMovement YAZILMAZ** (partner yok, iç tüketim — cari bakiyeyi kirletir). Mikro da sarfı stok+GL'ye yazar, cariye değil.
- **Muhasebe/gider hesabı çalıştırılmaz** (GL/K1 ertelendi). Gider zaten alış faturasında (PurchaseInvoice) belgelendi — VUK ihlali yok. Tüketim = maliyetin stok→gider sınıflandırması = GL işi, ertelendi.
- `SourceDocType=CONSUMPTION` köprüsü ile GL açılınca gider mahsubu üretilebilir.
- CostCenterId opsiyonel boyut (masraf merkezi raporu).

---

## 2. Scope (taslak — Plan 24 sonrası netleşir)

### Kapsam dahili
- Yeni belge: `ConsumptionHeader` (CompanyId, **WarehouseId** [Header'da — mevcut Post SP pattern'i], DocNo, ConsumptionDate, Status DRAFT/POSTED/CANCELLED, CostCenterId NULL, audit).
- `ConsumptionLine`: ItemId, UomId, **Qty (orijinal) + QtyBase** (fn_GetConversionRate), BinId NULL.
- **Tablo adı çakışma:** `ProductionConsumption` ile karışmasın → `MaterialIssueHeader`/`StockConsumptionHeader` gibi ad (Faz 0 kararı).
- `sp_ConsumptionPost`: tek transaction StockMovement ISSUE (MovementType.ISSUE, SourceDocType=CONSUMPTION, UnitCost=anlık Moving Avg). **BinId fallback** (ISNULL(@BinId, PickingBin) — Shipping pattern; StockMovement.BinId NOT NULL). AccountMovement'a DOKUNMAZ.
- `sp_ConsumptionReverse`: StockMovement flag-only iptal (Plan 22 dersi — ters satır YOK).
- `Features/MaterialIssue/` CRUD UI; Item dropdown ItemType=CONSUMABLE filtreli.

### Kapsam dışı
- Gider hesabı mahsubu (GL/K1).
- Üretim sarfı (ProductionConsumption — ayrı).
- Otomatik min-stok sarf tetikleme.

## 3. Açık Kararlar (Plan 24 sonrası)
- [ ] STARTER mi MANUFACTURING mi? (denetim: tam-belge yaşam döngüsü STARTER için ağır olabilir)
- [ ] Tablo adı: MaterialIssue vs StockConsumption vs GeneralConsumption
- [ ] CostCenterId zorunlu mu opsiyonel mi (GL yok → opsiyonel öneri)

## 4. İlişkili
- `plans/24-purchase-invoice.md` — ItemType CONSUMABLE ön koşul
- `schema_M10_Consumption.sql` — ProductionConsumption (üretim sarfı, AYRI)
- `.claude/rules/document-immutability.md` — flag-only reversal
- competitor-analyst sarf-tüketim analizi (2026-06-01) — AccountMovement yazılmaz, GL ertelendi
