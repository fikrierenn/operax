---
name: llm-council
description: Yüksek belirsizlik + yüksek maliyet kararlar için 5 danışman + peer review + chairman sentezi. "council this", "war room this", "pressure-test this", "debate this", "/council-this" tetikler.
allowed-tools: Read, Grep, Glob, Bash
user-invocable: true
model: inherit
---

# LLM Council Skill (Operax)

Karpathy'nin LLM Council metodolojisi. Gerçek belirsizlik + yüksek maliyet kararlar için.

## Tetikleyiciler

**Kesin tetikleyiciler:** "council this", "war room this", "pressure-test this", "stress-test this", "debate this", "/council-this"

**Güçlü tetikleyiciler** (gerçek karar tradeoff'u varsa):
- "şunu mu yapayım bunu mu", "hangi seçenek", "doğru hamle mi", "validate et", "kararsız kaldım"

**Tetikleme — YAPMA:** Basit evet/hayır, tek doğru cevabı olan, stakes'siz sorularda.

## Operax'ta Ne Zaman Kullan

- Mimari kararlar: "Dapper mı, EF Core mı?" (cevap zaten Dapper, ama gelecek değişiklikler için)
- Önceliklendirme: "STARTER sonrası WMS_PRO mu, MANUFACTURING mı önce?"
- Modül sınırı: "M11 Finans + M16 e-Belge tek pakette mi?"
- Teknoloji seçimi: "Alpine.js mi, HTMX mi, vanilla?"
- Tier 3 plan içinde alternatif değerlendirme (plan-first.md §3)

**KULLANMA:** Zaten scopelanmış, onaylanmış planlarda. Plan-First sistemi yönetir.

---

## Süreç (4 adım)

### Adım 1 — Soruyu çerçevele

Kullanıcı sorusunu + CLAUDE.md / journal / plan bağlamını birleştirerek **tarafsız, net karar çerçevesi** yaz. Yönlendirme yok.

### Adım 2 — 5 Danışmanı paralel çalıştır

**Hepsini aynı anda** spawn et. Her biri 150-300 kelime, kendi lensinden — hedge etmeden.

| Danışman | Lens |
|---|---|
| **Contrarian** | Fatal flaw'u bul. Plan başarısız olursa neden? |
| **First Principles** | Yanlış soru mu soruyoruz? Gerçek problem ne? |
| **Expansionist** | Kaçırılan upside ne? Scope çok mu dar? |
| **Outsider** | Koda/projeye yabancı biri ne garip bulur? |
| **Executor** | İlk somut adım ne? Pazartesi sabahı? |

**Her danışmana prompt:**
```
Sen bir LLM Council'da [Danışman Adı] rolündesin.

Düşünce lensin: [yukarıdaki lens]

Karar sorusu:
---
[çerçevelenmiş soru]
---

Perspektifinden doğrudan yaz. Hedge etme, dengelemeye çalışma.
150-300 kelime. Başlık yok, direkt analiz.
```

### Adım 3 — Anonim peer review (paralel)

5 yanıtı A-E olarak anonim et. 5 reviewer aynı anda spawn:

1. En güçlü yanıt hangisi, neden?
2. En büyük blind spot nerede?
3. Hepsinin kaçırdığı ne?

### Adım 4 — Chairman sentezi

```markdown
## Council Verdict: {kısa konu}

### Konseyin Uzlaştığı Noktalar
[Bağımsız ulaşılan sonuçlar — yüksek güven sinyalleri]

### Konseyin Çatıştığı Noktalar
[Gerçek anlaşmazlıklar — her iki taraf + ayrışma sebebi]

### Yakalanan Blind Spot'lar
[Sadece peer review'da ortaya çıkanlar]

### Öneri
[Net, eyleme geçirilebilir. "Bağlıdır" yok. Chairman çoğunluktan ayrılabilir.]

### İlk Adım
[Tek somut adım. Liste değil. Bir şey.]
```

Chairman not: Çoğunluk 4-1 "yap" dese bile 1'in gerekçesi güçlüyse 1'e taraf ol, açıkla.

---

## Operax Bağlam Dosyaları (council öncesi 30 sn)

- `CLAUDE.md` — proje kimliği + kısıtlar
- `docs/MASTER_ROADMAP.md` — modül öncelik
- `docs/COMPETITOR_ANALYSIS.md` — rakip karşılaştırma
- Son journal: `docs/journal/YYYY-MM-DD.md`
- Varsa ilgili plan: `plans/NN-*.md`

Bu bağlam olmadan danışmanlar genelgeçer tavsiye üretir.

## Sonucu Kaydet

Transcript istenirse: `docs/journal/YYYY-MM-DD.md`'ye append (ayrı dosya değil).

## İlişkili

- `.claude/rules/plan-first.md` §3 — 5 lens kontrolü (council'in mini hali, her plan için)
- `plans/feature-template.md` §3 Alternatifler — council kararını buraya işle
