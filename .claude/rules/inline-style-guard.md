# Inline Style Yasağı (Operax)

`.cshtml` view yazarken / düzenlerken zorunludur. UI patterns ana referansı: `.claude/rules/ui-standard.md`.

## Kural

**`style="..."` HTML attribute kullanımı sıfır toleranslı yasaktır** — RENK, FONT, GÖLGE, BORDER, BORDER-RADIUS, PADDING, MARGIN için. Bu değerler semantic class içinde `:root` CSS değişkenleri ile yaşar.

## Tek istisnalar (sadece LAYOUT)

İzinli inline style kullanım alanları:
- **Tek-seferlik grid:** `style="display:grid;grid-template-columns:1fr 360px;gap:14px"`
- **Tek-seferlik boyut:** `style="height:200px;flex:1"` veya `style="max-width:1640px"`
- **Veri-driven CSS variable:** `style="--w: @pct%"` (örn. dinamik bar genişliği)
- **Runtime/dynamic style** — Chart.js canvas runtime yazımı, Alpine `:style` bağlamaları

## Razor expression style de yasak

Yasak:
```cshtml
<span style="color:@(condition ? "red" : "green")">...</span>
```

Doğru:
```cshtml
<span class="@(condition ? "text-danger" : "text-success")">...</span>
```

## Neden

- Tema değişikliği tüm view'larda yayılamaz
- CSP (Content Security Policy) eklersek inline style kırılır
- Maintenance — renk değişikliği için 100+ dosya değil 1 token
- Pattern tutarsızlığı — aynı bileşen farklı yerlerde farklı görünür

## Doğru Yol

1. **Mevcut semantic class kullan:** `.btn.btn-primary`, `.card`, `.badge.badge-success`, `.kpi`, `.data-table`, `.form-ctrl`
2. **Yoksa ekle:** `wwwroot/css/parts/_<konu>.css`'e yeni class ekle (200 satır limit)
3. **Hâlâ yoksa pattern tartış:** UI standardı `.claude/rules/ui-standard.md`'ye güncelleme öner

## Mevcut Token Sistemi

`wwwroot/css/parts/_tokens.css` — bütün renk/gölge/font değerleri burada. Class'lar bunları referans alır:

```css
.btn-primary {
    background: var(--brand-grad);
    color: #fff;
    box-shadow: var(--shadow-brand);
}
```

## Tarama

```bash
grep -rn 'style="' src/Operax.Web/Features/
```

`session-start.sh` hook her oturumda inline style sayısı raporlar.

## İlişkili

- `.claude/rules/ui-standard.md` — Tek CSS katmanı, semantic class kataloğu
- `.claude/rules/razor-conventions.md` — Razor sayfa yapısı
- `.claude/rules/turkish-ui.md` — UI dili
