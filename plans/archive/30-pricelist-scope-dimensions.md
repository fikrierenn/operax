# Plan 30 — PriceList Kapsam Boyutları (şube/müşteri/genel + tarih + aktiflik)

**Tier 3** · Durum: ✅ TAMAMLANDI — Faz A–E hepsi bitti (build+sql-sp-reviewer+code-reviewer+security-reviewer+browser smoke geçti) · 2026-06-03

## ✅ FAZ E (CRUD UI) — TAMAMLANDI (2026-06-03)
- `Features/MasterData/PriceLists/Index.cshtml(.cs)` — liste + KPI(satış/alış) + filtre(yön/aktiflik/arama) + soft-delete.
- `Features/MasterData/PriceLists/Details.cshtml(.cs)` — New+Edit tek sayfa; başlık (Code/Name/Direction/Cari/Şube/Priority/Currency/Valid*/IsActive) + satır grid (ürün/brüt/MinQty/LineType/zincir/net) + "10+5+3" iskonto kısayolu → child kademe + ürün fiyat öneri JS.
- Sidebar: Ana Veri → "Fiyat Listeleri". Dtos.cs: PriceDirection + PriceLineType sabitleri.
- **Browser smoke (gerçek login):** Liste render ✅, Yeni→kaydet ✅, satır ekle 10+5+3 → **net 82,94** ✅, console hatasız. Smoke verisi temizlendi.
- **Review:** security-reviewer temiz (IDOR/mass-assign/SQLi/CSRF/XSS yok). code-reviewer: utility-salad (sistemik, tüm ekranlarda — tutarlılık için bırakıldı); tarih formatı dd MMM yyyy'e düzeltildi; PriceListLine CompanyId bulgusu YANLIŞ POZİTİF (kolon yok, parent üzerinden bağlı).

## 🔨 IMPLEMENT İLERLEME (2026-06-03)
- **Faz A ✅** `schema_M02_Costing.sql`: PriceList += BranchId(FK)/Priority/CreatedAt-By/UpdatedAt/IsDeleted + Direction NOT NULL+CHECK backfill; PriceListLine += LineType(CK)/IsDeleted; yeni `PriceListLineDiscount`(FK+CK Pct 0-100+UQ LineId,Seq). Canlı VT'de doğrulandı.
- **Faz B ✅** `db_objects_starter.sql` `tvf_PriceListEffective(@CompanyId)` — ROW_NUMBER dense-rank + recursive CTE zincir (DECIMAL(28,12), boşluk/offset-toleranslı anchor), BasePrice/NetMultiplier/EffectivePrice/TotalDiscountAmount AYRI. Smoke: 10+5+3 → **82,9350** (Seq 1,2,3 ve boşluklu 5,10,15 ikisi de).
- **Faz C ✅** `sp_CheckPriceVariance` += @BranchId; TOP 1 ORDER BY MatchScore(Partner×2+Branch×1) DESC, Priority, MinQty, ValidFrom, LineId. NET efektif kıyas. Smoke: cari(95,skor2) şubeyi(90,skor1) ezdi; cari yokken şube(90) kazandı.
- **Faz D ✅** `PurchaseOrders/Details.CheckPriceVarianceAsync` → PO Warehouse'tan @BranchId türetip geçiyor. Satış/fatura: aktif resolver caller yok, tvf tek-kaynak hazır (capability).
- **Review düzeltmeleri:** CRIT-1 (Seq=1 hardcoded anchor → dense-rank) + IMP-1 (Direction NULL backfill+NOT NULL+CHECK) + code HIGH (PriceCheckCtx.PartnerId non-nullable, gereksiz guard kaldırıldı) uygulandı.
- **DEBT (kapsam dışı, pre-existing):** IMP-2 — sp_CheckPriceVariance MinQty kademesini belge miktarına göre filtrelemiyor (@OrderQty yok); en yüksek MinQty'li kademe seçilebilir → ulaşılmamış toplu-fiyat. Eski SP'de de vardı; toplu-fiyat devreye alınınca @OrderQty eklenmeli. → `docs/TODO.md`.
- **Karar:** Satış fiyat önceliği = Alış (CARİ BASKIN, Partner×2+Branch×1) — kullanıcı onayı 2026-06-03.

