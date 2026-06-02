# Plan 31 — Fiyat Listesi Toplu Giriş (Excel-grade grid + çoğalt + import)

**Tarih:** 2026-06-03
**Yazan:** Fikri / Claude
**Durum:** `Tamamlandı` (2026-06-03)

## ✅ TAMAMLANDI (2026-06-03)
- **P31-1** Tabulator 6.3 self-host (`wwwroot/lib/tabulator/`, MIT, CDN'siz).
- **P31-2** `dbo.PriceLineTVP` (ItemId+ItemCode) + `sp_PriceListBulkUpsert` (@DryRun + @SyncDelete + MERGE idempotent + STRING_SPLIT ordinal→ROW_NUMBER Seq).
- **P31-3** `sp_PriceListClone` (satır+iskonto).
- **P31-4** `pricelist-grid.js` — Tabulator init + clipboard range paste + Ctrl+D fill-down + net önizleme + bulk-save fetch.
- **P31-5** `PriceListBulkService` (TVP DataTable) + Details grid + Tüm-Ürün/Çoğalt/Excel butonları + BulkSave JSON handler.
- **P31-6** `Import.cshtml(.cs)` — Excel paste/CSV → DryRun önizleme (satır hata raporu) → onay → upsert.
- **Browser smoke (gerçek login):** grid bulk-save (HTTP 200, net 82,93) · grid round-trip · import preview (ZZZ-999 yakalandı) · import confirm (additive, net 128,25/300,00) · Tüm-Ürün (5 ürün). Console temiz.
- **Review-gate:** security-reviewer temiz (≥80 yok). sql-sp-reviewer 4 bulgu → hepsi düzeltildi:
  - CRIT-2 + HIGH-3 (kök: PriceListLine'da ürün-tekillik yoktu) → **`UX_PriceListLine_ListItem` filtered unique index** (ürün başına tek fiyat satırı). Smoke: dup-satır reddedildi.
  - HIGH-1 (Seq ordinal boşluğu) → ROW_NUMBER renumber. Smoke: "10++5+3" → Seq 1,2,3.
  - HIGH-2 (boş grid + SyncDelete tüm listeyi siler) → grid JS boş-kaydet onayı (UI katmanı; SQL semantiği doğru — otoriter senkron).

### KAPSAM DIŞI (karar)
- **Qty-break (aynı ürün farklı MinQty kademeleri):** ürün-tekil index ile dışlandı. MinQty = ürün-fiyatının tek minimum miktarı. Çok-kademeli fiyat ayrı plan + ayrı model gerektirir (Plan 30 IMP-2 borcu bununla birleşti — qty-break devreye alınırsa hem resolver MinQty filtresi hem PriceListLine modeli birlikte ele alınmalı).

**Modül:** M01 / M02 (PriceList)
**Paket:** STARTER

> Devam planı: **Plan 32** = SupplierItem tedarikçi-ürün kataloğu + lead-time replenishment (bu plandan SONRA).

---

## 1. Problem

Plan 30 ile fiyat listesi mimarisi olgunlaştı (şube/cari/öncelik + zincir iskonto + tek-kaynak `tvf_PriceListEffective`). Ancak **satır girişi tek-tek POST** ile yapılıyor — 100lerce ürünlü bir liste pratikte doldurulamaz, sürdürülemez. Rakipler (Mikro/Logo masaüstü) klavye-sürücülü Excel-benzeri grid + Excel import sunuyor; Operax'ta hiç toplu-giriş yok. Kullanıcı: "satır satır eklemek imkansız, Excel kadar pratik olmalı, masaüstü grid gibi".

## 2. Scope

### Kapsam dahili
1. **Excel-grade editable grid** (Tabulator, self-host MIT) — PriceList Details satır bölümünü değiştirir: tüm satırlar aynı anda düzenlenebilir, Excel'den yapıştır (tab-delimited), fill-down, klavye nav, tek "Kaydet" → bulk POST.
2. **Tek SQL backend:** `sp_PriceListBulkUpsert` (TVP + `MERGE` `(PriceListId, ItemId)` idempotent) — satırları upsert eder, zincir iskontoyu (`"10+5+3"`) `PriceListLineDiscount`'a yeniden yazar. `@DryRun BIT` ile yazma-yapmadan satır-bazlı validasyon (önizleme).
3. **Listeden çoğalt:** `sp_PriceListClone` — mevcut bir listenin satır + iskonto kademelerini yeni/var olan listeye kopyalar.
4. **Tüm ürünleri ekle:** tek tıkla tüm aktif ürünleri grid'e satır olarak getir (fiyat = ItemCost.AvgCost veya 0), kullanıcı grid'de düzenler.
5. **Excel/CSV import:** dosya yükle → kolon eşleme (whitelist) → `@DryRun` önizleme (satır-bazlı hata raporu) → onay → upsert. Backend `sp_PriceListBulkUpsert` ile paylaşılır.
6. **Excel'den yapıştır:** Tabulator clipboard (SheetJS GEREKMEZ — tab-delimited parse).

### Kapsam dışı
- **SupplierItem tedarikçi-ürün kataloğu** → Plan 32.
- **Lead-time replenishment entegrasyonu** → Plan 32.
- Tüm listelerde Tabulator (yalnız PriceList bulk-edit ekranı; basit listeler mevcut `_DataTable` kalır).
- SheetJS/jsPDF (Excel export sunucuda; paste için clipboard yeter).
- Variant/çoklu-UOM fiyat (Operax tek ItemId).

### Etkilenen dosyalar (tahmin)
- `docs/sql/schema_M02_Costing.sql` — `dbo.PriceLineTVP` table type (TVP).
- `docs/sql/db_objects_starter.sql` — `sp_PriceListBulkUpsert` + `sp_PriceListClone`.
- `src/Operax.Web/wwwroot/lib/tabulator/` — tabulator.min.js + .min.css (self-host).
- `src/Operax.Web/wwwroot/js/pricelist-grid.js` — grid init + paste + bulk-save fetch.
- `src/Operax.Web/Features/MasterData/PriceLists/Details.cshtml(.cs)` — grid + toplu butonlar + JSON bulk handler.
- `src/Operax.Web/Features/MasterData/PriceLists/Import.cshtml(.cs)` — Excel import + preview (yeni).
- `src/Operax.Web/Lib/Dtos.cs` — bulk satır DTO'su.

**Tahmini boyut:** ~8-10 dosya / ~700-900 satır (Tabulator hariç).

## 3. Alternatifler

### A: Hand-rolled vanilla JS grid
**Açıklama:** Excel-paste/range/fill-down/klavye-nav'ı sıfırdan ~500 satır JS ile yaz.
**Reddetme sebebi:** Clipboard parse + range select + undo + kenar durumları yüksek hata yüzeyi + sürekli bakım. Kullanıcı "Excel kadar pratik" istiyor — kırılgan el-yapımı bunu zor verir.

### B: Excel-import-only (grid minimal)
**Açıklama:** Toplu giriş yalnız Excel import + çoğalt + tüm-ürün; grid mevcut basit form kalır.
**Reddetme sebebi:** Kullanıcı açıkça "masaüstü grid gibi" + "Excel kadar pratik" dedi; import tek başına dosya-gidip-gel döngüsü, hızlı düzeltme deneyimi vermez.

### C: Tabulator (self-host MIT) — SEÇİLEN
**Açıklama:** Tabulator core+edit+clipboard+range modülleri wwwroot'a gömülü (CDN yok). Excel-paste/fill-down/range hazır. Backend tek `sp_PriceListBulkUpsert` (TVP+MERGE). Import aynı backend'i paylaşır.
**Sebep:** MIT + sıfır runtime bağımlılık + self-host → "vanilla, CDN yok" kuralına uyar (kullanıcı onayladı, "dış kütüphane" istisnası kabul). Excel-grade deneyim en az riskle. Rakip masaüstü hissi.

**5 lens kontrolü:**
- 🔴 **Contrarian:** Tabulator ~330KB — ilk yük artar. Mitigasyon: yalnız bulk-edit ekranında yüklenir (defer), basit listelerde değil. Fatal değil.
- 🔵 **First Principles:** Gerçek ihtiyaç "100 satırı hızlı gir/düzelt". Çözüm grid değil *toplu yazma* — backend `sp_BulkUpsert` asıl değer; grid sadece giriş yüzeyi. Bu yüzden import de aynı backend'e bağlanıyor.
- 🟢 **Expansionist:** Aynı bulk-upsert + Tabulator deseni ileride SupplierItem (Plan 32), stok sayım, sipariş satırlarına da uygulanabilir → tekrar-kullanılır altyapı.
- ⚪ **Outsider:** Yeni kullanıcı "neden bazı ekran grid bazı form" diye şaşırabilir; mitigasyon: grid yalnız fiyat satırı gibi yoğun-giriş yerlerde, tutarlı pattern dokümante.
- 🟡 **Executor:** Pazartesi: (1) Tabulator self-host indir, (2) TVP+sp_BulkUpsert yaz+smoke, (3) grid wire, (4) import ekranı, (5) çoğalt/tüm-ürün butonları.

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| Tabulator self-host sürüm/uyum | orta | düşük | Sabit sürüm pin (min.js+css), CDN'siz; smoke ile doğrula |
| TVP + MERGE çift-satır / yanlış upsert | yüksek | orta | Idempotent anahtar `(PriceListId,ItemId)`; sql-sp-reviewer; smoke (2× yükleme = çift yok) |
| Excel import kolon injection / kötü veri | orta | orta | Kolon whitelist; ItemCode→ItemId çözümleme; satır-bazlı `THROW 51xxx`; `@DryRun` önizleme |
| Bulk yazma POSTED faturayı etkiler mi | yüksek | düşük | PriceList satırı değişimi geçmiş faturayı ETKİLEMEZ (fatura net efektifi dondurur — Plan 30). Immutability güvenli |
| Büyük dosya (binlerce satır) timeout | orta | düşük | TVP tek transaction; satır limiti + uyarı; gerekirse batch |
| Grid JS — CSP ileride | düşük | orta | pricelist-grid.js harici dosya (inline değil); CSP uyumlu |

## 5. Done Criteria

- [ ] Tabulator grid'de 100+ satır Excel'den yapıştırılıp tek Kaydet ile yazılıyor (smoke).
- [ ] `sp_PriceListBulkUpsert` idempotent: aynı veri 2× → çift satır yok (smoke).
- [ ] `@DryRun=1` yazma yapmadan satır-bazlı hata listesi dönüyor (geçersiz ItemCode raporu).
- [ ] Excel/CSV import: yükle → önizleme → onay → upsert akışı çalışıyor; hatalı satır raporlanıyor.
- [ ] "Listeden çoğalt" satır + iskonto kademelerini kopyalıyor.
- [ ] "Tüm ürünleri ekle" aktif ürünleri grid'e getiriyor.
- [ ] Zincir iskonto ("10+5+3") bulk yolda da doğru: net 82,9350 (smoke).
- [ ] `operax-cli migrate` 0 hata · `dotnet build` 0 hata.
- [ ] Faz-kapanış: build + sql-sp-reviewer + security-reviewer (import = dosya+yeni PageModel) + browser smoke.

## 6. Rollback Planı

- Git revert: UI + JS + SP eklemeleri geri alınabilir (`git revert <commit>`).
- DB: yeni `sp_*` ve TVP eklenir; mevcut tablo şeması DEĞİŞMEZ (PriceList/Line/Discount Plan 30'dan). TVP/SP düşürme: `DROP PROCEDURE/TYPE` idempotent.
- Mevcut tek-satır form yolu korunabilir (grid yanında fallback) → riskli durumda grid kapatılıp form'a dönülür.

## 7. Adımlar / İçerdiği TODO maddeleri

1. [ ] **P31-1** Tabulator self-host indir → `wwwroot/lib/tabulator/` (min.js+css, sürüm pin).
2. [ ] **P31-2** `dbo.PriceLineTVP` table type + `sp_PriceListBulkUpsert` (@DryRun + MERGE + iskonto yeniden yaz) → smoke.
3. [ ] **P31-3** `sp_PriceListClone` (satır + iskonto kopyala) → smoke.
4. [ ] **P31-4** `pricelist-grid.js` — Tabulator init + clipboard paste + bulk-save fetch (JSON → handler).
5. [ ] **P31-5** Details.cshtml(.cs) — grid entegrasyonu + bulk JSON handler + "çoğalt"/"tüm ürün" butonları.
6. [ ] **P31-6** Import.cshtml(.cs) — Excel/CSV yükle + kolon eşleme + DryRun önizleme + onay.
7. [ ] **P31-7** migrate + build + faz-kapanış review (sql-sp-reviewer + security-reviewer) + browser smoke.
8. [ ] **P31-8** TODO.md + journal senkron; plan arşivle.

> `docs/TODO.md`'ye de eklenecek; plan ve TODO senkron.

## 8. İlişkili

- Önceki: `plans/archive/30-pricelist-scope-dimensions.md` (fiyat mimarisi + tvf_PriceListEffective).
- Devam: **Plan 32** (SupplierItem katalog + lead-time replenishment).
- Araştırma: reference-researcher (Odoo supplierinfo / ERPNext data-import / Tabulator MIT) + competitor-analyst (TR parite) — 2026-06-03 oturum.
- Kurallar: `architecture.md §4` (SQL-first, TVP+MERGE), `document-immutability.md` (bulk POSTED'a değmez), `inline-style-guard.md` (grid JS harici dosya).

## 9. Onay

- [ ] Plan kullanıcıya gösterildi
- [ ] Geri bildirim alındı
- [ ] Onay alındı: <tarih>
