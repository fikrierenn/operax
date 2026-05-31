# Plan 18 — Açık-Kalem Kapama (AccountReconciliation) [M-F1.2]

**Tarih:** 2026-06-01 · **Güncelleme:** 2026-06-01 (reference-researcher doğrulaması) · **Durum:** `Taslak (onay bekliyor)` · **Modül:** M11 · **Kaynak:** B16 (MASTER_EXECUTION_PLAN M-F1.2)

> **Referans doğrulaması (2026-06-01):** Mikro V17 `CARI_HAREKET_BORC_ALACAK_ESLEME` + ERPNext
> `account.partial.reconcile` + Odoo `against_voucher` — üçü de `DebitMovementId↔CreditMovementId+Amount`
> modelini kullanıyor. Plan 18 endüstri standardıyla uyumlu. Aşağıdaki güncellemeler araştırmadan eklendi.

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
  - `IsReversal BIT DEFAULT 0` — ters kayıt mı (append-only iptal mekanizması)
  - `ReversalOfId UNIQUEIDENTIFIER NULL` → ters kayıt hangi orijinal satırı iptal ediyor
  - `CompanyId`, `PartnerId`, `MovementDate`, `CreatedAt`, `CreatedBy`
  - **Append-only** (Plan 14 immutability): silme yok, iptal → `IsReversal=1` ters satır
  - ⚠️ `IsDeleted` EKLEME — append-only AM ile tutarlı (Mikro `_iptal`, ERPNext `delinked` ruhunda)
  - **İki yönlü index ZORUNLU** (Mikro+Odoo kanıtı):
    - `IX_Recon_Debit (CompanyId, DebitMovementId)` — "bu faturayı ne kapattı?"
    - `IX_Recon_Credit (CompanyId, CreditMovementId)` — "bu tahsilat neyi kapattı?"
