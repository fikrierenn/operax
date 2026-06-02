# Plan 28 — Sipariş Kontrollü Mal Kabul Terminali (+ fazla→iade alanı)

**Tier 3** · Durum: TASLAK (onay bekliyor) · 2026-06-03

## Problem
Terminal mal kabul **serbest mod**: DRAFT belgeye herhangi barkod okutulup eklenir. Sipariş kontrolü yok:
- Ürün siparişte mi kontrol edilmiyor (yanlış ürün kabul edilir).
- **Bug:** tarama `QtyOriginal` (beklenen) + `QtyBase` (alınan) İKİSİNİ birden artırıyor → beklenen hep alınana eşit, ilerleme sayacı anlamsız.
- Fazla okutma (sipariş 100, gelen 120) hiç işaretlenmiyor.

## Kullanıcı Kararları
- **Fazla okutma:** UYAR + kabul et; aşan miktar **iade alanına** (return-pending) ayrılır → sonra tedarikçiye **iade faturası** kesilir.
- **Serbest (siparişsiz) mod:** kalsın ama **yetki dahilinde** (yetkisiz kullanıcı PO'suz mal kabul yapamaz).

## Scope — 3 Faz

### Faz A — Şema + beklenen/alınan ayrımı (DO-NOW)
- `ReceivingLine` += `ReturnQty DECIMAL(18,6) DEFAULT 0` (iade alanına ayrılan fazla miktar) + `PurchaseOrderLineId` (zaten var mı doğrula; yoksa ekle).
- **Bug fix:** beklenen miktar PO satırından TÜRETİLİR (canlı `PurchaseOrderLine.QtyOrdered`), `QtyOriginal` artık "beklenen" değil = okutulan orijinal birim miktarı. Terminal DTO: `Ordered` (PO'dan), `Received` (QtyBase), `ReturnPending` (ReturnQty).
- İade alanı = `Warehouse.IsReceivingArea` benzeri bir **karantina/iade bin'i** (yeni `IsReturnArea` bin flag veya mevcut KABUL bin'i alt-ayrımı — Faz A'da kolon, fiziksel yerleştirme Faz B).

### Faz B — Terminal sipariş kontrollü tarama (DO-NOW)
`OnPostScanAsync` PO-bağlı belgede (`ReceivingHeader.PurchaseOrderId` set):
1. Barkod→ItemId; **ürün PO'da mı?** (`PurchaseOrderLine WHERE HeaderId=@PoId AND ItemId`). Değilse → THROW/Error "Bu ürün siparişte yok".
2. `remaining = QtyOrdered - (bu PO için toplam alınan)`. 
3. `qty <= remaining` → `QtyBase += qty`.
4. `qty > remaining` → `QtyBase += remaining`; `ReturnQty += (qty - remaining)`; **UYAR**: "Sipariş aşıldı: X adet iade alanına ayrıldı". (terminal-scan error/warn feedback)
5. PO-suz belge → serbest (mevcut), AMA **yetki**: `user.HasRole(...)` veya `RoleModuleAccess` "ReceivingFree" yoksa → "Siparişsiz mal kabul yetkiniz yok".
- Stok hareketi: kabul edilen depo bin'ine; iade-pending miktar **iade/karantina bin'ine** (sp_ReceivingPost güncellenir — ReturnQty ayrı bin'e RECEIPT veya ayrı işaret).

### Faz C — Tedarikçiye iade faturası (DEFERRED — İade modülü bağımlı)
- `ReturnQty > 0` mal kabuller → tedarikçiye iade faturası (mali-evrak: SourceInvoiceLineId, DocumentTypeCode=RETURN, fatura no+tarih zorunlu).
- **Bağımlılık:** İade modülü (M-F2.2) henüz YOK. Bu faz İade modülü kurulunca. Şimdilik ReturnQty kaydı + raporu yeterli (iade bekleyenler listesi).

## Faz sonu (A+B)
build-validator → sql-sp-reviewer (sp_ReceivingPost + ReturnQty) → security-reviewer (yetki) → E2E smoke (PO 100 → 120 okut → 100 kabul + 20 iade-pending + uyarı; yanlış ürün → red; yetkisiz serbest → red).

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
