---
name: mali-islem-akislari
description: >
  Operax M11 Finans + M02 Costing modüllerinde MALİ İŞLEM AKIŞI doğruluğu rehberi —
  mutabakat (GL↔subledger/banka/cari, reconciling-item sınıflama + aging/escalation),
  varyans analizi (fiyat/hacim/karma ayrıştırma, maddiyet eşiği, waterfall), yevmiye/
  ledger kaydı doğruluğu (borç/alacak, append-only ters-kayıt), dönem kapanışı
  (AccountingPeriod OPEN/CLOSED/LOCKED + sp_GuardPeriodOpen). Operax'ın GERÇEK
  şema objelerine (AccountMovement, FinancialTransaction, PriceVariance, reconciliation
  SP/TVF, AccountingPeriod) + Türkçe UI'ya uyarlanmıştır. "mutabakat", "reconciliation",
  "varyans analizi", "variance", "cari hesap kapatma", "açık kalem", "dönem kapanışı",
  "ay sonu kapanış", "borç alacak doğru mu", "ters kayıt" denildiğinde veya M11/M02
  finans/costing özelliği yazarken çağrılır. SALT-REHBER — operasyonel muhasebe
  doğruluğunu doğrular, kod yazmadan önce hangi iş kuralının geçerli olduğunu söyler.
allowed-tools: Read, Grep, Glob, Bash
user-invocable: true
model: inherit
---

# Mali İşlem Akışları (Operax M11/M02)

> **Kaynak:** Anthropic `knowledge-work-plugins/finance` plugin'inin operasyonel muhasebe
> workflow'ları (reconciliation · variance-analysis · journal-entry-prep · close-management ·
> financial-statements · audit-support) Operax'ın gerçek SQL şemasına + Türkçe + single-tenant
> mimarisine uyarlandı. Generic muhasebe ESASLARI alındı; stack/UI Operax'ın kendi kuralları
> (`.claude/rules/architecture.md` §4 SQL-First, `document-immutability.md` ledger append-only).

## Ne zaman tetiklenir

M11 Finans (Accounts/Cheques/Loans/PaymentPlan/Payments/Aging) veya M02 Costing
(PriceVariance/ItemCost) özelliği **yazarken/denetlerken**. Bu skill operasyonel muhasebe
DOĞRULUĞUNA bakar — `mali-evrak-mevzuat` (VUK/KDV/TTK yasal) ve `erp-isleyis-danismani`
(statü/modelleme) ile **tamamlayıcı**, dup değil.

## Tamamlayıcılık (footprint-ladder — dup engeli)

| Skill/Agent | Bakış açısı |
|---|---|
| **mali-islem-akislari** (bu) | Operasyonel muhasebe akış doğruluğu (mutabakat/varyans/kapanış işleyişi) |
| `mali-evrak-mevzuat` | Yasal mevzuat (VUK tarih, e-Belge, iptal/düzeltme) |
| `erp-isleyis-danismani` | Statü/yön/tip modelleme kararı |
| `competitor-analyst` | Rakip parite |

---

## 1. Mutabakat (Reconciliation)

> ⚠️ Mutabakat sonucu **kalifiye mali personel tarafından imzalanmadan** kesinleşmez —
> Operax otomatik eşler, kullanıcı onaylar (`sp_RespondReconciliation`).

### Operax'taki 3 mutabakat tipi

| Tip | Ne eşlenir | Operax objesi |
|---|---|---|
| **Cari (Partner)** | Cari hesap defteri bakiyesi ↔ karşı taraf beyanı | `AccountMovement` ↔ `sp_CreateReconciliationStatement` / `sp_RespondReconciliation` |
| **Banka** | GL nakit ↔ banka ekstresi (outstanding çek, yoldaki para, kaydedilmemiş masraf) | `FinancialTransaction.IsReconciled` flag |
| **Açık kalem (Open-Item)** | Fatura ↔ ödeme eşleştirme | `tvf_OpenItems` / `tvf_OpenItemAging` / `sp_ReconcileMovements` / `sp_UnreconcileMovements` |

### Reconciling-item sınıflama (3 kova)

1. **Zamanlama farkı** (1-5 günde kendiliğinden kapanır) → düzeltme YOK
2. **Düzeltme gerekli** (kaydedilmemiş banka masrafı, faiz, hata) → yevmiye/ters kayıt gerekir
3. **İnceleme gerekli** (açıklanamayan fark, ihtilaflı tutar, yaşlanmış kalem) → kök-neden analizi

### Yaşlandırma + escalation (`tvf_OpenItemAging` ile uyumlu)

- Güncel (0-30), yaşlanıyor (31-60), gecikmiş (61-90), bayat (90+).
- Eşik aşımı (tutar veya 60-90+ gün) → escalation. Kalemi süresiz devretme — kök-nedeni kovala.

> 🐞 **Operax notu (2026-06-23):** reconciliation SP/TVF'leri C#'tan çağrılıyor
> (`sp_CreateReconciliationStatement`, `sp_ExpireReconciliations`, `sp_RespondReconciliation`)
> ama `db_objects_reconciliation.sql` + `schema_M11_Reconciliation.sql` **migrate listesinde YOK**
> (`src/Operax.Cli/Program.cs`). Fresh install'da bu SP'ler oluşmaz → runtime hata.
> Mutabakat feature'ına dokununca migrate listesine eklenmeli (SQL-side ayrı karar).

---

## 2. Varyans Analizi (Variance Analysis)

Operax'ta varyans = beklenen ↔ gerçekleşen farkı (`document-immutability.md` §4: kaynak değişmez,
fark ayrı **Variance** belgesi).

### Ayrıştırma (decomposition)

