# Operax — Detaylı Gap Dökümü (Madde Madde)

> Tarih: 2026-05-28 · Kaynak: AUDIT_REPORT_2026-05-28.md derinleştirmesi
> Her gap: **Ne** (eksik özellik) · **Neden** (iş değeri) · **Nerede** (dosya/şema) · **Backend** (SP/tablo) · **UI** (sayfa) · **Efor** · **Kabul kriteri**

Öncelik: 🔴 Olmazsa-Olmaz · 🟡 Olgun · 🟢 İleri (STARTER dışı)

---

## M11 — FİNANS (en kritik modül)

### 🔴 G1.1 — Hesap Açma Formu (Account Create)
- **Ne:** Kasa/Banka/Kredi Kartı/Kredi hesabı tanımlama ekranı
- **Neden:** Kullanıcı şu an seed dışında hesap ekleyemiyor; tüm finans buna bağlı
- **Nerede:** `Features/Finance/Accounts/` — sadece Index+Details var
- **Backend:** SP gereksiz, doğrudan INSERT (FinancialAccount). AccountType seçimi (CASH/BANK/CREDIT_CARD/LOAN/POS)
- **UI:** `Accounts/Create.cshtml(.cs)` — form: Code, Name, AccountType, Currency, BankName/Branch/IBAN (banka ise), OpeningBalance, CreditLimit (kart/kredi ise)
- **Efor:** ~1 saat
- **Kabul:** Yeni banka hesabı aç → Index'te bakiye 0 ile görünür → ekstre sayfası açılır

### 🔴 G1.2 — Çek/Senet Girişi (Cheque/Note Create)
- **Ne:** Alınan/verilen çek ve senet kayıt formu
- **Neden:** Portföy yönetiminin girişi; tahsilat/ödeme çek ile yapılıyor
- **Nerede:** `Features/Finance/Cheques/` — Index+Details var, Create yok
- **Backend:** INSERT Cheque/PromissoryNote (Direction RECEIVED/ISSUED)
- **UI:** `Cheques/Create.cshtml(.cs)` — ChequeNo, BankName, DrawerName, Amount, ChequeDate, DueDate, PartnerId, Direction; type=note için NoteNo+IssueDate
- **Efor:** ~1.5 saat (çek+senet tek form, type switch)
- **Kabul:** Alınan çek gir → portföyde PORTFOLIO statüde görünür → Bankaya Ver akışı çalışır

### 🔴 G1.3 — Kredi Açma Formu (Loan Create) ⭐
- **Ne:** 7 hesap yöntemli kredi tanımlama
- **Neden:** sp_CreateLoan TAM hazır (7 tip test edildi) ama UI yok — bağlanmamış backend
- **Nerede:** `Features/Finance/Loans/` — Index+Details var, Create yok
- **Backend:** ✅ `sp_CreateLoan` hazır (@CalcMethod, @GracePeriodMonths, @BalloonAmount)
- **UI:** `Loans/Create.cshtml(.cs)` — LoanNo, BankName, AccountId(dropdown), Principal, InterestRate, TermMonths, StartDate, CalcMethod(dropdown), koşullu: BALLOON→BalloonAmount, grace period
- **Efor:** ~45 dk (SP hazır)
- **Kabul:** ANUITE kredi aç → taksit takvimi otomatik üretilir → Details'te 12 taksit + banka hesabına gelir hareketi

