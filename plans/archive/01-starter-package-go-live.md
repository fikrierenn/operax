# Plan 01 — STARTER Paketinin Canlıya Alınması

**Tarih:** 2026-05-28
**Yazan:** Claude
**Durum:** `TAMAM` (DOĞRULANDI 2026-06-01 — canlı kod + E2E smoke)
**Modül:** M00 + M01 + M02 + M03 + M04 + M11
**Paket:** STARTER

---

> **TAMAM — 2026-06-01 STARTER E2E doğrulaması (transaction+rollback smoke + tarayıcı):**
> - **Faz 1 Loan:** `sp_CreateLoan` ANUITE/EQUAL_PRINCIPAL/BALLOON → 12 taksit, balon max anapara=40000 ✅
> - **Faz 2 Purchase→cost:** `sp_CreateReceivingFromPO`→`sp_ReceivingPost` → 2 RECEIPT hareket + ItemCost.AvgCost>0 ✅
> - **Faz 3 PriceVariance:** SP + PO/Details + PriceVariances sayfası mevcut ✅
> - **Faz 4 Shipping→Invoice:** `sp_GenerateSalesInvoiceFromShipping` + InvoiceMode INSTANT ✅ (tarayıcı E2E)
> - **Faz 5 formlar:** Loan CalcMethod selector+balloon JS · CreditCard LinkedBankAccountId · Partner risk/vade/e-Fatura ✅
> - **Faz 6 Aging detay + hesap ekstre:** Finance/Aging/Details + Accounts/Details (son 100 hareket) ✅
> - **Faz 7 Payment loop:** `sp_RecordPaymentAndAutoClose` → FinancialTransaction + AccountMovement Credit + (AutoClosePayments=1 ise) FIFO PaymentPlan kapama + AccountReconciliation ✅
> - **Sales zinciri (SO→Shipping→Invoice→ledger):** tarayıcı E2E (N:1 merge) ✅
> Build 0/0. Plan 21 (N:1 fatura) STARTER üstü ek olarak tamamlandı.

## 1. Problem

STARTER paketinin kapsamı `docs/COMPETITOR_ANALYSIS.md` §6'da net: M00 (Sistem), M01 (Master), M02 (Stok), M03 (Satınalma), M04 (Satış+Fatura), M11 (Finans). Şu an:

- M11 Finans şemaları + SP'leri hazır ama bazı UI sayfaları (Aging Detayı, Hesap Ekstre detayı, Çek detay aksiyon SP'leri) eksik
- M03 Satınalma SP düzeyinde `sp_CheckPriceVariance` var ama PO/Details PageModel buna bağlanmadı
- M04 Satış Faturası SP var ama Shipping POSTED akışı SP'yi otomatik çağırmıyor
- M02 Maliyet `sp_UpdateItemCostMovingAvg` var ama `sp_ReceivingPost` içine wire edilmedi
- Partner risk/vade kolonları şema seviyesinde var ama UI'da kullanılmıyor (Edit form'da yok)
- Kredi tipleri (ANUITE / EQUAL_PRINCIPAL / BALLOON / SPOT / ROTATIVE / KMH / DBS) şemada `Loan.CalcMethod` olarak var ama `sp_CreateLoan` sadece ANUITE hesaplıyor
- CreditCard ↔ Bank link şemada (`LinkedBankAccountId`) var ama UI'da bağlanma yok

Bu plan STARTER paketini end-to-end çalışır hale getirir.

## 2. Scope

### Kapsam dahili
- M02: `sp_UpdateItemCostMovingAvg` çağrısının `sp_ReceivingPost` içine wire edilmesi
- M03: `sp_CheckPriceVariance` çağrısının PO/Details PageModel'ine wire edilmesi + uyarı UI
- M04: Shipping POSTED akışında otomatik `sp_GenerateSalesInvoiceFromShipping` çağrısı (Parameter InvoiceMode=INSTANT ise)
- M11: `sp_CreateLoan` SP'sinin 7 kredi tipini desteklemesi (ANUITE + EQUAL_PRINCIPAL + BALLOON + SPOT + ROTATIVE + KMH + DBS)
- M11: CreditCard Create/Edit form'a `LinkedBankAccountId` dropdown
- M11: Aging detay sayfası (`/finance/aging/{partnerId}`)
- M11: FinancialAccount ekstre sayfası satır detayları
- M01: Partner Edit form'a risk + vade alanları (RiskScore, RiskCategory, MaxOverdueDays, DefaultPaymentMethod, PaymentTermPolicy, EFaturaMukellef)
- E2E test senaryosu: PO → Receiving → ItemCost → SO → Shipping → SalesInvoice → PaymentPlan → Çek tahsilat → Bank balance