- **Fiyat/Hacim:** PO fiyat 10₺ ↔ fatura 11₺ → fiyat varyansı (`PriceVariance` kaydı, DRAFT,
  yönetici onayı → `ItemCost.AvgCost` güncellenir). Qty 100→95 → hacim varyansı (`QtyVariance` — TODO, henüz yok).
- **Karma (mix):** çok-kalemli sepette ürün karışımı kayması.
- **Maddiyet eşiği:** eşik altı varyans narrative gerektirmez; üstü gerekçe + onay.

### Operax kuralı

- Varyans **kaynağı değiştirmez** — `PriceVariance`/`InvoiceVariance`/`QtyVariance` ayrı belge.
- Moving-avg costing: varyans onayı `ItemCost.AvgCost`'a yansır (Plan 47 Faz 1/3 — MOVING_AVG kilit).

---

## 3. Yevmiye / Ledger Kaydı Doğruluğu

Operax ledger'ı (`AccountMovement`, `StockMovement`, `FinancialTransaction`) **append-only**
(`document-immutability.md` §1.b):

- **Borç/Alacak dengesi:** `AccountMovement` bakiye = `SUM(Borc - Alacak)` tüm satırlar.
- **Düzeltme = SİLME DEĞİL:**
  - `AccountMovement` (IsCancelled YOK) → `SourceDocType='REVERSAL'` **ters satır** (orijinali nötrler).
  - `StockMovement` (IsCancelled VAR) → `IsCancelled=1` flag, ters satır YAZILMAZ (çift-sayım yasak).
- **Standart kayıt türleri** (JE prep): tahakkuk (accrual), peşin gider (prepaid), bordro, gelir —
  her biri doğru borç/alacak + destekleyici detay (`SourceDocType`/`SourceDocId` izi).

---

## 4. Dönem Kapanışı (Close Management)

Operax'ta ay-sonu kapanış = `AccountingPeriod(CompanyId, Year, Month, Status)` durum makinesi
(`document-immutability.md` §1.b, `schema_M11_LedgerIntegrity.sql`):

| Durum | Anlam | Davranış |
|---|---|---|
| **OPEN** | Dönem açık | Serbest hareket, iz yok |
| **CLOSED** | Yumuşak kilit | Yalnız Administrator/Finance + gerekçe → `PeriodOverrideLog` atomik iz |
| **LOCKED** | e-Defter berat | **Koşulsuz THROW** — tek çözüm: sonraki açık döneme düzeltme |

- Her ledger hareket/onay SP'sinin **ilk satırı** `sp_GuardPeriodOpen(@CompanyId, @MovementDate, @UserId, ...)`.
- **Görevler ayrılığı:** `OverriddenBy ≠ ApprovedBy` (kendi override'ını onaylayamaz).
- **Kapanış checklist sırası:** sarf/üretim sarfları → maliyet güncelleme → mutabakat → varyans onayı →
  dönem CLOSED. Bağımlılık sırasına uy (maliyet mutabakattan önce, varyans maliyetten sonra).

---

## 5. Operax Obje Haritası (hızlı referans)

> **🔑 Katman ilkesi (architecture.md §5):** Operax **ön muhasebe**; resmi GL/yevmiye/9xx-nazım katmanı
> **YOK** ama tüm işlemler resmi-muhasebeye **eşlenebilir (posting-rule→yevmiye)** tasarlanır. Aşağıdaki
> "GL" etiketli objeler aslında **subledger** (alt-defter); gerçek GL fişi ileride ayrı modül. Mutabakat
> "GL↔subledger" derken GL ucu henüz operasyonel alt-defterin kendisi (doğru yön/an/belge-izi şart ki ileride postlanabilsin).

| Muhasebe kavramı | Operax objesi | Dosya |
|---|---|---|
| Cari hesap defteri (subledger) | `AccountMovement` | `schema_M11_AccountMovement.sql` |
| Kasa/banka (subledger) | `FinancialTransaction` (`IsReconciled`) | `schema_M11_Finance.sql` |
| Dönem kilidi | `AccountingPeriod` + `PeriodOverrideLog` + `sp_GuardPeriodOpen` | `schema_M11_LedgerIntegrity.sql` |
| Mutabakat | `sp_Create/RespondReconciliation`, `tvf_OpenItem(Aging)`, `sp_(Un)ReconcileMovements` | `db_objects_reconciliation.sql` (Plan 48 migrate'te ✅) |
| Kredi teminatı (off-balance) | `LoanCollateral` (nazım 90/91/92) — Plan 58 | `muhasebe-mevzuat §2.5` |
| Fiyat varyansı | `PriceVariance` + `ItemCost.AvgCost` | `schema_M02_Costing.sql` |
| Yaşlandırma | `Features/Finance/Aging` + `tvf_OpenItemAging` | — |

---

## Çıktı disiplini

- Kod yazmadan ÖNCE: hangi iş kuralı geçerli (mutabakat tipi? varyans ayrıştırma? dönem durumu?) → 1-2 cümle.
- Emin değilsen **DOĞRULANMADI** de (`.claude/rules/todo-verification.md`); şema iddiasını canlı VT'den teyit et.
- Ledger'a dokunan her SP → `phase-review-gate.md` (sql-sp-reviewer + smoke zorunlu).

## İlişkili

- `.claude/rules/document-immutability.md` — ledger append-only, dönem kilidi, variance belgeleri
- `.claude/rules/architecture.md` §4 — SQL-First (iş mantığı SP'de)
- `.claude/skills/mali-evrak-mevzuat/SKILL.md` — yasal mevzuat (tamamlayıcı)
- `.claude/skills/sql-migration-writer/SKILL.md` — SP/migration yazımı