### 🔴 G2 — Ödeme/Tahsilat Kaydetme (Payment Create) ⭐
- **Ne:** Cariye ödeme yapma / tahsilat alma ekranı
- **Neden:** sp_RecordPaymentAndAutoClose hazır (FIFO auto-close) ama UI yok
- **Nerede:** `Features/Finance/Payments/` — klasör yok
- **Backend:** ✅ `sp_RecordPaymentAndAutoClose` hazır (Partner.AutoClosePayments=1 ise PaymentPlan'a FIFO dağıtır)
- **UI:** `Payments/Create.cshtml(.cs)` — PartnerId, AccountId, Amount, TxType(INCOME/EXPENSE), InstrumentType(CASH/EFT/CHEQUE/CARD), Description
- **Efor:** ~1 saat (ilk sürüm tek-araç)
- **Kabul:** Müşteriden tahsilat gir → FinancialTransaction + açık fatura PaymentPlan otomatik kapanır → hesap bakiyesi artar

### 🟡 G1.4 — Kart Tanımı + Ekstre İşlemleri
- **Ne:** Kredi kartı tanımı + ekstre kapatma/ödeme
- **Backend:** SP gerek — `sp_CloseStatement`, `sp_PayCreditCardStatement` (YOK)
- **UI:** `CreditCards/Create.cshtml(.cs)` + Details'te ekstre butonları
- **Efor:** ~2 saat
- **Kabul:** Kart tanımla → ekstre kapat → banka hesabından öde → bakiye düşer

### 🟡 G1.5 — Senet Statü SP'leri
- **Ne:** sp_DepositNote/CollectNote/ReturnNote (çekte var, senette yok)
- **Nerede:** Cheques/Details senet için statü butonları gösterilmiyor
- **Efor:** ~30 dk (çek SP kopyası, PromissoryNote tablosu)
- **Kabul:** Alınan senet bankaya ver → tahsil et → banka hesabına gelir

### 🟢 G1.6 — Nakit Projeksiyon / Banka Mutabakatı / Virman (İleri)
STARTER dışı. tvf_CashProjection + reconciliation + account transfer.

---

## M01 — MASTER DATA

### 🟡 G3 — Partners Risk/Vade/eFatura Alanları
- **Ne:** Cari kartta risk skoru, vade, kredi limiti, e-fatura mükellef bilgisi
- **Neden:** Şemada kolonlar VAR (`schema_M01_M11_RiskAndLoanTypes.sql`) ama UI'da gösterilmiyor; kredi limit kontrolü bunlara bağlı
- **Nerede:** `Partners/Details.cshtml.cs:59` — PartnerDto eksik: RiskScore, RiskCategory, MaxOverdueDays, CreditLimit, BlockOnLimitExceed, PaymentTermDays, PaymentTermPolicy, DefaultPaymentMethod, EFaturaMukellef, EFaturaAlias, IbanForRefund, AutoClosePayments
- **Backend:** Kolonlar hazır, INSERT/UPDATE genişlet
- **UI:** Partners/Details form'a sekme/section: Genel · Adres · Mali (risk+vade) · e-Belge · Banka
- **Efor:** ~1.5 saat
- **Kabul:** Cari aç, risk skoru 4 + vade 60 gün + e-fatura mükellef set et → kaydet → tekrar aç değerler dolu

### 🟢 Items varyant matrisi (beden/renk), Locations Create (İleri)
STARTER dışı.

---

## M03/M04 — EVRAK BÜTÜNLÜĞÜ

### 🟡 G4 — Receiving/Shipping Cancel Handler
- **Ne:** POSTED mal kabul/sevkiyatı iptal (ters StockMovement)
- **Neden:** Yanlış post edilen evrak geri alınamıyor; stok kalıcı yanlış
- **Nerede:** `Receiving/Details.cshtml.cs` Cancel yok, `Shipping/Details.cshtml.cs` Cancel yok
- **Backend:** SP gerek — `sp_ReceivingCancel`, `sp_ShippingCancel` (ters hareket + status CANCELLED + child invoice kontrol)
- **UI:** Details toolbar'a "İptal Et" butonu (POSTED durumda, child yoksa)
- **Efor:** ~2 saat (2 SP + UI)
- **Kabul:** POSTED receiving iptal → StockMovement ters kayıt → stok düşer → status CANCELLED; faturalı ise iptal reddedilir

### 🟡 G5 — Satır Silme (PO/SO/Receiving/Shipping)
- **Ne:** DRAFT evrakta eklenen satırı silme
- **Neden:** Yanlış eklenen satır kaldırılamıyor (sadece Add var)
- **Nerede:** Tüm evrak Details — OnPostDeleteLineAsync yok
- **Backend:** DELETE satır (DRAFT kontrolü + document-immutability guard)
- **UI:** Satır tablosunda "Sil" butonu (DRAFT durumda)
- **Efor:** ~1 saat (4 evrak × benzer handler)
- **Kabul:** DRAFT PO'da satır sil → tablo güncellenir; POSTED'da buton görünmez

### 🟡 G4b — PO/SO Cancel SP'ye Taşı
- **Ne:** Mevcut PO/SO Cancel direct UPDATE → SP + StatusTransition kontrolü
- **Nerede:** `PurchaseOrders/Details.cshtml.cs:181` direct UPDATE (sp_ValidateStatusTransition bypass)
- **Efor:** ~1 saat
- **Kabul:** Geçersiz statü geçişi reddedilir (örn. CANCELLED→DRAFT)

---

## M00 — ADMIN

### 🟡 G6 — Parameters CRUD
- **Ne:** Parametre ekleme/düzenleme/silme
- **Nerede:** `Admin/Parameters/Index.cshtml.cs` salt liste
- **Efor:** ~1 saat
- **Kabul:** Yeni parametre ekle (InvoiceMode vb.) → düzenle → değer değişir

### 🟡 G6b — StatusTransitions CRUD
- **Ne:** Durum geçiş kuralı ekle/düzenle
- **Nerede:** `Admin/StatusTransitions/Index.cshtml.cs` salt liste
- **Efor:** ~1 saat
- **Kabul:** RECEIVING DRAFT→POSTED kuralı ekle → sp_ValidateStatusTransition kullanır

### 🟡 G7 — Settings içerik + Companies CRUD
- **Settings:** boş OnGet → şirket geneli ayar paneli
- **Companies:** klasör yok → Admin/Companies CRUD (multi-company)
- **Efor:** ~2 saat
- **Kabul:** Şirket bilgileri düzenlenebilir; yeni şirket eklenebilir

---

## M02 — INVENTORY

### 🟡 G8 — FIFO Maliyet Yöntemi
- **Ne:** Moving Average yanında FIFO katman maliyeti
- **Neden:** Bazı sektörler FIFO zorunlu (Parameter.CostingMethod=FIFO seçilebilir ama motor yok)
- **Backend:** `ItemCostLayer` tablosu (FIFO katmanları) + sp_UpdateItemCostFIFO
- **Efor:** ~3 saat (STARTER sonrası)
- **Kabul:** FIFO seçili item'da çıkış en eski katmandan maliyetlenir

---

## ♻️ EXCESS — Temizlik

### E1 — Boş `Sales/` Klasörü Sil
- SalesOrders ayrı klasörde; boş `Sales/` kafa karıştırıcı, route üretmiyor
- **Efor:** 1 dk · `rmdir Features/Sales`

### E2 — DRY: ActionLabel + 'Sistem' Magic
- PO+SO Details aynı switch → `UiHelpers.AuditActionLabel`
- SQL `'Sistem'` → UI `L.T` (artık tek dil, sabit "Sistem")
- **Efor:** ~30 dk

### E3 — Hardcoded 14-gün Vade
- PO Index/Details `DATEADD(DAY,14,...)` → `Partner.PaymentTermDays`
- **Efor:** ~30 dk

### E4 — Diğer boş placeholder (Incentives/Integration/Project/Service)
- Gelecek modül — `.gitkeep` ile işaretle veya roadmap'e referans, şimdilik dokunma

---

## Toplam Efor Tahmini (STARTER tam)

| Plan | Gap'ler | Efor |
|---|---|---|
| Plan 02 (M11 Create) | G1.1-1.5, G2 | ~7 saat |
| Plan 03 (evrak bütünlüğü + CRIT) | G4, G4b, G5, CRIT-1..4 | ~6 saat |
| Plan 04 (Partners + Admin) | G3, G6, G6b, G7 | ~6 saat |
| Hızlı temizlik | E1, E2, E3 | ~1 saat |
| E2E test | — | ~1 saat |
| **TOPLAM** | | **~21 saat / 3-4 oturum** |

Faz 2B (STARTER sonrası): G8 FIFO, RFQ, e-Belge sync, kredi limit kontrolü.
