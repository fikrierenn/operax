# UI Standardı — Tek CSS Katmanı + Ortak Partial'lar

Bu dosya, Operax platformunun arayüz katmanının uyması gereken tek kaynaklı tasarım standardını tanımlar. Amaç: Tüm ekranların aynı görsel kimliği taşıması, ortak pattern'ların kopyalanmadan paylaşılması ve sayfa içi kod hacminin minimumda tutulmasıdır.

Referans: `tasarım/OPERAX Platform Pure.standalone.html` ve çıkarılmış kaynaklar `tasarım/_unbundled/`.

---

## 1. Tek CSS Katmanı

*   **Tek Giriş Dosyası (Entry):** `src/Operax.Web/wwwroot/css/input.css` Tailwind v4 entry'sidir; sadece `@import` ifadeleri içerir, kendisi component CSS'i barındırmaz.
*   **Parça (Part) Dosyaları:** Tüm component stilleri `wwwroot/css/parts/` klasörü altında konuya göre ayrılmış parça dosyalarda yaşar. Her parça dosyası **200 satırı geçmemelidir** (CLAUDE.md kuralı ile uyumlu).
    *   `parts/_tokens.css` — `:root` değişkenleri
    *   `parts/_base.css` — html/body reset, scrollbar, focus
    *   `parts/_shell.css` — sidebar + topbar
    *   `parts/_page.css` — sayfa wrapper, header, breadcrumb
    *   `parts/_buttons.css`, `_cards.css`, `_badges.css`, `_forms.css`, `_tables.css`, `_kpi.css`, `_status.css`, `_overlays.css`, `_misc.css`
*   **Compile Çıktısı:** `npx @tailwindcss/cli -i wwwroot/css/input.css -o wwwroot/css/site.css` komutu **tek bir** `site.css` üretir. `_Layout.cshtml` yalnızca `site.css` dosyasına link atar.
*   **Yasaklı:** `wwwroot/css/parts/` dışında veya feature klasörleri içinde `.css` dosyası **oluşturulamaz**. `output.css`, `dashboard.css` vb. dağınık dosyalar **eklenemez**.
*   **Sayfa İçi `<style>` Yasak:** Razor sayfaları içinde `<style>` bloğu yazılmaz.
*   **Belirteç (Token):** Tüm renk, gölge, font ve gradient `:root` üzerindeki CSS değişkenleridir. Kod içinde sabit renk değeri (örn. `color:#6366f1` veya `hsl(...)`) **yasaktır**, daima `var(--brand-500)` gibi token referansı kullanılır.
*   **Yeni Component Ekleme:** Önce ilgili `parts/_*.css` dosyasına eklenir. İlgili part 200 satırı aşacaksa yeni bir part dosyası açılır ve `input.css` içine `@import` satırı eklenir.

### Token İsimleri (özet)
*   Marka: `--brand-500`, `--brand-400`, `--brand-grad`, `--brand-tint-15`, `--brand-glow`
*   İçerik: `--bg`, `--bg-2`, `--surface`, `--surface-2`, `--border`, `--border-strong`
*   Metin: `--text`, `--text-2`, `--text-3`, `--text-4`, `--text-mute`
*   Sidebar: `--side-bg`, `--side-bg-2`, `--side-border`, `--side-text`, `--side-text-strong`, `--side-text-dim`
*   Semantik: `--success`, `--warn`, `--danger`, `--info` + her biri için `-bg`, `-text`, `-border` türevleri
*   Gölge: `--shadow-sm`, `--shadow`, `--shadow-md`, `--shadow-lg`, `--shadow-brand`, `--shadow-success`, `--shadow-danger`

---

## 1.5 Veri Kaynağı Politikası — Sıfır Hardcoded Veri

