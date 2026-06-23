# Razor Pages Konvansiyonları (Operax)

## Sayfa Yapısı

1. **Model:** `@model Operax.Web.Features.<Modül>.IndexModel` — DTO/record kullan, Entity yasak
2. **Layout:** `Layout = "_Layout";` varsayılan, override gereksiz
3. **ViewData / ViewBag:** Minimum. Mümkünse Model property
4. **Helper kullanım:** `@using Operax.Web.Lib` üst satırda

## Sayfa İskeleti Zorunlu

`.claude/rules/ui-standard.md` §3 gereği her sayfa:

```html
<div class="page" data-screen-label="[Ekran Adı]">
    <partial name="_PageHeader" model="header" />
    <!-- içerik -->
</div>
```

- `class="page"` zorunlu (max-width + padding)
- `data-screen-label` analytics + debug

## Form Kuralları

- **POST handler:** PageModel'de `OnPostAsync()` veya `OnPost<Action>Async()`
- **AntiForgery:** `<form method="post">` içinde `@Html.AntiForgeryToken()` zorunlu
- **Validation:** `<div asp-validation-for="Field"></div>` veya `<span class="form-error">`
- **Input:** `.form-ctrl` class
- **Layout:** `.form-row` (2 col) veya `.form-row-3` (3 col)

```html
<div class="form-row">
    <div class="form-group">
        <label class="form-label">Etiket <span class="req">*</span></label>
        <input asp-for="Field" class="form-ctrl" />
        <span asp-validation-for="Field" class="form-error"></span>
    </div>
</div>
```

## Güvenlik

- **`@Html.Raw` minimum.** Sadece güvenli helper çıktısı (UiHelpers) veya kontrollü partial
- **Model binding:** Kritik alanlar `[BindNever]`
- **ReturnUrl:** `Url.IsLocalUrl` yetmez. Ek: `returnUrl.StartsWith("/") && !returnUrl.StartsWith("//")`

## Türkçe UI

- UTF-8, düzgün karakter. Detay: `.claude/rules/turkish-ui.md`
- `<html lang="@(L.IsEn ? "en" : "tr")">` `_Layout.cshtml`'de
- Tüm UI metni `L.T("tr", "en")` çift dil helper'ı

## CSS

- **Sadece semantic class** (`btn-primary`, `card`, `data-table`, `kpi`)
- **Inline style yasak** (renk/font/border için) — sadece tek-seferlik layout grid
- **Tailwind utility salatası yasak** — template custom class kullan
- Detay: `.claude/rules/ui-standard.md` + `.claude/rules/inline-style-guard.md`

## Icon

- **Inline SVG** (CDN bağımlılığı yok)
- Standart pattern: `<svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.75" viewBox="0 0 24 24">...`

## JavaScript Yaklaşımı (vanilla-first)

- **Framework kararı (2026-06-23 revize):** Server-rendered Razor Pages. SPA (React/Vue/Angular) YASAK. Etkileşim için **vanilla JS** standart.
- **⚠️ Alpine.js KULLANMA (CSP ile uyumsuz):** Site CSP'si `script-src 'self' 'unsafe-inline'` — `unsafe-eval` YOK (güvenlik kararı, `Program.cs`). Alpine 3 standard build `x-data`/`x-show`/`x-on` ifadelerini `eval`/`Function()` ile değerlendirir → **sessizce çalışmaz** (konsol hatası bile yok, ekran ölü görünmez ama etkileşim çalışmaz). Bu tip iki ekranı sessizce kırdı (Plan 51 + Sözlük Değerleri). Yeni `x-` attribute YAZMA.
- **Declarative durum (toggle/dropdown/satır-içi edit/koşullu gösterim):** vanilla — `data-*` attribute + inline `onclick="fn(this)"` (CSP `unsafe-inline` event-handler'a izin verir, `eval` değil) + küçük `<script>` veya `wwwroot/js/*.js`'de fonksiyon. Display toggle = `el.style.display`.
- **Karmaşık logic / fetch / 3.parti lib (Tabulator):** `wwwroot/js/*.js` harici dosya, IIFE wrap.
- **jQuery genişletme YASAK** — sadece Identity/validation scaffold'undan mevcut; yeni iş vanilla.
- **Tabulator** = grid-özel lib (`wwwroot/lib/tabulator/`).
- **İleride Alpine gerekirse:** `@alpinejs/csp` build'i (`Alpine.data()` zorunlu, inline ifade yok) tek seçenek — CSP'ye `unsafe-eval` EKLEME (güvenlik zayıflar).

## Inline JS

- **Minimum.** Kısa handler OK (`onclick="fn()"` — CSP unsafe-inline ile çalışır), büyük logic `wwwroot/js/`'e
- IIFE wrap, global namespace koru
- **CSP:** inline `<script>` ve `onclick` çalışır (`unsafe-inline`); `eval`/`new Function()` ÇALIŞMAZ (`unsafe-eval` yok) — Alpine standard build, `DataTable.Compute`-tarzı eval bu yüzden kırık.

## Partial View

- Tekrar eden UI parçası → `Features/Shared/_PartialName.cshtml`
- `<partial name="_PartialName" model="model" />` syntax
- Ortak partial kataloğu: `.claude/rules/ui-standard.md` §4

## Hata / Mesaj Gösterimi

```cshtml
@if (TempData["Success"] is string ok)
{
    <div class="alert-banner alert-info">@ok</div>
}
@if (TempData["Error"] is string err)
{
    <div class="alert-banner alert-danger">@err</div>
}
```

## Çift Dil (Localization)

`Lib/L.cs` üzerinden:
```cshtml
<button class="btn btn-primary">@L.T("Kaydet", "Save")</button>
@DateTime.Now.ToString(L.T("dd MMM yyyy", "yyyy-MM-dd"))
```

## İlişkili

- `.claude/rules/ui-standard.md`
- `.claude/rules/turkish-ui.md`
- `.claude/rules/inline-style-guard.md`
