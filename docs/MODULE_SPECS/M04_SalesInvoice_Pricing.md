# M04 — Satış Faturası + Fiyat Politikası

> Sürüm: v1 · Tarih: 2026-05-28
> Önkoşul: `schema_M04_SalesInvoice.sql` (SalesInvoice + SalesInvoiceLine tabloları var)
> Bağlı: `schema_M02_Costing.sql` (PriceList genişlemesi), `schema_M11_Finance.sql` (PaymentPlan)

---

## 1. Kapsam

Operax içerisinden satılan tüm ürün/hizmetin faturalandırılması, fiyat politikalarının uygulanması, müşteri bazlı koşulların yönetimi ve müşteri ile tahsilat planının oluşturulması. e-Fatura UBL XML üretimi M16 entegrasyon modülü ile yapılır.

---

## 2. Fiyat Politikası — Çok Katmanlı Çözüm

Tipik bir Türk işletmesinin fiyat ihtiyacı 4 katmanda yönetilir. Operax PriceList'i bu 4 katmanı destekler.

```
Katman 1:  Item.SalesPrice               (varsayılan liste fiyatı)
Katman 2:  PriceList (Direction=SALES, PartnerId=NULL)
                                         (kategorik liste — ör. "Bayi Listesi")
Katman 3:  PriceList (PartnerId=Müşteri) (müşteri özel listesi)
Katman 4:  Promotion                     (kampanya dönemsel iskonto)
```

Sipariş satırı oluşturulurken `sp_ResolveSalesPrice` çağrılır; sıralama Katman 4 → 3 → 2 → 1. En sondaki bulunur.

### 2.1 `sp_ResolveSalesPrice`

```sql
CREATE OR ALTER PROCEDURE sp_ResolveSalesPrice
    @CompanyId UNIQUEIDENTIFIER, @PartnerId UNIQUEIDENTIFIER,
    @ItemId UNIQUEIDENTIFIER, @Qty DECIMAL(18,6),
    @AsOf DATE = NULL,
    @ResolvedPrice DECIMAL(18,4) OUTPUT,
    @PriceSource NVARCHAR(50) OUTPUT,    -- 'PROMOTION', 'PARTNER_LIST', 'GENERAL_LIST', 'ITEM_DEFAULT'
    @DiscountPercent DECIMAL(8,4) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @AsOf = ISNULL(@AsOf, CAST(GETUTCDATE() AS DATE));
    SET @DiscountPercent = 0;

    -- Katman 4: Promosyon
    SELECT TOP 1 @ResolvedPrice = pr.DiscountedPrice, @PriceSource = 'PROMOTION'
    FROM Promotion pr
    WHERE pr.CompanyId = @CompanyId AND pr.ItemId = @ItemId
      AND pr.IsActive = 1 AND @AsOf BETWEEN pr.ValidFrom AND pr.ValidTo
      AND (pr.PartnerId IS NULL OR pr.PartnerId = @PartnerId)
      AND (pr.MinQty IS NULL OR @Qty >= pr.MinQty)
    ORDER BY pr.Priority DESC;
    IF @ResolvedPrice IS NOT NULL RETURN;

    -- Katman 3: Partner-spesifik liste
    SELECT TOP 1 @ResolvedPrice = pll.UnitPrice, @PriceSource = 'PARTNER_LIST'
    FROM PriceList pl
    JOIN PriceListLine pll ON pll.PriceListId = pl.Id
    WHERE pl.CompanyId = @CompanyId AND pl.Direction = 'SALES'
      AND pl.PartnerId = @PartnerId AND pl.IsActive = 1
      AND (pl.ValidFrom IS NULL OR pl.ValidFrom <= @AsOf)
      AND (pl.ValidTo IS NULL OR pl.ValidTo >= @AsOf)
      AND pll.ItemId = @ItemId AND @Qty >= pll.MinQty
    ORDER BY pll.MinQty DESC;
    IF @ResolvedPrice IS NOT NULL RETURN;

    -- Katman 2: Genel liste (PartnerId IS NULL)
    SELECT TOP 1 @ResolvedPrice = pll.UnitPrice, @PriceSource = 'GENERAL_LIST'
    FROM PriceList pl
    JOIN PriceListLine pll ON pll.PriceListId = pl.Id
    WHERE pl.CompanyId = @CompanyId AND pl.Direction = 'SALES'
      AND pl.PartnerId IS NULL AND pl.IsActive = 1
      AND (pl.ValidFrom IS NULL OR pl.ValidFrom <= @AsOf)
      AND (pl.ValidTo IS NULL OR pl.ValidTo >= @AsOf)
      AND pll.ItemId = @ItemId AND @Qty >= pll.MinQty
    ORDER BY pll.MinQty DESC;
    IF @ResolvedPrice IS NOT NULL RETURN;

    -- Katman 1: Ürün varsayılan satış fiyatı
    SELECT @ResolvedPrice = SalesPrice, @PriceSource = 'ITEM_DEFAULT'
    FROM Item WHERE Id = @ItemId;
END
GO
```

