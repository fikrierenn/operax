# Plan 16 — Cari Hesap Defteri Otomatik Besleme (B3 — HAFİF)

**Tarih:** 2026-05-29 · **Güncelleme:** 2026-06-01 · **Durum:** `Faz 5 devam` · **Modül:** M11 · **Kaynak:** R0 (REFERENCE_STUDY.md) + KARAR K3
**Öncelik:** 3 (plan 12 ve plan 14 paketinden sonra) · **Bağımlılık:** plan 14 (dönem kontrolü + immutability omurgası) önce gelmeli

---

## 1. Problem

`AccountMovement` (cari hesap defteri) **hiçbir onay SP'si tarafından beslenmiyor.** Tek besleyici
`migrate_backfill_accountmovement.sql` (tek seferlik backfill) + `seed_finance_starter.sql`. Onay
SP'leri (`sp_ReceivingPost`, `sp_ShippingPost`, `sp_GenerateSalesInvoiceFromShipping`) StockMovement +
ItemCost yazıyor ama cari deftere INSERT yok → **backfill sonrası cari bakiye gerçekten sapar (drift).**
Kanıt: REFERENCE_STUDY.md §1 + R0; Grep ile SP gövdelerinde `INSERT INTO AccountMovement` = 0 match.

## 2. Scope (KARAR K3 — HAFİF)

### YAPILACAK
- Onay SP'leri AccountMovement'a **atomik** cari borç/alacak yazsın (aynı transaction içinde):
  - `sp_GenerateSalesInvoiceFromShipping` → satış faturası: PartnerId **Borç** (alacağımız artar).
  - Alış faturası onayı (ExpenseInvoice post) → **Alacak** (borcumuz artar).
  - Tahsilat/ödeme (FinancialTransaction ile eşleşen) → ters yön.
- İşaret kuralı (mevcut tasarım, `schema_M11_AccountMovement.sql:5`): NetBakiye = SUM(Borç) − SUM(Alacak);
  Satış faturası→Borç · Tahsilat→Alacak · Alış faturası→Alacak · Ödeme→Borç.
- Çift-post koruması: mevcut `UX_AccountMovement_Source(SourceDocType, SourceDocId)` unique index'e güven;
  her kaynak belge tek hareket üretir (REVERSAL farklı SourceDocType ile ikinci kez yazabilir).
- `sp_GuardPeriodOpen` (plan 14) çağrısı: cari hareket yazmadan önce dönem OPEN kontrolü.

### KAPSAM DIŞI (büyük harfle — K3/K1/K2)
- **KEBİR FİŞİ YOK.** Çift-taraflı GL yok.
- **COGS YOK.** Satış maliyeti muhasebe kaydı yok (stok tarafı ItemCost zaten var).
- **SRBNB (Stock Received But Not Billed) KÖPRÜ HESABI YOK.**
- **HESAP PLANI / MASRAF MERKEZİ YOK.**
- **PERİYODİK MUHASEBELEŞTİRME / YEVMİYE FİŞİ YOK** (= K1/K2 ertelenmiş muhasebe modülü).
- Omurga çift-taraflıya HAZIR tutulur (Borc/Alacak kolonları zaten var) ama **muhasebe modülü AÇILMAZ.**

## 3. Reddedilen Alternatifler

