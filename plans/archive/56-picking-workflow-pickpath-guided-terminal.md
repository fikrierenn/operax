# Plan 56 — Picking Workflow: Pick-Path Optimize + Guided Terminal + Shipping Staging

**Tarih:** 2026-06-24
**Yazan:** Fikri / Claude
**Durum:** `Tamamlandı` (2026-06-24, A-D hepsi · ledger sınırı korundu)
**Modül:** M-WMS (Picking) + M04 (Shipping ara-statü)
**Paket:** WMS_PRO

---

## 1. Problem

Toplama (picking) akışı temelde var ama operatör verimli toplayamıyor ve topla→sevk görünürlüğü yok:

1. **Raf-sırası yok:** Pick kalemleri `ORDER BY l.Id` (giriş sırası) — operatör depoda zikzak yürür. `Bin.Zone`+`SortNo` kolonları VAR ama (a) sorguda kullanılmıyor, (b) `SortNo` boş (0).
2. **Çoklu-adres allocation yok:** Bir ürün birden çok bin'de olabilir; `sp_ShippingCreatePickTask` `TOP 1` FIFO bin alıyor — bir bin yetmezse kalan adreslenmiyor.
3. **Terminal doğrulama eksik:** El-terminali tek-kalem (iyi) ama yalnız ÜRÜN barkodu tarıyor — **lokasyon (raf) doğrulanmıyor** (yanlış raftan toplama yakalanmaz), **miktar adımı yok** (short-pick imkânsız), istisna (raf boş/atla/hasarlı) yok.
4. **RELEASED statü yok:** DRAFT→ASSIGNED atlıyor; "havuza bırakıldı, atanmamış" (operatör pull) modellenemiyor.
5. **Topla→Sevk staging görünmez:** Pick COMPLETED Shipping'i etkilemiyor; "toplanıyor/toplandı, sevke hazır" durumu yok.

Kullanıcı net: "siparişleri toplama emirlerine dönüştür + en performanslı algoritmaya göre (ürün çok-adresli) sıralayıp raflarda gezdir."

## 2. Scope

### Kapsam dahili
- **Pick-path serpentine sıralama:** pick sorgusu `ORDER BY Bin.Zone, Bin.SortNo`; `Bin.SortNo` doldurma (Code'dan türet).
- **Multi-bin allocation:** ürün birden çok bin'de → `sp_ShippingCreatePickTask` kalanı sıradaki bin'e böler (her bin = ayrı PickTaskLine), FIFO + miktar.
- **`PickTaskLine.PickSeq`** — task açılışında serpentine sıra dondurulur (`ROW_NUMBER() OVER (ORDER BY Zone, SortNo)`).
- **RELEASED statü** (DRAFT→RELEASED→ASSIGNED→IN_PROGRESS→COMPLETED→CANCELLED) + StatusTransition seed.
- **Guided multi-step terminal:** Adım1 raf-doğrula (bin barkod/check-digit) → Adım2 ürün-doğrula → Adım3 miktar (short-pick) + istisna menüsü (raf-boş/atla/hasarlı) + aktif-ekran "Kalem 3/10" sayacı.
- **`sp_PickConfirm` genişletme:** `@ActualQty` + `@ExceptionType` (NULL/SHORT/SKIP/DAMAGED); short-pick satırı AÇIK kalır.
- **Shipping ara-statü:** `DRAFT→PICKING→PICKED→POSTED`. PickTask RELEASED→Shipping PICKING; COMPLETED→PICKED (staging); kullanıcı manuel POSTED (stok burada çıkar — değişmez).

### Kapsam dışı
- ❌ **Wave/Cluster/Batch** (çoklu sipariş tek tur) — danışman + kullanıcı: ertele, ihtiyaç kanıtlanınca ayrı plan.
- ❌ **Tam VRP/TSP route optimizasyonu** — serpentine (Zone+SortNo) endüstri-standart "yeterli iyi" (%15-35 kazanç); AI-route over-engineering.
- ❌ **Voice-directed picking** (TTS/STT donanım).
- ❌ **Lot/SKT FEFO** — şimdilik FIFO; lot-tracking modülü gelince FEFO.
- ❌ **SalesOrder→PickTask doğrudan** — pick task Shipping'e ait (danışman: SO'ya bağlamak yanlış, stok 2× riski).

