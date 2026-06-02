# Plan 30 — PriceList Kapsam Boyutları (şube/müşteri/genel + tarih + aktiflik)

**Tier 3** · Durum: UYGULANIYOR · 2026-06-02

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
