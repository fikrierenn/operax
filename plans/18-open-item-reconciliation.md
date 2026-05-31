# Plan 18 — Açık-Kalem Kapama (AccountReconciliation) [M-F1.2]

**Tarih:** 2026-06-01 · **Durum:** `Taslak (onay bekliyor)` · **Modül:** M11 · **Kaynak:** B16 (MASTER_EXECUTION_PLAN M-F1.2)

---

## 1. Problem

Şu an `PaymentPlan` + `sp_AutoClosePayments` fatura ↔ ödeme planı ilişkisini tutuyor.
Ancak **AccountMovement (AM) bazlı doğrudan eşleştirme yok:**

- Hangi AM Debit (satış faturası) hangi AM Credit (tahsilat) ile kapandı → bilinmiyor
- Kısmi eşleştirme: 1.000₺ fatura, 600₺ tahsilat → 400₺ açık AM bazlı tutulmuyor
- Cari mutabakat (partner ile "sana borçlumuz: X fatura, şu kadar kapandı") üretilemiyor
- `tvf_PaymentPlanAging` PaymentPlan.PaidAmount üzerinden çalışıyor — AM tutarlılığı garanti değil

**Mevcut boşluklar:**
1. AM Debit ↔ AM Credit doğrudan bağ yok (`AccountReconciliation` tablosu eksik)
2. `sp_AutoClosePayments` sessiz (THROW yok) — tutarsızlık loglama/exception yok
3. Yaşlandırma (`tvf_PaymentPlanAging`) AM'yi değil PaymentPlan'ı okuyor → iki kaynak arası drift riski

---

## 2. Scope

### Dahili (yapılacak)
- **`AccountReconciliation` tablosu:** AM Debit ↔ AM Credit eşleştirme kaydı
  - `DebitMovementId` → AccountMovement.Id (fatura/borç hareketi)
  - `CreditMovementId` → AccountMovement.Id (tahsilat/alacak hareketi)
  - `Amount` — eşleştirilen tutar (kısmi kapama için)
  - `CompanyId`, `PartnerId`, `CreatedAt`, `CreatedBy`
  - **Append-only** (Plan 14 immutability): silme yok, iptal → ters reconciliation kaydı
- **`sp_ReconcileMovements`:** Manuel veya otomatik eşleştirme SP'si
  - Debit/Credit AM ID'leri + tutar → doğrulama + kayıt
  - Aşım koruması: toplam eşleştirilen tutar > hareket tutarı → THROW
  - sp_GuardPeriodOpen dönem kilidi
- **`tvf_OpenItems`:** Kapatılmamış AM hareketleri (fatura − eşleştirilen = açık)
  - CompanyId + PartnerId + Direction parametreli
  - Her Debit/Credit'in kapatılan tutarını `AccountReconciliation`'dan SUM ile hesapla
  - Açık kalem = `Debit/Credit - SUM(Amount WHERE DebitMovementId/CreditMovementId = AM.Id)`
- **`tvf_OpenItemAging`:** `tvf_OpenItems` üzerine yaşlandırma (0-30/31-60/61-90/>90 gün)
  - `tvf_PaymentPlanAging`'in AM bazlı karşılığı
- **sp_AutoCloseWithReconciliation güncelleme:** `sp_AutoClosePayments` çağrıldığında
  PaymentPlan kapatmanın yanı sıra `AccountReconciliation`'a da kayıt yazar

### Kapsam Dışı
- UI / ekran (sadece backend + sorgular)
- Otomatik mutabakat (partner onayı — M11 ilerisi)
- Çapraz para birimi eşleştirme (kur farkı) — ayrı plan

---

## 3. Alternatifler

**A: Sadece PaymentPlan'a güven, AccountReconciliation yazma**
- Reddedildi: AM ↔ PaymentPlan arası drift riski; AM bakiyesi ≠ PaymentPlan bakiyesi olunca kim doğru?
- Reddedildi: Cari mutabakat AM bazlı olmalı (muhasebe standardı)

**B: AccountReconciliation yaz ama yaşlandırma PaymentPlan'da kalsın**
- Kabul: Kısa vadede `tvf_PaymentPlanAging` korunur, `tvf_OpenItemAging` ek olarak gelir
- `tvf_PaymentPlanAging` → ödeme planı bazlı (taksitli faturalar için)
- `tvf_OpenItemAging` → AM bazlı (gerçek açık kalem raporu)