### Etkilenen dosyalar
- `docs/sql/schema_*` — `PickTaskLine.PickSeq` + `PickTaskLine.ExceptionNote` + `ShippingHeader` status genişleme; `Bin.SortNo` backfill
- `docs/sql/db_objects*.sql` — `sp_ShippingCreatePickTask` (multi-bin + serpentine), `sp_PickConfirm` (qty+exception), pick okuma sorguları, StatusTransition seed
- `src/Operax.Web/Features/Picking/Terminal.cshtml(.cs)` — guided multi-step
- `src/Operax.Web/Features/Picking/Index/Details` — RELEASED statü görünüm
- `src/Operax.Web/Features/Shipping/*` — ara-statü görünüm + geçiş
- `src/Operax.Web/Lib/Dtos.cs` — PickTaskStatus.Released + Shipping statü sabitleri

**Tahmini boyut:** ~12 dosya / orta-büyük. Faz faz.

## 3. Alternatifler (pick-path modeli — asıl karar)

### A: Naive `ORDER BY SortNo` (tek bin/ürün)
**Açıklama:** Sadece sorguya ORDER BY ekle, çoklu-adres yok.
**Reddetme sebebi:** Kullanıcı "ürün çok-adresli, optimize et" dedi; tek-bin allocation yetmez (bir bin yetmezse kalan toplanamaz).

### B: Tam VRP/TSP route engine
**Açıklama:** Gerçek en-kısa-yol optimizasyonu (koridor grafiği + TSP).
**Reddetme sebebi:** Over-engineering (footprint-ladder); orta-ölçek single-tenant'ta serpentine %15-35 kazancın çoğunu zaten verir; bakım + hesap maliyeti yüksek.

### C: Serpentine (Zone+SortNo) + multi-bin allocation — SEÇİLEN
**Açıklama:** Allocation ürünün birden çok bin'ini FIFO+miktar ile böler (her bin=PickTaskLine); satırlar `Zone, SortNo` serpentine sırasına dizilir, `PickSeq` ile dondurulur. `SortNo` Bin.Code'dan türetilir (yapısal kod: koğuş-raf-göz).
**Sebep:** Kullanıcının "optimize + çok-adres" ihtiyacını karşılar, endüstri-standart (SAP EWM/serpentine), footprint-ladder uyumlu (mevcut Zone/SortNo kolonları). İleride VRP'ye yükseltme kapısı açık (PickSeq üretimini değiştir).

**5 lens kontrolü:**
- 🔴 **Contrarian:** Fatal flaw = `Bin.SortNo` yanlış türetilirse sıra anlamsız → mitigation: Code-parse kuralı + Locations'ta elle ezme + smoke (gerçek depo sırası).
- 🔵 **First Principles:** Doğru soru "operatör en az yürüsün" → serpentine bunu çözer; "mükemmel rota" değil "iyi rota" yeterli.
- 🟢 **Expansionist:** İleride wave/zone eklenebilir — PickSeq + Zone tasarımı o kapıyı kapatmıyor.
- ⚪ **Outsider:** "Neden ürün tek yerde değil" → çok-adres gerçek depo gerçeği; allocation bunu modellemeli.
- 🟡 **Executor:** Pazartesi = Faz A (ORDER BY + SortNo backfill + RELEASED), anında "sıra ile gezdir" değeri.

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| Shipping ara-statü çift-düşüm fix'ini bozar (stok yalnız POSTED'da çıkmalı) | **yüksek** | orta | `sp_PickLinePost`/`sp_PickConfirm` ASLA ISSUE yazmaz; stok yalnız `sp_ShippingPost`. PICKED→POSTED manuel. sql-sp-reviewer + smoke (net bakiye) |
| Bin.SortNo yanlış türetme → sıra bozuk | orta | orta | Code-parse + Locations elle ezme + smoke gerçek sırayla |
| Multi-bin allocation eşzamanlı task'ta aynı bin'i önerir | orta | düşük | available-to-pick (açık task düş) TVF; single-tenant düşük eşzamanlılık |
| RELEASED statü geçiş seed eksik → terminal patlar | orta | düşük | StatusTransition seed (Guid.Empty sistem-fallback) + fresh-DB |
| Terminal multi-step state (Alpine yasak) | orta | düşük | server-render `step` query param / hidden field + vanilla data-step toggle |

## 5. Done Criteria

