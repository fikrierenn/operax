# Plan 53 — Tasarım Sistemi Standardı + Uygulama

**Durum:** Taslak (onay bekliyor) · **Tarih:** 2026-06-23 · **Tier:** 3 (UI-geneli, çok-ekran, yeni-pattern)

## Problem

Tasarım dili tutarsız. 102 feature ekranından **60'ı semantic `.card` sistemini** kullanıyor (olgun: `.card`/`.kpi`/`.kpi-grid`/`.kpi-glow`/`.kpi-delta`/`.bars` + token). Yeni aktive edilen Dashboard (Mosaic dili) ise **utility-soup** ile yazıldı (`bg-[var(--surface)] border rounded-2xl shadow-sm` + inline sparkline) → tek aykırı ekran. İronik: Mosaic'i aktive ederken ekranı sistemin dışına çıkardık.

İstenen Mosaic görsel dili (yuvarlak kart + uppercase mikro-etiket + mono sayı + anlam-renkli delta pill + sparkline + hover-lift) **arzu edilir** ama yalnızca dashboard'da inline yaşıyor — yeniden kullanılabilir semantic bileşen değil. `ui-standard.md` utility-soup'u görsel için zaten YASAKLIYOR; dashboard kuralı ihlal ediyor. `--radius` token'ı yok (`.card` 14px hardcode); sparkline/stat-card semantic class'ı yok.

## Hedef

**Tek kanonik tasarım sistemi.** Mosaic görsel dili dokümante standart olur ve **semantic class + partial** olarak uygulanır (utility-soup DEĞİL). Dashboard bu sisteme refactor edilir (aykırılık giderilir). Diğer ekranlar worst-first yakınsar.

## Kapsam (Fazlar)

### Faz A — Standart katman (CSS + doküman) [çekirdek]
- `_tokens.css`: `--radius: 14px; --radius-lg: 16px` ekle (sihirli sayı yerine token).
- `_kpi.css` veya yeni `_stat.css`: 
  - `.stat-card` (Mosaic sales kartı: mikro-etiket + mono değer + delta pill + sparkline yuvası).
  - `.stat-spark` (sparkline svg wrapper) + anlam-renkli çizgi/dolgu (success/warn/danger varyant).
  - `.delta-pill` (yeniden kullanılabilir; mevcut `.kpi-delta` ile uzlaştır — tek isim).
- `.card` radius'u `--radius` token'a bağla (görsel değişmez, 14px sabit kalır → 60 ekran etkilenmez).
- `ui-standard.md` güncelle: kanonik kart/stat/sparkline kataloğu + **gerçek partial envanteri** (mevcut: _KpiCard/_PageHeader/_EmptyState/_StatusFlow/_Tabs/_Pager; doküman hayali _DataTable/_DocHeader vb. işaretlenir) + "kart kabuğu için utility-soup yasak, `.card`/`.stat-card` kullan".

### Faz B — Dashboard refactor (aykırıyı sisteme çek) [kanıt]
- `Dashboard/Index.cshtml` utility-soup → semantic class (`.card`/`.card-hdr`/`.stat-card`/`.kpi-grid`/`.stat-spark`).
- Layout/responsive Tailwind utility (grid/gap/flex) KALIR (ui-standard 2026-06-23 kararı: layout serbest). Yalnız **görsel kabuk** semantic'e döner.
- Tarayıcı görsel-doğrula: dashboard görünümü birebir korunur (token-swap, degrade yok — memory `ui-visual-verify-no-degrade`).

### Faz C — Sweep & worst-first uygulama [yayılım]
- 102 ekranı sapmaya göre tara (inline renk `style="color"`, utility renk `bg-white`/`text-slate-*`, ad-hoc kart markup, eksik `_PageHeader`).
- Worst-first sırala; her ekran **ayrı commit + phase-review-gate + mobil-verify**.
- DEBT TODO'ları (Expenses/Details + Report + MaterialIssue tablo utility-salata) bu fazda kapanır.