### 2.2 Kademeli (Tier) İskonto

`PriceListLine.MinQty` zaten kademeli destek verir. Örn:
| ItemId | UnitPrice | MinQty |
|---|---|---|
| ABC | 100.00 | 1 |
| ABC | 95.00  | 50 |
| ABC | 88.00  | 200 |

50+ adet sipariş için 95, 200+ için 88 fiyat uygulanır.

### 2.3 Promosyon Tablosu (yeni)

```sql
CREATE TABLE Promotion (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    Code NVARCHAR(50) NOT NULL,
    NameTr NVARCHAR(200) NOT NULL,
    ItemId UNIQUEIDENTIFIER NULL,        -- NULL = tüm ürünler
    PartnerId UNIQUEIDENTIFIER NULL,     -- NULL = tüm müşteriler
    PromotionType NVARCHAR(20),          -- DISCOUNT_PCT, DISCOUNT_AMT, BUY_X_GET_Y, BUNDLE
    DiscountPercent DECIMAL(8,4),
    DiscountedPrice DECIMAL(18,4),
    MinQty DECIMAL(18,6),
    Priority INT DEFAULT 100,
    ValidFrom DATE NOT NULL,
    ValidTo DATE NOT NULL,
    IsActive BIT DEFAULT 1,
    -- audit kolonları
);
```

UI: `/sales/promotions` — kampanya listesi + aktif kampanya rozetleri (saatlik kampanya, dönemsel kampanya).

---

## 3. Müşteri Kredi Limit Kontrolü

`Partner` tablosuna yeni alanlar:
```sql
ALTER TABLE Partner ADD
    CreditLimit DECIMAL(18,2) DEFAULT 0,        -- 0 = sınırsız
    CreditLimitCurrency NVARCHAR(10) DEFAULT 'TRY',
    BlockOnLimitExceed BIT DEFAULT 1;            -- 1 = aşıldığında sipariş kabul edilmez
```

SO açılırken `sp_CheckCreditLimit`:
```sql
CREATE OR ALTER PROCEDURE sp_CheckCreditLimit
    @PartnerId UNIQUEIDENTIFIER, @NewOrderAmount DECIMAL(18,2),
    @CurrentBalance DECIMAL(18,2) OUTPUT, @CreditLimit DECIMAL(18,2) OUTPUT,
    @IsExceeded BIT OUTPUT, @CanProceed BIT OUTPUT
AS
BEGIN
    -- @CurrentBalance = PaymentPlan açık RECEIVABLE toplamı
    -- @CreditLimit = Partner.CreditLimit
    -- @IsExceeded = @CurrentBalance + @NewOrderAmount > @CreditLimit
    -- @CanProceed = NOT @IsExceeded OR NOT Partner.BlockOnLimitExceed
END
```

UI: SO/Details'ta üst banner — limit aşıldıysa kırmızı uyarı + yetkili kullanıcı override edebilir.

---

## 4. Satış Faturası Akışı

```
[ Sevkiyat POSTED ]
    │
    ├── Parameter "InvoiceMode" = "INSTANT"
    │       │
    │       └─→ sp_GenerateSalesInvoiceFromShipping
    │              │
    │              ├─ SalesInvoice (DRAFT) oluşturulur
    │              ├─ SalesInvoiceLine'lar ShipmentLine'dan kopyalanır
    │              ├─ UnitCost = ItemCost.AvgCost (COGS için)
    │              ├─ TaxAmount hesaplanır (Item.TaxRate × LineSubtotal)
    │              └─ Status = POSTED, PaymentPlan üretilir
    │
    └── Parameter "InvoiceMode" = "EOD_FROM_SHIPMENT"
            │
            └─→ Hangfire job (her gün 23:55) → sp_GenerateEodInvoices
                    │
                    └─ O gün sevki yapılmış henüz faturalanmamış shipment'ları toplu fatura olarak işler
```

### 4.1 `sp_GenerateSalesInvoiceFromShipping`

