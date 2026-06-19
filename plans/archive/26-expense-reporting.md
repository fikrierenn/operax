# Plan 26 — Kırılımlı Gider Raporlama (CostCenter + ExpenseType)

**Tarih:** 2026-06-01 · **Durum:** `TAMAM (2026-06-01)` · **Modül:** M18 (Gider) + M11 (Finans) · **Paket:** STARTER-üstü olgunluk (competitor-analyst: olmazsa-olmaz değil, değerli) · **Kaynak:** kullanıcı gereksinimi + competitor-analyst (2026-06-01)

> **GL bağımsız:** Operax GL ertelendi (K1/K3). Bu rapor **belge/subledger katmanından** çıkar — GL şart değil. Mikro GL fişine bağlamış; Operax belge satırından raporlar (subledger-gerçek-zamanlı felsefesi).

---

## 1. Problem

Kullanıcı gereksinimi: "Muhasebeye gitmese de gider tanımları + gider merkezi olmalı; **dağıtılmış gider merkezi bazında + gider tipi bazında kırılımlı rapor**; gider merkezi **fatura başlığında olabildiği gibi kalem bazında da** olabilir."

Mevcut durum (keşif 2026-06-01):
- `CostCenter` (Code/Name/Type/ParentId hiyerarşi/IsActive) — VAR ama dağıtım oranı yok.
- `ExpenseType` (Code/Name/UnitOfMeasure/Description) — VAR ama **hiyerarşi YOK** (düz liste).
- `ExpenseInvoiceLine.CostCenterId` + `ExpenseTypeId` — **kalem bazlı NOT NULL**. Başlık bazlı CostCenter YOK.
- `v_ExpenseDistribution` — VAR ama dar (join yok, ham satır; partner/tip/merkez adı yok).
- Gider raporu ekranı — YOK.
- `AccountMovement.CostCenterId/ExpenseTypeId` — kolon var ama **boş** (kasıtlı: cari defter analitikle kirlenmesin; rapor v_ExpenseDistribution'dan).

---

## 2. Scope

### Kapsam dahili
- **Faz A — Şema:**
  - `ExpenseType` hiyerarşi: `ParentId UNIQUEIDENTIFIER NULL` (ağaç — Mikro grup-kodu deseni). Mevcut tipler kök.
  - `ExpenseInvoice` (başlık) → `CostCenterId UNIQUEIDENTIFIER NULL` ekle (başlık default merkez).
  - `ExpenseInvoiceLine.CostCenterId` → **NOT NULL'dan NULL'a** çevir (NULL = başlıktan miras). Rapor `COALESCE(Line.CostCenterId, Header.CostCenterId)`.
  - **Başlık default + kalem override** (kullanıcı gereksinimi — Mikro değil, Operax kararı; DOĞRULANMADI not).
- **Faz B — Rapor TVF + UI:**
  - `tvf_ExpenseBreakdown(@CompanyId, @FromDate, @ToDate)` → `GROUP BY COALESCE(Line.CostCenterId,Header.CostCenterId), Line.ExpenseTypeId` + ad join. POSTED filtreli, MovementDate/InvoiceDate bazlı.
  - `v_ExpenseDistribution` genişlet (CostCenter/ExpenseType/Partner adı join + COALESCE merkez).
  - `Features/Expenses/Report/` Razor sayfası: gider merkezi × gider tipi kırılım matrisi + tarih filtresi + drill-down (merkez → tip → fatura).
- **Faz C — Seed + smoke + reviewer.**

### Kapsam dışı (ayrı plan / sonra)
- **Çok-merkeze yüzde dağıtım (allocation key):** Mikro `som_DagAnahKodu` + 13 yöntem — periyodik maliyet/yansıtma katmanı = K1/K2 GL. Plan 26 = tek/satır merkez + tip kırılımı (dağıtım anahtarı DEĞİL).
- Sarf tüketim gideri (Plan 25 CONSUMABLE — CostCenter boyutu orada).
- GL gider hesabı mahsubu (K1).
- ExpenseType bazında merkez-zorunlu flag (Mikro muh_sorum_merk) — Faz C opsiyonel.

---

## 3. Alternatifler
- **A: Belge katmanından rapor (SEÇİLEN)** — ExpenseInvoice/Line CostCenter+ExpenseType → TVF GROUP BY. GL'siz, subledger-gerçek-zamanlı. competitor-analyst onaylı.
- **B: GL fişine bağla (Mikro deseni)** — Reddedildi: GL ertelendi (K1), STARTER'ı bloklar.
- **C: AccountMovement analitik kolonlarını doldur** — Reddedildi: cari defteri gider analitiğiyle kirletir (mevcut kasıtlı boş karar korunur).

## 4. Riskler
| Risk | Önlem |
|---|---|
| ExpenseInvoiceLine.CostCenterId NOT NULL→NULL migration mevcut veriyi bozar | NULL'a çevirmeden önce mevcut satırlar dolu kalır; COALESCE NULL-safe; backfill gerekmez |
| Başlık+kalem çift merkez karışıklığı | Rapor COALESCE(Line, Header); UI'da net "kalem boşsa başlık" notu |
| ExpenseType ağaç döngü (parent kendine) | Seed + UI guard (ParentId != Id); rapor düz GROUP BY (recursive değil v1) |
| Çok-merkez dağıtım beklentisi | Kapsam dışı açıkça yazılı (K1/K2); v1 tek merkez |

## 5. Done Criteria
- [ ] ExpenseType.ParentId + ExpenseInvoice.CostCenterId + ExpenseInvoiceLine.CostCenterId NULL idempotent migrate
- [ ] tvf_ExpenseBreakdown: merkez×tip kırılım, COALESCE(line,header), POSTED, tarih filtre
- [ ] v_ExpenseDistribution genişletildi (ad join + COALESCE)
- [ ] Expenses/Report sayfası: kırılım matrisi + tarih filtre + drill-down; Türkçe; UI standardı
- [ ] Seed: örnek CostCenter ağacı + ExpenseType kategorileri
- [ ] Smoke: 2 merkez + 3 tip gider faturası → rapor doğru toplar (başlık miras + kalem override)
- [ ] build 0/0; code-reviewer + sql-sp-reviewer

## 6. Rollback
ExpenseType.ParentId DROP; ExpenseInvoice.CostCenterId DROP; Line.CostCenterId NOT NULL geri; tvf/view DROP; Report sayfa sök. Mevcut gider faturaları etkilenmez.

## 7. Adımlar
- [ ] **Faz A:** schema_M18_ExpenseReporting.sql (ExpenseType.ParentId + Header.CostCenterId + Line.CostCenterId NULL) + CLI migrate
- [ ] **Faz B:** tvf_ExpenseBreakdown + v_ExpenseDistribution genişlet + Expenses/Report UI
- [ ] **Faz C:** seed + smoke + reviewer

## 8. 5 Lens
- 🔴 **Contrarian:** Fatal = çok-merkez dağıtımı v1'e sokmak (allocation key karmaşası, GL bağı). Önlem: kapsam dışı, tek/satır merkez.
- 🔵 **First Principles:** "Gider nereye, ne tipte" — merkez (departman/proje) + tip (kira/elektrik). GL değil, belge satırı yeter.
- 🟢 **Expansionist:** ExpenseType ağaç + CostCenter ileride K1 GL maliyet dağıtımının temeli.
- ⚪ **Outsider:** "Gider raporunu nereden alırım" → yeni Report sayfası; tek tıkla merkez×tip matrisi.
- 🟡 **Executor:** Pazartesi Faz A şema (3 ALTER); en kritik COALESCE merkez mantığı (Faz B TVF).

## 9. İlişkili
- `docs/sql/schema_M18_Expenses.sql` — CostCenter + ExpenseType + ExpenseInvoice mevcut
- `docs/sql/db_objects_reconciliation.sql:252` — v_ExpenseDistribution mevcut (genişletilecek)
- `plans/25-consumption.md` — sarf CostCenter boyutu (sinerji)
- `docs/reference/MIKRO_V16_ANALYSIS.md` §3.5 (SORUMLULUK_MERKEZLERI, dağıtım anahtarı — kapsam dışı kısım)
- `docs/reference/REFERENCE_STUDY.md` K1/K3 (GL ertelendi — rapor GL'siz)
- competitor-analyst (2026-06-01): başlık+kalem Operax kararı, dağıtım K1/K2, ExpenseType ağaç

## 10. Onay
- [ ] Plan gösterildi
- [ ] Onay alındı