## 🎯 NİHAİ KARAR — CARİ BASKIN (kullanıcı: "cari özelinde varsa cari olmalı")
- **Skor: `Partner×2 + Branch×1`** → cari-özel (T-P, skor 2) şube-özel'i (T-B, skor 1) EZER.
- Katman sırası: **T-PB(3) > T-P(2) > T-B(1) > T-G(0)**. Cari-özel fiyat varsa şube fiyatına bakılmaz.
- Para hesabı: **DECIMAL** (float YASAK — kuruş kayması). Zincir iskonto: recursive CTE (tam kesin) tercih; log-exp basit alt (ROUND'lu).

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

## ✅ KARAR VERİLDİ — Cari baskın (yukarı bkz). Açık karar kalmadı.
- (Opsiyonel ileride) D365-tarzı "en düşük fiyat" politika anahtarı — şimdilik gerek yok.

## İMPLEMENT FAZLARI (onaylı — sırayla, her faz review-gate)
- **A — Şema:** `PriceList += BranchId, Priority INT DEFAULT 0, CreatedAt/By, UpdatedAt/By` (Direction/PartnerId/Valid* zaten var) + yeni `PriceListLineDiscount(Id, LineId, Seq, Pct DECIMAL, FK)` + `PriceListLine += LineType ('FIXED'/'DISCOUNT')`.
- **B — iTVF:** `tvf_PriceListEffective(@CompanyId)` → liste×ürün satırı + boyutlar + zincir net çarpan (recursive CTE) + EffectivePrice (DECIMAL, ROUND).
- **C — Resolver:** `sp_ResolveSalesPrice` / alış karşılığı → TOP 1 `ORDER BY MatchScore DESC, Priority DESC, MinQty DESC, ValidFrom DESC, Id ASC`. MatchScore = Partner×2+Branch×1.
- **D — Wire:** SO/PO/fatura satır fiyatı + PriceVariance resolver'dan beslenir (tek kaynak).
- **E — UI:** PriceList CRUD ekranı (yok!) — liste başlık (Direction/Cari/Şube/Priority/tarih) + satır (ürün/fiyat/LineType) + iskonto "10+5+3" kısayol → child satır.
- Faz sonu: build + sql-sp-reviewer + smoke (cari-özel şube-özeli ezer; 10+5+3 → 82,935 doğrula).

## ⚖️ MEVZUAT DOĞRULAMASI (mali-evrak-mevzuat, 2026-06-02)
- **(a) KDV matrahı [DOC — KDVK md.25/a]:** iskonto sonrası NET üzerinden KDV. Şart: iskonto faturada gösterilmeli.
- **(c) Gösterim [DOC/YORUM]:** iskonto faturada AÇIKÇA görünmeli (brüt + iskonto + net). Efektifi tek satır yazıp iskontoyu gizlemek matrahı brüt yapar → YANLIŞ. ⇒ **iTVF `EffectivePrice` (net) + `BasePrice` (brüt) + `TotalDiscountAmount` AYRI dönmeli** (fatura gösterimi).
- **(e) Variance [YORUM]:** net-efektif fiyat ↔ PO net fiyat kıyası. Brüt-net karıştırma = yanlış variance.
- **(b/d) UBL-TR:** satır `AllowanceCharge` (ChargeIndicator=false). Zincir → tek net iskonto tutarına indirgenip gösterilir (yaygın). **DOĞRULANMADI:** çoklu vs tek AllowanceCharge — e-Belge faz öncesi GİB kılavuz teyidi.

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