## Reddedilen Alternatifler
1. **Utility-soup'u 102 ekrana yay** (dashboard'u referans al, kopyala) → `ui-standard.md` ihlali, tema tek-noktadan değişmez, bakım kâbusu. RED.
2. **Dashboard'u aykırı bırak** → kullanıcının düzeltmek istediği tutarsızlık kalır. RED.
3. **`.card`'ı rounded-2xl+shadow-sm'e çevir (60 ekranı topluca değiştir)** → 60 ekranda kontrolsüz görsel regresyon riski. RED (radius token'a bağlanır ama değer korunur; değişiklik additive).

## Riskler & Azaltma
- **60 `.card` ekranında regresyon:** Faz A değişikliği additive (token bağlama, değer sabit) → görsel değişmez. Temsili 3-4 ekran görsel-doğrula.
- **Sparkline SVG `fill="var(...)"` token:** mevcut dashboard'da çalıştığı kanıtlı; class'a taşırken aynı pattern.
- **Faz C hacmi (102 ekran):** worst-first + batch; her batch ayrı commit, tek seferde hepsi değil. Kullanıcı durdurabilir.

## 5 Lens
- 🔴 **Contrarian:** Fatal flaw → `.card`'a dokunmak 60 ekranı bozabilir; bu yüzden Faz A additive, değer-koruyan.
- 🔵 **First Principles:** Doğru soru "utility mi semantic mi" değil — "tek kaynak nerede"; cevap zaten var olan semantic katman, dashboard ona dönmeli.
- 🟢 **Expansionist:** Daha büyük fırsat → stat-card/sparkline semantic olunca raporlar/modül anasayfaları da aynı bileşeni kullanır.
- ⚪ **Outsider:** Yabancı "neden 1 ekran 102'den farklı?" der — tam da düzeltilen şey.
- 🟡 **Executor:** Pazartesi → Faz A CSS+token+doküman (yarım gün), Faz B dashboard refactor+verify (yarım gün), Faz C batch batch.

## Done Criteria
- [ ] `--radius` token + `.stat-card`/`.stat-spark`/`.delta-pill` semantic class'ları mevcut.
- [ ] `ui-standard.md` kanonik katalog + gerçek partial envanteri güncel.
- [ ] Dashboard utility-soup = 0 (`grep bg-\[var(--surface)\]` → 0); semantic class kullanıyor; görsel birebir korundu (tarayıcı-doğrula).
- [ ] Faz C: en az worst 5 ekran standarda çekildi (her biri commit + verify) — kalan batch'ler TODO'da sıralı.
- [ ] Build Web 0/0; her faz phase-review-gate.

## Rollback
Her faz ayrı commit → `git revert <faz-commit>`. Faz A CSS additive olduğu için izole geri alınabilir.

## Adımlar (sıra)
1. Faz A: token + stat CSS + ui-standard doküman → build → temsili ekran verify → commit. ✅ (commit Faz A+B)
2. Faz B: Dashboard refactor → build → görsel-doğrula (birebir) → commit. ✅
3. Faz C: divergence sweep raporu → worst-first batch → her ekran commit+verify. ⏳ başladı

## Faz C — Worst-first divergence kuyruğu (2026-06-23 sweep)

**utility renk-class sayısı** (terminal ekranları HARİÇ — el-terminali ayrı UI yüzeyi, `_TerminalLayout`):

| # | Ekran | utility-renk | inline-renk style |
|---|---|---|---|
| ✅ | **MasterData modülü TAMAM** (Items yapısal + 7 ekran token+yapısal, modül batch commit) | — | — |
| ✅ | **Warehouses modülü TAMAM** (Details yapısal + Index token) | — | — |
| 3 | `Production/Details.cshtml` | 40 | — |
| 4 | `Picking/Details.cshtml` | 31 | — |
| 5 | `Manufacturing/BOM/Details.cshtml` | 28 | — |
| 6 | `LPN/Details.cshtml` | 27 | — |
| 7 | `MasterData/PriceLists/Index.cshtml` | 25 | — |
| 8 | `Transfer/Replenishment` · `CycleCount/Details` · `Transfer/Details` | 24/24/23 | — |
| 9 | `Shipping/Details` · `Lot/Details` · `Receiving/Details` | 21/21/20 | — |
| 10 | `SalesOrders/Details.cshtml` | — | 12 (inline renk) |
| 11 | `Finance/Aging/Details` · `Finance/Snapshot/Index` | — | 10/7 |
| 12 | DEBT (TODO): `Expenses/Details` · `Report/Index` · `MaterialIssue/Details` | — | 5 + utility-salata |

**Kural (her ekran):** renk-only token-swap (`bg-white`→`bg-[var(--surface)]`/`.card`, `text-slate-*`→`var(--text-*)`, inline renk style→semantic), layout/responsive utility KALIR, **computed-style doğrula** (screenshot subsystemi bu oturumda wedged → getComputedStyle ile gerçek renk teyidi), ayrı commit + build 0/0.
**Terminal ekranları (Receiving/Shipping/Picking/Transfer Terminal):** ayrı el-terminali yüzeyi — bu sweep KAPSAMI DIŞI (kendi standardı).
