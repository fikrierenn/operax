# Plan 25 — Sarf Tüketim (Consumption / Material Issue)

**Tarih:** 2026-06-01 · **Durum:** `Taslak (bekliyor — Plan 24 sonrası)` · **Modül:** M02 (Stok) · **Paket:** STARTER/MANUFACTURING sınırı (denetim: tartışmalı) · **Kaynak:** Plan 24'ten bölündü + competitor-analyst sarf-tüketim analizi (2026-06-01)

> **Bağımlılık:** Plan 24 (ItemType genişletme — CONSUMABLE değeri) önce bitmeli.

---

## 1. Problem

Stoklu ürün **tüketildiğinde stok düşmeli** — ama genel/idari sarf (kırtasiye, temizlik, bakım, ambalaj) için belge yok. `ProductionConsumption` üretim-emri zorunlu (`ProductionOrderId NOT NULL`) — genel sarfı karşılamaz.

### 1.b Kritik: tüketim ürün-sabiti değil, harekete göre (BKM senaryosu, kullanıcı 2026-06-01)
Aynı ürün hem satılır hem sarf edilir → ItemType kısıt DEĞİL, davranış filtresi:

| ItemType | Satılır (SO/Shipping) | Sarf edilir (Consumption) | Örnek |
|---|---|---|---|
| **STOCK** | ✅ | ✅ | Kağıt, kırtasiye (hem satış hem iç sarf) |
| **CONSUMABLE** | ❌ (satış ekranında gizli) | ✅ | Poşet, ambalaj, süs (sadece sarf) |
| **SERVICE** | ✅ (hizmet) | ❌ (stoksuz) | Danışmanlık |

→ **Tüketim fişi STOCK + CONSUMABLE kabul eder** (her stoklu ürün sarf edilebilir; SERVICE hariç). CONSUMABLE = "satışta gizle" flag'i, sarf kısıtı değil. Satış ekranı (SO/Shipping) ileride CONSUMABLE'ı filtreler.

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
- `Features/MaterialIssue/` CRUD UI; Item dropdown **ItemType IN (STOCK, CONSUMABLE)** filtreli (SERVICE hariç — stoksuz). Her stoklu ürün sarf edilebilir.
- **Satış filtresi (ayrı/sonra):** SO/Shipping item dropdown CONSUMABLE gizler — bu plan kapsamında opsiyonel not, asıl iş Consumption fişi.

### Kapsam dışı
- Gider hesabı mahsubu (GL/K1).
- Üretim sarfı (ProductionConsumption — ayrı).
- Otomatik min-stok sarf tetikleme.
- SO/Shipping CONSUMABLE filtre UI (ileride — ürün satışta gizleme).

## 3. Kararlar (netleşti 2026-06-01 — kullanıcı onayı)
- [x] **Tüketim ItemType filtresi:** STOCK + CONSUMABLE (SERVICE hariç) — BKM senaryosu çözüldü. Sarf evrağına giren = sarf (kısa çözüm: belge türü belirler, ürün-sabiti değil).
- [x] **STARTER** — basit belge (DRAFT→POSTED→CANCELLED), ağır değil
- [x] **Tablo adı:** `MaterialIssueHeader` / `MaterialIssueLine` (sarf fişi; ProductionConsumption ile karışmaz)
- [x] **CostCenterId opsiyonel (NULL)** — GL yok; masraf merkezi raporu etiketi

## 4. İlişkili
- `plans/24-purchase-invoice.md` — ItemType CONSUMABLE ön koşul
- `schema_M10_Consumption.sql` — ProductionConsumption (üretim sarfı, AYRI)
- `.claude/rules/document-immutability.md` — flag-only reversal
- competitor-analyst sarf-tüketim analizi (2026-06-01) — AccountMovement yazılmaz, GL ertelendi
