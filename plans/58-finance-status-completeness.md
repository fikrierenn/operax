# Plan 58 — Finance Statü-Tamlık (4 yetim/hayalet statü kapatma)

**Durum:** Onay bekliyor (sabah uygulanacak) · **Tier:** 3 · **Kaynak:** Finance mali denetim 2026-06-25 (erp-isleyis-danismani + sql-sp-reviewer + muhasebe-mevzuat)

## Problem

Finance ledger/SP **doğruluğu temiz** (borç/alacak yön · ters-kayıt · dönem guard · atomiklik kanıtlandı). AMA **4 statü-tamlık boşluğu** — etiket/CHECK/tvf'te tanımlı ama **yazan SP yok** (yarım-modellenmiş özellik):

| # | Statü | Tanımlı | Yazan SP | Sonuç |
|---|---|---|---|---|
| 1 | Çek/Senet **ENDORSED** (ciro) | CHECK+label+Dtos | **YOK** | Alınan çeki 3. tarafa devir yok (TR'de yaygın) |
| 2 | Çek **PAID** (verilen çek ödendi) | CHECK+label+`tvf_FinancialPosition` PAID bekliyor | **YOK** | ISSUED çek sonsuza dek "açık borç" görünür |
| 3 | **Loan RESTRUCTURED** | CHECK+label+Dtos | **YOK** | Yapılandırma akışı yok |
| 4 | **OVERDUE** (PaymentPlan+Loan) | kısmen | **YOK** (runtime hesaplanıyor) | hayalet statü — kümede var, ölü |

## Muhasebe yönleri (muhasebe-mevzuat 2026-06-25, TDHP/MSUGT)

- **Ciro (ENDORSED):** alınan müşteri çeki (101) tedarikçiye devir → **Borç 320 Satıcılar / Alacak 101**. Operax: `AccountMovement` **Debit** EndorsedToPartnerId üzerine (tedarikçi borcu kapanır) + Cheque ENDORSED. Belge izi (özün önceliği · belgelendirme kavramı).
- **Verilen çek PAID:** çek VERİLDİĞİ an cari kapanmalı (Borç 320 / Alacak 103). ÖDENDİĞİ an (PAID): **Borç 103 / Alacak 102 Banka** → `FinancialTransaction` **EXPENSE**, **cari'ye DOKUNMA**. ⚠️ ÖN-KONTROL (Faz B adım 0): Operax verilen-çek girişinde AccountMovement yazıyor mu? Yazmıyorsa cari hiç kapanmıyor demektir → ayrı bulgu (issue-time cari), Faz B kapsamına alınır.
- **Loan RESTRUCTURED:** eski kredi kalan anaparası yeni krediye taşınır → **borç transferi** (300/400 eski kapanır, yeni açılır). **Banka/kasa hareketi YOK**, **cari (120/320) etkisi YOK** (banka kredisi, partner değil). Yalnız Loan tablo statü+bakiye + yeni Loan FK izi.

## Scope

**Dahil:** çek+senet ciro/ödeme SP'leri + Loan restructure SP + gerekli kolonlar + UI buton/handler + OVERDUE kozmetik temizlik.
**Hariç:** GL yevmiye fişi (subledger→GL posting ayrı modül) · banka mutabakat enforcement (ayrı) · SalesInvoice→AccountMovement drift doğrulaması (ayrı denetim, aşağıda not).

## Fazlar

### Faz A — Çek/Senet Ciro (ENDORSED)
- **Şema** `migration_58a_endorse.sql`: `Cheque.EndorsedToPartnerId UNIQUEIDENTIFIER NULL` + `EndorseDate DATETIME2 NULL` (+ `PromissoryNote` aynı). İdempotent COL_LENGTH guard.
- **SP** `sp_EndorseCheque(@ChequeId,@CompanyId,@UserId,@ToPartnerId,@Date)`:
  - Guard: Cheque PORTFOLIO/IN_BANK + Direction=Received (yalnız alınan çek ciro edilir; verilen çek edilemez). Aksi THROW 55xxx.
  - `sp_GuardPeriodOpen` ilk satır.
  - `AccountMovement` **Debit** @ToPartnerId (tedarikçi borcu kapanır, SourceDocType='CHEQUE_ENDORSE').
  - Cheque Status=ENDORSED + EndorsedToPartnerId + EndorseDate.
  - XACT_ABORT+TRY/CATCH+THROW Türkçe. `sp_EndorseNote` simetrik (senet 121).
- **UI** Cheques/Details + Notes/Details: "Ciro Et" butonu (PORTFOLIO/IN_BANK + Received iken) + partner seçim modal → OnPostEndorse handler.
- **Smoke:** alınan çek PORTFOLIO → ciro tedarikçiye → AccountMovement Debit doğru partner+tutar, Cheque ENDORSED, tedarikçi bakiye azaldı. Reverse senaryosu (ciro iptal — kapsam dışı, statü terminal).

### Faz B — Verilen Çek/Senet Ödeme (PAID)
- **Adım 0 (ÖN-KONTROL):** verilen-çek girişinde (Create ISSUED) AccountMovement yazılıyor mu? `sp_*` + PageModel oku. Yazılmıyorsa → issue-time cari kapama da bu faza eklenir (Borç 320/Alacak 103 girişte).
- **SP** `sp_PayIssuedCheque(@ChequeId,@CompanyId,@UserId,@BankAccountId,@Date)`:
  - Guard: Cheque Direction=Issued + Status IN (PORTFOLIO/IN_BANK). `sp_GuardPeriodOpen`.
  - `FinancialTransaction` **EXPENSE** (banka çıkışı @BankAccountId, Borç 103/Alacak 102).
  - **AccountMovement YAZMA** (cari verildiği an kapandı — Adım 0 sonucu farklıysa revize).
  - Cheque Status=PAID. `sp_PayIssuedNote` simetrik (senet 321).
- **UI** "Ödendi İşaretle" butonu (Issued + PORTFOLIO/IN_BANK) + banka hesap seçim → OnPostPay.
- **Smoke:** verilen çek → PAID → FinancialTransaction EXPENSE banka, cari DEĞİŞMEDİ, tvf_FinancialPosition artık "açık borç" göstermiyor.

### Faz C — Loan Yapılandırma (RESTRUCTURED)
- **Şema** `migration_58c_loan_restructure.sql`: `Loan.RestructuredFromLoanId UNIQUEIDENTIFIER NULL` (yeni krediden eskiye iz).
- **SP** `sp_RestructureLoan(@OldLoanId,@CompanyId,@UserId,@NewPrincipal,@NewTermMonths,@NewRate,@Date)`:
  - Guard: eski Loan Status=ACTIVE. `sp_GuardPeriodOpen` (kayıt tarihi).
  - Eski Loan: Status=RESTRUCTURED, OutstandingBalance=0 (kalan yeni krediye taşındı).
  - Yeni Loan INSERT (Principal=@NewPrincipal [genelde eski OutstandingBalance ± fark], RestructuredFromLoanId=@OldLoanId, Status=ACTIVE) — `sp_CreateLoan` mantığı yeniden kullan/çağır.
  - **FinancialTransaction YOK** (nakit hareketi yok, borç transferi). Yeni taksit planı üret.
  - XACT_ABORT+TRY/CATCH.
- **UI** Loans/Details: "Yapılandır" butonu (ACTIVE iken) + yeni şart formu → OnPostRestructure.
- **Smoke:** ACTIVE loan → restructure → eski RESTRUCTURED+OutstandingBalance=0, yeni ACTIVE loan RestructuredFromLoanId dolu, yeni taksit planı, FinancialTransaction YAZILMADI.

### Faz D — OVERDUE Kozmetik Temizlik (düşük efor, smoke yok)
- **Karar:** OVERDUE persist EDİLMEZ → hesaplanan alan kalır (tek-kaynak doğruluğu, drift yok). Statü kümesinden ÇIKAR:
  - `Dtos.cs` PaymentPlanStatus/LoanStatus'tan OVERDUE'yu sabit olarak BIRAK ama "runtime-only" yorumu ekle (tvf hesaplıyor); VEYA WHERE `IN (...,'OVERDUE')` listelerinden çıkar (asla eşleşmiyor, ölü kod).
  - `schema_M11_Finance.sql:186` Loan yorum-CHECK tutarsızlığı düzelt (yorumdan OVERDUE çıkar — CHECK'te yok).
  - Çek SP THROW yorum-kod stale (`60001/60002/60004/60010` → gerçek `55001/55002/55004/55010`) yorumları düzelt.
- Reviewer/smoke gerekmez (kozmetik/yorum). build-validator yeterli.

## Alternatifler (reddedilen)
- **OVERDUE'yu Hangfire job ile persist et:** red — runtime hesap zaten doğru, ekstra job + drift riski gereksiz (tek-kaynak ilkesi).
- **ENDORSED/PAID statülerini kaldır:** red — TR pratiğinde ciro + verilen-çek ödeme gerçek ihtiyaç; tvf zaten PAID bekliyor.

## Riskler (5 lens)
- 🔴 **Contrarian:** Verilen-çek cari kapama anı yanlışsa (issue vs pay) çift-kapama/eksik-kapama → Faz B Adım 0 ön-kontrol ZORUNLU.
- 🔵 **First Principles:** Ciro gerçek cari hareketi (özün önceliği) — sadece statü değil, AccountMovement Debit şart.
- 🟢 **Expansionist:** EndorsedToPartnerId ileride çek-portföy raporu + ciro zinciri izine yarar.
- ⚪ **Outsider:** Loan restructure'da nakit yok ama yeni taksit planı var — kullanıcı "para çıkmadı neden taksit" şaşırabilir → UI açıklama.
- 🟡 **Executor:** Faz D (kozmetik) en hızlı; A/B/C her biri schema+SP+UI+smoke ~yarım gün.

## Done criteria
- [ ] Faz A: alınan çek ciro → ENDORSED + AccountMovement Debit doğru tedarikçi, smoke net
- [ ] Faz B: verilen çek → PAID + FinancialTransaction EXPENSE, cari doğru (Adım 0 sonucuna göre), tvf açık-borç göstermiyor
- [ ] Faz C: loan restructure → eski RESTRUCTURED + yeni ACTIVE + FK iz + taksit, nakit yazılmadı
- [ ] Faz D: OVERDUE ölü-kod temiz, şema-yorum + THROW-yorum düzeltildi
- [ ] Her Tier-3 faz: sql-sp-reviewer CRITICAL yok + fresh-DB 0 fail + smoke
- [ ] Yetim statü kalmadı (her CHECK statüsünün yazan SP'si var veya bilinçli runtime-only)

## Rollback
- Migration kolonları NULLABLE → eski SP'ler görmezse çalışır (geri uyumlu). SP'ler CREATE OR ALTER → revert + migrate.

## Adımlar (sıra)
1. Faz B Adım 0 ön-kontrol (verilen-çek issue-time cari) — A'dan önce netleşsin
2. Faz A (ciro) — schema+SP+UI+review+smoke
3. Faz B (verilen çek ödeme) — schema(gerekirse)+SP+UI+review+smoke
4. Faz C (loan restructure) — schema+SP+UI+review+smoke
5. Faz D (OVERDUE kozmetik) — build-validator
6. Commit faz başına + plan arşiv

## Ayrı denetim borcu (Plan 58 dışı, denetimden çıktı)
- SalesInvoice→AccountMovement Debit yazımı (MEMORY R0 cari drift) — DOĞRULANMADI, ayrı kontrol.
- IsReconciled=1 banka mutabakat edit-kilit enforcement — DOĞRULANMADI.
- sp_CancelPayment / PaymentPlan.CANCELLED reversal akışı — DOĞRULANMADI.

## İlişkili
- `.claude/skills/muhasebe-mevzuat/SKILL.md` §2 (çek/senet 3-an muhasebe) · §1 (TDHP yön)
- `.claude/rules/document-immutability.md` §2.4 (Loan yapılandırma = yeni Loan) · §1.b (ledger append-only)
- `.claude/rules/phase-review-gate.md` (her faz sql-sp-reviewer + smoke)
- Finans SP'leri: `docs/sql/db_objects_starter.sql` (çek/senet/kredi) · `schema_M11_Finance.sql`