1. **Tam perpetual GL (ERPNext stock_value_difference → 2 dengeli GL satırı):** Reddedildi — kebir/hesap
   planı/COGS gerektirir; K1/K2 ile ertelendi (önce muhasebe-mevzuat skill'i). Şimdi açmak = mevzuat
   olgunluğu olmadan yanlış model riski.
- 2. **Hiç besleme yapma, backfill'i periyodik tekrar et:** Reddedildi — drift sürer, cari mutabakat
   güvenilmez; "tek doğru kaynak" ilkesi (plan 09) bozulur.

## 4. Riskler

- **Çift-post / mükerrer kayıt:** Mevcut UNIQUE index yeterli mi, REVERSAL senaryosunda kilitlenir mi → test.
- **Mevcut backfill ile çakışma:** Besleme açıldıktan sonra eski backfill verisiyle çift sayım riski →
  migration sırası + idempotency kontrolü.
- **İşaret hatası:** Borç/Alacak yönü ters yazılırsa bakiye tersine döner → her belge tipi için açık test.
- **Transaction büyümesi:** Onay SP'sine ek INSERT → kilit süresi; index'li tek satır INSERT, kabul edilebilir.

## 5. Done Criteria

- [x] Satış faturası onayı → AM Debit (NetAmount=Subtotal, TaxAmount ayrı) — `7569dbd`
- [x] Alış faturası onayı → AM Credit satır bazlı (CostCenterId, ExpenseTypeId, KDV ayrı) — `112000b`
- [x] Tahsilat/ödeme → Credit/Debit — `7569dbd`
- [x] Çek tahsili → AM Credit (`sp_CollectCheque`) — `446b9be`
- [x] Senet tahsili → AM Credit (`sp_CollectNote`) — `446b9be`
- [x] Kapalı dönemde AM yazımı reddediliyor (`sp_GuardPeriodOpen` her SP'de) — plan 14
- [ ] Aynı belge ikinci kez post edilemiyor (UNIQUE / idempotent) — **smoke test lazım**
- [ ] `tvf_AccountLedger` / cari ekstre bakiyesi belge zinciriyle tutarlı — **smoke lazım**
- [ ] Backfill ile çift sayım yok — **kontrol lazım**

> **Scope genişleme (2026-06-01):** AccountMovement şema: DueDate, TaxAmount, ExchangeRate,
> AmountForeign, NetAmount, CostCenterId, ExpenseTypeId — raporlama boyutları.
> sp_ExpenseInvoicePost (YENİ): ham C# → SP. Sistematik SP analizi: db_objects_starter.sql
> tüm SP'ler tarandı, AM yazması gereken 5 SP tam, eksik yok.

## 6. Rollback

SP'ler `CREATE OR ALTER`; önceki sürüme geri yüklenir. AccountMovement'a yazılan satırlar SourceDocType ile
ayırt edilebilir → gerekirse `REVERSAL` ile geri (silme YOK — immutability, plan 14).

## 7. Adımlar (uygulama — ONAY SONRASI)

- [x] **Faz 0:** plan 14 önkoşul — tamamlandı
- [x] **Faz 1:** İşaret matrisi: Satış→Debit · Alış→Credit · Tahsilat→Credit · Ödeme→Debit · Çek/Senet tahsil→Credit
- [x] **Faz 2:** `sp_GenerateSalesInvoiceFromShipping` → AM Debit
- [x] **Faz 3:** `sp_ExpenseInvoicePost` (yeni) → AM Credit satır bazlı
- [x] **Faz 4:** `sp_RecordPaymentAndAutoClose`, `sp_CollectCheque`, `sp_CollectNote` → AM Credit/Debit
- [ ] **Faz 5A — AM Reversal SP'leri (eksik ters kayıtlar):**
  - `sp_ReturnCheque` → mevcut SP'ye AM REVERSAL ters kayıt ekle (karşılıksız çek → CHEQUE_IN geri al)
  - `sp_ReturnNote` → aynı (karşılıksız senet)
  - `sp_SalesInvoiceReverse` → yeni SP: satış faturası iptali → AM Debit ters kayıt
  - `sp_ExpenseInvoiceReverse` → yeni SP: alış faturası iptali → AM Credit satır ters kayıt
  - `sp_PaymentReverse` → yeni SP: tahsilat/ödeme iptali → AM ters yön
- [ ] **Faz 5B:** Backfill çakışma kontrolü + idempotency smoke (UX_AccountMovement_Source test)
- [ ] **Faz 6:** sql-sp-reviewer + cari ekstre tutarlılık smoke + journal → plan kapat
- [ ] **Faz 7:** Tüm SP'lere Türkçe açıklama header (ne yapar, parametreler, side effects, THROW aralığı)

## 8. 5 Lens

- 🔴 **Contrarian:** Fatal flaw = backfill ile çift sayım; migration sırası yanlışsa cari iki kat şişer.
- 🔵 **First Principles:** Soru "cari bakiye neden sapıyor?" değil "tek doğru kaynak neden beslenmiyor?" — besleme = SP'nin işi.
- 🟢 **Expansionist:** Daha büyük fırsat = periyodik GL (K1) ama bilinçli ertelendi; bu plan onun temiz alt-katmanı.
- ⚪ **Outsider:** "Cari defter var ama otomatik dolmuyor" — yabancı için en şaşırtıcı eksik; hızlı kapatılmalı.
- 🟡 **Executor:** Pazartesi = işaret matrisi yaz + sp_GenerateSalesInvoiceFromShipping'e tek INSERT ekle.

## 9. İlişkili

- `docs/REFERENCE_STUDY.md` §1 (R0) + §7 (K3) — kanıt + karar
- `plans/14-ledger-pk-immutability.md` — dönem kontrolü + immutability omurgası (ön koşul)
- `plans/09-cari-hesap-defteri-accountmovement.md` — AccountMovement subledger tasarımı
- `.claude/rules/document-immutability.md` §2.4 (finans zinciri) — REVERSAL
- `docs/sql/schema_M11_AccountMovement.sql` — tablo + işaret kuralı yorumu
- `docs/sql/db_objects_starter.sql` — sp_GenerateSalesInvoiceFromShipping (besleme noktası)
