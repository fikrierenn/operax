# Plan 50 — M2 Master Veri Modül Tamamlama (DoD'a kapat)

**Durum:** Taslak · onay bekliyor
**Tier:** 3 (çok dosya, schema + UI + SP, go-live kritik fresh-install bug)
**Tarih:** 2026-06-23
**Roadmap:** Plan 45 §3 sıra — M0 ✅ · M1 ✅ · **M2 sıradaki.** EXECUTION-FIRST: mevcut modülü DoD D1-D8'e kapat.

---

## 1. Problem (Faz 0 audit — 3 paralel kod-kanıtlı denetim)

M2 (Partner/cari + Item/ürün + Warehouse/Bin/Branch) **işlevsel ama DoD altında**. Doğrulanmış gap'ler:

### CARİ (Partner + AccountMovement)
- ✅ D1/D4/D5 sağlam (ledger atomik+idempotent+simetrik reversal, izolasyon, ekstre UI tam).
- 🔴 **D3-a (çek cari-leg):** çek PORTFOLIO'dayken cari borç azalmıyor (`Cheques/Create.cshtml.cs:42-67` + `db_objects_starter.sql:741-749` — Credit yalnız COLLECTED'da). Vade boyu yanlış bakiye/aging/risk. **MEVZUAT-ONAYLI (2026-06-23, TDHP):** alış-anı cari kapat (101/120) · tahsilde cari'ye dokunma (102/101) · karşılıksız=ters iade (120/101) — bkz. `.claude/skills/muhasebe-mevzuat`. **Model kesinleşti → M6/çek planında uygulanır (bu plan KAPSAM DIŞI).**
- 🟠 **D3-b:** COLLECTED çek karşılıksız çıkamıyor (`sp_ReturnCheque` COLLECTED reddediyor, bounce yok). **M6 kapsamı (mevzuat-onaylı iade kaydı).**
- 🟡 D8: `Partners/Details.cshtml.cs` 554 satır (500 kırmızı çizgi) → split · D6: reconciliation handler non-business SqlException log'suz · D8: `Index.cshtml.cs:58,63` PartnerType magic string · Partner edit AuditLog yok · D7: cari-leg E2E smoke yok.

### ÜRÜN (Item + PriceList + SupplierItem + UDF)
- ✅ D1/D5/D6 geçti (bulk upsert atomik, UDF whitelist, THROW→Türkçe). PriceList/SupplierItem/UDF "bitti"ye yakın.
- 🔴 **D2 (CRITICAL):** "Pasif Yap" butonu ürünü pasifleştirmiyor — `Items/Details.cshtml:25-27` `asp-page-handler` yok, `OnPostDeactivate` handler yok.
- 🟠 D3: Item soft-delete handler yok (`Items/Index.cshtml:127`) · P32-7: SupplierItemCode liste'de yok (`Items/Index.cshtml.cs:37`) · Qty-break yok (bilinen, **ayrı plan**).
- 🟡 D4: `class="page"`/`_PageHeader`/`_EmptyState` yok · D8: ItemType magic string (`Details.cshtml:167`) · `Items/Details.cshtml.cs` 317 satır · `PriceLists/Index.cshtml:56` sayaç `Rows.Count()` (FilteredCount olmalı).

### DEPO/ŞUBE (Warehouse + Bin + Branch)
- ✅ D5 geçti (izolasyon/IDOR/injection/authz).
- 🔴 **C1 (CRITICAL):** `Bin.IsStorageArea` şemada YOK ama `Warehouses/Details.cshtml.cs:111` INSERT'te kullanıyor → **fresh-install raf ekleme patlar** (`Invalid column name`). Dev'de elle eklenmiş.
- 🟠 H1: Bin/Lokasyon Create+Edit işlevsiz (`Locations/Index.cshtml` stub) · H2: Warehouse/Branch try-catch+ILogger yok (generic 500, izsiz) · H3: BranchType CHECK constraint yok (tipo→fn_DefaultBranchId sessiz NULL).
- 🟡 M1: Branch soft-delete yok + BranchId orphan riski · M2: Warehouse soft-delete yok · M3: ui-standard eksik (4 sayfa) · M4: BranchType magic string · M5: Warehouses ModelState guard yok · M6: hardcoded URL + Tailwind salata.

## 2. Scope

**DAHİL (M2 DoD kapanışı):**
- **Faz 1 — CRITICAL fresh-install + lifecycle:** C1 `Bin.IsStorageArea` schema migration (kolon ekle, idempotent) + fresh-DB ritüeli · Item "Pasif Yap" handler fix · Warehouse ModelState guard.
- **Faz 2 — HIGH tamlık + hata:** Item/Warehouse/Branch soft-delete handler · Bin Create/Edit işlevsel (Locations veya Warehouse/Details) · Warehouse/Branch try-catch+ILogger (D6) · BranchType CHECK constraint · P32-7 SupplierItemCode liste'de.
- **Faz 3 — UI standard + hijyen:** `class="page"`+`_PageHeader`+`_EmptyState` (Item/Warehouse/Branch/Locations) · magic string→sabit (ItemType/BranchType/PartnerType) · hardcoded URL→asp-page · PriceLists sayaç fix · reconciliation handler ILogger.
- **Faz 4 — Dosya split + smoke:** Partners/Details 554→<300 (tab loader/service) · Items/Details 317→<300 · cari-leg + raf-ekle E2E smoke.