### Kapsam dışı
- M03 RFQ (Teklif Yönetimi) — Faz 2B
- M03 Multi-level approval workflow — Faz 2B
- M04 e-Fatura outbound submission — ana ERP omurgasında, biz sadece inbound sync yapacağız (Plan 02)
- M11 Banka mutabakatı detay UI — Faz 2B
- M11 Nakit projeksiyon dashboard — Faz 2B
- Resmi muhasebe (defter/beyanname) — kapsam dışı

### Etkilenen dosyalar (tahmin)
- `docs/sql/db_objects.sql` — `sp_ReceivingPost` revize + `sp_ShippingPost` revize
- `docs/sql/db_objects_starter.sql` — `sp_CreateLoan` revize (multi-method)
- `docs/sql/schema_M01_M11_RiskAndLoanTypes.sql` — kontrol et, gerekirse ek kolon
- `src/Operax.Web/Features/PurchaseOrders/Details.cshtml.cs` — `sp_CheckPriceVariance` çağrısı
- `src/Operax.Web/Features/PurchaseOrders/Details.cshtml` — variance uyarı UI
- `src/Operax.Web/Features/Shipping/Details.cshtml.cs` — POSTED akışında invoice tetikleme
- `src/Operax.Web/Features/MasterData/Partners/Edit.cshtml(.cs)` — risk + vade alanları
- `src/Operax.Web/Features/Finance/CreditCards/Create.cshtml(.cs)` — bank link dropdown
- `src/Operax.Web/Features/Finance/Loans/Create.cshtml(.cs)` — calcMethod dropdown
- `src/Operax.Web/Features/Finance/Aging/Details.cshtml(.cs)` — yeni sayfa
- `src/Operax.Web/Features/Finance/Accounts/Statement.cshtml(.cs)` — yeni sayfa
- `src/Operax.Web/Features/Shared/_Layout.cshtml` — sidebar zaten temiz, kontrol

**Tahmini boyut:** ~15 dosya / ~1500 satır.

## 3. Alternatifler

### A: Modül modül teslim (M02 önce, sonra M03, sonra M04, sonra M11)
**Açıklama:** Her modülü tek başına tamamla, kullanıcı her birini ayrı test etsin.
**Reddetme sebebi:** M11 dolması M03/M04 PaymentPlan + ItemCost akışına bağlı. Modüller arası bağ var, izole teslim sığ kalır.

### B: Yalnızca SP wire (UI değişikliği yok)
**Açıklama:** SP'leri mevcut PageModel'lere bağla, yeni UI ekleme.
**Reddetme sebebi:** Risk/vade alanları, CreditCard bank link, Aging detay UI olmadan kullanıcı end-to-end test yapamaz.

### C: ✅ End-to-end STARTER kapatma (seçilen)
**Açıklama:** SP wire + eksik UI + Partner risk form + Aging detay + kredi tipleri tek planda. End-to-end test senaryosu açık.
**Sebep:** STARTER paketi kullanıcıya bütün halinde teslim edilebilir; rakip karşılaştırmasında Logo/Mikro/Netsis'e denk hizmet sunumu mümkün olur.

