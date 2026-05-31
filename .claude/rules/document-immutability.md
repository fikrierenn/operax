# Evrak Bütünlüğü Kuralı (Document Immutability)

Operax'taki tüm evrak modülleri (PO / Receiving / SO / Shipping / ExpenseInvoice / SalesInvoice / Transfer / CycleCount / Production WorkOrder) bu kurala tabidir. VUK ve denetim açısından da bağlayıcıdır.

---

## 1. Temel İlke

> **Bir evrak başka bir evraktan beslendiyse, kaynak belge değiştirilemez.**

İş zinciri ileri doğru akar, geriye değişiklik propagasyonu kabul edilmez. Fark çıkarsa **kaynak değişmez**, fark için ayrı bir **Variance** belgesi açılır.

---

## 1.b Ledger Immutability + Dönem Kilidi (Plan 14)

Ledger tabloları (`StockMovement`, `AccountMovement`, `FinancialTransaction`) **append-only**'dir:

- **Silme YOK; düzeltme mekanizması tabloya göre iki ayrı yöntem (Plan 22 Faz C1 ile netleşti):**
  - **`AccountMovement`** (IsCancelled kolonu YOK) → düzeltme = `SourceDocType='REVERSAL'` **ters satır**. Bakiye = `SUM(Borc-Alacak)` tüm satırlar; ters satır orijinali nötrler.
  - **`StockMovement`** (IsCancelled kolonu VAR) → cancel = orijinal hareketlere **`IsCancelled=1`** flag (+`CancelledAt`/`CancelledBy` audit izi). **Ters hareket YAZILMAZ.** Bakiye = `SUM(QtyBase WHERE IsCancelled=0)` (tvf_InventoryBalance + 11 okuyucu bu filtreyi kullanır). ⚠️ Hem flag hem ters-satır yazmak **çift-sayım** (stok 2× geri gelir) — yasak.
  - İki yöntem de append-only: hiçbir satır fiziksel silinmez. (`AccountMovement.IsDeleted` kaldırılıyor — vestigial.)
- **Dönem kilidi (ZAMAN bazlı):** `AccountingPeriod(CompanyId, Year, Month, Status)` — `OPEN/CLOSED/LOCKED`. Her ledger hareket/onay SP'sinin ilk satırı `sp_GuardPeriodOpen(@CompanyId, @MovementDate, @UserId, ...)`:
  - **OPEN** (veya dönem kaydı yok) → serbest, iz yok.
  - **CLOSED** → yalnızca dar yetki (`UserCompany.Role IN Administrator/Finance`) + zorunlu gerekçe (kategori + metin) ile aşılır → `PeriodOverrideLog`'a **atomik** iz; aksi `THROW`.
  - **LOCKED** (e-Defter berat) → **koşulsuz `THROW`**, istisna yok. Tek çözüm: sonraki açık döneme düzeltme kaydı.
