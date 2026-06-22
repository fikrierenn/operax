# Plan 40 — Generic Print/Export Bileşeni

**Tarih:** 2026-06-22
**Yazan:** Fikri / Claude
**Durum:** `Tamamlandı`
**Modül:** M00 (Platform Core / cross-cutting)
**Paket:** STARTER

---

## 1. Problem

Plan 39 Faz 1'de Cari Ekstre için CSV export (formula-injection guard + UTF-8 BOM + RFC-4180) ve yazdırma (`@media print` + shell gizleme) yazıldı — ama her şey `Statement.cshtml.cs` ve `_print.css`'e **gömülü**. Aynı ihtiyaç çok yerde var: Satınalma/Satış sipariş listeleri (butonlar kablolu, handler YOK), Yaşlandırma, Gider Raporu, ileride stok/hareket raporları. Şu an ortak `CsvExport`/print helper'ı **yok** (kanıt: `Lib/` taraması — UiHelpers var, export helper yok). Her rapor kendi CSV string'ini ve formula-guard'ını kopyalarsa: (a) güvenlik guard'ı bir yerde unutulur → injection açığı, (b) negatif-sayı/escape hataları her kopyada tekrar, (c) bakım N yerde.

## 2. Scope

### Kapsam dahili
- **`Lib/CsvExport.cs`** — tipli-hücre CSV helper: başlık + satırlar → `byte[]` / `FileContentResult`. UTF-8 BOM, formula-injection guard (yalnız metin hücrede), RFC-4180 kaçış, tr-TR sayı/tarih biçimi.
- **Generic print CSS** — `_print.css`'e tema-bağımsız, rapor-agnostik sınıflar: `.no-print`, `.print-only`, `.print-doc` + genel `@media print` shell-gizleme bloğu. Mevcut `.stmt-*` sınıfları korunur (Statement kırılmaz).
- **Dogfood:** `Statement.cshtml.cs` `BuildCsv`/`Csv` → `CsvExport` çağrısına refactor (helper'ı kendi referansıyla kanıtla). `Statement.cshtml` print sınıflarını generic'e taşı (opsiyonel, düşük risk).
- **Proof tüketici (1 adet):** `Finance/Aging/Index` — yeni `OnGetExportAsync` + print desteği. Helper'ın 2. gerçek tüketicisi (negatif/tarihli kolon kanıtı).

### Kapsam dışı
- PurchaseOrders / SalesOrders / Expenses export handler'ları — Faz 2 (ayrı tur). Bu plan helper + pattern + 1 proof tüketici.
- Excel (.xlsx) native üretimi (ClosedXML/EPPlus). CSV yeterli; xlsx ayrı NuGet bağımlılığı → ayrı Tier 3 kararı.
- PDF export, server-side print render, e-posta gönderimi.
- `_ReportHeader` ortak partial (firma+başlık+dönem) — faydalı ama Faz 2; bu turda Statement kendi header'ını tutar.

### Etkilenen dosyalar (tahmin)
- `src/Operax.Web/Lib/CsvExport.cs` — **YENİ** (~70 satır)
- `src/Operax.Web/wwwroot/css/parts/_print.css` — generic blok ekle (+ input.css zaten import ediyor)
- `src/Operax.Web/wwwroot/css/site.css` — Tailwind rebuild
- `src/Operax.Web/Features/MasterData/Partners/Statement.cshtml.cs` — `BuildCsv`/`Csv` → helper
- `src/Operax.Web/Features/MasterData/Partners/Statement.cshtml` — print sınıfı (ops.)
- `src/Operax.Web/Features/Finance/Aging/Index.cshtml.cs` — `OnGetExportAsync` ekle
- `src/Operax.Web/Features/Finance/Aging/Index.cshtml` — Excel(CSV) + Yazdır butonları + `.print-doc` wrap

**Tahmini boyut:** ~7 dosya / ~200 satır.

## 3. Alternatifler

### A: Her rapor kendi CSV'sini yazmaya devam etsin (status quo)
**Açıklama:** Statement pattern'ini kopyala-yapıştır.
**Reddetme sebebi:** Formula-guard her kopyada tekrar → biri unutursa injection. Negatif-sayı hatası (bkz. Risk R1) her kopyada. Kullanıcı talebi açıkça "genel yap, hazır component" — kopya buna aykırı.

### B: Native Excel (.xlsx) üreten servis (ClosedXML/EPPlus)
**Açıklama:** Gerçek Excel dosyası — çok-sayfa, format, formül.
**Reddetme sebebi:** Yeni NuGet bağımlılığı (footprint-ladder: harici bağımlılık = üst basamak). CSV %95 ihtiyacı (Excel CSV'yi açar) karşılıyor. xlsx gerçekten gerekince ayrı plan. Şimdi over-engineering.

### C (SEÇİLEN): İnce static helper (`CsvExport`) + generic print CSS sınıfları
**Açıklama:** Tek `Lib/CsvExport.cs` (state'siz, tipli hücre) + CSS'te rapor-agnostik print sınıfları. Tüketici sadece başlık+satır verir; biçim/escape/BOM/guard helper'da. Print için sayfayı `.print-doc` ile sarar.
**Sebep:** footprint-ladder basamak 1-2 (mevcut Lib + CSS part genişlet, yeni sayfa/bağımlılık yok). Güvenlik guard'ı tek yerde → bir kez doğru, her yerde doğru. Dogfood + 1 proof tüketici ile kanıtlanır.

**5 lens kontrolü:**
- 🔴 **Contrarian:** Fatal flaw = negatif sayı formula-guard'a takılıp metne dönerse rapor toplamları Excel'de bozulur. → Tipli hücre (sayı guard'sız) bunu kökten çözer; aksi halde helper Statement'tan kötü olurdu.
- 🔵 **First Principles:** Gerçek ihtiyaç "Excel dosyası" değil "Excel'in doğru açtığı tablo + güvenli + Türkçe". CSV+BOM+tr biçim bunu karşılar; xlsx motoru gereksiz.
- 🟢 **Expansionist:** Helper ileride `_ReportHeader` partial + scheduled e-posta (Plan 39 Faz 3) ile birleşir — ortak rapor altyapısının ilk taşı.
- ⚪ **Outsider:** Yeni biri "neden her ekranda ayrı export kodu?" derdi — tam da bu planın gerekçesi.
- 🟡 **Executor:** Pazartesi: `CsvExport.cs` yaz → Statement'ı ona bağla (regresyon yok, smoke aynı CSV) → Aging'e ekle → build/review/smoke.

## 4. Riskler

| Risk | Etki | Olasılık | Mitigation |
|---|---|---|---|
| R1: Negatif/ondalık sayı formula-guard ile metne döner (Excel toplam bozulur) | yüksek | yüksek (status quo'da kesin) | Tipli hücre: `decimal/int/double/DateTime` helper'da biçimlenir, guard YOK; guard yalnız `string` hücrede |
| R2: tr-TR sayı biçimi (virgül ondalık) + `;` ayraç çakışması | orta | orta | Sayı hücresi N2 tr-TR üretir, ayraç `;` → Excel-TR doğru parse; ondalık-virgül RFC-4180 tırnak gerektirmez (sayı hücresi escape'siz, güvenli) |
| R3: Statement refactor mevcut CSV çıktısını değiştirir (regresyon) | orta | düşük | Refactor sonrası byte-bazlı aynı CSV smoke (aynı başlık, DEVİR/KAPANIŞ satırları) |
| R4: Generic print CSS mevcut `.stmt-*` ile çakışır | düşük | düşük | `.stmt-*` aynen korunur; generic sınıflar ek (additive), Statement'ı son adımda taşı |
| R5: Print renk token'ı karanlık temada okunmaz çıktı | orta | düşük | `_print.css`'teki sabit `#fff/#f3f3f3` kuralı generic bloğa taşınır (zaten bilinçli desen) |

## 5. Done Criteria

- [x] `Lib/CsvExport.cs` yazıldı — tipli hücre, BOM, guard (yalnız metin), RFC-4180, tr-TR biçim
- [x] `Statement` helper'a refactor — CSV çıktısı eskisiyle **bayt-aynı** (browser smoke: header+DEVİR+N2 tr+CRLF+BOM özdeş)
- [x] `Finance/Aging/Index` — CSV export + Yazdır çalışıyor (browser smoke: export 200, TOPLAM doğru)
- [x] Generic print sınıfları (`.no-print/.print-only/.print-doc/.print-title`) `_print.css`'te + `site.css` rebuild
- [x] `dotnet build` 0 hata · Plan 40 dosyaları 0 uyarı (72 CS0108 + 2 ASPDEPR005 PRE-EXISTING, kapsam dışı → ayrı TODO)
- [x] security-reviewer: CRIT-1 (önde-boşluk guard bypass) düzeltildi; IDOR/CompanyId temiz
- [x] code-reviewer: CsvExport + Statement temiz; Aging HIGH'ları PRE-EXISTING inline-style/hardcoded-renk/magic-string (kapsam dışı); benim eklediğim inline `margin` → `.print-title` class'a taşındı
- [x] Browser smoke: Statement + Aging export — sayı kolonları sayı kalıyor (R1/negatif-güvenli doğrulandı)

## 6. Rollback Planı

- Git revert: helper + 2 tüketici ayrı commit'ler → tüketici revert'i Statement'ı eski inline koda döndürmez (helper bağımlılığı kalır); bu yüzden **commit sırası**: (1) helper, (2) print CSS, (3) Statement refactor, (4) Aging. Sorun çıkarsa son commit'ten geriye revert.
- DB değişikliği YOK → migration rollback gereksiz.
- CSS: `site.css` rebuild geri alınırsa eski compile yeterli (generic sınıf kullanılmıyorsa zararsız).

## 7. Adımlar / İçerdiği TODO maddeleri

1. [ ] **P40-1** `Lib/CsvExport.cs` — `ToFile(fileName, headers, rows)` + `ToBytes(...)`; hücre tipi switch (string→guard, sayı/tarih→biçim); `Field()` RFC-4180
2. [ ] **P40-2** `_print.css` generic blok (`.no-print/.print-only/.print-doc` + shell-gizleme) + `site.css` rebuild
3. [ ] **P40-3** `Statement.cshtml.cs` `BuildCsv`/`Csv` → `CsvExport` çağrısı; bayt-aynı smoke
4. [ ] **P40-4** `Statement.cshtml` print sınıflarını generic'e taşı (opsiyonel, düşük risk)
5. [ ] **P40-5** `Finance/Aging/Index.cshtml.cs` `OnGetExportAsync` + `.cshtml` Excel/Yazdır buton + `.print-doc` wrap
6. [ ] **P40-6** build → code-reviewer + security-reviewer → browser smoke (Statement + Aging)
7. [ ] **P40-7** Faz 2 TODO satırı: PurchaseOrders/SalesOrders/Expenses export wire (ayrı tur)

> `docs/TODO.md`'ye P40-1..7 eklenecek; plan ve TODO senkron.

## 8. İlişkili

- Önceki plan: `plans/39-cari-ekstre-statement.md` (Statement = referans implementasyon, Faz 2 batch/scheduled bu helper'ı kullanır)
- Kural: `.claude/rules/footprint-ladder.md` (basamak seçimi — Lib genişlet), `.claude/rules/security-principles.md` §10 (formula/injection), `.claude/rules/inline-style-guard.md` (print CSS token istisnası)
- Konuşma referans: `docs/journal/2026-06-22.md`

## 9. Onay

- [x] Plan kullanıcıya gösterildi
- [x] Geri bildirim alındı (varsa düzeltildi)
- [x] Onay alındı: 2026-06-22 (Fikri "evet")
