# Plan 32 — Tedarikçi-Ürün Kataloğu (SupplierItem)

**Tarih:** 2026-06-03
**Yazan:** Fikri / Claude
**Durum:** `Tamamlandı` (2026-06-03) — Faz A-F + review-gate. P32-7 (PO kod görünürlük) ERTELENDİ (hafif, TODO).

## ✅ TAMAMLANDI (2026-06-03)
- **A** SupplierItem tablosu + UQ(Company,Partner,Item) + filtered preferred UQ + TVP. Smoke: UQ + preferred-exclusive.
- **B** sp_SupplierItemBulkUpsert (MERGE + reaktivasyon + tek-tercih + SyncDelete) + sp_SetPreferredSupplier. Smoke: idempotent/syncdelete/reaktivasyon (orphan yok).
- **C** SupplierItemService (list/bulk/setPreferred, ILogger+ct).
- **D** supplieritem-grid.js (Tabulator + tek-tercih cellEdited).
- **E** Item kartı "Tedarikçiler" tab + grid + bulk handler. Browser smoke: round-trip (TED-002/VND-X1/7gün/50/120,50/tercih), bozuk JSON→{ok:false}.
- **F** tvf_ReplenishmentSuggestions += tercih edilen tedarikçi (ad/kod/lead-time/MOQ) + Replenishment ekran kartı.
- **Review-gate:** security-reviewer temiz; sql-sp-reviewer IMP-1 (reaktivasyon orphan) + IMP-2 (tvf CompanyId/IsDeleted) düzeltildi. Full migrate 0 fail.
- **ERTELENDİ:** P32-7 PO satırında SupplierItemCode görünürlük (hafif; PO Details grid'e JS; gerçek ihtiyaç/dokunuş olunca) → TODO.
- Commit: 1dfb212 (A-B) · 68bbcaf (C-F) · b2fb0c9 (review fix).

## ⚖️ Rakip validasyonu (competitor-analyst, 2026-06-03)
TR-standart gap: Mikro/Logo'da tedarikçi stok kodu + temin süresi (lead-time) + MOQ standart; Operax ❌. Alan adları rakiple birebir (SupplierItemCode/LeadTimeDays/MinOrderQty/IsPreferred). Min/Max sipariş önerisi tüm rakiplerde var, Operax ❌ [COMPETITOR_ANALYSIS s.68] → replenishment wire ilk adım. 🎯 Web grid (Tabulator) + katalog≠fiyat ayrımı = Mikro fat-client'a karşı farklılaşma. Plan over-engineering değil, doğrulandı.
**Modül:** M01 (MasterData) + M07 (Replenishment)
**Paket:** STARTER

> Devam planı: Plan 30 (PriceList boyutlar) + Plan 31 (toplu giriş) tamamlandı. Bu plan onların üzerine tedarikçi-ürün eşleme katmanını ekler.

---

## 1. Problem

Operax'ta **tedarikçi ile ürün arasında eşleme yok**: bir tedarikçinin hangi ürünleri sattığı, kendi ürün kodu (supplier part no), teslim süresi (lead time), minimum sipariş miktarı (MOQ), son alış fiyatı bilinmiyor. Sonuç: (a) PO'da tedarikçi seçince satıcının kendi koduyla referans gösterilemiyor, (b) `tvf_ReplenishmentSuggestions` reorder önerisinde "kimden, ne kadar sürede" bilgisi yok → satınalmacı elle araştırıyor, (c) tercih edilen tedarikçi kaydı yok. Rakipler (Odoo `product.supplierinfo`, SAP B1 "BP Catalog Numbers", ERPNext `item_supplier`) bu katmanı standart sunuyor.

## 2. Scope

### Kapsam dahili
1. **Yeni tablo `SupplierItem`** (tam Odoo deseni): `PartnerId, ItemId, SupplierItemCode, SupplierItemName, LeadTimeDays, MinOrderQty, LastPrice, Currency, IsPreferred` + zorunlu audit set. `UQ(CompanyId, PartnerId, ItemId)`. Tek-tercih kuralı: `UQ filtered (CompanyId, ItemId) WHERE IsPreferred=1`.
2. **`SupplierItemService`** — ürüne göre listele, bulk-upsert (Item kartı grid'inden), sil (soft), tercih-ata (exclusive).
3. **Item Details "Tedarikçiler" tab'ı** — mevcut tab yapısına (`general/uom/barcodes`) 4. tab; Tabulator child grid (Plan 31 grid altyapısı yeniden kullanılır) + tek-Kaydet bulk.
4. **Replenishment wire** — `tvf_ReplenishmentSuggestions` çıktısına tercih edilen tedarikçi (kod/ad) + `LeadTimeDays` + `MinOrderQty` eklenir (LEFT JOIN SupplierItem IsPreferred=1). Replenishment ekranı bu kolonları gösterir.
5. **PO satırında tedarikçi ürün kodu görünürlüğü** (hafif): PO'da tedarikçi seçiliyse satır ürününün `SupplierItemCode`'u gösterilir (varsa).

### Kapsam dışı
- **Reorder formül revizyonu** (SafetyStock + ADC×LeadTime gerçek hesabı) → ayrı plan (bu plan sadece lead-time/tedarikçi BİLGİSİNİ yüzeye çıkarır, formülü değiştirmez).
- **LastPrice resolver'a girmez** — yalnız bilgilendirici (mimari kural: fiyat/iskonto PriceList tek-kaynağında kalır, Plan 30).
- Çoklu-cari fiyat kıyas ekranı ("bu ürünü en ucuz kim veriyor") → ayrı plan (capability bu tabloyla hazır).
- Excel import (bu fazda yok; Item grid bulk yeterli, import sonra).

### Etkilenen dosyalar (tahmin)
- `docs/sql/schema_M01_SupplierItem.sql` — yeni tablo (yeni dosya) + migrate listesine ekle.
- `docs/sql/db_objects.sql` — `tvf_ReplenishmentSuggestions` revize (preferred supplier JOIN).
- `docs/sql/db_objects_supplieritem.sql` — `sp_SupplierItemBulkUpsert` + `sp_SetPreferredSupplier` (yeni dosya).
- `src/Operax.Web/Lib/SupplierItemService.cs` — backend.
- `src/Operax.Web/Features/MasterData/Items/Details.cshtml(.cs)` — Tedarikçiler tab + grid + bulk handler.
- `src/Operax.Web/wwwroot/js/supplieritem-grid.js` — Tabulator grid (pricelist-grid.js deseni).
- `src/Operax.Web/Features/Replenishment/*` veya ilgili liste — yeni kolonlar.

**Tahmini boyut:** ~8-9 dosya / ~600-800 satır.

## 3. Alternatifler

### A: Item tablosuna tek-tedarikçi alanları ekle (ERPNext-zayıf deseni)
**Açıklama:** Item'a `PreferredSupplierId, LeadTimeDays, SupplierCode` kolonları ekle.
**Reddetme sebebi:** Ürün başına TEK tedarikçi varsayımı yanlış — bir ürün birden çok tedarikçiden alınır (fiyat/lead-time farklı). ERPNext'in bu zayıflığını kopyalamayız.

### B: Tedarikçi fiyatını da bu tabloda tut (Odoo birleşik deseni)
**Açıklama:** SupplierItem.LastPrice'ı bağlayıcı fiyat yap, resolver buradan okusun.
**Reddetme sebebi:** Plan 30 `tvf_PriceListEffective` tek-doğruluk-kaynağı mimarisini bozar; iki fiyat kaynağı = drift riski. LastPrice yalnız bilgilendirici kalır.

### C: SupplierItem ayrı tablo (eşleme/lojistik) + PriceList (fiyat) AYRI — SEÇİLEN
**Açıklama:** Katalog = kod/lead-time/MOQ/tercih/LastPrice(info); PriceList = bağlayıcı fiyat/iskonto/variance. Item'a O2M.
**Sebep:** Çok-tedarikçi + tedarikçi-başına lead-time (Odoo altın standart) + Plan 30 mimarisi korunur. LastPrice info olarak hızlı referans verir, resolver'a girmez.

**5 lens kontrolü:**
- 🔴 **Contrarian:** Katalog boş kalırsa (kimse doldurmaz) değer üretmez → mitigasyon: PO/Receiving onayında `LastPrice` otomatik güncellenebilir (faz dışı opsiyon, not edildi).
- 🔵 **First Principles:** Gerçek ihtiyaç "kimden, ne kadar sürede, hangi kodla" — fiyat değil. Bu yüzden fiyat PriceList'te kalır, katalog lojistik taşır.
- 🟢 **Expansionist:** Aynı tablo ileride en-ucuz-tedarikçi kıyas + otomatik PO önerisi + lead-time bazlı reorder için temel.
- ⚪ **Outsider:** "Neden fiyat hem katalogda hem PriceList'te" sorusu → LastPrice=info, PriceList=bağlayıcı ayrımı net dokümante.
- 🟡 **Executor:** Pazartesi: (1) SupplierItem şema+smoke, (2) service+SP, (3) Item tab grid, (4) replenishment JOIN, (5) PO kod görünürlük.

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| IsPreferred birden çok tedarikçide işaretlenir | orta | orta | `UQ filtered (CompanyId,ItemId) WHERE IsPreferred=1` + `sp_SetPreferredSupplier` exclusive (eskiyi 0'la) |
| LastPrice yanlışlıkla resolver'a sızar | yüksek | düşük | Mimari kural + code-review; resolver yalnız `tvf_PriceListEffective` kullanır (Plan 30) |
| Item Details dosyası şişer (439+266 satır) | orta | yüksek | Grid + bulk handler `SupplierItemService` + ayrı JS; partial'a böl |
| Replenishment JOIN performansı (preferred yoksa) | düşük | düşük | LEFT JOIN + IsPreferred=1 filtered index; SARGable |
| Bulk upsert idempotent değil | orta | orta | MERGE `(CompanyId,PartnerId,ItemId)`; smoke 2× yükleme çift yok |

## 5. Done Criteria

- [ ] `SupplierItem` tablosu canlı VT'de (FK + UQ + filtered preferred UQ); migrate 0 hata.
- [ ] Item kartı "Tedarikçiler" tab'ında grid: tedarikçi+kod+lead-time+MOQ+LastPrice+tercih satır gir/düzenle/sil, tek Kaydet.
- [ ] Tercih exclusive: bir ürüne 2. tedarikçi preferred yapılınca eski otomatik düşer (smoke).
- [ ] `tvf_ReplenishmentSuggestions` çıktısında preferred tedarikçi + lead-time + MOQ görünür.
- [ ] LastPrice resolver'a GİRMİYOR (code-review + grep: `tvf_PriceListEffective` tek fiyat kaynağı).
- [ ] PO satırında tedarikçi seçiliyse SupplierItemCode görünür.
- [ ] build 0 hata · sql-sp-reviewer · security-reviewer (yeni PageModel handler) · browser smoke.

## 6. Rollback Planı

- Git revert (commit bazlı). Yeni tablo/SP eklenir; mevcut şema DEĞİŞMEZ (sadece tvf_ReplenishmentSuggestions revize — eski sürüm CREATE OR ALTER ile geri yüklenebilir).
- `DROP TABLE SupplierItem` (FK yok başka tablodan → güvenli düşürme).
- Item Details tab eklenmesi izole; tab kaldırılırsa sayfa çalışır.

## 7. Adımlar / İçerdiği TODO maddeleri

1. [ ] **P32-1** `schema_M01_SupplierItem.sql` — tablo + FK + UQ + filtered preferred UQ; migrate listesine ekle; smoke.
2. [ ] **P32-2** `sp_SupplierItemBulkUpsert` (TVP+MERGE) + `sp_SetPreferredSupplier` (exclusive) → smoke.
3. [ ] **P32-3** `SupplierItemService` (list/bulk/delete/setPreferred).
4. [ ] **P32-4** `supplieritem-grid.js` (Tabulator, pricelist-grid deseni) + tedarikçi/para lookup.
5. [ ] **P32-5** Item Details "Tedarikçiler" tab + grid + bulk JSON handler (+ partial split gerekirse).
6. [ ] **P32-6** `tvf_ReplenishmentSuggestions` += preferred supplier/lead-time/MOQ + ekran kolonları.
7. [ ] **P32-7** PO satırında SupplierItemCode görünürlük (hafif).
8. [ ] **P32-8** migrate + build + review-gate (sql-sp-reviewer + security-reviewer) + browser smoke.
9. [ ] **P32-9** TODO/journal senkron; plan arşivle.

## 8. İlişkili

- Önceki: `plans/archive/30-pricelist-scope-dimensions.md` (fiyat mimarisi — LastPrice ayrımı buna saygı), `plans/archive/31-pricelist-bulk-entry.md` (Tabulator grid + bulk upsert deseni yeniden kullanılır).
- Araştırma: reference-researcher (Odoo product.supplierinfo / ERPNext item_supplier / SAP B1 BP Catalog) — 2026-06-03 oturum.
- Kurallar: `architecture.md §4` (SQL-first, MERGE), `sql-conventions.md §1` (zorunlu audit kolonları), `document-immutability.md` (LastPrice info, ledger'a değmez).
- TODO: Qty-break DEBT (ayrı), reorder formül revizyonu (bu plandan sonra).

## 9. Onay

- [ ] Plan kullanıcıya gösterildi
- [ ] Geri bildirim alındı
- [ ] Onay alındı: <tarih>
