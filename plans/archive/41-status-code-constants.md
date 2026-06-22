# Plan 41 — Statü/Yön Kod Sabitleri + VT CHECK Constraint

**Tarih:** 2026-06-22
**Yazan:** Fikri / Claude
**Durum:** `TAMAMLANDI` ✅ 2026-06-22 (TAM kapsam — Faz 1-7 bitti, build 0/0, sql-sp-reviewer temiz, CHECK smoke OK)
**Modül:** M00 (cross-cutting) + M11 (Finans)
**Paket:** STARTER

---

## 1. Problem

Statü/yön kodları (Direction, Cheque/PromissoryNote status, Loan status, PaymentPlan status, Cheque direction) C# tarafında **magic string** olarak yaşıyor — 5 domain'in hiç sabit sınıfı yok, ~9 dosyada literal. `UiHelpers.StatusText` düzeltmesinde ortaya çıkan tutarsızlığın (yarısı `DocStatus.*`, yarısı literal) aynısı bu domain'lerde tümüyle var. Ayrıca **VT tarafında bu kolonlarda hiç CHECK constraint yok** — vokabüler yalnızca SP/uygulama mantığıyla zorlanıyor; yanlış kod yazımı (typo, casing) DB'de yakalanmaz. Tek `DocStatus` literal kaçağı da var: `Expenses/Index.cshtml.cs:70` `"PAID"`.

## 2. Scope

### Kapsam dahili
- **`Lib/Dtos.cs`** — 5 yeni sabit sınıfı (VT+SP+document-immutability.md ile birebir):
  - `PaymentDirection`: `RECEIVABLE`, `PAYABLE`
  - `ChequeDirection`: `RECEIVED`, `ISSUED`
  - `InstrumentStatus` (Cheque + PromissoryNote ortak): `PORTFOLIO`, `IN_BANK`, `COLLECTED`, `RETURNED`, `ENDORSED`, `PAID`
  - `LoanStatus`: `ACTIVE`, `CLOSED`, `RESTRUCTURED`
  - `PaymentPlanStatus`: `OPEN`, `PARTIAL`, `PAID`, `OVERDUE`, `CANCELLED`