**5 lens kontrolü:**
- 🔴 **Contrarian:** 15 dosya tek planda fazla mı? Risk: oturum yarıda kalırsa dağılır. Mitigation: 6 fazı sıralı yap, her faz sonrası commit + dotnet build doğrula.
- 🔵 **First Principles:** Kullanıcı "STARTER aktif olsun" istiyor — sadece SP yetmez, alış-satış-tahsilat çevrimi UI'da bitebilmeli.
- 🟢 **Expansionist:** e-Fatura inbound sync de ekle? Hayır — ayrı plan (Plan 02), STARTER MVP'yi şişirme.
- ⚪ **Outsider:** PartnerEdit'te RiskScore varken hâlâ "Aydın Endüstri A.Ş." hardcoded var mı? Sweep et.
- 🟡 **Executor:** Pazartesi sabahı: Faz 1 `sp_CreateLoan` multi-method (~80 satır SQL).

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| `sp_ReceivingPost` revize geriye uyumsuz olur | Yüksek | Düşük | Mevcut SP imzası korunur, sadece body içinde `sp_UpdateItemCost` çağrısı eklenir |
| Kredi hesap formülleri yanlış (ROTATIVE/KMH/SPOT) | Orta | Orta | Her tip için unit test SQL bloğu yaz, Excel ile karşılaştır |
| Shipping POSTED → Invoice → PaymentPlan zinciri kırılır | Orta | Orta | SP içinde transaction, `Parameter InvoiceMode='INSTANT'` kontrolü, kontrol noktası 3 |
| Partner edit form 15+ alan olur, kullanıcı kaybolur | Düşük | Yüksek | Tab/section: Genel · Adres · Mali (risk+vade) · e-Belge · Banka |
| 15 dosya tek commit'te kalabalık | Orta | Yüksek | Faz başına commit (6 commit) |

## 5. Done Criteria

- [ ] Faz 1: `sp_CreateLoan` 7 calcMethod destekli; ANUITE/EQUAL_PRINCIPAL/BALLOON için unit test SQL bloğu pass
- [ ] Faz 2: `sp_ReceivingPost` POSTED'da `sp_UpdateItemCostMovingAvg` çağırıyor; PO/Receiving test verisi sonrası `ItemCost.AvgCost` doğru
- [ ] Faz 3: PO/Details satır kaydında `sp_CheckPriceVariance` çağrılıyor; sapma > tolerans ise UI'da uyarı banner gösteriliyor
- [ ] Faz 4: Shipping POSTED → otomatik SalesInvoice oluşuyor (`InvoiceMode='INSTANT'`); PaymentPlan kaydı açılıyor
- [ ] Faz 5: Partner Edit form'a risk+vade+e-belge alanları eklendi; CreditCard Create form'a `LinkedBankAccountId` dropdown eklendi; Loan Create form'da CalcMethod seçilebiliyor
- [ ] Faz 6: Aging detay sayfası `/finance/aging/{partnerId}` çalışıyor; FinancialAccount ekstre satır click → tx detay
- [ ] E2E: Tek tıkla "PO Aç → Onayla → Mal Kabul → Onayla → SO Aç → Onayla → Sevkiyat → Onayla → Fatura → Tahsilat" akışı %100 yeşil
- [ ] `operax-cli migrate` 0 hata
- [ ] `dotnet build` 0 hata 0 uyarı
- [ ] `docs/TODO.md` güncel — kapanan TODO'lar `[x] ✅` işaretli
- [ ] Plan dosyası `plans/archive/01-starter-package-go-live.md` olarak taşındı

## 6. Rollback Planı

- Git: her faz sonrası ayrı commit; problemli faz `git revert <hash>`
- DB: schema değişikliği YOK (mevcut kolonlar kullanılır), sadece SP CREATE OR ALTER — bir önceki SP versiyonu git history'de kalır
- UI: Razor sayfa silinmesi yok, sadece form genişletme — geri alma kolay

## 7. Adımlar

### Faz 1 — `sp_CreateLoan` Multi-Method (Backend)
1. [ ] `sp_CreateLoan` revize: parametre @CalcMethod
2. [ ] ANUITE branch (mevcut)
3. [ ] EQUAL_PRINCIPAL branch (her taksit anapara = P/n, faiz azalan bakiye)
4. [ ] BALLOON branch (ilk n-1 küçük taksit, son taksit BalloonAmount + kalan anapara)
5. [ ] SPOT branch (tek taksit vade sonunda, faiz = P × r × t)
6. [ ] ROTATIVE/KMH/DBS branch — taksit tablosu üretilmez, sadece Loan kaydı, taksitler kullanım hareketinde işlenir
7. [ ] Test SQL: 3 tip için manuel SELECT — toplam taksit ve faiz tutarlarını doğrula
8. [ ] Commit: `feat(M11): sp_CreateLoan 7 kredi tipi (plan: 01)`

