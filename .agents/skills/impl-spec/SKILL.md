---
name: impl-spec
description: Tier 3 plandan (veya scope'lanmış feature'dan) KOD-TEMELLİ dosya-dosya uygulama spesifikasyonu üretir — kesin DDL, metot imzaları, markup, entegrasyon satır referansları. Spec yazmadan ÖNCE dokunulacak her dosyayı okur (çakışma/karar noktasını koddan çıkarır). "detaylı plan", "dosya dosya plan", "ne nerede değişecek", "uygulama spec'i", "impl spec" denildiğinde veya feature-dev planından sonra implementasyon öncesi tetiklenir.
allowed-tools: Read, Grep, Glob, Bash, Edit, Write, Agent, AskUserQuestion
user-invocable: true
model: inherit
---

# impl-spec Skill (Operax)

## Amaç

`feature-dev` plan **NE + HANGİ dosya + NEDEN** der (faz/bileşen seviyesi). Bu skill bir adım derine iner: **her dosyada TAM olarak ne yazılacak** — kesin DDL, metot imzaları, partial markup, DI satırı, entegrasyon noktaları (`dosya:satır`). Plan ile kod arasındaki köprü.

**En kritik disiplin:** Spec yazmadan ÖNCE dokunulacak **her** dosya okunur. Varsayımla spec yazmak yasak — kod gerçeği planı çürütebilir (örn. Plan 34'te `Item.Description` zaten sahte-UDF JSON çantası çıktı, `AdditionalFields` sanılıyordu). Çakışma/karar noktası **koddan** çıkarılır, implementasyon ortasında değil.

## feature-dev / code-architect / planner'dan farkı

| Araç | Çıktı | Seviye |
|---|---|---|
| `feature-dev` | Tier tespiti + plan dosyası (problem/scope/alternatif/risk) | NE yapılacak |
| `code-architect` (agent) | Mimari blueprint, pattern keşfi | NASIL kurgulanacak |
| `planner` (agent) | plans/NN-*.md yürütülebilir adımlar | SIRA/bağımlılık |
| **`impl-spec`** | **dosya-dosya kesin değişiklik spec'i, koddan doğrulanmış** | TAM olarak NE yazılacak |

Tipik akış: `feature-dev` (plan + onay) → **`impl-spec`** (derin spec + karar noktaları) → implementasyon → `phase-review-gate`.

## Ne zaman tetikle

- Onaylı/taslak bir `plans/NN-*.md` var, implementasyona başlamadan önce.
- Kullanıcı "detaylı plan", "dosya dosya", "ne nerede değişecek", "uygulama spec'i" diyor.
- 5+ dosya etkileyen iş; körlemesine başlamak regresyon riski.

## Workflow

### Adım 1 — Kapsam + plan oku
- İlgili `plans/NN-*.md` (yoksa kullanıcıdan scope al) → etkilenen dosya listesini çıkar.
- Plan §2 "Etkilenen dosyalar" + §7 "Adımlar" başlangıç noktası.

### Adım 2 — DOKUNULACAK HER DOSYAYI OKU (zorunlu, atlanamaz)
`before-major-change.md §4` (Fact-Force Gate) burada uygulanır:
- Her hedef dosyanın değişecek bölgesi + çevre 50 satır okunur.
- Yeni dosya ise: aynı türden mevcut örneği oku (pattern taklidi — kendi stilini dayatma).
- SP/şema ise: `docs/sql/db_objects*.sql` / `schema_M*.sql`'de mevcut tanımı oku.
- Migration kaydı: `src/Operax.Cli/Program.cs` migrate listesini oku (yeni schema buraya eklenir).
- Çok dosya varsa **paralel** `Read` (tek mesajda) veya `code-explorer` (haiku) fan-out.

> Bu adımın çıktısı: "plan X diyordu ama kod Y" farkları. Bunlar Adım 3'ün girdisi.

### Adım 3 — Çakışma / karar noktalarını ÖNCE çıkar
Okuma sırasında bulunan her karar noktası implementasyon ÖNCESİ kullanıcıya `AskUserQuestion` ile sorulur:
- Mevcut bir alanın/kolonun yeniden mi kullanılacağı yoksa yenisi mi (regresyon riski).
- Drive-by refactor gerekip gerekmediği (`coding-discipline.md` — kapsam dışı refactor yasak, ayrı iş).
- Pattern seçimi (iki mevcut pattern çatışıyorsa).

Karar verilmeden ilgili dosyanın spec'i yazılmaz.

### Adım 4 — Dosya-dosya spec yaz
Her dosya için **sabit şablon**:

```
### <dosya yolu>  [YENİ | DÜZENLE]
**Amaç:** <1 cümle>
**Değişiklik:**
- <kesin içerik: DDL kolonları / metot imzası + logic özeti / markup / DI satırı>
- Entegrasyon noktası: <mevcut dosyada `dosya:satır` — neyin yanına/yerine>
**Pattern referansı:** <taklit edilen mevcut dosya:satır>
**Dikkat:** <bu dosyaya özel risk/açık — varsa>
```

Zorunlu içerik kalitesi:
- **Şema:** tam kolon listesi + tip + zorunlu kolonlar (`CompanyId/IsDeleted/CreatedAt/By` — `sql-conventions.md`) + index/unique + idempotent `IF NOT EXISTS` pattern.
- **SP:** `SET XACT_ABORT ON` + TRY/CATCH + THROW aralığı (50000-59999) + `@CompanyId/@UserId`.
- **PageModel:** primary ctor DI imzası + OnGet/OnPost'ta tam entegrasyon noktası (satır ref) + CompanyId/IDOR guard.
- **View/partial:** semantic class (inline style yasak — `inline-style-guard.md`) + Türkçe UI metni.
- **Migration:** `Program.cs` migrate listesine eklenecek tam satır + sıra gerekçesi.

### Adım 5 — Sıra + bağımlılık + güvenlik özeti
- Uygulama sırası (şema → Lib/DTO → backend → view → seed → migrate+test — `before-major-change.md §3`).
- Güvenlik kontrol özeti: SQL injection (parametreli), whitelist, sunucu validasyon, mass assignment, CompanyId — hangi dosyada nasıl kapandığı.

### Adım 6 — Plana işle
- Spec'i ilgili `plans/NN-*.md` içine "Faz N — Dosya Dosya Uygulama Spesifikasyonu" bölümü olarak ekle (Edit), ayrı dosya açma.
- Adım 3 kararlarını plan §3/§4'e yansıt (alternatif/risk güncelle).
- Onay sonrası `plan-tracker` ile TODO.md + TodoWrite sync.

## Kurallar

1. **Okumadan spec yazma.** Adım 2 atlanamaz — varsayımla yazılan spec implementasyonda kırılır.
2. **Karar noktasını implementasyona bırakma.** Çakışma okumada bulunur, kod ortasında değil (Adım 3).
3. **Kod yazma.** Bu skill SPEC üretir, `src/` altına dokunmaz. Sadece plan dosyası (Edit) + araştırma.
4. **Pattern taklit et, dayatma.** Mevcut dosya stiline uy (`before-major-change.md §4.2`).
5. **Salt-okuma ajanlar.** Fan-out gerekiyorsa `code-explorer`/`code-architect` (read-only) — yazma ajanı çağırma.

## Çıktı Beklentisi

- Geliştirici (veya implementasyon turu) spec'i açıp **düşünmeden** uygulayabilmeli: her dosya, her satır, her karar net.
- Kullanıcı okumadan "şu dosyada şu olacak" tablosunu görebilmeli.

## İlişkili

- `.Codex/skills/feature-dev` (komut) — Tier tespiti + plan dosyası (bu skill'in girdisi)
- `.Codex/agents/code-architect.md` — blueprint (pattern keşfi)
- `.Codex/agents/planner.md` — yürütülebilir adım listesi
- `.Codex/skills/plan-tracker` — onay sonrası TODO + plan sync
- `.Codex/rules/before-major-change.md` §3-4 — aşamalı geçiş + Fact-Force Gate
- `.Codex/rules/plan-first.md` — Tier 3 disiplini
- `.Codex/rules/phase-review-gate.md` — spec sonrası faz kapanış kapısı