- [ ] Pick kalemleri terminal+details'te `Zone, SortNo` (serpentine) sırasında; `PickSeq` dondurulmuş
- [ ] `Bin.SortNo` dolu (Code türevli + Locations elle ezme)
- [ ] Çoklu-adres: ürün 2 bin'de + tek bin yetmez → 2 PickTaskLine (FIFO sıra)
- [ ] RELEASED statü çalışıyor (havuz→ata akışı) + StatusTransition seed
- [ ] Terminal guided: raf-doğrula → ürün → miktar (short-pick) + istisna + "Kalem N/M" sayaç; mobil görsel-verify (375)
- [ ] `sp_PickConfirm` qty+exception; short-pick satırı açık kalır, görev COMPLETED olmaz
- [ ] Shipping `DRAFT→PICKING→PICKED→POSTED`; **stok yalnız POSTED'da** (smoke net bakiye)
- [ ] `operax-cli migrate` + fresh-DB 0 fail · `dotnet build` 0/0
- [ ] Faz kapanış: build + code-reviewer + sql-sp-reviewer + security (terminal POST) + smoke

## 6. Rollback

- Her faz ayrı commit (plan: 56) → `git revert`.
- Şema: yeni kolon (`PickSeq`/`ExceptionNote`) nullable/default → geri-uyumlu; status genişleme additif.
- SP: `CREATE OR ALTER` idempotent, önceki sürüm migrate ile geri.

## 7. Fazlar (footprint-ladder sırası)

1. [x] ✅ **Faz A** pick sorguları `ORDER BY Zone,SortNo`→`PickSeq` · Bin.SortNo zaten seed'li · `PickTaskStatus.Released`+dict 'Havuzda'+terminal sayaç · build+smoke (6c21243)
2. [x] ✅ **Faz B** `PickTaskLine.PickSeq`+`ExceptionNote` · `sp_ShippingCreatePickTask` multi-bin FIFO split + kısmi-pick + serpentine PickSeq + 3 IMP fix · sql-sp-reviewer+fresh-DB+E2E smoke (8dacf51)
3. [x] ✅ **Faz C** guided 3-adım terminal (raf-doğrula→ürün→miktar) + HID auto-advance + short-pick + istisna(atla/hasar) menü · `sp_PickConfirm` 9-param (bin/qty/exception) · mobil-verify(375 overflow yok, autofocus, auto-advance, sayaç) + SP smoke (short-pick SHORT + yanlış-raf THROW 51582) + fresh-DB 0 fail
4. [x] ✅ **Faz D** Shipping `DRAFT→PICKING→PICKED→POSTED` ara-statü: sp_ShippingCreatePickTask→PICKING · sp_PickConfirm/PickLinePost COMPLETED→PICKED · POST DRAFT/PICKING/PICKED'ten · iptal PICKING/PICKED (exit gate, ledger'a dokunmaz, PickTask CANCELLED) · SHIPMENT geçiş seed + STATUS dict + badge fix · **E2E ledger smoke: stok YALNIZ POSTED'da (-5), staging 0** · sql-sp-reviewer (ledger temiz, IMP-1/2 exit gate düzeltildi) · fresh-DB 0 fail (UpdatedAt-bug yakalandı)
5. [→] **Faz E:** TODO/journal senkron (sonraki). **Kalan gap (backlog):** PickTask cancel→shipping geri-DRAFT (IMP-3) · multi-pick-task guard (OBS-1) — düşük, statü-tamlık.

> Faz A bağımsız değer üretir (bugün gezdir). B-D üstüne kurar.

## 8. İlişkili

- Araştırma: reference-researcher (agentId aea2e53a) — guided pick UX (SAP EWM/Voice/serpentine) · erp-isleyis-danismani (a613025e) — workflow model (Shipping kaynak, RELEASED, staging)
- Mevcut: `sp_ShippingCreatePickTask` (db_objects.sql:266) · `sp_PickConfirm` (db_objects_putaway_pick.sql:113) · `sp_PickLinePost` çift-düşüm fix (776-779) · Bin.Zone/SortNo (schema_all.sql:445)
- Kural: `.claude/rules/document-immutability.md` (ledger — stok yalnız POSTED) · `ux-design-patterns` M1-M7 (terminal) · `architecture.md §7` (WMS otomasyon)
- Backlog: b-* dead badge (task_02b56a5f)

## 9. Onay

- [ ] Plan kullanıcıya gösterildi
- [ ] Onay alındı: <tarih>
