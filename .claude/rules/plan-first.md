# Plan-First Disiplini (Tier Sistemi)

Bu kural Operax üzerindeki her oturuma uygulanır. `paths:` filtre yoktur — compact ve clear sonrası da etkilidir.

## Temel kural

**Tier 3 işlerde plan ZORUNLU.** Plan onaylanmadan kod yazılmaz, plan referansı olmadan Tier 3 commit atılmaz.

## Tier eşikleri

| Tier | Tanım | Plan? | Operax örnek |
|---|---|---|---|
| **1 — Trivial** | <30 satır, 1-2 dosya, sıfır yeni pattern, geri alınması kolay | **YOK** | Typo, label çevirisi, version bump, eksik using ekleme |
| **2 — Standard** | <5 dosya, mevcut pattern, küçük feature/fix | **TODO satırı yeterli** | Tek SP düzeltme, tek Razor sayfasında badge ekleme, mevcut form'a yeni input |
| **3 — Substantial** | 3+ dosya yeni pattern, schema/security/UX/harici bağımlılık, kullanıcı-görünür değişiklik | **TAM PLAN ZORUNLU** (`plans/NN-<slug>.md`) | Yeni modül (M11 Finans), schema migration, e-Belge altyapısı, çoklu ekran refactor (template port), STARTER paket aktivasyon |

## Tier 3 sinyalleri

Şu sinyallerden BİRİ varsa Tier 3 sayılır:

1. **3+ farklı klasöre dokunma** (örn. `docs/sql/` + `src/Operax.Web/` + `.claude/`)
2. **Yeni dosya tipi** (yeni `schema_M*.sql`, yeni SP grubu, yeni Razor feature klasörü)
3. **Geri alınması zor** (DB migration, kolon silme, FK constraint ekleme)
4. **Kullanıcı-görünür** UI/UX/sidebar/route değişiklik
5. **Harici bağımlılık** (yeni NuGet paketi, yeni Hangfire job, yeni env var)
6. **Mimari karar** (yeni pattern, yeni standart, ADR seviyesi karar)

Şüphede kal? **Kullanıcıya sor:** "Bu Tier 2 mi Tier 3 mü, plan yazayım mı?"

## Plan-First Workflow

### 1. Tier tespiti

- 1-2 dosya, sade fix → Tier 1, planla zaman kaybetme
- 3-5 dosya, mevcut pattern → Tier 2, TODO satırı yeterli
- 3+ dosya / yeni pattern / schema → Tier 3, plan yaz

### 2. Plan yaz (Tier 3)

```bash
ls plans/*.md | grep -E '^plans/[0-9]' | tail -1   # son ID'yi bul
cp plans/feature-template.md plans/NN-<slug>.md
```

Doldur: Problem, Scope, Alternatifler (en az 2 reddedilen), Riskler, Done criteria, Rollback, Adımlar.

**5 lens kontrolü (her biri için 1 cümle):**
- 🔴 **Contrarian:** Fatal flaw nerede?
- 🔵 **First Principles:** Yanlış soruyu mu soruyoruz?
- 🟢 **Expansionist:** Daha büyük fırsat kaçırılıyor mu?
- ⚪ **Outsider:** Yabancı biri ne garip bulurdu?
- 🟡 **Executor:** Pazartesi sabahı ne yapılır?

### 3. Onay

Kullanıcıya göster, geri bildirim al, düzeltme yap. **Onay olmadan implement etme.**

### 4. Implementation

- Her commit message'da plan referansı: `feat(M11): hesap ekstre (plan: 03)`
- `docs/TODO.md`'de plan adımları (Faz X altında)

### 5. Tamamlanma

- Plan dosyasını arşive taşı: `git mv plans/NN-*.md plans/archive/`
- Done criteria check'le
- Journal'da plan'in çıktı özeti

### 6. Stale plan disiplini

**Plan ölüm tarihi:** 14 gün dokunulmamış aktif plan ya **yeniden ısıt** ya **arşive taşı**.

Karar 3 yoldan biri:
- Plan hâlâ geçerli + iş başlayacak → bu oturumda Faz 1 adımına başla
- Plan geçerli ama zamanlama uzak → `plans/archive/` (notla: "Faz 0 başlamadı, ileride")
- Plan artık geçersiz (proje yönü değişti) → `plans/archive/` (notla: "Reddedildi: <gerekçe>")

## İstisnalar

### Acil bug fix (production down)
Plan-first **bypass edilebilir** ama:
1. Kullanıcıya: "Bypass yapıyorum, retro plan yazacağım"
2. Commit: `fix(<modül>): bug X (plan: BYPASS-<tarih>)`
3. Sonradan retro plan: `plans/archive/BYPASS-<tarih>.md`

### Kullanıcı "hızlıca yap" derse
- Tier 3 sinyali varsa hâlâ uyarı: "Bu 3+ dosyayı etkiliyor, mini-plan yazayım mı (5 dakika) yoksa direkt mi?"
- "Direkt" derse: TODO'ya `[plan-skipped: <gerekçe>]` notu

## İlişkili

- `plans/README.md` — klasör yapısı + workflow
- `plans/feature-template.md` — şablon
- `.claude/rules/commit-discipline.md` — Tier 3 commit'lerde plan referansı
- `.claude/rules/todo-verification.md` — TODO doğrulama disiplini
- `docs/MASTER_ROADMAP.md` — modül öncelik sıralaması
