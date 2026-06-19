# M03 — Satınalma Genişletilmiş Spec

> Sürüm: v2 · Tarih: 2026-05-28
> Önkoşul: M03 temel akış (`PurchaseOrderHeader` + `PurchaseOrderLine` + `db_objects.sp_ReceivingPost`)
> Bağlı şemalar: `schema_M02_Costing.sql` (PriceVariance), `schema_M11_Finance.sql` (PaymentPlan)

---

## 1. Kapsam ve Hedef

Tipik bir Türk işletmesinin satınalma sürecinin **tam yaşam döngüsünü** tek bir modülde toplamak: teklif istemek, en uygun teklifi seçmek, onay zinciri, sipariş açmak, fiyat listesi kontrolü, mal kabul, fiyat farkı, fatura, ödeme planı ve maliyetlendirme. Logo Tiger / Mikro Fly / SAP B1 / Odoo eşdeğeri özellik kapsamı hedeflenir.

---

## 2. Eksik Özellikler ve Şema Eklemeleri

### 2.1 Tedarikçi Fiyat Listesi Kontrolü (M03.P1)

**Sorun:** PO satırı oluşturulurken `Price` serbest girilir. Kullanıcı yanlış fiyat yazabilir, tedarikçinin gönderdiği fiyat listesinden saparsa fark eden olmaz.

**Çözüm:**
- `PriceList` tablosu (zaten var) `PartnerId` + `Direction='PURCHASE'` + `ValidFrom/ValidTo` ile filtrelenecek.
- PO satırı kaydedilirken Dapper `BeforeSavePoLine` trigger'ı (veya C# servisi) fiyat listesi ile karşılaştırır.
- Sapma varsa `PriceVariance` kaydı oluşturulur. Belirli eşiğin üstünde (örn. %5) PO `PRICE_REVIEW` durumuna geçer.

**Stored Procedure:** `sp_CheckPriceVariance`
```
@CompanyId, @PoHeaderId, @ItemId, @ActualPrice, @ToleranceBps (200 = %2)
→ Karşılaştır → Sapma > tolerans ise PriceVariance INSERT + RETURN var
```

**Şema:** ✅ `schema_M02_Costing.sql` içinde `PriceVariance` ve `PriceList.PartnerId/ValidFrom/ValidTo/Direction` zaten eklendi.

**UI:**
- PO/Details satır kaydında inline uyarı: "Liste fiyatı 12.50 ₺, girdiğiniz 13.20 ₺ (%5.6 sapma). Sebep belirtin."
- `/purchasing/price-variances` ekranı: bekleyen onaylar listesi (yönetici görür).

### 2.2 Otomatik Fiyat Farkı (M03.P2)

**Senaryo:** PO 100 adet @ 10₺ açıldı. Mal kabulde tedarikçi 100 adet getirdi ama faturasında 11₺ yazıyor. Sistem otomatik olarak fiyat farkı kaydı oluşturur ve karara bağlar.

**Akış:**
1. `ReceivingPost` SP'si mal kabulü işlerken `PurchaseOrderLine.Price` ile gerçek satış faturası fiyatını karşılaştırır.
2. Fark varsa `PriceVariance` (Status=DRAFT) kaydı eklenir.
3. Yönetici onaylarsa:
   - PO satır fiyatı güncellenir
   - Maliyet motoru farkı `ItemCost.AvgCost`'a yansıtır
   - `sp_PostInvoiceVariance` ile fatura toplamına ekleme/çıkarma yapılır.

**Stored Procedure:** `sp_ApprovePriceVariance @VarianceId, @UserId`

### 2.3 Çok Seviyeli Onay (M03.A1)

**Senaryo:** PO'nun toplamı / kategorisine göre belirli rollerin onayı gerekir.

