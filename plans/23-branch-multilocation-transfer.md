# Plan 23 — Şube (Branch) + Çok-Lokasyon Transfer (STARTER)

**Tarih:** 2026-06-01 · **Durum:** `Taslak (onay bekliyor)` · **Modül:** M01 (Master) + M02/M07 (Stok/Transfer) · **Paket:** STARTER · **Kaynak:** kullanıcı gereksinimi + competitor-analyst (2026-06-01)

> **Gereksinim (kullanıcı):** "Depo ayrı, satış şubeleri ayrı; ikisi de farklı lokasyonlarda olabilir, ikisi de stok tutabilir. Transfer 4 tip olmalı: depolar arası, şubeler arası, depodan şubeye, şubeden depoya. İleri WMS olmasa bile (temel stok) STARTER'da olmalı — WMS'te şart."

---

## 1. Problem

Mevcut şema **Şube (Branch) kavramını tanımıyor.** Hiyerarşi yalnız `Company → Warehouse → Bin`; `Warehouse`'ta lokasyon/adres bile yok (`Code`, `Name`, `IsActive` sadece). `StockTransfer.TransferType` yalnız `WH_TO_WH` + `BIN_TO_BIN` destekliyor. Sonuç:

- Farklı lokasyonlardaki satış şubeleri modellenemiyor.
- "Depodan şubeye / şubeden depoya / şubeler arası" transfer ayrımı yapılamıyor.
- Çok-şubeli (zincir) TR işletmeleri için temel parite eksik (Mikro/Logo base'inde var).

### 1.b Competitor bulgusu (model yönünü belirledi)
- **Mikro/Logo Şube'yi depo TİPİ olarak değil, dik org/lokasyon boyutu tutar:** her stok/cari/fiş satırında **hem `subeno` hem `depono`** var; **bir şube N adet depoya sahip**; stok fiziksel olarak **Depo** seviyesinde (`sth_giris_depo`). [NOT] `docs/reference/MIKRO_V16_ANALYSIS.md` §62/100/131/262.
- → Şube ≠ Depo. Şube, depolara sahip org/lokasyon birimidir. Stok hep Depo'da.

---

## 2. Scope

### Kapsam dahili (STARTER — bu plan)
- **Faz A — Şema (Branch + Warehouse lokasyon):**
  - Yeni `Branch` tablosu: `Id, CompanyId, Code, Name, City, Address, Phone, IsActive, IsDeleted` + standart audit (`CreatedAt/By, UpdatedAt/By`).
  - `Warehouse` tablosuna: `BranchId UNIQUEIDENTIFIER NULL` (FK Branch), `City NVARCHAR(100) NULL`, `Address NVARCHAR(500) NULL`. Mevcut depolar `BranchId=NULL` (geriye uyumlu — "şubesiz / merkez").
  - Idempotent: `IF COL_LENGTH(...) IS NULL ALTER`, `IF NOT EXISTS sys.tables`.
- **Faz B — Branch CRUD (Master Data):**
  - `Features/MasterData/Branches/Index` + `Details` (liste + yeni/düzenle). UI standardı (`_PageHeader`, `_DataTable`, `form-ctrl`). Türkçe.
  - Warehouse Create/Edit'e **Şube** dropdown'u (BranchId).
- **Faz C — Transfer 4 tip türetme:**
  - `StockTransfer.TransferType` artık from/to deponun `BranchId`'sinden **türetilir** (yeni evrak tipi/sütun gerekmez):
    - `BranchId` aynı + farklı bin → `BIN_TO_BIN` (depo-içi, mevcut)
    - aynı `BranchId`, farklı Warehouse → DEPO↔DEPO (aynı şube içi depolar arası)
    - iki Warehouse'un `BranchId`'si farklı, ikisi de DEPO-tipi → "şubeler arası" türevi
  - **Türetme C# helper** (`TransferTypeResolver`) veya SQL computed — UI sadece doğru Türkçe etiketi gösterir. Transfer formunda Kaynak/Hedef seçilince tip otomatik etiketlenir.
  - Mevcut `StockTransfer` şeması **değişmez** (FromWarehouseId/ToWarehouseId yeter; her ikisi de bir şubeye bağlı depodur).
- **Faz D — Transfer UI + rapor kırılımı:**
  - Transfer Index/Details'e Şube kolonu/filtresi (Kaynak Şube → Hedef Şube).
  - Mevcut Transfer reversal UI (Plan 22 Faz C1) korunur.
- **Faz E — Seed + doğrulama:**
  - `seed_demo.sql`: 1-2 örnek Şube + depolarını şubeye bağla.
  - Smoke: 4 transfer tipi POSTED→stok hareketi + reversal; build 0/0.

### Kapsam dışı (ayrı plan — ERTELE)
- **Belge-seviye şube boyutu:** Fatura/Sevkiyat/Cari hareketlere `BranchId` (Mikro `subeno` her belgede). M03/M04/M11 tüm evraka dokunur → büyük, ayrı plan. Transferden bağımsız.
- **Şubeler arası SATIŞ (intercompany fatura, Mikro evrak tip 15):** cari/fatura üretir → ileri muhasebe. Transfer (stok hareketi) STARTER'ı karşılar.
- **Şube bazlı P&L / şube cari mizanı:** muhasebe modülü (K1/K2) ile.
- **İleri WMS** (wave/zone/LPN, M05-M09).

---

## 3. Alternatifler (Şube modeli)

- **A: Ayrı `Branch` tablosu + `Warehouse.BranchId` (SEÇİLEN)** — Stok Depo'da kalır (ledger ripple YOK), şube depolara sahip org birimi. Mikro/Logo deseniyle uyumlu. Şube altında çok depo mümkün. İleride belge-seviye BranchId'ye temiz genişler.
- **B: `Warehouse.LocationType` ('DEPO'/'SUBE')** — Reddedildi: rakip pratiğine ters (Şube depo tipi değil), şube altında çok depo olamaz, belge/cari kırılımına genişlemez. competitor-analyst reddetti.
- **C: Şube = Depo (ayrı kavram yok)** — Reddedildi: kullanıcı "depo ayrı, şube ayrı" dedi; depo/şube ayrımı kaybolur.
- **D: Stok'u `LocationId`'ye taşı (Branch+Warehouse birleşik stok-lokasyon)** — Reddedildi: `StockMovement.WarehouseId` + tüm tvf/maliyet/ledger zinciri kırılır (yüksek risk, K6/K7 maliyet altyapısına çarpar). STARTER için aşırı.

---

## 4. Riskler

| Risk | Önlem |
|---|---|
| Mevcut depolar `BranchId=NULL` → transfer tip türetme null patlar | NULL şube = "Merkez/şubesiz"; türetme NULL-safe (her iki taraf NULL → depolar arası varsay) |
| Warehouse ALTER mevcut sorguları bozar (`SELECT *` yok kuralı sayesinde düşük) | Yeni kolonlar nullable; mevcut INSERT'ler kolon-listeli; grep ile Warehouse okuyan PageModel'ler taranır (before-major-change) |
| Transfer tip türetme yanlış etiket | C# helper unit-test + smoke 4 senaryo |
| FK Branch→Company + Warehouse→Branch cycle yok | Branch CompanyId FK; Warehouse.BranchId nullable FK |
| Scope creep (belge-seviye şube) | Belge BranchId açıkça kapsam dışı, ayrı plan |

---

## 5. Done Criteria
- [ ] `Branch` tablosu + `Warehouse.BranchId/City/Address` idempotent migrate (0 hata)
- [ ] Branch CRUD (Index+Details) Türkçe, UI standardı; Warehouse formunda Şube dropdown
- [ ] Transfer formu Kaynak/Hedef seçince 4 tipten doğru Türkçe etiketi gösterir (türetme)
- [ ] Transfer Index/Details Şube kolonu + filtre
- [ ] Seed: örnek şube + depo bağlama; 4 transfer tipi smoke POSTED + reversal
- [ ] build 0/0; StockMovement/tvf/maliyet zincirine sıfır dokunuş doğrulandı
- [ ] code-reviewer + (şema için) sql-sp-reviewer

## 6. Rollback
`Branch` tablosu DROP; `Warehouse` yeni kolonlar DROP (idempotent geri); Branch CRUD route sök; Transfer türetme helper'ı kaldır → eski `TransferType` dropdown'a dön. Stok/ledger hiç etkilenmediği için veri kaybı yok.

## 7. Adımlar
- [ ] **Faz A:** `schema_M01_Branch.sql` (Branch + Warehouse ALTER) + CLI migrate listesine ekle
- [ ] **Faz B:** Branch CRUD + Warehouse formu Şube dropdown
- [ ] **Faz C:** Transfer tip türetme (`TransferTypeResolver` + UI etiket)
- [ ] **Faz D:** Transfer Index/Details şube kolonu/filtresi
- [ ] **Faz E:** Seed + 4 senaryo smoke + reviewer

## 8. 5 Lens
- 🔴 **Contrarian:** Fatal = stok'u şubeye bağlamaya kalkmak (ledger kırılır). Önlem: stok Depo'da kalır, şube sadece depoyu gruplar.
- 🔵 **First Principles:** Soru "şube nasıl modellenir" değil — "stok nerede fiziksel durur" → her zaman Depo. Şube org boyutu, stok seviyesi değil.
- 🟢 **Expansionist:** `Branch` tablosu ileride belge-seviye BranchId + şube P&L + intercompany satışın temeli; doğru atılan ilk taş.
- ⚪ **Outsider:** Yeni kullanıcı "şube açtım ama faturada görünmüyor" derse → belge-seviye şube ertelendi, açıkça iletilmeli (UI not / dokümQan).
- 🟡 **Executor:** Faz A+B (şube tanımı + depo bağlama) en hızlı görünür değer; transfer türetme (C) onun üstüne oturur.

## 9. İlişkili
- competitor-analyst raporu (2026-06-01) — Mikro `subeno`+`depono` dik boyut, şube N depo
- `docs/reference/MIKRO_V16_ANALYSIS.md` §62/100/131/262 (subeno her satır; tip 15 depolar arası satış)
- `docs/COMPETITOR_ANALYSIS.md` §70 (multi-warehouse parite)
- `docs/sql/schema_M07_Transfer.sql` (mevcut StockTransfer — değişmez)
- `plans/22-document-status-reversal-wiring.md` Faz C1 (Transfer reversal UI — korunur)
- `.claude/rules/architecture.md` §1 (single-tenant; şube = firma-içi alt birim, firma ≠ şube)
- **Açık karar (sabah):** belge-seviye şube boyutu (fatura/cari BranchId) ne zaman? Ayrı plan 24 mü, bu planın Faz F'i mi?
