# Plan 47 — M1 Modül Tamamlama: Envanter/Stok + Costing

**Durum:** ✅ TAMAMLANDI (2026-06-23) — Faz 1-5 hepsi DoD'a kapandı (commit'ler: 513d6d3·28d1ad6·9e134a8·7002609·e1233b0). M1 envanter/stok+costing DoD karşılandı.
**Roadmap:** Plan 45 §3 sıra — M0 (✅ kapandı) sonrası M1. EXECUTION-FIRST: yeni modül değil, mevcut modülü DoD'a kapat.
**Tarih:** 2026-06-23

---

## 1. Problem

Plan 44 stok-hareket motoru **sertifikalı sağlam** (idempotency, applock/concurrency, oversell invariantı, immutability trigger, reverse flag-only). Ama motorun ÜSTÜNDE DoD boşlukları var (3 paralel audit, kod-kanıtlı):

- **🔴 CRITICAL (D1):** `ItemCost.OnHandQty` snapshot drift — kolon yalnız moving-avg SP'sinin running-qty'si; reverse/transfer/sayım onu güncellemiyor → snapshot ≠ ledger (`tvf_InventoryBalance`). Her iptal/transfer/sayım kalıcı drift. Maliyet hesabı (moving-avg formülünde qty olarak kullanılır) yanlış taban alır.
- **🟠 HIGH (D3):** Lot Status (QUARANTINE/BLOCKED) FEFO'da enforce edilmiyor → bloke stok serbest sevk.
- **🟠 HIGH (D3):** `CostingMethod` parametresi FIFO/STANDARD sunuyor ama `sp_UpdateItemCostMovingAvg` method'a bakmıyor → kullanıcı FIFO seçer, sessizce moving-avg (yanlış sonuç).
- **🟠 HIGH (D3):** Zayiat/fire/hurda stok-düşüren akış yok (VUK zayiat).
- **🟡 D8:** `DynamicBomService` (184 satır, 0 caller) dead.

## 2. Scope (kullanıcı + ana-ajan kararları 2026-06-23)

**DAHİL:**
- **Faz 1 — OnHandQty drift fix (CRITICAL):** "En doğrusu" (kullanıcı) = türetilmiş + **grain düzeltmesi**.
  - **Grain bulgusu (impl-spec):** ItemCost canlıda **per-depo** saklanıyor (WarehouseId SET) ama 2 okuyucu (PO öneri `Details.cshtml.cs:55`, `PriceListBulkService.cs:55`) **WarehouseId filtresiz şirket-genel** okuyor → çoklu-depo ürününde çoklu-satır JOIN belirsizliği. Canlıda bir satır **OnHandQty=-54** (drift manifest). OnHandQty hiçbir okuyucuda YOK — sadece AvgCost okunuyor; OnHandQty yalnız SP'nin moving-avg ağırlığı.
  - **Karar:** ItemCost grain → **şirket-genel (WarehouseId NULL)**. SMB moving-avg standardı + okuyucu beklentisiyle hizalı + depolar-arası transfer şirket-toplamı değiştirmez (**G2 çözülür**).
  - **sp_UpdateItemCostMovingAvg yeniden yaz:** running-qty'yi ledger'dan (`SUM StockMovement.QtyBase WHERE Company+Item, IsCancelled=0`) türet; `@QtyBefore = @QtyNow − (RECEIPT? +@Qty : −@Qty)` ile ağırlıklandır. Concurrency: `sp_getapplock 'itemcost:company:item'` Exclusive (consume applock'tan SONRA → tutarlı sıra). OnHandQty = ledger (bilgi amaçlı, türev). Yalnız 2 caller (Receiving 1678 RECEIPT / Shipping 1791 ISSUE) — @WarehouseId=NULL geçirilir.
  - **migration_47:** per-depo ItemCost satırlarını şirket-genel'e collapse (ağırlıklı AvgCost merge + ledger OnHandQty), per-depo satırları sil. Program.cs migrate listesine kaydet.
  - AvgCost ItemCost'ta kalır (meşru snapshot). View'lar (schema_all:594, BinBalance:15) zaten ledger-türevli — dokunulmaz. Reverse SP'leri costing çağırmaya gerek YOK (OnHandQty türev, okunmuyor; AvgCost moving-avg'de geçmişe-dönük geri-sarılamaz = standart best-effort).