```sql
CREATE OR ALTER PROCEDURE sp_GenerateSalesInvoiceFromShipping
    @ShippingId UNIQUEIDENTIFIER, @UserId UNIQUEIDENTIFIER,
    @NewInvoiceId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET XACT_ABORT ON; SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CompanyId UNIQUEIDENTIFIER, @PartnerId UNIQUEIDENTIFIER, @SoId UNIQUEIDENTIFIER;
        SELECT @CompanyId = CompanyId, @PartnerId = PartnerId, @SoId = SalesOrderId
        FROM ShippingHeader WHERE Id = @ShippingId AND Status = 'POSTED';
        IF @CompanyId IS NULL THROW 70001, 'Sevkiyat bulunamadı veya henüz POSTED değil.', 1;

        IF EXISTS (SELECT 1 FROM SalesInvoice WHERE ShippingId = @ShippingId AND IsDeleted = 0)
            THROW 70002, 'Bu sevkiyat için zaten fatura oluşturulmuş.', 1;

        -- Yeni fatura no üret (INV-YYYY-XXXXX)
        DECLARE @InvNo NVARCHAR(50);
        EXEC sp_GenerateDocNo @CompanyId, 'INVOICE', @InvNo OUTPUT;

        SET @NewInvoiceId = NEWID();
        DECLARE @PaymentTermDays INT;
        SELECT @PaymentTermDays = ISNULL(PaymentTermDays, 30) FROM Partner WHERE Id = @PartnerId;

        INSERT INTO SalesInvoice (Id, CompanyId, InvoiceNo, InvoiceDate, DueDate,
                                  PartnerId, ShippingId, SalesOrderId,
                                  Subtotal, TaxAmount, GrandTotal, Status, CreatedBy)
        VALUES (@NewInvoiceId, @CompanyId, @InvNo, GETUTCDATE(),
                DATEADD(DAY, @PaymentTermDays, GETUTCDATE()),
                @PartnerId, @ShippingId, @SoId, 0, 0, 0, 'POSTED', @UserId);

        -- Satırları kopyala
        INSERT INTO SalesInvoiceLine
            (Id, InvoiceId, ItemId, UomId, Description, Qty, UnitPrice,
             LineSubtotal, TaxRatePercent, TaxAmount, LineTotal, UnitCost)
        SELECT NEWID(), @NewInvoiceId, sl.ItemId, sl.UomId,
               i.Name, sl.QtyBase, ISNULL(sol.UnitPrice, i.SalesPrice),
               sl.QtyBase * ISNULL(sol.UnitPrice, i.SalesPrice),
               ISNULL(i.TaxRate, 20),
               sl.QtyBase * ISNULL(sol.UnitPrice, i.SalesPrice) * ISNULL(i.TaxRate, 20) / 100,
               sl.QtyBase * ISNULL(sol.UnitPrice, i.SalesPrice) * (1 + ISNULL(i.TaxRate, 20)/100),
               ic.AvgCost
        FROM ShippingLine sl
        JOIN Item i ON i.Id = sl.ItemId
        LEFT JOIN SalesOrderLine sol ON sol.Id = sl.SalesOrderLineId
        LEFT JOIN ItemCost ic ON ic.CompanyId = @CompanyId AND ic.ItemId = sl.ItemId
        WHERE sl.HeaderId = @ShippingId;

        -- Header toplamlarını güncelle
        UPDATE SalesInvoice
        SET Subtotal   = (SELECT SUM(LineSubtotal) FROM SalesInvoiceLine WHERE InvoiceId = @NewInvoiceId),
            TaxAmount  = (SELECT SUM(TaxAmount) FROM SalesInvoiceLine WHERE InvoiceId = @NewInvoiceId),
            GrandTotal = (SELECT SUM(LineTotal) FROM SalesInvoiceLine WHERE InvoiceId = @NewInvoiceId)
        WHERE Id = @NewInvoiceId;

        -- PaymentPlan üret
        EXEC sp_GeneratePaymentPlanFromInvoice @NewInvoiceId, @UserId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION; THROW;
    END CATCH
END
GO
```

### 4.2 KDV Kademeli Vergi Hesabı (Tevkifat)

Bazı kalemler tevkifatlı (örn. hurda, demir-çelik) — özel oran hesabı.

```sql
ALTER TABLE Item ADD
    TaxWithholdingRate DECIMAL(8,4) NULL,       -- tevkifat oranı (örn 5/10)
    TaxWithholdingType NVARCHAR(20) NULL;       -- TEVKIFAT_5_10, TEVKIFAT_7_10 vs.
```