*   **Kod İçi Veri Yasak:** PageModel sınıflarında veya `.cshtml` dosyalarında demo amaçlı sabit veri (`new(...)` ile doldurulmuş listeler, `if (X == 0) X = 14;` gibi fallback değer atamaları, sahte tedarikçi/ürün/kullanıcı isimleri, hardcoded para tutarları, hardcoded ay/dönem rakamları) **kesinlikle yazılmaz**.
*   **Tek Kaynak Veritabanı:** Tüm sayısal değer, liste ve metin Dapper sorguları üzerinden veritabanından gelir. Sorgu hiçbir kayıt döndürmüyorsa görünüm `_EmptyState` partial'ı ile yanıt verir.
*   **Statik UI Etiketi İstisnası:** Sütun başlıkları, buton etiketleri, breadcrumb metinleri, validasyon hata mesajları gibi *gösterim* metinleri `L.T("tr", "en")` ile yazılır — bu "veri" değildir.
*   **Şirket / Kullanıcı Adı:** "Aydın Endüstri A.Ş." gibi şirket adları, "Mehmet Yılmaz" gibi kişi adları hardcoded yazılmaz — daima `CurrentCompany.Name`, `CurrentUser.UserName` üzerinden okunur.
*   **Aylık / Dönem Toplamları:** Aylık satınalma, satış, ciro gibi metrikler doğrudan SQL'de `GROUP BY DATEFROMPARTS(YEAR, MONTH, 1)` ile hesaplanır; C# tarafında hardcoded ay listesi tutulmaz.
*   **Eksik Demo Veri:** Geliştirme sırasında bir ekrana ait veri yoksa `docs/sql/` altında ilgili `seed_*.sql` dosyası genişletilir; PageModel hiçbir koşulda fallback değer atamaz.

## 2. Inline Style Politikası

*   **İzinli (Sadece Layout):** Tek seferlik grid ya da boyut kararları için inline style kullanılabilir.
    *   `style="display:grid;grid-template-columns:1fr 360px;gap:14px"`
    *   `style="height:200px;flex:1"`
    *   `style="max-width:1640px"`
*   **Yasaklı (Görsel Stil — RENK):** Sabit RENK değeri inline/utility yazılmaz (`bg-white`, `text-slate-500`, `color:#6366f1`, `bg-indigo-50`). Renk/gölge/marka mutlaka **token** (`var(--surface)`, `var(--brand-500)`) veya semantic class üzerinden — tema tek noktadan değişsin.
*   **🔄 KARAR DEĞİŞİKLİĞİ (2026-06-23, kullanıcı): Tailwind utility'leri LAYOUT + RESPONSIVE için SERBEST.** Eski "utility-salata yasak" kuralı mobil-responsive'i baltaladı (elle media-query yavaş + hataya açık). Artık:
    *   **Layout/responsive utility SERBEST:** `flex`, `grid`, `grid-cols-*`, `gap-*`, `md:flex-col`, `sm:hidden`, `w-full`, `max-w-*`, spacing (`px-*/py-*/mt-*`) — Tailwind responsive framework'ünün asıl gücü. Mobil uyum bunlarla yapılır (`flex flex-col md:flex-row`).
    *   **RENK/görsel hâlâ token/semantic:** `bg-white`→`bg-[var(--surface)]` veya `.card`; `text-slate-500`→`var(--text-3)`. Tema tutarlılığı korunur.
    *   **Semantic component class korunur** (`.card`, `.btn`, `.form-ctrl`, `.data-table`) — utility ile birlikte kullanılabilir (component=görsel kimlik, utility=yerleşim).
    *   **Sonuç:** ekran yazarken responsive layout için Tailwind utility kullan; renk/component için token/semantic. İkisi karışır.

---

## 3. Sayfa İskeleti

Her Razor sayfasının (`.cshtml`) iskeleti şöyle olmalıdır:

```html
<div class="page" data-screen-label="[Ekran Adı]">

    <partial name="_PageHeader" model='new PageHeaderVm {
        Crumbs = new[] { "Anasayfa", "Modül", "Sayfa" },
        Title  = "Sayfa Başlığı",
        Sub    = "Açıklama satırı veya tarih",
        Actions = "..."
    }' />

    <!-- Sayfa içeriği: card, kpi-grid, data-table veya kombinasyon -->

</div>
```

*   `class="page"` zorunlu (max-width ve padding yönetir).
*   `data-screen-label` analytics ve debug için.

---

## 4. Ortak Partial Kataloğu

Partial'lar `src/Operax.Web/Features/Shared/` altında yaşar. **Durum** = bugün repoda mevcut mu (✅) yoksa henüz yazılmamış hedef mi (⛔). ⛔ olanlar yazılana kadar inline pattern + semantic class kullanılır.