- **Faz 2 — Lot Status enforce:** consume/FEFO cursor `ItemLot.Status NOT IN (QUARANTINE, BLOCKED)` filtresi. Bloke lot sevk edilemez (THROW veya atla). (Ben karar: ucuz correctness.)
- **Faz 3 — Costing method kilidi:** UI'dan FIFO/STANDARD seçeneğini kaldır, `CostingMethod` MOVING_AVG sabit. Sessiz-yanlış-sonuç kapanır. (Kullanıcı "ikisi de olabilir" → güvenli correctness.)
- **Faz 4 — Zayiat (scrap):** MaterialIssue'ya reason-code (HASAR/FIRE/HURDA). Stok-düşüren hareket + ledger. (Ben karar: en dar basamak, yeni evrak yok.)
- **Faz 5 — Dead code temizliği:** DynamicBomService + (caller'sız doğrulanan) production WIP servisleri sil.

**HARİÇ (M1 dışı, gerekçeli — sonraki plan):**
- **Serial tam lifecycle** (StockMovement.SerialNo + ItemSerial IN_STOCK→SHIPPED→SCRAPPED): büyük, ürünün serial-takipli olduğu kanıtı yok → tablolar WIP işaretle. (Kullanıcı "sen karar ver".)
- **FIFO/STANDARD costing motoru:** ayrı plan (Faz 3 kilidi sessiz-bug'ı kapatır; gerçek FIFO ihtiyaç olunca).
- **`sp_GuardStockFrozen` freeze implementasyonu:** medium; sayım-bütünlüğü için ayrı iş.
- **In-transit transfer statüsü:** çok-şube; tek-site WMS'de düşük.

## 3. Alternatifler (reddedilen)

- **Drift: simetrik güncelle** (8 SP'de OnHandQty güncelle) — reddedildi: snapshot kaçak riski sürer, "kolaya kaçma" (kullanıcı). Türetilmiş kökten çözer.
- **Costing: FIFO motorunu şimdi yaz** — reddedildi: büyük, M1'i şişirir; kilit sessiz-bug'ı yeterince kapatır.
- **Zayiat: ayrı belge** — reddedildi: footprint-ladder, MaterialIssue genişletme daha dar.

## 4. Riskler

| Risk | Etki | Mitigasyon |
|---|---|---|
| OnHandQty kolonuna gizli bağımlı okuyucu kalır | yüksek | impl-spec öncesi tam grep (C#+SQL+rapor); View'lar zaten türevli doğrulandı |
| Moving-avg tvf-okuma performansı (her giriş hareketi tvf çağırır) | orta | tvf SARGable + IX_StockMovement index zaten var (MASTER_ROADMAP perf kuralı); exec plan doğrula |
| Lot Status filtresi mevcut sevkleri kırar (tüm lotlar NULL status ise) | orta | NULL/ACTIVE serbest; yalnız QUARANTINE/BLOCKED bloklanır; smoke |
| Dead servis silme gizli referans kırar | düşük | grep 0 caller doğrulandı (audit); before-major-change |

## 5. Done Criteria (M1 DoD)
- [x] Faz 1 ✅ (513d6d3): OnHandQty ledger-türev + şirket-genel grain; migration drift SIFIR; SP test AvgCost/qty doğru; sql-sp-reviewer geçti (CRIT-1/IMP-1 fix)
- [x] Faz 2 ✅: QUARANTINE/BLOCKED lot consume'da bloklanır. sp_ConsumeInventory guard (THROW 53004) + FEFO ön-kontrol/cursor bloke lotu serbest stoktan dışlar. sql-sp-reviewer 6/6 geçti. Smoke (tran-rollback): bloke lot consume → 53004 yakalandı. NOT: IMP-1 nadir TOCTOU (consume sürerken karantinaya çekme → temiz rollback, veri kaybı yok) — lot-statü-değiştirme SP'si yazılınca aynı applock alınmalı.
- [x] Faz 3 ✅: CostingMethod MOVING_AVG kilit. Bulgu: FIFO/STANDARD UI dropdown'u YOK + param/Item.CostingMethod hiçbir kod tarafından okunmuyor (sessiz-yanlış-sonuç teorik). migration_48: yanıltıcı Description düzelt + MOVING_AVG dışı değer reset. FIFO motoru ayrı gelecek plan.
- [x] Faz 4 ✅ (e1233b0): MaterialIssue.ReasonCode (NULL=tüketim · DAMAGE/FIRE/WASTE=zayiat) → sp_MaterialIssuePost StockMovement.MovementType='SCRAP'; reverse değişmedi (CONSUMPTION eşler). migration_49 (kolon+CHECK idempotent). Smoke: DAMAGE→SCRAP/-2 · normal→ISSUE · scrap+reverse→SCRAP/Cancelled. sql-sp-reviewer temiz; code-reviewer HIGH (kod-dili HASAR/HURDA→DAMAGE/WASTE) fix.
- [x] Faz 5 ✅ (7002609): DynamicBomService + OperaxDbContext + AutoTraceabilityService + PrintServer/Class1 git rm; ProductionReceipt/Activity zaten yoktu. Web/Cli build 0/0.
- [x] HARİÇ kararlar (serial/FIFO/freeze/in-transit) dokümante (§2 HARİÇ + journal)
- [x] Her faz: build → code/sql-sp reviewer → E2E smoke (phase-review-gate uygulandı her fazda)
- [ ] Plan arşive + journal

## 6. Faz sırası / bağımlılık
1. **Faz 1 (drift)** önce — en kritik, diğerlerinden bağımsız, costing tabanını düzeltir.
2. **Faz 3 (costing kilit)** — küçük, Faz 1 ile aynı costing alanı, birlikte mantıklı.
3. **Faz 2 (lot status)** — bağımsız, consume katmanı.
4. **Faz 4 (zayiat)** — yeni MovementType + reason, bağımsız.
5. **Faz 5 (dead code)** — en son, hijyen.

## 7. 5 Lens
- 🔴 **Contrarian:** Fatal flaw? OnHandQty'yi türetince moving-avg her giriş hareketinde tvf çağırır — perf. Ama index var, hareket başına 1 SARGable SUM kabul edilebilir; alternatif drift'tir (kabul edilemez).
- 🔵 **First Principles:** Doğru soru "snapshot mı türev mi?" — tek doğru kaynak ledger; snapshot her zaman drift kaynağı. Türev doğru cevap.
- 🟢 **Expansionist:** Daha büyük fırsat? Serial/FIFO/freeze birlikte tam WMS olur — ama scope patlar; M1 correctness'e odak, gerisi sıradaki modüller.
- ⚪ **Outsider:** Yabancı ne garip bulur? "OnHandQty hem view'da (türev) hem kolonda (snapshot) — iki kaynak" → kafa karıştırıcı, tekilleştir.
- 🟡 **Executor:** Pazartesi? Faz 1 impl-spec: sp_UpdateItemCostMovingAvg + ItemCost.OnHandQty okuyucularını grep'le, tvf'ye repoint.

## 8. İlişkili
- `plans/archive/44-stock-consume-primitive.md` (stok motoru çekirdeği)
- `plans/45-module-completion-roadmap.md` (M1 sırası + DoD D1-D8)
- `.claude/rules/phase-review-gate.md` · `.claude/rules/document-immutability.md` (ledger immutability)
- `docs/sql/schema_M02_Costing.sql` (OnHandQty kolonu) · `db_objects_starter.sql` (moving-avg SP) · `db_objects_consume.sql` (FEFO/lot)