**C: PaymentPlan'ı kaldır, sadece AccountReconciliation kullan**
- Reddedildi: PaymentPlan taksitli ödeme planlaması için hâlâ gerekli (DueDate, InstallmentNo)

**→ Seçilen: Plan B** — `AccountReconciliation` ekle + `tvf_OpenItemAging` ekle; `tvf_PaymentPlanAging` kalsın.

---

## 4. Riskler

| Risk | Etki | Önlem |
|---|---|---|
| Mevcut AM kayıtları reconcile edilmemiş | Açık kalem raporu boş görünür | Backfill: PaymentPlan.FinancialTransactionId → AM eşleştirme |
| Çift eşleştirme | Aynı Debit iki kez Credit'e bağlanır | `CHECK`: toplam Amount ≤ Debit tutarı |
| AM ↔ PaymentPlan drift | İki kaynak çelişirse hangisi doğru? | AM öncelikli; PaymentPlan ikincil gösterge |
| sp_AutoClosePayments sessiz failure | THROW yok, hata yutulur | THROW 51500+ ekle |

---

## 5. Done Criteria

- [ ] `AccountReconciliation` tablosu canlı DB'de
- [ ] `sp_ReconcileMovements` — eşleştirme + aşım guard + dönem kilidi
- [ ] `tvf_OpenItems(@CompanyId, @PartnerId, @Direction)` — açık AM hareketleri
- [ ] `tvf_OpenItemAging(@CompanyId)` — yaşlandırma analizi
- [ ] `sp_AutoClosePayments` → reconciliation kayıt ekle + THROW aralığı 51500-51599
- [ ] Backfill: mevcut PAID PaymentPlan'lardan AccountReconciliation seed
- [ ] Smoke: `tvf_OpenItems` + `tvf_PartnerBalance` tutarlı (açık = bakiye)
- [ ] sql-sp-reviewer

---

## 6. Rollback

`AccountReconciliation` tablosu DROP. SP'ler `CREATE OR ALTER` ile önceki sürüme dön.
`tvf_OpenItems`/`tvf_OpenItemAging` DROP. `sp_AutoClosePayments` önceki sürüme dön.

---

## 7. Adımlar

- [ ] **Faz 1:** `AccountReconciliation` şema + index (`docs/sql/schema_M11_Reconciliation.sql`)
- [ ] **Faz 2:** `sp_ReconcileMovements` + `tvf_OpenItems` + `tvf_OpenItemAging`
- [ ] **Faz 3:** `sp_AutoClosePayments` güncelle (reconciliation kayıt + THROW)
- [ ] **Faz 4:** Backfill — mevcut PAID planlardan reconciliation seed
- [ ] **Faz 5:** Smoke — bakiye tutarlılık kontrolü + sql-sp-reviewer

---

## 8. 5 Lens

- 🔴 **Contrarian:** AccountReconciliation + PaymentPlan iki kaynak → drift riski gerçek. AM öncelikli kararı netleştir.
- 🔵 **First Principles:** Asıl soru: "hangi ödeme hangi faturayı kapattı?" — bu bilgi tam olarak hiçbir yerde yok.
- 🟢 **Expansionist:** Bu tablo daha sonra otomatik mutabakat, kur farkı, gecikme faizi hesabının temeli.
- ⚪ **Outsider:** "Fatura ödendi mi?" sorusuna cevap vermek için 3 tabloyu birleştiriyoruz — çok karmaşık.
- 🟡 **Executor:** Önce şema + tvf_OpenItems. `sp_AutoClosePayments` güncelleme sonra.

---

## 9. İlişkili

- `plans/16-account-movement-auto-feed.md` — AM kayıt omurgası (ön koşul)
- `plans/14-ledger-pk-immutability.md` — immutability kuralı (silme yok)
- `docs/sql/schema_M11_AccountMovement.sql` — AM tablo şeması
- `docs/sql/db_objects_starter.sql` — sp_AutoClosePayments, tvf_PaymentPlanAging
- `docs/MASTER_EXECUTION_PLAN.md` — M-F1.2