| Partial | Durum | Açıklama |
|---|---|---|
| `_PageHeader.cshtml` | ✅ | Breadcrumb + h1 + alt başlık (`Sub` escape / `SubHtml` raw) + `ActionsHtml` |
| `_KpiCard.cshtml` | ✅ | KPI bloğu (label + value + delta) — `.kpi` sistemi |
| `_EmptyState.cshtml` | ✅ | Boş liste için ikon + başlık + açıklama |
| `_StatusFlow.cshtml` | ✅ | DRAFT → POSTED → CANCELLED timeline |
| `_Tabs.cshtml` · `_Pager.cshtml` | ✅ | Sekme şeridi · sayfalama |
| `_DocFlowButtons.cshtml` · `_CustomFields.cshtml` | ✅ | Evrak akış butonları · UDF render |
| `_FilterBar` · `_DataTable` · `_DocHeader` · `_DocLines` · `_DocToolbar` · `_Avatar` · `_SmartButtons` | ⛔ | Henüz YOK — ihtiyaç olunca yazılır; o ana dek `.data-table`/`.card`/`form-ctrl` semantic ile inline. |

### 4.5 Stat Kartı / Sparkline (anasayfa & rapor metrik bileşeni) — Plan 53

Anasayfa üst-satır metrik kartlarının **kanonik semantic bileşeni** (`_stat.css`). Utility-soup ile kart kabuğu yazmak YASAK — bu class'lar kullanılır. Layout (grid/gap) Tailwind utility serbest.

```html
<!-- Sparkline'lı sales kartı -->
<div class="stat-card">
  <h3 class="stat-card-title">Onaylı Satınalma</h3>
  <span class="stat-label">Tutar</span>
  <div class="stat-row">
    <span class="stat-value">1.731.100 ₺</span>
    <span class="delta-pill down">-58,4%</span>   <!-- up · down · (boş=nötr) -->
  </div>
  <div class="stat-spark is-success">             <!-- is-success · is-warn · is-danger -->
    <svg viewBox="0 0 100 36" preserveAspectRatio="none">
      <polygon class="spark-area" points="..." /><polyline class="spark-line" points="..." />
    </svg>
  </div>
</div>

<!-- Sparkline'sız mini-KPI -->
<div class="stat-card">
  <span class="stat-label">Stoklu Konum</span>
  <div class="stat-value">6</div>                  <!-- danger varyant: class="stat-value danger" -->
  <div class="stat-sub">dolu hücre sayısı</div>
  <!-- ilerleme barı: <div class="stat-bar"><div class="stat-bar-fill" style="width:35%"></div></div> -->
</div>
```

- Sparkline rengi **CSS modifier ile** (`is-success/warn/danger`) — inline `fill`/`stroke` YOK (CSP + tema tek-nokta). SVG saf, Chart.js yok.
- `.stat-card` radius = `--radius-lg` (16px, hero metrik); içerik panelleri `.card` (`--radius` 14px).
- İçerik panelleri (grafik/liste/tablo) `.card` + `.card-hdr` + `.card-title`/`.card-sub` + `.card-body`.

### 4.6 Standart Bileşen Kataloğu — HER ekran bunları kullanır (bespoke YASAK)

Her UI öğesi için **tek kanonik bileşen**. Aynı işi gören ad-hoc/bespoke markup (özel `bg-…rounded` buton, elle KPI kutusu, `w-full px-4…rounded-xl` input) **yazılmaz** — aşağıdaki sınıf kullanılır. Modül standardizasyonunda her ekran bu kataloğa çekilir.

