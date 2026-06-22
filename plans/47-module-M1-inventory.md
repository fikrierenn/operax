# Plan 47 — M1 Modül Tamamlama: Envanter/Stok + Costing

**Durum:** Faz 0 (audit) ✅ · uygulama onayı bekliyor
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
- **Faz 1 — OnHandQty drift fix (CRITICAL):** "En doğrusu" (kullanıcı) = türetilmiş. `sp_UpdateItemCostMovingAvg` running-qty'yi `ItemCost.OnHandQty` yerine `tvf_InventoryBalance`'tan (ledger SUM, IsCancelled=0) okur. `ItemCost.OnHandQty` artık otoriter değil — kolon ya bırakılır (recompute/informational) ya da kaldırılır. AvgCost ItemCost'ta kalır (meşru moving-avg snapshot'ı). Okuyucular: View'lar (schema_all:594, BinBalance:15) zaten ledger-türevli — dokunmaya gerek yok; yalnız ItemCost.OnHandQty kolonuna bağımlı kod repoint edilir.
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
- [ ] Faz 1: OnHandQty türetilmiş; iptal/transfer/sayım sonrası `ItemCost`-türevi = `tvf_InventoryBalance` (drift 0, E2E smoke ile kanıt)
- [ ] Faz 2: QUARANTINE/BLOCKED lot consume'da bloklanır (smoke)
- [ ] Faz 3: FIFO/STANDARD UI'dan kalktı, MOVING_AVG sabit
- [ ] Faz 4: Zayiat çıkışı MaterialIssue reason-code ile ledger'a düşüyor + reverse
- [ ] Faz 5: DynamicBomService + dead production servisleri silindi, build 0/0
- [ ] HARİÇ kararlar (serial/FIFO/freeze/in-transit) dokümante + TODO
- [ ] Her faz: build → code/sql-sp/security reviewer → E2E smoke (phase-review-gate)
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
