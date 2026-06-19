# M16 — Entegrasyon Köprüsü (e-Fatura · Kargo · Resmi Muhasebe)

> Sürüm: v1 · Tarih: 2026-05-28

Operax dışı sistemlere giden bütün dataları tek noktadan yönetir. Çıkış yönü (Operax → diğer sistem) ve giriş yönü (webhook'lar). Asenkron tasarım — Hangfire job kuyruğu üzerinden.

---

## 1. Çıkış Modülleri

### 1.1 GİB e-Fatura / e-Arşiv / e-İrsaliye (M16.E1, M16.E2, M16.E3)

**Entegratör seçenekleri:** Foriba, eLogo, Mikro EDM, Uyumsoft, Trink, BizimHesap.

**Mimari:**
- `sp_GenerateUblXml @InvoiceId` → UBL 2.1 zarf XML üretimi
- `EInvoiceQueue` tablosu — gönderim kuyruğu
- Hangfire job `EInvoiceDispatcher` — REST POST entegratöre
- Webhook geri dönüş `/api/einvoice/callback` — UUID + statü güncelle (SalesInvoice.EInvoiceUUID)

**Yeni Tablo:**
```sql
CREATE TABLE EInvoiceQueue (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    InvoiceId UNIQUEIDENTIFIER NOT NULL,        -- SalesInvoice.Id
    InvoiceType NVARCHAR(20),                   -- E_FATURA, E_ARSIV, E_IRSALIYE
    Status NVARCHAR(20),                        -- PENDING, SENT, SUCCESS, FAILED, RETRY
    AttemptCount INT DEFAULT 0,
    LastError NVARCHAR(MAX),
    UblXml NVARCHAR(MAX),
    ExternalUUID NVARCHAR(50),
    SentAt DATETIME2,
    AckAt DATETIME2,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);
```

### 1.2 Resmi Muhasebe Yazılımlarına Yansıtma (M16.L1, M16.L2)

Faaliyetler:
- Satış faturası → Logo/Mikro/Netsis (XML/REST)
- Alış faturası (ExpenseInvoice) → muhasebe fişi
- Banka hareketleri → muhasebe
- Stok hareketleri → maliyet defteri

**Format Seçenekleri:**
- Logo Tiger ENT → `tlsoap.dll` üzerinden REST (Newtonsoft + WSDL)
- Mikro Fly → Mikro Web Service (XML SOAP)
- Netsis → Netsis API V8 (REST)
- Luca → Excel export + manuel yükleme veya REST API (Pro)
- BizimHesap → REST API

**Şema:**
```sql
CREATE TABLE AccountingMap (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ExternalSystem NVARCHAR(50),                -- LOGO, MIKRO, NETSIS, LUCA, BIZIMHESAP
    OperaxEntityType NVARCHAR(50),              -- ITEM, PARTNER, ACCOUNT
    OperaxEntityId UNIQUEIDENTIFIER NOT NULL,
    ExternalCode NVARCHAR(100) NOT NULL,        -- karşılığı muhasebe kodu (örn 153.01)
    Mapping NVARCHAR(MAX)                       -- ek alan eşlemeleri JSON
);

CREATE TABLE AccountingExportQueue (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    ExternalSystem NVARCHAR(50) NOT NULL,
    SourceDocType NVARCHAR(50),
    SourceDocId UNIQUEIDENTIFIER,
    Payload NVARCHAR(MAX),
    Status NVARCHAR(20),
    AttemptCount INT,
    LastError NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    SentAt DATETIME2,
    AckAt DATETIME2
);
```

### 1.3 Kargo Entegrasyonu (M16.K1, M16.K2)

**Desteklenecek firmalar:** UPS, MNG, Yurtiçi, Aras, Sürat, PTT, Hepsijet, Trendyol Express.

**Akış:**
1. Shipment POSTED → `sp_GenerateCarrierShipment @ShipmentId, @CarrierCode`
2. Carrier API'sine yük bilgisi POST → tracking number döner
3. `ShippingHeader.CarrierTrackingNumber` set edilir
4. Etiket PDF/ZPL geri döner → `Operax.PrintServer`'a gönderilir
5. Webhook `/api/carrier/{carrierCode}/webhook` ile statü güncellemeleri alınır (IN_TRANSIT, DELIVERED, EXCEPTION)

**Şema:**
```sql
ALTER TABLE ShippingHeader ADD
    CarrierCode NVARCHAR(20),                   -- UPS, MNG, YURTICI vb.
    CarrierTrackingNumber NVARCHAR(100),
    CarrierLabelUrl NVARCHAR(500),
    CarrierStatus NVARCHAR(30),                 -- PENDING, IN_TRANSIT, DELIVERED, RETURNED
    CarrierLastUpdate DATETIME2;

CREATE TABLE CarrierWebhookLog (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CarrierCode NVARCHAR(20),
    TrackingNumber NVARCHAR(100),
    Payload NVARCHAR(MAX),
    NewStatus NVARCHAR(30),
    ReceivedAt DATETIME2 DEFAULT GETUTCDATE()
);
```

### 1.4 Marketplace Senkronizasyonu (M16.M1)

Pazaryeri entegrasyonları:
- Trendyol Seller API
- Hepsiburada Marketplace API
- N11 API
- Amazon SP-API
- Çiçeksepeti API

**Akış (gelen sipariş):**
1. Webhook gelir → MarketplaceOrder kaydı (henüz SalesOrder değil)
2. Manuel veya otomatik onay → SalesOrder oluşturulur
3. Stok senkronizasyonu: Operax stok değiştiğinde Hangfire push → marketplace

```sql
CREATE TABLE MarketplaceOrder (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    MarketplaceCode NVARCHAR(30),               -- TRENDYOL, HEPSIBURADA, N11
    MarketplaceOrderId NVARCHAR(100),
    Payload NVARCHAR(MAX),                      -- ham JSON
    Status NVARCHAR(20),                        -- NEW, IMPORTED, REJECTED
    SalesOrderId UNIQUEIDENTIFIER,              -- import edilince
    ReceivedAt DATETIME2 DEFAULT GETUTCDATE()
);
```

---

## 2. Webhook Altyapısı

Endpoints `/api/integration/{system}/webhook` — JWT veya HMAC ile doğrulanır. WebhookEvent tablosuna kaydedilir, Hangfire async olarak işler.

```sql
CREATE TABLE WebhookEvent (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Source NVARCHAR(50),                        -- TRENDYOL, MNG, UPS, FORIBA
    EventType NVARCHAR(50),
    Payload NVARCHAR(MAX),
    Signature NVARCHAR(500),
    Status NVARCHAR(20),                        -- RECEIVED, PROCESSING, DONE, FAILED
    ProcessedAt DATETIME2,
    Error NVARCHAR(MAX),
    ReceivedAt DATETIME2 DEFAULT GETUTCDATE()
);
```

---

## 3. UI Ekranları

| Yol | Açıklama |
|---|---|
| `/integration/e-invoice-queue` | e-Fatura kuyruğu (PENDING/SENT/SUCCESS/FAILED) |
| `/integration/accounting-export` | Muhasebe yansıtma kuyruğu + manuel retry |
| `/integration/carrier-tracking` | Aktif kargo takipleri (canlı statü) |
| `/integration/marketplace-orders` | Pazaryeri siparişleri (import beklemeli) |
| `/integration/webhook-log` | Webhook geçmişi |
| `/admin/integration-settings` | Entegratör API key + endpoint ayarları |

---

## 4. Resmi Muhasebe Kapsamı

**Operax üretmez:**
- e-Defter (Yevmiye + Kebir) — Logo/Mikro/Luca yapar
- KDV/Stopaj/Muhtasar Beyannamesi XML
- BA/BS formları
- Mali bilanço (VUK formatı)

**Operax üretir ve M16 üzerinden dışarı verir:**
- Satış/alış faturası muhasebe fişi (gelir/borç dengesi ile)
- Tahsilat/ödeme muhasebe fişi
- Maliyet fişi (üretim, stok değer değişimi)
- Banka hareket fişi
- Kasa hareket fişi

Bu fişler ExternalSystem hedef formatına `Dapper + Adapter Pattern` ile çevrilir; her adapter `IAccountingAdapter` arayüzünü uygular.
