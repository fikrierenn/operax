# Plan 30 — PriceList Kapsam Boyutları (şube/müşteri/genel + tarih + aktiflik)

**Tier 3** · Durum: ⏸️ PARK — tasarım kararları bekliyor (kullanıcı: "ezme konusunu sonra netleştirelim") · 2026-06-02

## 📚 LİTERATÜR + RAKİP TARAMASI (2026-06-02 — reference-researcher + competitor-analyst)
- **SAP** access-sequence (en spesifik→genel, ilk-bulunan, Exclusive durdurur). **Odoo** en spesifik; eşitse en yüksek fiyat. **ERPNext** açık sayısal `priority` (kullanıcı) + eşitse THROW conflict. **D365** politika anahtarı (en düşük fiyat / rank). **NetSuite** sabit hiyerarşi (müşteri-özel>grup>kart>baz).
- **TR rakip [NOT]:** Müşteri-bazlı fiyat hepsi ✅ (Netsis güçlü). Kademeli iskonto + kampanya hepsi ✅, Operax ❌. **Mikro `ft_iskonto1..6` = 6 ZİNCİR iskonto slotu** (denormalize) → TR'de zincir standart.
- **TR PRATİĞİ (kullanıcı):** iskonto `10+5+3` gibi **ZİNCİRLEME (ardışık)** — toplanmaz: 100→90→85,5→82,935 (net ≈%17,065). 
- **Operax mevcut:** sp_ResolveSalesPrice zaten SAP-tarzı 4 katman sıralı-SELECT (M04 spec). Eksik: Branch boyutu + **eşitlik tie-break tanımsız** (planı bloke eden asıl şey).

## ✅ KARARLAR (bu oturum netleşti)
1. **Model = ERPNext deseni:** `PriceList += Priority INT` (kullanıcı niyeti açık) + spesifiklik skoru.
2. **Deterministik tie-break zinciri** (eşitlik matematiksel İMKANSIZ → THROW gereksiz): `MatchScore DESC → Priority DESC → MinQty DESC → ValidFrom DESC → Id ASC`.
3. **İskonto saklama = C (child tablo)** `PriceListLineDiscount(LineId, Seq, Pct)` — normalize, sıralı, sınırsız kademe. UI `"10+5+3"` kısayolu kabul → kayıtta child satıra açılır. (Denormalize Mikro-slot + string-parse REDDEDİLDİ.)
4. **Zincir hesap set-based:** `NetMultiplier = EXP(SUM(LOG(1 - Pct/100.0)))`, `Effective = Base × NetMultiplier`. Precision: sonda ROUND; Pct=100 guard (LOG(0)). Tam kesinlik şartsa recursive CTE alternatifi.
5. **İskonto bazı = kazanan listenin kendi BasePrice'ı** (Item baz fiyat yalnız hiç liste yoksa fallback).
6. **Mimari = iTVF tek doğruluk kaynağı:** `tvf_PriceListEffective(@CompanyId)` → boyutlar + zincir-iskonto + EffectivePrice. Resolver = TOP 1 (tie-break sıralı). Tüm çağıranlar (fatura/sipariş/variance) aynı TVF.
7. **Stacking YOK** ama **zincir VAR** — fark: tek kazanan liste seçilir (stacking değil), o listenin iskonto ZİNCİRİ uygulanır.

## ⏸️ KALAN TEK KARAR (kullanıcı onayı)
- **Cari mı Şube mi baskın** (T-P vs T-B): araştırma `Partner×2 + Branch×1` (CARİ baskın — TR B2B pazarlık fiyatı bağlayıcı) öneriyor. Plan taslağındaki `Branch×2+Branch×1` şube-baskındı → ters. **Tek satırlık skor formülü = tüm kararı belirler.** Detay: `docs/reference/PriceList_Override_Senaryolar.xlsx`.
- (Opsiyonel ileride) D365-tarzı "en düşük fiyat" politika anahtarı — şimdilik gerek yok.

## NOT
Şema ALTER (BranchId + audit) bu oturumda yazılıp **geri alındı** — Priority + child iskonto tablosu + iTVF ile BİRLİKTE tek seferde yazılacak (parçalı şema asılmasın). Aşağıdaki orijinal Faz A bu kararlarla revize edilecek.

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
