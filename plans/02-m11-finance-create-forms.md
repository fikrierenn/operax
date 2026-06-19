# Plan 02 — M11 Finance Create Formları + Eksik SP'ler

**Tarih:** 2026-05-28
**Yazan:** Claude
**Durum:** `Uygulamada` (onay: 2026-05-28)
**Modül:** M11 Finance
**Paket:** STARTER

---

## 1. Problem

Gap analizi (`docs/MODULE_GAP_ANALYSIS.md`) en büyük STARTER boşluğunu gösterdi: M11 Finance'da **veri girişi (Create) formları hiç yok**. Kullanıcı sadece seed verisini görüntüleyebiliyor — hesap açamıyor, çek/senet giremiyor, kredi açamıyor (sp_CreateLoan hazır ama UI yok), ödeme/tahsilat kaydedemiyor (sp_RecordPaymentAndAutoClose hazır ama UI yok). Ayrıca senet statü SP'leri ve kart ekstre SP'leri eksik.

## 2. Scope

### Kapsam dahili
- **Create formları:** Account, Cheque/Note, Loan, CreditCard, Payment (5 form)
- **Eksik SP'ler:** sp_DepositNote/CollectNote/ReturnNote, sp_CloseStatement, sp_PayCreditCardStatement
- Cheques/Details senet için statü butonlarının aktifleştirilmesi (senet SP'leri sonrası)
- Index sayfalarına "Yeni" butonu bağlama

### Kapsam dışı
- Virman (account transfer) — Plan sonrası
- tvf_CashProjection + nakit projeksiyon ekranı — Plan sonrası
- Banka mutabakatı — Faz 2B
- sp_EndorseCheque (çek ciro) — düşük öncelik

### Etkilenen dosyalar (tahmin)
- `docs/sql/db_objects_starter.sql` — 5 yeni SP (senet ×3, kart ×2)
- `src/Operax.Web/Features/Finance/Accounts/Create.cshtml(.cs)`
- `src/Operax.Web/Features/Finance/Cheques/Create.cshtml(.cs)`
- `src/Operax.Web/Features/Finance/Loans/Create.cshtml(.cs)`
- `src/Operax.Web/Features/Finance/CreditCards/Create.cshtml(.cs)`
- `src/Operax.Web/Features/Finance/Payments/Create.cshtml(.cs)` — yeni klasör
- Index sayfaları: "Yeni" butonu (5 dosya)
- `src/Operax.Web/Features/Finance/Cheques/Details.cshtml` — senet statü butonları

**Tahmini boyut:** ~12 dosya / ~1400 satır.

## 3. Alternatifler

### A: Tek "birleşik finans giriş" formu
**Açıklama:** Tüm finans kayıtları tek sihirbaz formdan.
**Reddetme sebebi:** Her kayıt tipi farklı alanlar; tek form karmaşık + kullanıcı kaybolur.

### B: Sadece SP, UI'yi sonra
**Açıklama:** Eksik SP'leri yaz, form'ları ertele.
**Reddetme sebebi:** Kullanıcının asıl şikayeti veri girememe — UI olmadan değer yok.

### C: ✅ Kayıt-tipi başına ayrı Create formu (seçilen)
**Açıklama:** Her finans nesnesi için standart Create formu + eksik SP'ler birlikte.
**Sebep:** STARTER kullanıcısı her finans kaydını UI'dan girebilir; mevcut Index/Details pattern'iyle tutarlı.

**5 lens:**
- 🔴 Contrarian: 12 dosya tek planda riskli — faz faz commit ile böl.
- 🔵 First Principles: Kullanıcı "finans verisi giremiyorum" diyor; Create formu çekirdek ihtiyaç.
- 🟢 Expansionist: Virman/projeksiyon eklensin mi? Hayır — STARTER MVP şişmesin, ayrı plan.
- ⚪ Outsider: Loan Create yokken sp_CreateLoan neden var? Bağlanmamış backend — bu plan kapatır.
- 🟡 Executor: Pazartesi: Loan Create (SP hazır, en hızlı kazanım).

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| 12 dosya tek oturumda dağılır | Orta | Yüksek | Faz başına commit (5 faz) |
| Loan Create form 7 calcMethod karmaşık | Orta | Orta | calcMethod dropdown + koşullu alan (BALLOON→BalloonAmount göster) |
| Payment multi-instrument UI karmaşık | Yüksek | Orta | İlk sürüm tek-araç (nakit/EFT); multi-instrument sonra |
| Senet SP'leri çek SP'lerinin kopyası | Düşük | Yüksek | Aynı pattern, tablo değişir — hızlı |

## 5. Done Criteria

- [ ] Faz 1: Loan Create formu (calcMethod dropdown, koşullu balloon/grace alanları) → sp_CreateLoan
- [ ] Faz 2: Account Create + Cheque/Note Create formları
- [ ] Faz 3: Senet SP'leri (sp_DepositNote/CollectNote/ReturnNote) + Cheques/Details senet butonları aktif
- [ ] Faz 4: CreditCard Create + sp_CloseStatement + sp_PayCreditCardStatement + ekstre kapat/öde UI
- [ ] Faz 5: Payment kaydet ekranı (sp_RecordPaymentAndAutoClose)
- [ ] Tüm Index'lerde "Yeni" butonu çalışıyor
- [ ] `operax-cli migrate` 0 hata
- [ ] Her yeni PageModel ILogger<T> DI'lı + SqlException 50000-59999 yakalama
- [ ] Smoke: her form bir kayıt oluşturup Index'te görünüyor
- [ ] Plan arşive taşındı

## 6. Rollback

- Git: faz başına commit, problemli faz `git revert`
- DB: yeni SP'ler CREATE OR ALTER (eski versiyon git history); şema değişikliği yok
- UI: yeni Create sayfaları — silme kolay

## 7. Adımlar

### Faz 1 — Loan Create (sp_CreateLoan hazır, en hızlı)
1. [ ] Loans/Create.cshtml.cs: form DTO + OnPostAsync → sp_CreateLoan
2. [ ] Loans/Create.cshtml: calcMethod dropdown, BALLOON→BalloonAmount, grace period
3. [ ] Loans/Index "Yeni Kredi" butonu
4. [ ] Commit: feat(M11): kredi açma formu (plan: 02)

### Faz 2 — Account + Cheque/Note Create
1. [ ] Accounts/Create (CASH/BANK/CREDIT_CARD/LOAN type seçimi)
2. [ ] Cheques/Create (Direction RECEIVED/ISSUED, type cheque/note)
3. [ ] Index "Yeni" butonları
4. [ ] Commit: feat(M11): hesap + çek/senet giriş formları (plan: 02)

### Faz 3 — Senet SP'leri
1. [ ] sp_DepositNote, sp_CollectNote, sp_ReturnNote (çek SP kopyası, PromissoryNote tablosu)
2. [ ] Cheques/Details: IsNote için statü butonları aktif
3. [ ] Commit: feat(M11): senet statü SP'leri (plan: 02)

### Faz 4 — CreditCard Create + ekstre
1. [ ] CreditCards/Create (limit, ekstre günü, son ödeme günü, bağlı banka hesabı)
2. [ ] sp_CloseStatement + sp_PayCreditCardStatement
3. [ ] CreditCards/Details: ekstre kapat + öde butonları
4. [ ] Commit: feat(M11): kart tanımı + ekstre işlemleri (plan: 02)

### Faz 5 — Payment kaydet
1. [ ] Payments/Create (cari seç, tutar, araç tipi, hesap) → sp_RecordPaymentAndAutoClose
2. [ ] PaymentPlan/Index'ten "Tahsilat/Ödeme Al" linki
3. [ ] Commit: feat(M11): ödeme/tahsilat kaydetme (plan: 02)

### Faz 6 — Test + cleanup
1. [ ] Her form smoke test
2. [ ] docs/TODO.md güncelle
3. [ ] Plan arşivle

## 8. İlişkili

- `docs/MODULE_GAP_ANALYSIS.md` — gap kaynağı
- `docs/MODULE_SPECS/M11_Finance_Procedures.md` — SP spec
- Önceki plan: `plans/01-starter-package-go-live.md`
- Sonraki: `plans/03-document-integrity-crit-fixes.md`

## 9. Onay

- [ ] Plan kullanıcıya gösterildi
- [ ] Geri bildirim alındı
- [ ] Onay alındı