| Öğe | Kanonik bileşen | Bespoke (YASAK) |
|---|---|---|
| Sayfa başlığı | `.page-hdr` (`.page-hdr-l`/`.breadcrumb`/`h1`/`.page-hdr-sub`/`.page-hdr-r`) veya `_PageHeader` | elle `flex justify-between` + `text-3xl font-black` |
| KPI / metrik kutusu | `.stat-card` (+`.stat-label`/`.stat-value`/`.stat-sub`/`.stat-spark`/`.stat-bar`) veya liste sayfasında `.kpi-grid`+`.kpi` | elle `.card p-4` + özel font/etiket |
| Delta/trend rozeti | `.delta-pill` (`up`/`down`) | elle renkli pill |
| Buton | `.btn` + `.btn-primary`/`.btn-secondary`/`.btn-ghost`/`.btn-danger` (+`.btn-sm`) | `bg-[var(--brand-500)] text-white px-4 py-2 rounded-lg` |
| İçerik paneli | `.card` + `.card-hdr` + `.card-title`/`.card-sub` + `.card-body` | elle `bg-[var(--surface)] rounded-xl border shadow-sm` |
| Tablo | `.data-table` | elle `<table class="w-full text-sm">` + özel thead |
| Form | `.form-row`/`.form-row-3` · `.form-group` · `.form-label`(+`.req`) · `.form-ctrl` · `.form-hint`/`.form-error` | `w-full px-4 py-2.5 …rounded-xl` input, elle label, **legacy `class="input"`/`class="label"`** (→ `.form-ctrl`/`.form-label`) |
| Switch / Checkbox | `.switch`(+`.switch-track`) · `.chk-row`/`.chk`/`.chk-box` | `accent-emerald-600` ham checkbox |
| Durum rozeti | `.badge`+`.badge-success`/`-warn`/`-danger`/`-neutral` (veya `b-green`/`b-amber`/`b-indigo`/`b-gray`) + `UiHelpers.StatusBadge` | elle renkli span |
| Boş durum | `_EmptyState` veya `.card` içi ortalanmış ikon+metin | — |
| Sayfalama / sekme | `_Pager` · `_Tabs` (veya `.page-hdr` altı tab şeridi) | — |
| Grid/yerleşim | Tailwind responsive utility (`grid grid-cols-* gap-*` + `sm:`/`md:`/`xl:`) — SERBEST | sabit px genişlik |
| Modal scrim | `bg-[var(--overlay)]` | `bg-slate-900/40` / `bg-[var(--text)]/40` |

**Renk:** tüm renk/gölge/radius token (`var(--…)`); ham `bg-white`/`text-slate-*`/hex YASAK (§1/§2). **Dil:** görünür metin %100 Türkçe (`turkish-ui.md`) — `UOM`→Birim, `Zone`→Bölge, `Staging`→Hazırlık gibi İngilizce kelime kalmaz (kod identifier `bin.Zone` hariç).

### View Model'leri (DTO'lar)

`Lib/UiVms.cs` dosyasında tek bir yerde:

```csharp
public record PageHeaderVm(string[] Crumbs, string Title, string? Sub = null, string? Actions = null);
public record FilterBarVm(IEnumerable<TabVm> Tabs, string? SearchPlaceholder = null, string? ActionsHtml = null);
public record DataTableVm(IEnumerable<ColumnVm> Cols, IEnumerable<IDictionary<string,object>> Rows, string? RowClickRoute = null);
public record StatusFlowVm(string Current, IEnumerable<StatusStepVm> Steps);
// ...
```

---

## 5. Evrak Yaşam Döngüsü — Tek Pattern

Her belge modülü (Receiving, Shipping, Transfer, CycleCount, PurchaseOrders, SalesOrders, Production WorkOrder) aşağıdaki üç sayfa pattern'ini uygular:

### A. List Sayfası (`/{Modül}/Index`)
```
_PageHeader  ── Title · Sub · Yeni butonu
_FilterBar   ── Tabs(All/Draft/Posted/Cancelled) · Search · Chip filtreler
_DataTable   ── DocNo · Date · Partner · Status badge · Total · row click
```

### B. New Sayfası (`/{Modül}/Create`)
```
_PageHeader  ── Title="Yeni ..."
_DocHeader   ── DocNo (boş) · Date (today) · Partner lookup · Warehouse · Notes
_DocLines    ── Boş tablo + "Satır Ekle" butonu
_DocToolbar  ── Taslak Kaydet · Kaydet & Onayla
```

### C. Detail/Edit Sayfası (`/{Modül}/Details/{id}`)
```
_PageHeader  ── Title · Geri · _SmartButtons (metrik kutucuklar)
_StatusFlow  ── Mevcut durum highlight'lı timeline
_DocHeader   ── POSTED ise readonly · DRAFT ise editable
_DocLines    ── POSTED ise readonly · DRAFT ise editable
_DocToolbar  ── Status'a göre buton seti: 
                DRAFT  → Kaydet · Onayla · Sil
                POSTED → İptal Et · Yazdır · Denetim İzi
                CANCELLED → sadece Yazdır
```