**Şema (yeni):**
```sql
CREATE TABLE ApprovalRule (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    DocType NVARCHAR(50) NOT NULL,     -- PURCHASE_ORDER, SALES_ORDER, EXPENSE_INVOICE
    RuleOrder INT NOT NULL,
    MinAmount DECIMAL(18,2),
    MaxAmount DECIMAL(18,2),
    RoleCode NVARCHAR(100),            -- onaylayacak rol
    IsActive BIT DEFAULT 1
);

CREATE TABLE ApprovalLog (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    DocType NVARCHAR(50), DocId UNIQUEIDENTIFIER,
    StepNo INT, RoleCode NVARCHAR(100),
    ApprovedBy UNIQUEIDENTIFIER, ApprovedAt DATETIME2,
    Decision NVARCHAR(20),             -- APPROVED, REJECTED, PENDING
    Notes NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);
```

**SP:** `sp_RequestApproval` — PO POSTED olmadan önce ApprovalRule'a göre adımları hesaplar, ApprovalLog'a PENDING kayıt atar.

**UI:**
- PO/Details sağ üst köşede aktif adım gösterilir: "2/3 onay bekliyor (Genel Müdür)"
- `/admin/approval-rules` ekranı yöneticiye matriks tanımlama imkanı verir.

### 2.4 Teklif Yönetimi (RFQ) (M03.R1, M03.R2)

**Akış:** Sipariş açmadan önce 3+ tedarikçiye fiyat sorulur, gelen teklifler karşılaştırılır, en iyi olan PO'ya dönüştürülür.

**Şema (yeni):** `schema_M03_RFQ.sql`
```sql
CREATE TABLE Rfq (             -- Request for Quotation
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    RfqNo NVARCHAR(50) NOT NULL,
    RequestedDate DATE NOT NULL,
    DueDate DATE NOT NULL,
    Status NVARCHAR(20) DEFAULT 'OPEN',    -- OPEN, CLOSED, CONVERTED, CANCELLED
    Notes NVARCHAR(MAX),
    -- standart audit kolonları
);

CREATE TABLE RfqLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RfqId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    UomId UNIQUEIDENTIFIER NOT NULL,
    QtyRequested DECIMAL(18,6) NOT NULL,
    Notes NVARCHAR(500)
);

CREATE TABLE RfqSupplier (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RfqId UNIQUEIDENTIFIER NOT NULL,
    PartnerId UNIQUEIDENTIFIER NOT NULL,
    InviteSentAt DATETIME2, RespondedAt DATETIME2,
    Currency NVARCHAR(10) DEFAULT 'TRY',
    LeadTimeDays INT NULL,
    PaymentTerm NVARCHAR(100),
    Notes NVARCHAR(MAX),
    Status NVARCHAR(20)                 -- INVITED, RESPONDED, SELECTED, REJECTED
);

CREATE TABLE RfqSupplierLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RfqSupplierId UNIQUEIDENTIFIER NOT NULL,
    RfqLineId UNIQUEIDENTIFIER NOT NULL,
    UnitPrice DECIMAL(18,4),
    DiscountPercent DECIMAL(8,4),
    TotalPrice DECIMAL(18,2),
    LeadTimeDays INT,
    AvailableQty DECIMAL(18,6),
    Notes NVARCHAR(500)
);
```

**SP'ler:**
- `sp_RfqInviteSuppliers @RfqId, @PartnerIdList` — toplu davet (e-posta job kuyruğuna)
- `sp_RfqCompare @RfqId` — karşılaştırma matrisi (min/max/avg per line)
- `sp_RfqConvertToPo @RfqId, @RfqSupplierId, @UserId` — seçilen tedarikçi için PO oluştur

**UI:**
- `/purchasing/rfq` liste sayfası
- `/purchasing/rfq/{id}` detay: sol panel kalemler, sağ panel davet edilen tedarikçiler
- `/purchasing/rfq/{id}/compare` karşılaştırma: satır × tedarikçi matriksi, en uygunu renkli vurgu