Fatura hesabı:
- LineSubtotal × KDVOranı = TotalKDV
- TotalKDV × TevkifatOranı = TevkifatTutarı (örn 5/10 ise %50)
- Müşteri öder: LineSubtotal + (TotalKDV - TevkifatTutarı)
- Tevkifatlı KDV: muhasebe fişine ayrı yansıtılır (M16 export'unda)

---

## 5. EOD (End-of-Day) Toplu Fatura

`Parameter` tablosunda `InvoiceMode = 'EOD_FROM_SHIPMENT'` set edildiğinde Hangfire job her gün 23:55'te çalışır:

```sql
CREATE OR ALTER PROCEDURE sp_GenerateEodInvoices
    @CompanyId UNIQUEIDENTIFIER, @Date DATE
AS
BEGIN
    DECLARE cur CURSOR FOR
    SELECT Id FROM ShippingHeader
    WHERE CompanyId = @CompanyId AND Status = 'POSTED'
      AND CAST(PostedAt AS DATE) = @Date
      AND Id NOT IN (SELECT ShippingId FROM SalesInvoice WHERE IsDeleted = 0);

    DECLARE @ShipId UNIQUEIDENTIFIER, @NewInvId UNIQUEIDENTIFIER;
    OPEN cur; FETCH NEXT FROM cur INTO @ShipId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC sp_GenerateSalesInvoiceFromShipping @ShipId, NULL, @NewInvId OUTPUT;
        FETCH NEXT FROM cur INTO @ShipId;
    END
    CLOSE cur; DEALLOCATE cur;
END
GO
```

---

## 6. Konsinye Satış (Consignment)

Müşteriye ürün gönderilir ama satılana kadar fatura kesilmez. Stok hala bizim sayılır.

**Şema:** `SalesOrderHeader.OrderType` kolonu:
```sql
ALTER TABLE SalesOrderHeader ADD OrderType NVARCHAR(20) DEFAULT 'NORMAL';
-- NORMAL, CONSIGNMENT, SAMPLE, RMA_REPLACEMENT
```

**Akış:**
- OrderType=CONSIGNMENT → Shipment POSTED'da fatura üretilmez
- Stok bizim defterimizde "müşterideki konsinye stok" alanında tutulur (ayrı warehouse)
- Müşteri kullandığında ConsignmentUsage formu → otomatik SalesInvoice + Stock ISSUE

**Yeni Şema:**
```sql
CREATE TABLE ConsignmentUsage (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    SoHeaderId UNIQUEIDENTIFIER NOT NULL,
    ReportDate DATE NOT NULL,
    Notes NVARCHAR(MAX)
);
CREATE TABLE ConsignmentUsageLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UsageId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    QtyUsed DECIMAL(18,6) NOT NULL,
    UnitPrice DECIMAL(18,4)
);
```

---

## 7. UI Ekranları

| Yol | Açıklama |
|---|---|
| `/sales/invoices` | Fatura listesi (DRAFT/POSTED/PAID/CANCELLED filtre) |
| `/sales/invoices/{id}` | Fatura detay + PDF + e-Fatura statüsü |
| `/sales/invoices/batch` | Toplu fatura üretme (EOD manuel tetik) |
| `/sales/price-lists` | Fiyat listeleri (genel + müşteri özel) |
| `/sales/price-lists/{id}` | Liste detay + satır editör |
| `/sales/promotions` | Kampanya yönetimi |
| `/sales/credit-limits` | Müşteri kredi limit görünümü (kullanım oranı bar'larıyla) |
| `/sales/consignment` | Konsinye sipariş listesi + kullanım raporu |

---

## 8. Test Senaryoları

1. **Kademeli iskonto:** 49 adet @ 100 ₺, 50. adette 95 ₺'ye düşer mi?
2. **Müşteri özel liste > genel liste:** Müşteri A'ya 80 ₺ liste tanımlı, genel listede 100 ₺. Sipariş A'ya 80 ₺ uygular.
3. **Promosyon > müşteri listesi:** A müşterisinin özel listesi 80 ₺, kampanya 75 ₺ — kampanya öncelikli.
4. **Kredi limit aşımı:** 100K limit, 95K açık fatura var, 10K yeni sipariş — bloklanır veya yetkili override.
5. **INSTANT fatura:** Sevkiyat POSTED → 1 sn içinde fatura ekranda görünür.
6. **EOD toplu fatura:** Aynı müşteriye gün içi 3 sevk → tek faturada birleştir.
7. **Tevkifatlı satış:** Hurda kalemi → KDV 18'in %50'si fatura toplamından düşülür, ayrı muhasebe kalemi.
8. **Konsinye:** Sipariş ASTYPE=CONSIGNMENT → sevk yapıldı ama stok düşmedi. Kullanım formu → stok düşer + fatura kesilir.

---

## 9. Entegrasyon (e-Fatura)

M16 modülü `sp_GenerateUblXml @InvoiceId` çağırır → UBL 2.1 zarf XML'i üretir → GİB e-Fatura entegratörüne (Foriba, eLogo, Mikro EDM vb.) POST eder. Cevap UUID `SalesInvoice.EInvoiceUUID`'ye yazılır.
