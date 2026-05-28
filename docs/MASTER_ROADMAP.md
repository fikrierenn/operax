# Operax Master Roadmap — Faz 1/2/3 Backend + UI

Bu belge tüm modüllerin backend ve UI kapsamını fazlar halinde sıralar.
Her kalem tamamlandığında `[x]` ile işaretlenir.

> Detay modül spec'leri için: [`docs/MODULE_SPECS/`](MODULE_SPECS/README.md)
> Rakip analizi ve önceliklendirme: [`docs/COMPETITOR_ANALYSIS.md`](COMPETITOR_ANALYSIS.md)
>
> **Kapsam dışı (resmi muhasebe):** e-defter, beyannameler, BA/BS, mali bilanço — M16 üzerinden Logo/Mikro/Netsis/Luca'ya yansıtılır.

---

## Faz 1 — Mali Akış (Fiyat + Maliyet + Fatura)

### 1A. PriceList — Tedarikçi/Müşteri Bazlı Fiyat Listeleri
- [ ] Şema: `PriceList` tablosuna `PartnerId` (NULL=genel, dolu=tedarikçi/müşteri özel), `ValidFrom`/`ValidTo` kolonları
- [ ] Şema: `PriceListLine`'a `Currency` kolonu
- [ ] TVF: `tvf_GetItemPrice(@CompanyId, @ItemId, @PartnerId, @Date)` — geçerli fiyatı döner (önce partner+ürün, yoksa genel)
- [ ] Seed: Demo tedarikçi/müşteri için ürün fiyat listeleri

### 1B. PriceVariance — Fiyat Farkı Tespiti ve Onayı
- [ ] Şema: `PriceVariance` (HeaderId, LineId, ItemId, ExpectedPrice, ActualPrice, Variance, Status DRAFT/APPROVED/REJECTED, ApprovedBy, ApprovedAt)
- [ ] SP: `sp_CheckPriceVariance` — PO/SO satır eklenirken çağrılır, tolerans dışında ise variance kaydı oluşturur
- [ ] Parametre: `PriceTolerancePercent` (default %5)
- [ ] PO ve SO line insert/update'lerinde otomatik kontrol

### 1C. ItemCost — Moving Average Maliyetlendirme
- [ ] Şema: `ItemCost` tablosu (CompanyId, ItemId, WarehouseId, AvgCost, OnHandQty, UpdatedAt)
- [ ] SP: `sp_UpdateItemCost` — Receiving POSTED'da çağrılır
  - `NewCost = (OnHand*OldCost + ReceivedQty*PurchasePrice) / (OnHand+ReceivedQty)`
- [ ] StockMovement.UnitCost kolonu eklenir
- [ ] Receiving Post SP'sinde `sp_UpdateItemCost` çağrısı

### 1D. ExpenseInvoice — Alış Faturası
- [ ] Şema kontrol: `ExpenseInvoice` zaten var (M07), `ExpenseInvoiceLine` ekle
- [ ] `ExpenseInvoice.PurchaseOrderId` kolonu (3-way matching)
- [ ] SP: `sp_GenerateExpenseInvoiceFromPO` — POSTED PO'dan otomatik fatura üretir
- [ ] Vade alanı: PaymentTermDays + DueDate

### 1E. SalesInvoice — Satış Faturası
- [ ] Şema: `SalesInvoice` + `SalesInvoiceLine` yeni tablolar
- [ ] SP: `sp_GenerateSalesInvoiceFromShipping` — Shipping POSTED'da Parameter `InvoiceMode=INSTANT` ise oto fatura
- [ ] EOD job için `sp_GenerateBatchSalesInvoices`

### 1F. CoGS — Satılan Malın Maliyeti
- [ ] Shipping POSTED'da StockMovement.UnitCost = ItemCost.AvgCost
- [ ] Rapor: COGS toplamı, Karlılık (Revenue - COGS)

---

### 1G. M11 — Finans Modülü (Kasa / Banka / Çek / Senet / Kredi / Kredi Kartı)

**Şemalar (yeni, `schema_M11_Finance.sql` dosyasında):**
- [ ] `FinancialAccount` — Hesap (CASH/BANK/CREDIT_CARD/LOAN/POS), IBAN, banka, limit, faiz
- [ ] `FinancialTransaction` — Tüm hareketler (gelir/gider/virman), source doc referansı, instrument bilgisi
- [ ] `Cheque` — Çek (alınan + verilen), Status (PORTFOLIO/IN_BANK/COLLECTED/RETURNED/ENDORSED/PAID)
- [ ] `PromissoryNote` — Senet (alınan + verilen), aynı statü makinesi
- [ ] `Loan` — Banka kredisi (anapara, faiz, vade)
- [ ] `LoanPayment` — Kredi taksitleri (vade, anapara, faiz, ödendi mi)
- [ ] `CreditCard` — Kredi kartı (limit, ekstre günü, son ödeme günü)
- [ ] `CreditCardStatement` — Aylık ekstre kayıtları
- [ ] `CreditCardTransaction` — Slip bazlı işlemler (taksitli desteği)
- [ ] `PaymentPlan` — Fatura/sipariş taksit planı

