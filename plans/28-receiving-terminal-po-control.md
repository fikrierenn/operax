# Plan 28 — Sipariş Kontrollü Mal Kabul Terminali (+ fazla→iade alanı)

**Tier 3** · Durum: Faz A+B TAMAM (smoke+sql-sp-reviewer ✅) · Faz C/D kaldı · 2026-06-03

## İLERLEME
- ✅ **Faz A** (şema): ReceivingMode + tedarikçi irsaliye no/tarih + ReturnQty + Bin.IsReturnArea + IADE bin seed.
- ✅ **Faz B** (SP+handler+post): sp_ReceivingTerminalScan 3 mod (smoke: 500+400→800 kabul/100 iade); terminal handler SP+FREE yetki+fazla uyarı; sp_ReceivingPost ReturnQty→iade bin. sql-sp-reviewer: CRITICAL yok, 2 advisory eşzamanlılık notu dokümante edildi.
- ⬜ **Faz B-UI kalan:** terminal mod-seçim girişi (BULK tedarikçi seç / FREE oluşturma); tedarikçi irsaliye no/tarih giriş alanı; terminal ilerleme beklenen=PO'dan.
- ⬜ **Faz C:** toplu kabul → fatura aşaması PO eşleştirme (plan 21 satır-bağ).
- ⬜ **Faz D:** iade faturası (İade modülü bağımlı).

## Problem
Terminal mal kabul **serbest mod**: DRAFT belgeye herhangi barkod okutulup eklenir. Sipariş kontrolü yok:
- Ürün siparişte mi kontrol edilmiyor (yanlış ürün kabul edilir).
- **Bug:** tarama `QtyOriginal` (beklenen) + `QtyBase` (alınan) İKİSİNİ birden artırıyor → beklenen hep alınana eşit, ilerleme sayacı anlamsız.
- Fazla okutma (sipariş 100, gelen 120) hiç işaretlenmiyor.