### Faz 2 — `sp_ReceivingPost` ItemCost Wire (Backend)
1. [ ] `sp_ReceivingPost` içine her satır için `sp_UpdateItemCostMovingAvg @ItemId, ..., @Qty, @UnitCost=PoLine.Price, @MovementType='RECEIPT'`
2. [ ] `StockMovement.UnitCost` doldurulur (item cost AvgCost)
3. [ ] `sp_ShippingPost` ISSUE hareketinde ItemCost.OnHandQty düşür
4. [ ] Test: bir PO açıp mal kabul yap, ItemCost.AvgCost güncel mi?
5. [ ] Commit: `feat(M02): receiving/shipping ItemCost wire (plan: 01)`

### Faz 3 — `sp_CheckPriceVariance` UI Wire
1. [ ] PO/Details PageModel: satır kaydetme handler'ında `sp_CheckPriceVariance` çağır
2. [ ] Variance dönerse Razor view'da uyarı banner: "Liste fiyatı X ₺, girilen Y ₺, sapma %Z. Sebep belirtin."
3. [ ] `/purchasing/price-variances` listesi — bekleyen onaylar
4. [ ] Commit: `feat(M03): PriceVariance kontrolu UI (plan: 01)`

### Faz 4 — Shipping → Invoice Otomatik
1. [ ] `sp_ShippingPost` sonunda `Parameter InvoiceMode` oku
2. [ ] `INSTANT` ise `sp_GenerateSalesInvoiceFromShipping @ShippingId, @UserId` çağır
3. [ ] SalesInvoice oluştuğunda PaymentPlan otomatik açıldığını test et (SP zaten yapar)
4. [ ] Shipping Details UI'da "Fatura: INV-2026-0042" linki göster
5. [ ] Commit: `feat(M04): Shipping POSTED -> otomatik Invoice (plan: 01)`

### Faz 5 — Master Form Genişletmeleri
1. [ ] Partner Edit form'a tab/section yapısı: Genel · Adres · Mali · e-Belge · Banka
2. [ ] Mali sekmesinde: RiskScore (1-5 yıldız), RiskCategory dropdown, MaxOverdueDays, CreditLimit, BlockOnLimitExceed, PaymentTermDays, PaymentTermPolicy, DefaultPaymentMethod
3. [ ] e-Belge sekmesinde: EFaturaMukellef toggle, EFaturaAlias
4. [ ] Banka sekmesinde: IbanForRefund
5. [ ] CreditCard Create form: LinkedBankAccountId dropdown (FinancialAccount Type=BANK)
6. [ ] Loan Create form: CalcMethod dropdown + GracePeriodMonths + BalloonAmount (BALLOON tipi seçilirse)
7. [ ] Commit: `feat(M01,M11): master form genisletmeleri (plan: 01)`

### Faz 6 — Aging Detay + Hesap Ekstre Detay
1. [ ] `/finance/aging/{partnerId}` — partner bazlı detay yaşlandırma: hangi fatura hangi vade kovasında
2. [ ] PaymentPlan listesi (PartnerId filtre) + tıkla → ödeme/tahsilat formu
3. [ ] `/finance/accounts/{id}` — ekstrede satır click → tx detay drawer
4. [ ] Commit: `feat(M11): aging detay + hesap ekstre detay (plan: 01)`

### Faz 7 — E2E Test + Cleanup
1. [ ] Manuel test senaryosu: PO → Receiving → SO → Shipping → Invoice → Cheque → Collect → Bank
2. [ ] Build temiz
3. [ ] `docs/TODO.md` güncelle, kapanan tüm taskleri `[x] ✅` işaretle
4. [ ] Plan arşivle: `git mv plans/01-*.md plans/archive/`
5. [ ] Journal: `docs/journal/2026-05-XX.md` plan özeti

## 8. İlişkili

- Spec: `docs/MODULE_SPECS/M11_Finance_Procedures.md` (mevcut)
- Spec: `docs/MODULE_SPECS/M03_Purchasing_Extended.md` (mevcut)
- Spec: `docs/MODULE_SPECS/M04_SalesInvoice_Pricing.md` (mevcut)
- Roadmap: `docs/MASTER_ROADMAP.md` Faz 1 STARTER bölümü
- Sonraki plan: `02-ebelge-inbound-sync.md` (e-Fatura inbound sync, e-Belge entegrasyonu)

## 9. Onay

> Kullanıcı onay verene kadar implement edilmez.

- [ ] Plan kullanıcıya gösterildi
- [ ] Geri bildirim alındı (varsa düzeltildi)
- [ ] Onay alındı: <tarih, kullanıcı imzası>