**Stored Procedures (`db_objects.sql` içine):**
- [ ] `sp_RecordPayment` — Multi-instrument ödeme (nakit + çek + kart birleşik)
- [ ] `sp_DepositCheque` — Çeki bankaya verme
- [ ] `sp_CollectCheque` — Çek tahsilatı (bakiye hareketi otomatik)
- [ ] `sp_ReturnCheque` — Karşılıksız çek
- [ ] `sp_PayLoanInstallment` — Kredi taksit ödemesi
- [ ] `sp_PayCreditCardStatement` — Kart ekstre kapatma

**View / TVF'ler:**
- [ ] `v_AccountBalance` — Hesap bakiyeleri (FinancialTransaction'dan running sum)
- [ ] `v_ChequePortfolio` — Vadesi yaklaşan çekler (vade < @days)
- [ ] `v_LoanSummary` — Kredi kalan anapara + sonraki taksit
- [ ] `tvf_CashProjection(@CompanyId, @Days)` — Gelecek N gün nakit projeksiyonu (çek vade + kredi taksit + PO vade + SO vade)

**Entegrasyon:**
- [ ] PO POSTED → Tedarikçi vade tarihinde PaymentPlan kaydı (otomatik)
- [ ] SalesInvoice → Müşteri vade tarihinde PaymentPlan kaydı
- [ ] Çek vadesinde Hangfire job → FinancialTransaction (banka hesabına gelir)
- [ ] Kredi taksit gününde notification

**UI Ekranları:**
- [ ] `/finance/accounts` — Hesap listesi (kasa+banka+kart+kredi tek liste, type chip ile filtreli)
- [ ] `/finance/accounts/{id}` — Hesap ekstre (hareketler timeline)
- [ ] `/finance/cheques` — Çek portföyü (sekme: Alınan/Verilen, durum filtreleri)
- [ ] `/finance/cheques/{id}` — Çek detay + statü işlemleri
- [ ] `/finance/notes` — Senet portföyü (aynı şablon)
- [ ] `/finance/loans` — Kredi listesi + taksit takvimi
- [ ] `/finance/credit-cards` — Kart listesi + ekstre detay
- [ ] `/finance/payments` — Ödeme kaydetme (multi-instrument form)
- [ ] `/finance/cash-projection` — Nakit projeksiyon dashboard (30/60/90 gün)

---

## Faz 2 — Lojistik Akış

### 2A. PickTask — Toplama Görevi
- [ ] SO APPROVED → PickTask oluşturma trigger/SP
- [ ] FIFO/FEFO bin allocation SP (`sp_AllocatePickFromBins`)
- [ ] PickTaskLine — hangi bin'den ne kadar

### 2B. PutawayTask — Yerine Koyma
- [ ] Receiving POSTED → PutawayTask oluşturma
- [ ] Bin scan ile onaylama, STAGING → Bin transfer hareketi

### 2C. Shipping Post Eklemeleri
- [ ] PickTask COMPLETED zorunlu
- [ ] StockMovement ISSUE + UnitCost
- [ ] SalesOrderLine.QtyShipped güncelleme
- [ ] SO durumu PARTIAL/SHIPPED'a geçirme

### 2D. Replenishment
- [ ] TVF: `tvf_ReplenishmentSuggestions` var, hibrit ekran ile kullanılır
- [ ] Auto-replenishment job (Hangfire)

---

## Faz 3 — Üretim + Cycle Count + Raporlar

### 3A. Production
- [ ] BOM rezervasyon SP
- [ ] ProductionOrder Start/Stop terminal akışı
- [ ] Sarfiyat (CONSUMPTION) + mamul kabul (PRODUCTION) hareketleri
- [ ] QC PASS/FAIL/REWORK workflow
- [ ] Planned vs Actual maliyet varyans

### 3B. CycleCount
- [ ] BLIND mod (ExpectedQty gizlenir)
- [ ] Tolerance kontrolü, COUNT_ADJ otomatik
- [ ] Recount workflow

### 3C. Reports
- [ ] Stok Değer Raporu (`OnHand × AvgCost`)
- [ ] Satınalma Analiz (tedarikçi/ürün/dönem)
- [ ] Satış Dolum Oranı (RequestedQty vs ShippedQty)
- [ ] Varyans Raporu (sayım farkları, fiyat farkları)
- [ ] COGS / Brüt Kar
- [ ] Aged Receivables / Payables

---

## UI Portu — Backend bitince başlar

22 ekran sırası (`ui-standard.md` partial'larıyla):
1. PurchaseOrders/Index ✅
2. PurchaseOrders/Details ✅
3. SalesOrders/Index ✅
4. SalesOrders/Details ✅
5-22. Warehouses, Partners, Items, Inventory (Balance+Movements), Production (List+Details), Expenses (List+Details), Budget, Reports (List+View), Admin/Users (List+Create), Admin/Settings, Receiving (List+Details), Shipping (List+Details), Transfer (List+Details), CycleCount (List+Details)

---

## İlerleme Takibi

Her kalem `[x]` işaretlenirken commit mesajına eklenir.
Faz tamamlandığında `docs/journal/YYYY-MM-DD.md`'ye özet yazılır.