**HARİÇ (gerekçeli):**
- **Çek cari-leg modeli (D3-a/D3-b):** finansal-araç modelleme (çek portföy alt-defteri) = **M6 (Çek/Senet) kapsamı** + erp-isleyis-danismani kararı. M2'de çözülmez (cari altyapısı zaten sağlam; eksik çek tarafında).
- **Qty-break kademeli fiyat:** ayrı plan (Plan 30/31 birleşik debt), gerçek ihtiyaç olunca.
- **Partner edit AuditLog:** düşük etki, security-principles zorunlu listede değil → opsiyonel Faz 3 sonu.

## 3. Alternatifler (reddedilen)
- **C1'i elle dev'e ekle, schema'ya yazma:** reddedilen — fresh install kırık kalır (Plan 48 dersi tekrarı).
- **Çek cari-leg'i M2'de çöz:** reddedilen — M6 finansal-araç kapsamı, model kararı, scope patlar.
- **Hepsini tek faz:** reddedilen — CRITICAL'i izole et, faz başına kapanış kapısı.

## 4. Riskler
| Risk | Etki | Mitigasyon |
|---|---|---|
| C1 schema ekleme mevcut dev veriyi bozar | düşük | `IF COL_LENGTH IS NULL ADD` idempotent; default değer; fresh-DB ritüeli |
| Soft-delete FK orphan (Branch→Warehouse) | orta | soft-delete'te bağlı kayıt guard (DocumentLock pattern) |
| 554-satır split davranış değiştirir | orta | salt taşıma (lazy-load metotları service'e), davranış birebir; build+smoke |
| UI standard refactor görünüm bozar | düşük | tarayıcı verify (M1 Settings hub gibi) |

## 5. Done Criteria (M2 DoD)
- [ ] Faz 1: Bin.IsStorageArea schema'da + fresh-DB ritüeli raf-ekle 0-fail · Item Pasif Yap çalışıyor · Warehouse ModelState guard
- [ ] Faz 2: Item/Warehouse/Branch soft-delete · Bin Create işlevsel · Warehouse/Branch catch+ILogger · BranchType CHECK · P32-7
- [ ] Faz 3: ui-standard (4 sayfa) · magic string 0 · hardcoded URL 0 · PriceLists sayaç · reconciliation logger
- [ ] Faz 4: Partners/Details + Items/Details <300 satır · cari-leg + raf-ekle E2E smoke
- [ ] Her faz: build → code/sql-sp/security reviewer → smoke (phase-review-gate; schema değişince fresh-DB §3.5)
- [ ] HARİÇ (çek cari-leg→M6, qty-break) dokümante + TODO
- [ ] Plan arşive + journal

## 6. Faz sırası / bağımlılık
1. **Faz 1 (CRITICAL)** önce — fresh-install bug + bozuk lifecycle; izole, en yüksek risk.
2. **Faz 2 (HIGH)** — tamlık + hata yönetimi; Faz 1 schema'sına bağlı (Bin).
3. **Faz 3 (UI/hijyen)** — bağımsız, görsel.
4. **Faz 4 (split + smoke)** — en son, regresyon riski düşük.

## 7. Rollback
- Schema (Bin.IsStorageArea): kolon DROP (veri yoksa) veya bırak (nullable, zararsız).
- Kod: faz başına ayrı commit → git revert.

## 8. 5 Lens
- 🔴 **Contrarian:** Fatal flaw? C1 dev'de gizli — fresh-DB ritüeli olmadan fark edilmezdi; ritüel (§3.5) tam bunu yakalar.
- 🔵 **First Principles:** "Master veri CRUD'u tam mı + fresh install çalışıyor mu?" — hayır (Bin ekleme kırık, Item pasif kırık). Önce bunlar.
- 🟢 **Expansionist:** Çek cari-leg büyük fırsat ama M6 kapsamı — M2'yi şişirmeden devret.
- ⚪ **Outsider:** "Pasif Yap butonu pasifleştirmiyor" + "raf eklenmiyor" = temel master veri kırık, garip.
- 🟡 **Executor:** Pazartesi: Bin.IsStorageArea migration + fresh-DB → Item handler → soft-delete'ler.

## 9. İlişkili
- `plans/45-module-completion-roadmap.md` (DoD D1-D8 + modül sırası)
- `.claude/rules/phase-review-gate.md §3.5` (fresh-DB ritüeli — C1 kanıtı)
- `.claude/rules/document-immutability.md` (soft-delete + child guard)
- `.claude/rules/ui-standard.md` (Faz 3)
- M6 (Çek/Senet) — çek cari-leg D3-a/D3-b devri