- **Literal değişimi (~9 dosya C#/cshtml):** Aging (Index+Details), PaymentPlan, Cheques (Index+Create), Loans, CreditCards (statü kullanıyorsa), UiHelpers `FinanceStatusBadge`. + `Expenses/Index.cshtml.cs:70` `"PAID"` → `DocStatus.Paid`.
- **VT CHECK constraint (idempotent migration, `docs/sql/`):** `Cheque.Status`, `Cheque.Direction`, `PromissoryNote.Status`, `Loan.Status`, `PaymentPlan.Status`, `PaymentPlan.Direction`. Mevcut veri %100 uyumlu (doğrulandı: 0 ihlal) → `WITH CHECK` güvenli.

### Kapsam dışı
- `DocStatus` domain'i (zaten sabit; tek kaçak `"PAID"` dahil edildi).
- `MovementType`/`AccountType`/`TransactionType` — sabit sınıfları zaten var, literal kaçağı taranıp 0 çıktı.
- SP içi statü string'lerini sabit'e bağlamak — T-SQL'de sabit kullanılamaz, SP'ler literal kalır (canonical kaynak zaten SP). CHECK constraint SP literal'lerini doğrular.
- Çek/senet/kredi UI iş akışı değişikliği — sadece kod-hijyeni + DB guard.

### Etkilenen dosyalar (tahmin)
- `src/Operax.Web/Lib/Dtos.cs` — 5 sabit sınıfı (~35 satır)
- `src/Operax.Web/Lib/UiHelpers.cs` — FinanceStatusBadge sabitleştir
- ~9 Features dosyası — literal → sabit
- `docs/sql/schema_M11_*.sql` veya yeni `docs/sql/migration_41_status_checks.sql` — CHECK constraint

**Tahmini boyut:** ~13 dosya / ~150 satır.

## 3. Alternatifler

### A: Sadece C# sabitleri, VT'ye dokunma
**Reddetme sebebi:** Kullanıcı açıkça "gerekiyorsa DB tarafını da düzelt" dedi. VT guard'ı olmadan typo/casing yine DB'ye sızar (defense-in-depth eksik).

### B: Enum (C# `enum`) + VT'de tinyint
**Reddetme sebebi:** Tüm kod tabanı string statü kullanıyor (Dapper, SP, kolon `NVARCHAR`). enum→string dönüşüm + şema tipi değişimi devasa, geri-uyumsuz. Mevcut `DocStatus`-tarzı `const string` pattern ile tutarlı kal.

### C (SEÇİLEN): `const string` sabit sınıfları (DocStatus pattern) + idempotent CHECK constraint
**Sebep:** Mevcut pattern'i (DocStatus/MovementType) genişletir, footprint-ladder basamak 1. CHECK constraint VT-canonical vokabüleri kilitler, SP literal'lerini ve gelecekteki C# yazımını doğrular. Veri uyumlu → risksiz.

**5 lens kontrolü:**
- 🔴 **Contrarian:** Fatal flaw = CHECK eksik koda gelecekte meşru insert'i reddeder → vokabüler SP+rule+veri 3 kaynaktan çıkarıldı, ENDORSED/RESTRUCTURED (rule'da var, SQL'de henüz yok) dahil edildi.
- 🔵 **First Principles:** Statü "serbest metin" değil kapalı kümedir; kapalı küme hem kodda (sabit) hem şemada (CHECK) ifade edilmeli.
- 🟢 **Expansionist:** Sabit sınıfları statü-rozet helper'larını ve gelecek raporları besler.
- ⚪ **Outsider:** "Neden bir statü iki yazımla olabiliyor?" — CHECK bunu imkânsız kılar.
- 🟡 **Executor:** Pazartesi: Dtos sabitleri → literal replace → build → CHECK migration → migrate → smoke.

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| R1: CHECK eksik koda meşru insert'i reddeder | yüksek | düşük | Vokabüler SP+rule+veri'den; ENDORSED/RESTRUCTURED dahil. Veri 0-ihlal doğrulandı |
| R2: Literal replace ile yanlış domain'e sabit (örn. Cheque "RECEIVED" ≠ DocStatus.Received) | orta | orta | Her dosyayı bağlamıyla oku; Direction≠Status karıştırma; ChequeDirection.Received ayrı |
| R3: `INSTRUMENT` ortak sınıf Cheque+PN'yi karıştırır | düşük | düşük | İkisi de aynı zincir (rule §2.4) — kasıtlı ortak |
| R4: CHECK migration mevcut DB'de WITH CHECK fail | yüksek | çok düşük | 6 kolon 0-ihlal ön-doğrulandı; migration idempotent (NOT EXISTS guard) |
| R5: SP literal'i sabit kümede olmayan kod yazıyor | orta | düşük | CHECK eklemeden önce SP literal taraması yapıldı (vokabüler kapsıyor) |

## 5. Done Criteria

- [x] `Dtos.cs` 23 sabit sınıfı (VT vokabüleriyle birebir) — commit C1
- [x] ~50 dosyada literal → sabit; `Expenses` `"PAID"` → `DocStatus.Paid` — commit C2
- [x] `UiHelpers.FinanceStatusBadge` sabitleştirildi — ayrı commit
- [x] CHECK constraint migration (23 kolon, idempotent, `WITH CHECK`) — commit C3
- [x] migration canlıya uygulandı (23 CHECK aktif); veri uzlaştı (HAVALE→GIRO/NEW→DRAFT/''→NET)
- [x] `dotnet build` 0 hata 0 uyarı (Web + Cli)
- [x] code-reviewer (değer eşleme hatası YOK) + sql-sp-reviewer (CHECK doğru, ledger immutability korunuyor)
- [x] Smoke: yanlış-kod UPDATE CHECK ile reddedildi (`CK_Partner_RiskCategory` conflict)
- [ ] **BORÇ:** `FinancialTransaction.InstrumentType` (3× '') + `UserFieldDefinition.DataSourceType` (8× '') CHECK dışı bırakıldı. '' değeri iş kararıyla sınıflandırılıp ayrı migration'da migrate + CHECK eklenmeli. DataSourceType '' = non-lookup alan (meşru) → CHECK'e `OR ''` ile eklenebilir.

## 6. Rollback Planı

- C#: `git revert` (sabit sınıfları + replace ayrı commit'ler).
- VT: CHECK migration down — `ALTER TABLE ... DROP CONSTRAINT CK_*` (migration dosyasında down bölümü). CHECK eklemek veri SİLMEZ; drop güvenli.

## 7. Adımlar (TAM kapsam — audit sonrası 7 faz)

**Faz 1 — Literal kaçak (sabit ZATEN var, sıfır risk):** DocStatus.Paid/Approved/Completed/InProgress/Assigned/Pending/Draft · MovementType.Production · AccountType.* · TransactionType.* · PaymentMethod.* · PartnerType.* · PriceDirection.* — Picking/CycleCount/Accounts/Partners/Expenses/Serial/Lot/SalesOrders vb. ~15 site.

**Faz 2 — Yeni sabit sınıfları (`Dtos.cs`):** FinanceDirection · ChequeStatus · ChequeDirection · LoanStatus · PaymentPlanStatus · SerialStatus · LotStatus · LpnStatus · LpnType · ProductionStatus · PickTaskStatus · BudgetStatus · BudgetType · RiskCategory · ItemType · BranchType · LoanCalcMethod · CardType · InstrumentType · UdfFieldType · UdfDataSourceType · NumberSeriesType(varsa kontrol). VT distinct + SP literal + rule ile birebir.

**Faz 3 — Literal replace (yeni sabitlerle), domain-grup commit'leri:** finans (Aging/PaymentPlan/Cheques/Loans/CreditCards/Payments) · stok (Serial/Lot/LPN) · üretim (Production/Picking) · master (Partner/Item/Branch/Budget) · UDF.

**Faz 4 — 6 VT-kod uyumsuzluğu çöz (her biri: yetim-veri mi/ölü-kod mu/SP-yazımı mı):**
- BUG-1 InstrumentType `HAVALE` (VT) vs `EFT` (kod) — yetim veri mi?
- BUG-2 Partner.DefaultPaymentMethod `EFT` vs PaymentMethod.BankTransfer `BANK_TRANSFER` — kavram birleştir.
- BUG-3 ProductionOrder `NEW`(VT)/`RELEASED`(kod) — küme netleştir.
- BUG-4 ShippingHeader `NEW/PENDING`(VT) — DocStatus'a `New` ekle mi / SP yazımı mı.
- BUG-5 view `CONSUMPTION/PRODUCTION` MovementType — VT'de yok (ölü mü).
- BUG-6 `"SUPPLIER"` literal — ölü dal, kaldır.

**Faz 5 — CHECK constraint migration (idempotent, per-kolon 0-ihlal doğrulanmış):** Faz 4 kararlarına göre vokabüler kesinleşince. Eksik-değer riski olan kolonlarda (Shipping.Status NEW, FinancialTransaction HAVALE) önce veri/kod uzlaştır.

**Faz 6 — build → code-reviewer + sql-sp-reviewer → smoke** (render + yanlış-kod insert THROW).

**Faz 7 — TODO/journal senkron + plan arşiv.**

> Mevcut CHECK'li kolonlar (dokunma, referans): AccountingPeriod.Status, PartnerReconciliationLog.Status/Channel, PeriodOverrideLog.LockType/ReasonCategory, PriceList.Direction, PriceListLine.LineType.

## 7.b AUDIT BULGULARI (code-reviewer tam tarama 2026-06-22 — sonraki oturum referansı, YENİDEN ÜRETME)

### Literal kaçak — sabit ZATEN var (Faz 1, ~15 site)
| Sabit | Kaçak | Örnek dosya:satır |
|---|---|---|
| DocStatus.Paid | "PAID" | Expenses/Index.cshtml.cs:70 · PaymentPlan/Index.cshtml:85,86,88,90,98 |
| DocStatus.Approved | "APPROVED" | SalesOrders/Details.cshtml:11 |
| DocStatus.Completed | "COMPLETED" | CycleCount/Details:35,77 · CycleCount/Index:102,107 · Picking/Index:22,37,61,288,295 · Picking/Details:13,26,118 |
| DocStatus.InProgress | "IN_PROGRESS" | Picking/Index:32,62,289,296 · Production/Index:41 |
| DocStatus.Assigned | "ASSIGNED" | Picking/Index:63,290 · Picking/Details:26 |
| DocStatus.Pending | "PENDING" | Partners/Tabs/_Siparisler.cshtml:37 |
| DocStatus.Draft | "DRAFT" | Expenses/Index:67,73 + çok view (switch badge) |
| MovementType.Production | "PRODUCTION" | Serial/Details:95 · Lot/Details:134 |
| AccountType.* | CASH/BANK/CREDIT_CARD/LOAN | Finance/Accounts/Index:18-21,66-69 · Details:10-13 · Create:42-45 · Snapshot/Index.cshtml.cs:46,47 |
| TransactionType.* | INCOME/TRANSFER_IN | Finance/Accounts/Details:116 |
| PaymentMethod.Cash/Cheque | CASH/CHEQUE | Payments/Create:93,94 · Partners/Details:150 |
| PartnerType.* | CUSTOMER/VENDOR/BOTH | Partners/Index:33-35,57,61 · Details.cshtml.cs:117,336,337 (kısmen zaten sabit) |
| PriceDirection.Purchase | "PURCHASE" | Partners/Tabs/_Fiyatlar.cshtml:31 |

### Yeni sabit sınıfı gereken kapalı-küme domainler (Faz 2)
- **FinanceDirection** RECEIVABLE/PAYABLE (Aging+PaymentPlan) · **ChequeDirection** RECEIVED/ISSUED · **ChequeStatus** PORTFOLIO/IN_BANK/COLLECTED/RETURNED/ENDORSED?/PAID · **LoanStatus** ACTIVE/CLOSED/OVERDUE?/RESTRUCTURED? · **PaymentPlanStatus** OPEN/PARTIAL/PAID/OVERDUE/CANCELLED
- **SerialStatus** IN_STOCK/SHIPPED/SCRAPPED/QUARANTINE · **LotStatus** AVAILABLE/QUARANTINE/BLOCKED · **LpnStatus** IN_USE/AVAILABLE/LOADED/SHIPPED · **LpnType** PALLET/BOX/CARTON
- **ProductionStatus** (BUG-3 netleşince) · **PickTaskStatus** DRAFT/ASSIGNED/IN_PROGRESS/COMPLETED (DocStatus mu ayrı mı — danışman kararı) · **BudgetStatus** DRAFT/APPROVED/CLOSED · **BudgetType** OPERATIONAL/CASH_FLOW/INVESTMENT
- **RiskCategory** LOW/MEDIUM/HIGH/BLOCKED · **PaymentTermPolicy** NET/NET_EOM/INSTALLMENTS · **ItemType** STOCK/CONSUMABLE/SERVICE/FIXED_ASSET · **BranchType** SUBE/MERKEZ/FABRIKA/MAGAZA/OFIS
- **LoanCalcMethod** ANUITE/EQUAL_PRINCIPAL/BALLOON/SPOT/ROTATIVE/KMH/DBS · **CardType** CREDIT/BUSINESS/CORPORATE/DEBIT · **InstrumentType** (BUG-1 netleşince) · **UdfFieldType** BOOLEAN/DATE/NUMBER/SELECT/TEXT · **UdfDataSourceType** DICTIONARY/STATIC/TABLE
- Kontrol: **NumberSeriesType** zaten var mı (Partners/Details.cshtml.cs:336,337 kullanıyor) · StatusTransitionLog.DocumentType (PURCHASE_ORDER/SALES_ORDER) SourceDoc'a eksik · AccountMovement/FinancialTransaction.SourceDocType (COLLECTION/PAYMENT/REVERSAL/CHEQUE_COLLECTION/LOAN_INSTALLMENT) SourceDoc'a eksik

### 6 VT-kod uyumsuzluğu (Faz 4 — DANIŞMAN + KULLANICI KARARI 2026-06-22, ÇÖZÜLDÜ)
Danışman: erp-isleyis-danismani (confidence + VT teyitli). Netleştirme: kullanıcı.

| # | Sorun | KARAR | Canonical küme | Aksiyon |
|---|---|---|---|---|
| A1 | InstrumentType EFT/HAVALE yetim? + dil karışık | **AYIR — ikisi de meşru** (TR'de EFT≠Havale). **Hepsi İngilizce** (kullanıcı): `HAVALE→GIRO`, EFT korunur (uluslararası kısaltma) | `CASH,EFT,GIRO,CHEQUE,NOTE,CARD,LOAN,CC` | Faz 2: `InstrumentType` sabit sınıfı · Faz 4: VT `HAVALE→GIRO` migrate |
| A2 | DefaultPaymentMethod (EFT) vs PaymentMethod (BANK_TRANSFER) | **DefaultPaymentMethod → InstrumentType taksonomisi** (kullanıcı: "sen karar ver" → araç tipi). PaymentMethod ayrı kalır (kasa-banka fişi yöntemi) | DefaultPaymentMethod = InstrumentType küme | Faz 3: kolon InstrumentType'a bağlan; PaymentMethod.BankTransfer bu kolonda KULLANMA |
| A3 | ProductionOrder NEW/IN_PROGRESS vs RELEASED/COMPLETED | **NEW=yetim→DRAFT. RELEASED EKLEME** (SP'de yok, ölü olur) | `DRAFT,IN_PROGRESS,COMPLETED,CANCELLED` | Faz 4: VT `NEW`→`DRAFT` migrate; Faz 2 `ProductionStatus` bu küme |
| A4 | ShippingHeader NEW/PENDING | **legacy→DRAFT migrate** (kullanıcı: "sen karar ver" → DocStatus ile yürür, SP-desteksiz ara statü şişirme) | `DRAFT,POSTED,CANCELLED` (DocStatus) | Faz 4: VT `NEW`+`PENDING`→`DRAFT` migrate |
| A5 | CONSUMPTION/PRODUCTION MovementType | **YANLIŞ TEŞHİS: CONSUMPTION=SourceDocType, MovementType değil.** PRODUCTION hem MovementType (üretim girişi) hem SourceDoc | MovementType: `+PRODUCTION` · SourceDoc: `+CONSUMPTION` | Faz 2: MovementType.Production + SourceDoc.Consumption ekle (ölü DEĞİL) |
| A6 | "SUPPLIER" literal (Partners/Index) | **SUPPLIER=PARTNER_CATEGORY dict değeri, Partner.Type DEĞİL.** Type sorgusundaki SUPPLIER dalı ölü | Partner.Type: `CUSTOMER,VENDOR,BOTH` (SUPPLIER dict'te kalır) | Faz 3: Index.cshtml.cs WHERE'den `'SUPPLIER'` dalını kaldır |

### Tartışmalı vokabüler (B — DANIŞMAN + KULLANICI KARARI, ÇÖZÜLDÜ)
| # | Domain | KARAR | Küme |
|---|---|---|---|
| B1 | ChequeStatus | **+ENDORSED** (TR ciro pratiği, UiHelpers badge hazır) | `PORTFOLIO,IN_BANK,COLLECTED,RETURNED,PAID,ENDORSED` |
| B2 | LoanStatus | **+RESTRUCTURED** (kullanıcı seçti). OVERDUE EKLEME (Loan değil LoanPayment'a ait) | `ACTIVE,CLOSED,RESTRUCTURED` |
| B3 | PickTaskStatus | **Ayrı küme** (DocStatus alt kümesi değil — görev atama yaşam döngüsü) | `DRAFT,ASSIGNED,IN_PROGRESS,COMPLETED,CANCELLED` |
| B4 | InstrumentType vs PaymentMethod | **Ayrı kavram, birleştirme** (araç tipi ≠ ödeme yöntemi) | iki ayrı sınıf |

> **DOĞRULANMADI (impl sırasında teyit et):** A3 RELEASED kod literal'i SP/SQL'de bulunamadı (sadece COMPLETED canlı) · A4 PENDING kaynağı · B2 RESTRUCTURED VT'de henüz yok. CHECK migration öncesi A1 (HAVALE→GIRO) + A3/A4 (NEW→DRAFT) veri migrate ŞART yoksa WITH CHECK fail.

### Mevcut CHECK'li kolonlar (DOKUNMA): AccountingPeriod.Status(OPEN/CLOSED/LOCKED) · PartnerReconciliationLog.Status/SentChannel · PeriodOverrideLog.LockType/ReasonCategory · PriceList.Direction(SALES/PURCHASE) · PriceListLine.LineType(FIXED/DISCOUNT)
### CHECK YOK (Faz 5 ekle): Cheque.Status/Direction · PaymentPlan.Status/Direction · Loan.Status/CalcMethod · ItemSerial.Status · ItemLot.Status · LPN.Status/LpnType · ProductionOrder.Status · PickTask.Status · Budget.Status/Type · Partner.RiskCategory/ItemType/PaymentTermPolicy · Branch.BranchType · FinancialTransaction.InstrumentType

## 8. İlişkili

- `.claude/rules/document-immutability.md` §2.4 (çek/kredi/PaymentPlan zinciri = vokabüler kaynağı)
- `.claude/rules/sql-conventions.md` (CHECK + idempotent migration)
- `plans/archive/40-generic-export-print.md` (StatusText düzeltmesi bu planı tetikledi)

## 9. Onay

- [ ] Plan kullanıcıya gösterildi
- [ ] Onay alındı: <tarih>
