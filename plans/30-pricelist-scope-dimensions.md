# Plan 30 — PriceList Kapsam Boyutları (şube/müşteri/genel + tarih + aktiflik)

**Tier 3** · Durum: ⏸️ PARK — tasarım kararları bekliyor (kullanıcı: "ezme konusunu sonra netleştirelim") · 2026-06-02

## ⏸️ PARK GEREKÇESİ + AÇIK KARARLAR (implement etmeden önce netleşmeli)
1. **Override önceliği (cari mı şube mi baskın):** çakışmada hangi fiyat ezer kesinleşmedi. Deterministik 4 katman + tie-breaker (aktif + tarih aralığı + en güncel ValidFrom + MinQty kademesi) yazılacak — ama Cari>Şube mi Şube>Cari mi KARAR yok.
2. **🆕 Sabit fiyat listesi + İSKONTO girişi (kullanıcı):** PriceList yalnız "birim fiyat" değil; **sabit fiyat** VEYA **iskonto (% / tutar)** girişi de olabilir. Bu şemayı etkiler: `PriceListLine` += satır tipi (FIXED_PRICE / DISCOUNT_PCT / DISCOUNT_AMT?) + iskonto alanları. Liste seviyesinde de tip olabilir (komple iskonto listesi). 
   - Soru: iskonto neyin üzerine? (liste fiyatı / Item.SalesPrice / Item.PurchasePrice baz). Zincirleme iskonto mu (kademeli)?
   - Bu netleşmeden BranchId + audit eklemek yeterli ama EKSİK kalır → tüm boyut tasarımı birlikte yapılmalı.
3. Şema ALTER (BranchId + audit) bu oturumda dosyaya yazılıp **geri alındı** (revert) — park kararı sonrası yarım şema asılı kalmasın diye. Aşağıdaki Faz A geçerli ama iskonto kararıyla birlikte revize edilecek.

---
**Tier 3** · (orijinal taslak — park) · 2026-06-02

## Problem
Kullanıcı: PriceList'in giriş tarihi, aktiflik, geçerlilik (başlangıç/son) tarihi, **şube bazlı**, müşteri bazlı, genel kapsam özellikleri olmalı. Mevcut: `IsActive`, `ValidFrom`, `ValidTo`, `PartnerId`(NULL=genel), `Direction`(SALES/PURCHASE) VAR. **Eksik:** `BranchId` (şube bazlı) + giriş tarihi audit (`CreatedAt/By`).

## Kapsam matrisi (özellik durumu)
| Özellik | Kolon | Durum |
|---|---|---|
| Aktiflik | `IsActive` | ✅ var |
| Geçerlilik başlangıç | `ValidFrom` | ✅ var |
| Son geçerlilik | `ValidTo` | ✅ var |
| Müşteri/tedarikçi bazlı | `PartnerId` (NULL=genel) | ✅ var |
| Yön (alış/satış) | `Direction` | ✅ var |
| **Şube bazlı** | `BranchId` (NULL=tüm şubeler) | ➕ EKLE |
| **Giriş tarihi + kim** | `CreatedAt/By`, `UpdatedAt/By` | ➕ EKLE |

## Lookup önceliği (en özel → genel)
İki boyut (Branch, Partner). Belge bağlamı: Branch = `Warehouse.BranchId` (mevcut fn_DefaultBranchId fallback), Partner = cari.
Öncelik skoru: `Branch eşleşme(2) + Partner eşleşme(1)` DESC, sonra `MinQty DESC`.
1. Şube+Cari tam · 2. Cari (tüm şube) · 3. Şube (genel cari) · 4. Tam genel.
WHERE: `(BranchId=@b OR BranchId IS NULL) AND (PartnerId=@p OR PartnerId IS NULL)`.

## Fazlar
- **A — Şema:** `schema_M02_Costing.sql` PriceList ALTER += BranchId + CreatedAt/By + UpdatedAt/By (idempotent).
- **B — Lookup:** `sp_CheckPriceVariance` (+@BranchId param, opsiyonel) + `sp_PurchaseInvoicePost` PriceList CROSS APPLY → branch-aware öncelik. Branch belgeden (Warehouse) türetilir.
- **C — Caller:** `PurchaseOrders/Details.CheckPriceVarianceAsync` → PO warehouse'tan @BranchId geçir.

## Notlar
- PriceList CRUD UI YOK (SQL/seed yönetiliyor) → UI kapsam dışı; şema + lookup yeterli.
- TOLERANS YOK ilkesi korunur (Plan 27): her sapma variance.
- Satış fiyatlandırması ileride aynı branch-aware lookup'ı kullanır (capability hazır).

## Done
- PriceList şube bazlı + tam audit; lookup en-özel-önce; build + sql-sp-reviewer + smoke (şube-özel fiyat genel fiyatı ezer).
