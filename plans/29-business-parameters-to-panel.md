# Plan 29 — İş-Ayarı Magic-Number'ları Parameter Paneline Taşı

**Tier 3** · Durum: TAMAM (yaşlandırma kovaları gerekçeli kapsam-dışı) · 2026-06-02

## Problem
İş-ayarı niteliğindeki sabitler kod/SP içinde hardcoded. Müşteri başına değişebilmeli (Admin > Parametreler). Tarama (Explore, 2026-06-02) ~15 aday buldu; **yalnız iş-ayarları** kapsamda — güvenlik/altyapı (rate-limit, lockout, session, HSTS, cron) appsettings'te kalır (DB panele almak riskli), teknik sabitler (THROW, cache TTL, DocStatus) taşınmaz.

## Kapsam — 5 iş-ayarı parametresi (ModuleCode bazlı)

| Parameter.Code | Tip | Varsayılan | Kullanım yerleri |
|---|---|---|---|
| `DEFAULT_PURCHASE_TAX_RATE` | decimal | 20 | Alış fatura/sipariş SP+form fallback. KDV per-item (`Item.TaxRate`, çoklu oran 1/10/20) önceliklidir; bu yalnız ürün oranı yoksa fallback |
| `DEFAULT_SALES_TAX_RATE` | decimal | 20 | Satış fatura/sipariş fallback. Alış oranından FARKLI olabilir (kullanıcı notu). Per-item öncelikli |
| `DEFAULT_PAYMENT_TERM_DAYS` | int | 30 | Partner/PO/SO C# form default vade |
| `AGING_BUCKET_2_DAYS` | int | 30 | Cari yaşlandırma 1. eşik (0-30) |
| `AGING_BUCKET_3_DAYS` | int | 60 | 2. eşik (31-60) |
| `AGING_BUCKET_4_DAYS` | int | 90 | 3. eşik (61-90); üstü >90 |
| `PARTNER_RECON_DEADLINE_MONTHS` | int | 1 | Mutabakat sessiz onay deadline (`DATEADD(MONTH,@n,...)`) |
| `AI_INFERENCE_TIMEOUT_SECONDS` | int | 120 | PurchaseInvoices fiyat farkı AI denetim timeout |
| `INVOICE_AGING_DAYS` | int | 30 | (Plan 28 — zaten parametrik ✓) |

> Not: Column DEFAULT'lar (TaxRatePercent DEFAULT 20 vb.) güvenlik ağı olarak 20'de kalır; gerçek değer SP/uygulama tarafından param'dan set edilir. Item.TaxRate / Partner.PaymentTermDays kolonları zaten kayıt-bazlı — param yalnız YENİ kayıt fallback'i.
>
> **KDV modeli (kullanıcı notu 2026-06-02):** KDV oranı üründe taşınır (`Item.TaxRate`, çoklu oran 1/10/20). Öncelik: Item.TaxRate → yoksa belge tipine göre param (alış/satış ayrı). Aynı ürün için alış≠satış oranı GEREKİRSE ayrı `Item.PurchaseTaxRate` kolonu gerekir — şu an tek `Item.TaxRate`; bu ihtiyaç doğarsa AYRI plan (kapsam dışı). Mevcut: belge-tipi bazlı default ayrımı yeterli.

## Fazlar
- ✅ **A — Altyapı:** ParameterStore += GetDecimalAsync/GetIntAsync. `seed_business_params.sql` (idempotent, her şirket) + migrate listesi. DI kayıt.
- ✅ **B — C# tüketiciler:** AI timeout (PurchaseInvoices) + Item yeni-kayıt KDV (`DEFAULT_SALES_TAX_RATE`) + Partner yeni-kayıt vade (`DEFAULT_PAYMENT_TERM_DAYS`). PO/SO satır DTO `=20` initializer kozmetik bırakıldı (SP artık item rate kullanıyor, posting authoritative).
- ✅ **C — SP tüketiciler:** KDV fix (alış→Item.TaxRate+fallback, satış→@SalesDefaultTax, smoke PASS %0→0/0) + mutabakat deadline (`@DeadlineMonths`). **Yaşlandırma kovaları (30/60/90) ATLANDI** — inline TVF'de DECLARE yok, parametrik=16 subquery + 30/60/90 muhasebe standardı; maliyet>değer (gerekçeli, seed'den çıkarıldı).
- ✅ **D — Panel UX:** Admin > Parametreler generic key-value düzenleme zaten var; seed Türkçe açıklama + ModuleCode grupları.

## Done
- Hardcoded iş-ayarı kalmadı (güvenlik/altyapı hariç — gerekçeli).
- Param değiştirince yeni kayıt/SP davranışı değişir (mevcut POSTED belge immutability korunur).
- sql-sp-reviewer + build + smoke (KDV param değişince fatura tutarı doğru).

## Rollback
Param okuma fallback'i sabit varsayılana düşer (kayıt yoksa eski davranış); SP'ler eski liter'e döndürülebilir.