### 2.5 Tedarikçi Performans Skoru (M03.T1)

**Hesaplanan metrikler:**
- On-time delivery: PO vade tarihi vs Receiving tarih farkı
- Quality score: ReceivingLine'da `QcDefectQty` oranı
- Price stability: Geçmiş PriceVariance ortalama %
- Open balance: Cari hesap güncel borç

**View:** `v_SupplierScorecard`
```sql
SELECT p.Id, p.Code, p.Name,
       AVG(DATEDIFF(DAY, rh.UpdatedAt, h.DueDate)) AS AvgDelayDays,
       SUM(CASE WHEN rl.QcDefectQty > 0 THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS DefectRatePct,
       AVG(pv.VariancePercent) AS AvgPriceVariancePct,
       (...karma puan...)  AS OverallScore
FROM Partner p
LEFT JOIN PurchaseOrderHeader h ON h.PartnerId = p.Id
LEFT JOIN ReceivingHeader rh ON rh.PurchaseOrderId = h.Id
LEFT JOIN ReceivingLine rl ON rl.HeaderId = rh.Id
LEFT JOIN PriceVariance pv ON pv.PartnerId = p.Id
WHERE p.IsCustomer = 0  -- tedarikçi
GROUP BY p.Id, p.Code, p.Name;
```

**UI:** `/purchasing/suppliers/scorecard` — tedarikçi listesi puan sırasıyla, renk kodlu (yeşil/sarı/kırmızı).

### 2.6 Hizmet Kalemi (Stoksuz) Sipariş (M03.S1)

**Sorun:** Bazı satınalma kalemleri stok hareketi yapmaz (danışmanlık, kira, elektrik, yakıt).

**Çözüm:**
- `Item.ItemType` kolonuna `STOCK / SERVICE / FIXED_ASSET` değer set'i
- `sp_ReceivingPost` SERVICE tipinde StockMovement YAZMAZ — sadece PO'yu kapatır ve doğrudan ExpenseInvoice üretir.

**Şema:** `ALTER TABLE Item ADD ItemType NVARCHAR(20) DEFAULT 'STOCK';`

### 2.7 Vade ve Ödeme Şartı (M03.V1)

**Mevcut:** PO header'da ödeme vade alanı yok.

**Eklenecek kolon:** `PurchaseOrderHeader`
```sql
ALTER TABLE PurchaseOrderHeader ADD
    PaymentTermDays INT DEFAULT 30,                -- net 30/60/90
    PaymentInstallments INT DEFAULT 1,             -- taksit sayısı
    DueDate AS DATEADD(DAY, PaymentTermDays, OrderDate) PERSISTED,
    DiscountPercent DECIMAL(8,4) DEFAULT 0,
    Currency NVARCHAR(10) DEFAULT 'TRY',
    ExchangeRate DECIMAL(18,6) DEFAULT 1;
```

**Etkisi:** PO POSTED olunca `sp_GeneratePaymentPlanFromPO` çağrılır — `PaymentPlan` tablosuna taksit kayıtları açılır.

### 2.8 Çoklu Döviz + Kur Farkı (M03.D1)

**Mevcut:** Tüm tutarlar TRY varsayılır.

**Çözüm:**
- `Currency` ve `ExchangeRate` kolonu PO header'a eklenir (bkz. M03.V1).
- `PurchaseOrderLine.PriceTRY = Price * ExchangeRate` hesap kolonu.
- Mal kabul gününde kur farklı olabilir → `sp_ReceivingPost` farkı `KurFarkiGelir` veya `KurFarkiGider` kaleminde FinancialTransaction olarak yazar.

**Tablo:** Kur tarihçesi
```sql
CREATE TABLE ExchangeRate (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    Currency NVARCHAR(10) NOT NULL,
    Date DATE NOT NULL,
    BuyRate DECIMAL(18,6) NOT NULL,
    SellRate DECIMAL(18,6) NOT NULL,
    Source NVARCHAR(50)                  -- TCMB, MANUAL
);
CREATE UNIQUE INDEX UX_FX ON ExchangeRate(CompanyId, Currency, Date);
```