## Kullanıcı Kararları
- **Fazla okutma:** UYAR + kabul et; aşan miktar **iade alanına** (return-pending) ayrılır → sonra tedarikçiye **iade faturası** kesilir.
- **Serbest (siparişsiz) mod:** kalsın ama **yetki dahilinde** (yetkisiz kullanıcı PO'suz mal kabul yapamaz).
- **Üç mod (kullanıcı eklemesi):**
  1. **Tek sipariş** — bir PO seçilip ona karşı kontrollü kabul.
  2. **Çoklu / toplu (kör) kabul** — tedarikçinin TÜM açık siparişleri seçilip mal toptan okutulur (karışık gelen yükü PO bazında ayıklamak zor). Tarama anında PO ayrımı yapılmaz; ürün+miktar toplanır. **PO satır eşleştirmesi FATURA aşamasında** yapılır (fatura hangi siparişlere ne kadar dağıtacağını belirler).
  3. **Serbest** — siparişsiz (yetkili).

## Scope — 3 Faz

### Faz A — Şema + beklenen/alınan ayrımı (DO-NOW)
- `ReceivingLine` += `ReturnQty DECIMAL(18,6) DEFAULT 0` (iade alanına ayrılan fazla miktar) + `PurchaseOrderLineId` (zaten var mı doğrula; yoksa ekle).
- **Bug fix:** beklenen miktar PO satırından TÜRETİLİR (canlı `PurchaseOrderLine.QtyOrdered`), `QtyOriginal` artık "beklenen" değil = okutulan orijinal birim miktarı. Terminal DTO: `Ordered` (PO'dan), `Received` (QtyBase), `ReturnPending` (ReturnQty).
- İade alanı = `Warehouse.IsReceivingArea` benzeri bir **karantina/iade bin'i** (yeni `IsReturnArea` bin flag veya mevcut KABUL bin'i alt-ayrımı — Faz A'da kolon, fiziksel yerleştirme Faz B).

### Faz B — Terminal: 3 mod (DO-NOW)
Terminal girişi: belge seçimi yerine **mod + bağlam** seçilir.
`ReceivingHeader.ReceivingMode` (`SINGLE_PO` / `BULK_SUPPLIER` / `FREE`) + `PurchaseOrderId` (SINGLE) veya yalnız `PartnerId` (BULK).

**Mod 1 — Tek sipariş (SINGLE_PO):** `OnPostScanAsync`:
1. Barkod→ItemId; **ürün PO'da mı?** Değilse → "Bu ürün siparişte yok".
2. `remaining = QtyOrdered - bu PO için toplam alınan`.
3. `qty<=remaining` → `QtyBase+=qty`; `qty>remaining` → kalan kabul + aşan `ReturnQty`'ye + UYAR "Sipariş aşıldı: X iade alanına".

**Mod 2 — Çoklu/toplu kör kabul (BULK_SUPPLIER):**
- Tedarikçi seçilir; o tedarikçinin tüm açık PO satırları **beklenen havuzu** (ürün bazında toplanır: ürün X için ∑QtyOrdered − ∑alınan).
- Tarama: ürün havuzda mı? Değilse UYAR (tedarikçinin açık siparişinde yok — yine de yetkiyle kabul/iade'ye). Miktar toplanır (PO satırına DAĞITILMAZ — sadece ürün+miktar).
- Havuz toplamı aşılırsa → fazla `ReturnQty` + UYAR.
- **PO eşleştirme YOK bu aşamada.** ReceivingLine.PurchaseOrderLineId NULL kalır; fatura aşamasında doldurulur.

**Mod 3 — Serbest (FREE):** PO-suz; **yetki** (`RoleModuleAccess`/role "ReceivingFree") yoksa → "Siparişsiz mal kabul yetkiniz yok".

- Stok: kabul → depo bin'i; `ReturnQty` → **iade/karantina bin'i** (sp_ReceivingPost; ReturnQty ayrı bin RECEIPT, çift-sayım yok).

### Faz C — Fatura-aşaması PO eşleştirme (BULK için, DO-NOW-after-B)
Toplu kabul (BULK) → ReceivingLine PO satırına bağlı değil. Alış faturası kesilirken:
- `sp_CreatePurchaseInvoiceFromReceiving` / N:1 birleştirme ekranında: kabul edilen ürün miktarları, tedarikçinin **açık PO satırlarına dağıtılır** (öneri: en eski PO önce / FIFO + kullanıcı override).
- Eşleşen miktar `PurchaseInvoiceLine.SourceReceivingLineId` + ilgili `PurchaseOrderLineId` üzerinden bağlanır; PO `QtyReceived` güncellenir.
- Fazla (havuz dışı) → fatura dışı / iade.

### Faz D — Tedarikçiye iade faturası (DEFERRED — İade modülü bağımlı)
- `ReturnQty > 0` → tedarikçiye iade faturası (mali-evrak: SourceInvoiceLineId, DocumentTypeCode=RETURN, fatura no+tarih zorunlu).
- **Bağımlılık:** İade modülü (M-F2.2) henüz YOK. Şimdilik ReturnQty kaydı + "iade bekleyenler" raporu yeterli.

## Faz sonu (A+B)
build-validator → sql-sp-reviewer (sp_ReceivingPost + ReturnQty) → security-reviewer (yetki) → E2E smoke (PO 100 → 120 okut → 100 kabul + 20 iade-pending + uyarı; yanlış ürün → red; yetkisiz serbest → red).

## Mevzuat Denetimi (mali-evrak-mevzuat skill, 2026-06-03)
- **(a) Kör/toplu kabul VUK uygun** — mal kabul iç stok hareketi; resmi belge zorunluluğu yaratmaz. Kritik olan tedarikçi **sevk irsaliyesi** (VUK md.230).
- **(b) ZORUNLU EKLE:** `ReceivingHeader.SupplierWaybillNo` + `SupplierWaybillDate` (gelen tedarikçi irsaliyesi). Toplu kabulde birden çok irsaliye olabilir → satır-bazlı veya çoklu irsaliye referansı. 3-way match için şart.
- **(c) FAZLA MAL AKIŞI DÜZELTİLDİ:** "iade faturası" tek başına yanlış. Doğru:
  - Fazla → **karantina/iade alanı** (henüz fatura yok).
  - Yol 1: **Reddet + geri gönder** → iade İRSALİYESİ (faturasız, mal çıkışı), stok girmemiş sayılır.
  - Yol 2: **Kabul et** → tedarikçi fazlayı faturalarsa → **ALICI (biz) iade faturası** keser (Faz D). Faturalanmadıysa iade faturası YOK.
  - İade faturasını **ALICI keser** (mali-evrak §2) — biz tedarikçiye keseriz, doğru; ama yalnız faturalı fazlada.
- **(d) MovementDate = fiili teslim/irsaliye tarihi** (sistem tarihi değil — B19 gap). sp_ReceivingPost MovementDate'i irsaliye tarihinden almalı.
- **Faz D yeniden:** "iade faturası" → "fazla mal çözümü" (reddet-iade-irsaliyesi VEYA faturalı-fazla-iade-faturası). İkincisi İade modülü (M-F2.2) bağımlı.

## Rakip Analizi (competitor-analyst skill, 2026-06-03)
- **Pazar parite:** Mikro/Logo mal kabul = "Giriş İrsaliyesi" (PO bağı opsiyonel) + serbest "Depo Giriş Fişi". Plan'ın 3 modu pazarla uyumlu.
- **Toplu kabul→fatura = inbound aynası:** Mikro N irsaliye→1 fatura satır-bazlı bağ (§12.9). Operax outbound'da MEVCUT (plan 21, SourceShipmentLineId). Toplu kabul→fatura eşleştirme aynı pattern'i PO/inbound'a uygula → tutarlı + denenmiş.
- **TR gap M03.F1** (faturalı vs faturasız mal kabul, Operax ⚠️): plan kabul=stok / fatura=mali ayrımıyla kapatıyor ✓.
- **🎯 Farklılaşma:** terminal barkod-tarama ile kontrollü + toplu kabul; rakipler masaüstü form ağırlıklı. Saha hızı avantajı.

## VERDİKT (skill denetimi): Tasarım UYGUN — mevzuat fix'leri (b/c/d) işlenince. Toplu kabul→fatura için plan 21 satır-bağ pattern'i yeniden kullan.

## Riskler
- **sp_ReceivingPost değişimi** ledger'a dokunur (StockMovement) → iade-pending ayrı bin, çift-sayım yok (flag-only dersi).
- Beklenen/alınan ayrımı mevcut serbest akışı bozmamalı (PO-suz belgede Ordered=null → eski davranış).
- İade bin'i fiziksel: her depoda iade/karantina bin'i gerekebilir (seed).

## Done (A+B)
- PO-bağlı terminal: yanlış ürün red, beklenen=PO ordered (sabit), ilerleme doğru, fazla→ReturnQty+uyarı.
- PO-suz: yetkili serbest, yetkisiz red.
- ReturnQty>0 belgeler raporlanabilir (iade faturası Faz C'ye hazır).

## Rollback
ReturnQty/PurchaseOrderLineId kolonları kalır (zararsız); terminal scan eski serbest mantığa döndürülebilir.