- **`sp_ReconcileMovements`:** Manuel veya otomatik eşleştirme SP'si
  - Debit/Credit AM ID'leri + tutar → doğrulama + kayıt
  - **Kümülatif aşım guard (CHECK yetmez — SP'de SUM):**
    `SUM(Amount WHERE DebitMovementId=@x AND IsReversal=0) + @newAmount <= AM.Debit`
    Hem Debit hem Credit tarafı ayrı kontrol (Mikro iki-yön index'i bunun içindir)
  - THROW: 51500-51599 aralığı
  - sp_GuardPeriodOpen dönem kilidi
- **`tvf_OpenItems`:** Kapatılmamış AM hareketleri (fatura − eşleştirilen = açık)
  - CompanyId + PartnerId + Direction parametreli
  - Açık tutar = `AM.Debit - ISNULL(SUM(r.Amount) WHERE r.IsReversal=0, 0)`
    (Odoo `amount_residual` deseni — snapshot TUTULMAZ, her seferinde SUM, K6 uyumlu)
- **`tvf_OpenItemAging`:** `tvf_OpenItems` üzerine yaşlandırma (0-30/31-60/61-90/>90 gün)
  - Mevcut `tvf_PaymentPlanAging` CASE/DATEDIFF iskeletini kullan (`db_objects_starter.sql:1180`)
  - Vade için `AM.DueDate` kullan (plan 16'da eklendi, `schema_M11_AccountMovement.sql:43`)
  - `tvf_PaymentPlanAging` korunur (taksit planlaması için hâlâ gerekli)
- **sp_AutoCloseWithReconciliation güncelleme:** `sp_AutoClosePayments` çağrıldığında
  PaymentPlan kapatmanın yanı sıra `AccountReconciliation`'a da kayıt yazar
- **`sp_UnreconcileMovements`:** Eşleştirmeyi geri al (IsReversal=1 ters satır + ReversalOfId).
  Açık kalem matematiği yeniden açılır. sp_GuardPeriodOpen + THROW 51500-51599.
- **REVERSAL ↔ RECONCILIATION TUTARLILIĞI (KRİTİK):** Evrak iptal/silme eşleştirmeyi de etkiler.
  Karar (2026-06-01): **REJECT yaklaşımı** — mevcut "bağlı tahsilat varsa reject" guard'ıyla simetrik.
  - `sp_SalesInvoiceReverse` / `sp_ExpenseInvoiceReverse` / `sp_PaymentReverse`: iptal edilecek AM
    hareketinde **aktif (IsReversal=0) AccountReconciliation varsa THROW** —
    "Bu hareket eşleştirilmiş; önce eşleştirmeyi geri alın (sp_UnreconcileMovements)."
  - Akış: kullanıcı önce sp_UnreconcileMovements → sonra evrak iptali. Gizli cascade yok, denetim izi temiz.
  - Guard her iki tarafı kontrol eder: `EXISTS recon WHERE (DebitMovementId=@amId OR CreditMovementId=@amId) AND IsReversal=0`

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
| Reconcile edilmiş AM iptal edilirse açık kalem bozulur | Bakiye tutarsız | Reversal SP'lerde aktif recon varsa REJECT + sp_UnreconcileMovements |

---

## 5. Done Criteria

- [ ] `AccountReconciliation` tablosu canlı DB'de
- [ ] `sp_ReconcileMovements` — eşleştirme + aşım guard + dönem kilidi
- [ ] `tvf_OpenItems(@CompanyId, @PartnerId, @Direction)` — açık AM hareketleri
- [ ] `tvf_OpenItemAging(@CompanyId)` — yaşlandırma analizi
- [ ] `sp_AutoClosePayments` → reconciliation kayıt ekle + THROW aralığı 51500-51599
- [ ] `sp_UnreconcileMovements` — eşleştirme geri al (IsReversal ters satır)
- [ ] Reversal SP'lere recon guard: aktif reconciliation varsa iptal REJECT (THROW)
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
- [ ] **Faz 2:** `sp_ReconcileMovements` + `sp_UnreconcileMovements` + `tvf_OpenItems` + `tvf_OpenItemAging`
- [ ] **Faz 3:** `sp_AutoClosePayments` güncelle (reconciliation kayıt + THROW)
- [ ] **Faz 4:** Reversal SP'lere recon guard (sp_*Reverse aktif recon varsa REJECT)
- [ ] **Faz 5:** Backfill — mevcut PAID planlardan reconciliation seed
- [ ] **Faz 6:** Smoke — bakiye tutarlılık kontrolü + sql-sp-reviewer

---

## 8. 5 Lens

- 🔴 **Contrarian:** AccountReconciliation + PaymentPlan iki kaynak → drift riski gerçek. AM öncelikli kararı netleştir.
- 🔵 **First Principles:** Asıl soru: "hangi ödeme hangi faturayı kapattı?" — bu bilgi tam olarak hiçbir yerde yok.
- 🟢 **Expansionist:** Bu tablo daha sonra otomatik mutabakat, kur farkı, gecikme faizi hesabının temeli.
- ⚪ **Outsider:** "Fatura ödendi mi?" sorusuna cevap vermek için 3 tabloyu birleştiriyoruz — çok karmaşık.
- 🟡 **Executor:** Önce şema + tvf_OpenItems. `sp_AutoClosePayments` güncelleme sonra.

---

## 9. Referans + Mimari Audit Notları (2026-06-01)

**reference-researcher (Mikro/ERPNext/Odoo):**
- AM satır-bazlı/başlık-bazlı asimetri: **auditor haklı** — alış analitiği satır, satış subledger başlık
- Kur farkı (realized FX) mekanizması yok — `SourceDocType='FX_DIFF'` ayrı AM satırı gerekecek (K1 öncesi borç)
- Double-reversal guard: Faz 5A'ya eklendi (THROW 51422)
- `Debit/Credit` = her zaman TRY; `AmountForeign` = döviz → GL gelince netleştirilecek (ADR borcu)

**mali-evrak-mevzuat (VUK/TTK):**
- Append-only kararı **yasal zorunluluk** — TTK md.82 (10 yıl saklama, silme=ziya) + VUK md.280 → `IsDeleted` eklenmez (doğrulandı [DOC])
- Cari mutabakat (TTK md.94): yıl sonu kapatma + 1 ay sessiz onay = AYRI domain (partner onay/itiraz M11 ilerisi); AccountReconciliation kapama defteri, mutabakat belgesi değil
- **Ba-Bs kaldırıldı (Eylül 2024)** — backlog'a Ba-Bs export EKLENMEMELİ (ölü iş)
- Açık-kalem yöntemi VUK'ta zorunlu değil ama **dövizde md.280 dolaylı zorunlu** → `tvf_OpenItems` dövizli müşteri için vergisel gereklilik
- Realize FX: iki ayrı an (dönem sonu değerleme + kapama anı) — `SourceDocType='FX_DIFF'` AM satırı, reconciliation.Amount'a GÖMME (matematik bozulur)

**operax-erp-wms-auditor:**
- FIFO + manuel eşleştirme her ikisi de gerekli — plan 18 bunu doğru kurguluyor
- PREPAYMENT (avans) backfill: `sp_AutoClosePayments` PREPAYMENT satırı → reconciliation backfill (Faz 4) bu kenar durumu ele almalı — Faz 4'e not eklendi
- FinancialTransaction soft-delete immutability ihlali → **DÜZELTİLDİ** (sp_PaymentReverse ters FT kaydı, `db_objects_reversal.sql`)

## 10. İlişkili

- `plans/16-account-movement-auto-feed.md` — AM kayıt omurgası (ön koşul)
- `plans/14-ledger-pk-immutability.md` — immutability kuralı (silme yok)
- `docs/sql/schema_M11_AccountMovement.sql` — AM tablo şeması
- `docs/sql/db_objects_starter.sql` — sp_AutoClosePayments, tvf_PaymentPlanAging
- `docs/MASTER_EXECUTION_PLAN.md` — M-F1.2
