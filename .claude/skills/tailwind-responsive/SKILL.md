---
name: tailwind-responsive
description: >
  Operax ekranlarını Tailwind v4 ile MOBİL-FIRST responsive yapma standardı + hazır
  recipe'ler. 2026-06-23 kural değişikliği: layout/responsive için Tailwind utility SERBEST
  (eski "utility-salata yasak" mobil-responsive'i baltaladığı için kaldırıldı); RENK/görsel
  hâlâ token/semantic (tema). Header stack, kart-grid collapse, iki-kolon evrak, geniş-tablo
  scroll, KPI grid, form grid — kopyala-kullan breakpoint recipe'leri. "responsive", "mobil",
  "ekran sığmıyor", "tailwind", "breakpoint", "mobil uyum", "ekran yayılıyor", "kolonlar kesik"
  denildiğinde veya herhangi bir .cshtml ekranı yazarken/mobil-düzeltirken çağrılır.
allowed-tools: Read, Grep, Glob, Bash
user-invocable: true
model: inherit
---

# Tailwind Responsive (Operax — mobil-first)

> **🔄 KURAL (2026-06-23, kullanıcı kararı):** Tailwind utility'leri **layout + responsive** için SERBEST
> (eski "utility-salata yasak" kaldırıldı — elle media-query mobil-responsive'i baltalıyordu). **Tek kısıt:
> RENK/görsel sabit değer GÖMME yasak → token.** Detay: `.claude/rules/ui-standard.md §2`.

## Ne zaman tetiklenir
Herhangi bir `.cshtml` ekranı yazarken/düzeltirken VEYA "mobilde sığmıyor / yayılıyor / kolon kesik" sorununda.

## Altın Kural (2 satır)
1. **YERLEŞİM/RESPONSIVE → Tailwind utility.** `flex flex-col md:flex-row`, `grid grid-cols-1 md:grid-cols-3`, `gap-4`, `overflow-x-auto`, `w-full`, `hidden md:block`.
2. **RENK/GÖRSEL → token/semantic.** `bg-[var(--surface)]` `text-[var(--text-3)]` `.card` `.btn` — ASLA `bg-white`/`text-slate-500`/`#hex`. Tema tek noktadan.

## Mobil-First İlke
Base sınıf = **mobil** (en dar). `sm:/md:/lg:` = ekran büyüdükçe. Breakpoint'ler (Tailwind v4): `sm`=640 · `md`=768 · `lg`=1024 · `xl`=1280.
Yani `grid-cols-1 md:grid-cols-3` = mobilde tek kolon, ≥768px üç kolon.

---

## Kopyala-Kullan Recipe'ler (Operax'ın gerçek mobil bug'larını çözer)

### 1. Sayfa başlığı (başlık + aksiyonlar) — header stack
**Bug:** flex-row'da aksiyonlar sığmayıp başlığı 0px'e sıkıştırır (metin kelime-kelime alt alta).
```html
<div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
  <div class="min-w-0"><!-- başlık + alt başlık --></div>
  <div class="flex flex-wrap gap-2"><!-- aksiyon butonları --></div>
</div>
```

### 2. Evrak iki-kolon (ana + özet kenar çubuğu)
```html
<div class="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_320px]">
  <div class="space-y-4"><!-- doc + satırlar --></div>
  <div class="space-y-4"><!-- özet/aktivite (mobilde alta iner) --></div>
</div>
```

### 3. Geniş tablo — KESİLMEZ, kaydırılır (en sık mobil bug)
```html
<div class="overflow-x-auto">          <!-- tabloyu SAR -->
  <table class="data-table min-w-[640px]"><!-- min-w: kolonlar ezilmesin, scroll çıksın --></table>
</div>
```
> Global emniyet: `parts/_cards.css` kart-içi tabloya `@media(≤900px){overflow-x:auto}` ekli; ama tabloyu doğrudan `overflow-x-auto` ile sarmak en güvenlisi.

### 4. KPI / metrik kartları
```html
<div class="grid grid-cols-2 gap-3 lg:grid-cols-4"><!-- mobil 2'li, geniş 4'lü --></div>
```

### 5. Form alanları (çok-kolon)
```html
<div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4"><!-- mobil 1, tablet 2, geniş 4 --></div>
```

### 6. Mobilde gizle/göster
```html
<span class="hidden md:inline">Masaüstünde görünür</span>
<button class="md:hidden">Mobil hamburger</button>
```

---

## Renk Token Cetveli (utility içinde `[var(--…)]`)
| Yanlış (sabit) | Doğru (token) |
|---|---|
| `bg-white` | `bg-[var(--surface)]` veya `.card` |
| `text-slate-500` | `text-[var(--text-3)]` |
| `text-slate-800` | `text-[var(--text)]` |
| `bg-slate-50` | `bg-[var(--surface-2)]` |
| `border-slate-200` | `border-[var(--border)]` |
| `text-indigo-600` | `text-[var(--brand-500)]` |

## Hazır Semantic Component (utility ile birlikte)
Görsel kimlik component'te, yerleşim utility'de: `<div class="card overflow-x-auto">`, `<button class="btn btn-primary w-full md:w-auto">`, `<input class="form-ctrl" />`, `<table class="data-table min-w-[640px]">`.

## Doğrulama (ZORUNLU — mobil)
Ekran yazınca/düzeltince **`preview_resize` ile mobil(375) + desktop(1280) test et:**
- Yatay taşma yok (`document.documentElement.scrollWidth ≤ innerWidth`).
- Hiçbir element viewport'tan geniş + clip değil (geniş tablo `overflow-x-auto` ile scroll).
- Başlık tek-satır/temiz (kelime-kelime stack yok).
- Token renk (tema dark/light tutarlı).

## Referans
- **Prototip:** `Features/Expenses/Details.cshtml` (Plan 53 altın-standart) + `SalesOrders/Partners/Finance` (iyi örnekler).
- `parts/_page.css` `.doc-layout/.form-grid/.table-scroll` (semantic responsive helper — utility ile değiştirilebilir/birlikte).
- `parts/_shell.css` mobil off-canvas sidebar (hamburger).

## İlişkili
- `.claude/rules/ui-standard.md §2` (kural değişikliği) · `.claude/rules/inline-style-guard.md`
- `.claude/skills/screen-ux-standard/SKILL.md` (etkileşim/klavye/mobil) · `ux-design-patterns` (kanıtlı UX)
