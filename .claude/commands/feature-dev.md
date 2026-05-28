---
description: "Operax yeni feature başlatma akışı — Tier tespiti, plan yazımı, Spec→Plan→Execute zinciri"
argument-hint: "[feature açıklaması] (örn. M11 banka mutabakatı)"
allowed-tools: ["Bash", "Glob", "Grep", "Read", "Write", "Edit", "Task"]
---

# /feature-dev — Yeni Feature Başlatma

Yeni feature için Plan-First disiplinini uygular: Tier tespit → spec → plan dosyası → onay → implementation.

**Feature:** "$ARGUMENTS"

## Workflow

### 1. Tier Tespit

Feature açıklaması üzerinden Tier sınıflandır (`.claude/rules/plan-first.md`):

| Sinyal | Tier |
|---|---|
| 1-2 dosya, label değişimi | 1 (plan yok) |
| 3-5 dosya, mevcut pattern | 2 (TODO satırı) |
| 3+ dosya yeni pattern / schema / UI / API | **3 (PLAN ZORUNLU)** |
| Yeni modül başlangıcı | **3** |
| Mevcut modülün major genişletme | **3** |

Tier 1 → "Direkt yap, plan yazma" + TodoWrite tek item
Tier 2 → `docs/TODO.md`'ye 1-2 satır + iş yap
Tier 3 → **bu komutun ana akışı (aşağı devam)**

### 2. Bağlam Topla (Tier 3)

Paralel oku:
- `CLAUDE.md` — proje kısıtları
- `docs/MASTER_ROADMAP.md` — feature hangi modül + hangi faz
- `docs/COMPETITOR_ANALYSIS.md` — rakipler bu feature'a nasıl yapıyor
- `docs/MODULE_SPECS/M<NN>_*.md` — varsa modül spec
- Son journal: `docs/journal/YYYY-MM-DD.md`

### 3. Code-Architect Agent ile Blueprint

```
subagent_type: code-architect
description: Feature mimari blueprint
prompt:
  Operax için yeni feature mimari blueprint üret:

  Feature: $ARGUMENTS

  Hedef Modül: <MASTER_ROADMAP'ten>
  Paket: <STARTER/WMS_PRO/...>

  Mevcut pattern referansı için benzer modülün dosyalarını incele
  (örn. M11 banka mutabakatı → M11 mevcut Cheque/Loan UI pattern'ini referans).

  Blueprint'i .claude/agents/code-architect.md formatında ver:
  1. Bulunan pattern'ler + konvansiyonlar (file:line)
  2. Mimari karar + trade-off
  3. Component tasarımı (Tablo, SP, PageModel, View)
  4. Data flow
  5. Implementation sırası (faz check listesi)
  6. Kritik detaylar (error, security, transaction)
```

### 4. Plan Dosyası Oluştur

Blueprint çıktısını `plans/feature-template.md`'ye fitle:

```bash
# Son plan ID
ls plans/*.md | grep -E '^plans/[0-9]' | tail -1
# Yeni: NN + 1
cp plans/feature-template.md plans/<NN>-<slug>.md
```

Doldur:
- §1 Problem — 2-4 cümle
- §2 Scope (dahili + dışı + etkilenen dosyalar)
- §3 Alternatifler (en az 2 reddedilen + seçilen) + 5 lens kontrolü
- §4 Riskler
- §5 Done criteria (operax-cli migrate 0, dotnet build 0)
- §6 Rollback
- §7 Adımlar (blueprint'ten kopyala)
- §8 İlişkili
- §9 Onay

### 5. LLM Council (Opsiyonel)

Eğer alternatif kararsız veya scope büyük (>10 dosya):
```
/council-this "Bu feature'ı X yaklaşımı mı Y mı?"
```
Council Verdict'ı plan §3 Alternatifler'e işle.

### 6. Onay Bekle

Plan dosyasını kullanıcıya göster:
```
plans/<NN>-<slug>.md hazır. Bu plan kapsamına onay verir misin?

Özet:
- N faz
- ~X dosya
- ~Y satır
- Done: ...

Onay = Faz 1 başlar. Düzenle = ben düzeltirim. İptal = arşive.
```

### 7. TodoWrite + TODO.md

Onay sonrası:
1. **TodoWrite** her faz için item (Faz 1 in_progress)
2. **`docs/TODO.md`** "Aktif Planlar" bölümüne ekle:
   ```markdown
   ### Plan NN — <başlık>
   - [ ] Faz 1 · <açıklama> (plan: NN)
   - [ ] Faz 2 · ...
   ```
3. Faz 1 implementation'ı başlat

## Tier 1/2 Hızlı Akış

**Tier 1:** "Bu trivial, plan yazma, direkt yap" → TodoWrite tek item

**Tier 2:** `docs/TODO.md` "Faz X" altına 1-2 satır + TodoWrite → direkt iş

## Kullanım

```
/feature-dev M11 banka mutabakatı ekranı
/feature-dev M03 RFQ teklif yönetimi
/feature-dev Sidebar collapse animasyonu
```

## İlişkili

- `.claude/rules/plan-first.md` — Tier sistemi
- `plans/feature-template.md` — Tier 3 şablon
- `.claude/agents/code-architect.md` — Blueprint üretici
- `.claude/skills/llm-council/` — Alternatif değerlendirme
- `.claude/skills/plan-tracker/` — TODO + plan + TodoWrite sync