Hangfire job: `TcmbFxFetchJob` — günlük TCMB XML'i parse eder.

---

## 3. Stored Procedure Tam Listesi

| SP Adı | Amaç | Önkoşul |
|---|---|---|
| `sp_CheckPriceVariance` | Liste-fiili fiyat farkı tespiti | PriceList var, PartnerId set |
| `sp_ApprovePriceVariance` | Yönetici onayı + maliyet yansıtma | PriceVariance DRAFT |
| `sp_RequestApproval` | Çok seviyeli onay başlatma | ApprovalRule tanımı |
| `sp_RecordApproval` | Onaylayan kullanıcının kararı | ApprovalLog PENDING var |
| `sp_RfqInviteSuppliers` | RFQ'ya tedarikçi davet | RFQ OPEN |
| `sp_RfqCompare` | Teklif karşılaştırma matrisi | RFQ + RfqSupplierLine |
| `sp_RfqConvertToPo` | RFQ kazanan tedarikçi PO'ya | RFQ CLOSED, kazanan seçili |
| `sp_GeneratePaymentPlanFromPO` | PO POSTED → PaymentPlan | PO POSTED, PaymentTermDays |
| `sp_PostInvoiceVariance` | Fiyat farkı muhasebe yansıtma | Onaylı PriceVariance |

---

## 4. Yeni UI Ekranları

| Yol | Açıklama |
|---|---|
| `/purchasing/rfq` | Teklif istek listesi |
| `/purchasing/rfq/create` | Yeni RFQ |
| `/purchasing/rfq/{id}` | RFQ detay + tedarikçi davetleri |
| `/purchasing/rfq/{id}/compare` | Teklif karşılaştırma matrisi |
| `/purchasing/price-variances` | Fiyat farkı bekleyen onay listesi |
| `/purchasing/suppliers/scorecard` | Tedarikçi performans skoru |
| `/admin/approval-rules` | Onay zinciri matriks tanımı |
| `/admin/exchange-rates` | Kur tarihçesi |

---

## 5. Test Senaryoları

1. **Happy path RFQ → PO:** 3 tedarikçiye RFQ gönder, gelen teklifleri kıyasla, en uygunu PO'ya dönüştür, mal kabul yap.
2. **Fiyat sapma %3 (eşik %5 altında):** Otomatik PriceVariance oluşmaz, PO doğrudan POSTED.
3. **Fiyat sapma %12 (eşik üstü):** PriceVariance DRAFT, yönetici onayı bekler, onay sonrası PO POSTED.
4. **Multi-level approval — 50K PO:** Müdür onayı yeterli, GM görmez.
5. **Multi-level approval — 300K PO:** Hem müdür hem GM onayı zorunlu.
6. **Hizmet kalemi PO:** StockMovement oluşmaz, sadece ExpenseInvoice + FinancialTransaction.
7. **USD PO + kur farkı:** Kur 32 iken sipariş, 33 iken mal kabul → KurFarkiGider hareketi otomatik.
8. **Kısmi mal kabul (60/100):** PO PARTIAL_RECEIVED durumda kalır, kalan 40 için Receiving yeniden açılabilir.

---

## 6. Bağlı Modüller

- **M02 Maliyet:** Fiyat farkı onaylanınca `sp_UpdateItemCost` çağrılır
- **M11 Finans:** PO POSTED → PaymentPlan + FinancialTransaction (vade için)
- **M16 Entegrasyon:** RFQ daveti e-posta üzerinden tedarikçiye, kabul edilen PO Logo'ya export
- **M14 Komisyon:** Alım hacmi tedarikçi performansa ek olarak alıcı (buyer) komisyon hesabına da girdi olabilir