---

## 6. Form Standardı

Tek doğru form pattern'i — başka bir form yazılmaz:

```html
<div class="form-row">
    <div class="form-group">
        <label class="form-label">Etiket <span class="req">*</span></label>
        <input class="form-ctrl" />
        <span class="form-hint">Yardım metni</span>
        <span class="form-error">Hata mesajı</span>
    </div>
    <div class="form-group">
        <label class="form-label">Etiket 2</label>
        <select class="form-ctrl">
            <option>Seçiniz...</option>
        </select>
    </div>
</div>
```

*   `form-row` = 2 kolon · `form-row-3` = 3 kolon (responsive otomatik tek kolona düşer).
*   `<input>`, `<select>`, `<textarea>` her biri `.form-ctrl` class'ı taşır.
*   Switch: `.switch` · Checkbox: `.chk` (tasarım dosyasında tanımlı).

---

## 7. Status Badge Helper'ı

C# helper `Lib/UiHelpers.cs` içindedir, magic string yasağı uyarınca DTO sabitlerini kullanır:

```csharp
public static string StatusBadge(string statusCode) => statusCode switch
{
    DocStatus.Draft     => "<span class=\"badge badge-warn\"><span class=\"badge-dot\"></span>TASLAK</span>",
    DocStatus.Posted    => "<span class=\"badge badge-success\"><span class=\"badge-dot\"></span>ONAYLI</span>",
    DocStatus.Cancelled => "<span class=\"badge badge-danger\"><span class=\"badge-dot\"></span>İPTAL</span>",
    _ => $"<span class=\"badge badge-neutral\"><span class=\"badge-dot\"></span>{statusCode}</span>"
};
```

Razor içinde: `@Html.Raw(UiHelpers.StatusBadge(po.Status))`

---

## 8. Yeni Ekran Yazım Checklist'i

> **ÖNCE DANIŞ (ZORUNLU):** Ekran oluştururken VEYA mevcut ekranı revize ederken, kod yazmadan önce ilgili UX/tasarım danışman skill'ine danışılır (hangi pattern/akış/responsive reçete uygulanacak netleşsin):
> - **`screen-ux-standard`** — form akışı, otomatik doldurma, klavye, boş durum, hata geri bildirimi, satır girişi (etkileşim/akış).
> - **`ux-design-patterns`** — data grid, form, combobox/typeahead, inline validasyon, görsel hiyerarşi (kanıtlı NNGroup/Baymard/Fiori desenleri).
> - **`tailwind-responsive`** — mobil-first breakpoint reçeteleri (header stack, kart-grid collapse, geniş-tablo scroll, KPI/form grid).
> - Finans/muhasebe ekranıysa AYRICA `coding-discipline.md §5` (muhasebe-mevzuat / mali-evrak-mevzuat / mali-islem-akislari).
> "Ekran iyi görünüyor" yetmez — etkileşim/responsive/pattern skill ile teyit edilir. Bileşen seçimi §4.6 kataloğundan.

Yeni bir Razor sayfası açarken bu sırayla doğrulanır:
1. `class="page"` wrapper var mı?
2. `_PageHeader` partial'ı kullanıldı mı?
3. Yeni inline renk/font/gölge yazıldı mı? → **Düzeltilir, semantic class kullanılır.**
4. Layout/responsive Tailwind utility OK (2026-06-23 kural); ama **RENK utility (`bg-white`/`text-slate-*`) → token** (`bg-[var(--surface)]`). Mobil-first `md:/lg:` kullanıldı mı? (bkz. `tailwind-responsive` skill)
5. Form varsa `_DocHeader` veya `form-group`/`form-ctrl` pattern'i mi?
6. Liste varsa `_DataTable` partial'ı mı?
7. Belge sayfaları için `_StatusFlow` + `_DocToolbar` kullanıldı mı?
8. `npx @tailwindcss/cli` build sonrası `site.css` güncel mi?

---

## 9. Bağımlılıklar

*   Bu standart `architecture.md`, `coding-discipline.md` ve `turkish-ui.md` ile çakışmadan eksiksiz uygulanır.
*   Yeni component pattern'ı eklenmesi gerekirse önce bu dosyaya eklenir, sonra `input.css` ve partial dosyası yazılır.