- **`PeriodOverrideLog` SİLİNMEZ** — kim, hangi kilit, hangi tarih (giriş ≠ hareket tarihi ayrı), zorunlu gerekçe kategorisi. **Görevler ayrılığı:** `OverriddenBy ≠ ApprovedBy` (kendi override'ını onaylayamaz).
- **Kilit aileleri ayrı:** ZAMAN → `AccountingPeriod` (bu plan) · stok SATIRI → sayım freeze (M08) · partner+tarih → cari mutabakat (M11 sonra). Ortak nokta yalnızca guard zinciri + `PeriodOverrideLog.LockType` izi.

Mekanizma: `docs/sql/schema_M11_LedgerIntegrity.sql` · ADR: `docs/ADR/01-ledger-clustered-key.md`.

---

## 2. Modül Zincirleri

### 2.1 Satınalma Zinciri
```
PurchaseOrder (PO)
  └── ReceivingHeader (Mal Kabul)
        └── ExpenseInvoice (Alış Faturası)
              └── PaymentPlan / FinancialTransaction (Ödeme)
```

### 2.2 Satış Zinciri
```
SalesOrder (SO)
  └── ShippingHeader (Sevkiyat)
        └── SalesInvoice (Satış Faturası)
              └── PaymentPlan / FinancialTransaction (Tahsilat)
```

### 2.3 Üretim Zinciri
```
ProductionOrder
  ├── ProductionConsumption (Hammadde Sarfı → StockMovement ISSUE)
  └── ProductionReceipt (Mamul Girişi → StockMovement RECEIPT)
```

### 2.4 Finans Zinciri — Banka / Çek / Senet / Kredi

Finansal araçlar da "evrak" sayılır. Aynı immutability kuralı uygulanır:

```
FinancialTransaction (Kasa/Banka hareketi)
  └── PaymentPlan eşleşmesi (FinancialTransactionId set edildi → ödeme kapatıldı)

Cheque PORTFOLIO ──→ IN_BANK ──→ COLLECTED → FinancialTransaction INCOME
                  │              │
                  │              └── RETURNED (karşılıksız) — ters hareket gerekmez
                  └── ENDORSED (ciro) — ters hareket gerekmez

PromissoryNote — Cheque ile aynı zincir

Loan
  └── LoanPayment (her taksit)
        └── FinancialTransaction EXPENSE (taksit ödendi)

CreditCard
  └── CreditCardStatement (aylık ekstre)
        ├── CreditCardTransaction (slip işlemleri — ekstreye dahil edilince kilitli)
        └── FinancialTransaction EXPENSE (ekstre ödendi)
```

### 2.5 Sipariş Zinciri (PO/SO satır seviyesi)

Sipariş satırı (PurchaseOrderLine / SalesOrderLine) de evraktır:

| Belge | Durum | Bağlı Aşağı Kayıt | Düzenlenebilir mi? |
|---|---|---|---|
| PurchaseOrderLine | PO POSTED | ReceivingLine var | ❌ Hiçbir şey |
| SalesOrderLine | SO POSTED | ShippingLine var | ❌ Hiçbir şey |
| SalesOrderLine | SO POSTED | Hiç sevkiyat yok | ⚠️ Sadece Cancel |

---

## 3. Kilit Matrisi

| Belge | Durum | Bağlı Aşağı Belge | Düzenlenebilir mi? |
|---|---|---|---|
| PO | DRAFT | — | ✅ Tümü |
| PO | POSTED | Receiving yok | ⚠️ Sadece **Cancel** |
| PO | POSTED | En az 1 Receiving (DRAFT veya POSTED) | ❌ Hiçbir şey |
| Receiving | DRAFT | — | ✅ Tümü |
| Receiving | POSTED | ExpenseInvoice yok | ⚠️ Sadece **Cancel** (ters hareket) |
| Receiving | POSTED | ExpenseInvoice bağlı | ❌ Hiçbir şey |
| ExpenseInvoice | DRAFT | — | ✅ Tümü |
| ExpenseInvoice | POSTED | Ödeme yok | ⚠️ Sadece **Cancel** |
| ExpenseInvoice | POSTED | En az 1 ödeme | ❌ Hiçbir şey |
| SO / Shipping / SalesInvoice | (aynı mantık) | | |
| **FinancialTransaction** | herhangi | IsReconciled=1 (banka mutabakat) | ❌ Hiçbir şey, ters tx aç |
| **FinancialTransaction** | herhangi | IsReconciled=0 | ⚠️ Sadece açıklama düzenleme; tutar/tarih/hesap değişmez |
| **Cheque** | PORTFOLIO | — | ✅ Tüm alan |
| **Cheque** | IN_BANK | — | ⚠️ Sadece ReturnReason (karşılıksız durumunda) |
| **Cheque** | COLLECTED/PAID/RETURNED/ENDORSED | — | ❌ Hiçbir şey, sadece görüntüleme |
| **PromissoryNote** | Cheque ile aynı matriks | | |
| **Loan** | ACTIVE | LoanPayment yok (yeni açıldı) | ⚠️ Sadece Notes |
| **Loan** | ACTIVE | En az 1 ödenmiş LoanPayment | ❌ Anapara/faiz/vade değişmez (yapılandırma → yeni Loan açılır) |
| **Loan** | CLOSED/RESTRUCTURED | — | ❌ Hiçbir şey |
| **LoanPayment** | IsPaid=1 | — | ❌ Hiçbir şey (ödeme yapılmış) |
| **CreditCardStatement** | IsClosed=1 | — | ❌ Hiçbir şey (ekstre kapandı) |
| **CreditCardTransaction** | StatementId NULL | — | ✅ Tüm alan (henüz ekstreye düşmedi) |
| **CreditCardTransaction** | StatementId set | — | ❌ Hiçbir şey (ekstreye dahil) |
| **PaymentPlan** | OPEN | — | ✅ Tüm alan |
| **PaymentPlan** | PARTIAL | — | ⚠️ Sadece DueDate (vade ertelemesi) ve Notes |
| **PaymentPlan** | PAID/OVERDUE → PAID | — | ❌ Hiçbir şey (ters için sp_CancelPayment) |

---

## 4. Fark Yönetimi (Variance Documents)

Eğer beklenen ile gerçekleşen arasında fark varsa:

### 4.1 PO ↔ Receiving farkı
- **Qty farkı:** Receiving'de tedarikçi 100 sipariş etti, 95 getirdi → PO `OpenQty=5` kalır, kullanıcı seçer:
  - **Kalan beklensin** (PO POSTED kalır, yeni Receiving açılabilir)
  - **Sipariş kapansın** (PO STATUS=CLOSED_PARTIAL)
- **Price farkı:** PO 10₺/adet, fatura 11₺/adet → `PriceVariance` kaydı (DRAFT), yönetici onaylar → onay sonrası `ItemCost.AvgCost` güncellenir

### 4.2 Receiving ↔ ExpenseInvoice farkı
- **Tutar farkı:** Fatura net farkı → `InvoiceVariance` kaydı
- **Ek kalem:** Fatura'da olmayan satır → ExpenseInvoiceLine direkt eklenir (Receiving'e dokunulmaz)

### 4.3 Variance şemaları
- `PriceVariance` — şu an var (`schema_M02_Costing.sql`)
- `QtyVariance` — gerekli, henüz yok (TODO)
- `InvoiceVariance` — gerekli, henüz yok (TODO)

---

## 5. Enforcement Katmanları

Kural **üç katmanda** uygulanır (defense in depth):

### 5.1 UI Katmanı (Razor PageModel + View)
- Detail/Edit sayfasında `OnPost` handler ilk satırda `Guard.LockedIfChildExists(...)` çağrısı
- View'da Status=POSTED ise tüm `<input>` `readonly`, butonlar `disabled`
- `_DocToolbar` partial'ı statu+child varlığına göre buton seti gösterir

### 5.2 SP Katmanı
- Her `sp_*Edit` veya `sp_*UpdateLine` SP'si girişte `EXISTS (SELECT 1 FROM Child WHERE ParentId = @Id)` kontrolü yapar, varsa `THROW 51100, N'Belge kilitli: bağlı X kaydı mevcut.', 1`
- `sp_ValidateStatusTransition` (mevcut) zaten durum geçişi engelliyor — bu ek olarak veri değişimini engeller

### 5.3 DB Tetikleyici (Trigger)
- `tr_PreventChildEdit_<Table>` AFTER UPDATE trigger her bir kilitli kolon (Price, Qty, ItemId, UomId) için child kontrol yapar
- Trigger en son savunma hattı, UI/SP'nin atlandığı senaryolarda kurtarır

---

## 6. Cancel İşlemi (POSTED → CANCELLED)

POSTED bir belge cancel edilirse:

1. **Ters StockMovement** yazılır (eğer stok hareketi vardıysa)
2. **Down-chain belgeler** kontrol edilir:
   - Bağlı POSTED child varsa **cancel REDDEDİLİR** ("önce X-1234 cancel edilmeli")
   - Bağlı DRAFT child varsa kullanıcı onayı ile beraber cancel edilir
3. **PaymentPlan** PAID değilse otomatik CANCELLED
4. **Audit log:** Her cancel detaylı kayıt (kim, neden, hangi tarih)

---

## 7. Belge Bütünlüğü Helper'ı

`src/Operax.Web/Lib/DocumentLock.cs` (eklenecek):

```csharp
public static class DocumentLock
{
    // PO için child kontrolü
    public static bool PoHasReceiving(IDbConnection conn, Guid poId)
        => conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM ReceivingHeader WHERE PurchaseOrderId = @id AND IsDeleted = 0", new { id = poId }) > 0;

    // Receiving için child kontrolü
    public static bool ReceivingHasInvoice(IDbConnection conn, Guid receivingId)
        => conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM ExpenseInvoice WHERE ReceivingId = @id AND IsDeleted = 0 AND Status <> 'CANCELLED'", new { id = receivingId }) > 0;

    // ... benzer SO/Shipping/SalesInvoice helper'ları
}
```

PageModel kullanımı:
```csharp
public async Task<IActionResult> OnPostEditLineAsync(Guid lineId, ...)
{
    using var conn = db.Open();
    var line = await conn.QuerySingleAsync<PoLineDto>(...);

    // Guard: bağlı Receiving varsa kilitle
    if (DocumentLock.PoHasReceiving(conn, line.HeaderId))
        return new BadRequestObjectResult("Belge kilitli: bu siparişe mal kabul yapılmış.");

    // ... güncelleme
}
```

---

## 8. Test Senaryoları

- **PO POSTED → Receiving DRAFT açıldı → PO satır edit denemesi → REJECTED**
- **PO POSTED → Receiving POSTED → PO Cancel denemesi → REJECTED (önce Receiving cancel)**
- **Receiving POSTED → ExpenseInvoice POSTED → Receiving satır edit denemesi → REJECTED**
- **PO POSTED + tedarikçi 95/100 getirdi → Receiving POSTED → PO Status=CLOSED_PARTIAL, ItemCost.OnHand güncel**
- **PO Price 10₺ ↔ fatura 11₺ → PriceVariance DRAFT açılır, yönetici onaylar, ItemCost.AvgCost güncellenir**

---

## 9. İlişkili Dosyalar

- `.claude/rules/architecture.md` §3 (Evrak Yaşam Döngüsü)
- `.claude/rules/sql-conventions.md` §3 (SP standartları + THROW kuralları)
- `.claude/rules/before-major-change.md` (silme/rename öncesi)
- `docs/MODULE_SPECS/M03_Purchasing_Extended.md` §2.2 (otomatik fiyat farkı)
- `docs/sql/schema_M02_Costing.sql` (PriceVariance tablosu — şu an var)
- `plans/01-starter-package-go-live.md` (Faz 3 PriceVariance UI wire içeriyor)
- TODO (yeni): `QtyVariance` + `InvoiceVariance` şemaları
- TODO (yeni): `DocumentLock.cs` helper class
- TODO (yeni): SP-level `sp_*Edit` guard'ları
- TODO (yeni): DB trigger `tr_PreventChildEdit_*`
