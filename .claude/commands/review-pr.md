---
description: "Operax PR review — code-reviewer + security-reviewer + silent-failure-hunter paralel kapsamlı denetim"
argument-hint: "[range] (boş = uncommitted, HEAD~5..HEAD, origin/main..HEAD)"
allowed-tools: ["Bash", "Glob", "Grep", "Read", "Task"]
---

# /review-pr — Operax Kapsamlı PR Review

3 paralel agent ile son değişiklikleri kural setine göre kapsamlı denetler. `/security-check`'ten farkı: güvenlik + silent failure'ın yanında **kod kalitesi + Türkçe UI + UI standardı + evrak bütünlüğü** kontrolleri de yapar.

**Range:** "$ARGUMENTS"

## Workflow

### 1. Range + Scope Tespiti

```bash
RANGE="$ARGUMENTS"
[ -z "$RANGE" ] && RANGE="HEAD~1..HEAD"

git log --oneline "$RANGE" 2>/dev/null
git diff "$RANGE" --stat
```

Scope kategorile:
- PageModel (.cshtml.cs)
- View (.cshtml)
- SP/Schema (docs/sql/)
- Lib/Config (src/Operax.Web/Lib/, Program.cs)
- CSS (wwwroot/css/parts/)
- Doc/Plan (docs/, plans/, .claude/)

### 2. Paralel Agent — TEK MESAJDA

#### Agent 1 — `code-reviewer`

```
subagent_type: code-reviewer
description: Kod kalitesi denetimi
prompt:
  Operax range '<RANGE>' kapsamında kod review.
  Scope: git diff <RANGE>.

  Kurallar (sıralı):
  - CLAUDE.md fihrist
  - .claude/rules/csharp-conventions.md (300/500 satır, Dapper, primary ctor)
  - .claude/rules/coding-discipline.md (Türkçe yorum, 80-satır metot, guard clause)
  - .claude/rules/razor-conventions.md (page iskeleti, form pattern)
  - .claude/rules/turkish-ui.md (UI dili çift dil L.T)
  - .claude/rules/ui-standard.md (semantic class, partial katalog)
  - .claude/rules/inline-style-guard.md (renk/font inline yasak)
  - .claude/rules/document-immutability.md (POSTED kilitleme)

  Confidence ≥ 80 olan CRITICAL/HIGH/IMPORTANT döndür.
  file:line + spesifik kural referansı + fix snippet.
```

#### Agent 2 — `security-reviewer`

```
subagent_type: security-reviewer
description: Güvenlik denetimi
prompt:
  Operax range '<RANGE>' güvenlik audit.
  Odak: 12 mutlak kural (security-principles.md), SQL injection, XSS,
  CSRF, secret leak, mass assignment, evrak bütünlüğü.
  Confidence ≥ 80, file:line + saldırı senaryosu + fix.
```

#### Agent 3 — `silent-failure-hunter`

```
subagent_type: silent-failure-hunter
description: Error handling denetimi
prompt:
  Operax range '<RANGE>' silent failure + uygunsuz error handling.
  Odak: bare catch, ex.Message leak, log eksik, fallback masking,
  SqlException specific handling, OperationCanceled rethrow.
```

### 3. Sentez

```markdown
# PR Review Özeti — <range>

**Scope:** N dosya, K commit
**Tarih:** YYYY-MM-DD

## CRITICAL Bulgular (3/3 doğruladı veya kanıt güçlü)
### CRIT-1 · <başlık>
- **Agent:** ...
- **File:line:** ...
- **Kural:** ...
- **Fix:** ...

## HIGH Bulgular
### HIGH-1 · <başlık> (2/3)
...

## IMPORTANT (kalite, tek agent)
...

## POSITIVE (doğru pratik örnekleri)
- file:line — ...

## Karar Noktası
- **A)** CRITICAL + HIGH hemen fix (~Xh)
- **B)** Sadece CRITICAL acil, HIGH plan dosyasına
- **C)** Tümünü docs/TODO.md'ye, plan tamamlandıktan sonra
```

### 4. TodoWrite Takip

Fix kararı alınırsa her bulgu için TodoWrite item + `docs/TODO.md`'ye paralel kayıt.

## Operax Özel Kontroller

Her agent şunları da denetler:

- **`L.T("tr", "en")` çift dil:** UI metni var ama L.T yok mu?
- **`CompanyId` filtresi:** Her Dapper WHERE'de var mı?
- **`_PageHeader` partial:** Sayfa başında kullanıldı mı?
- **`data-screen-label`:** Sayfa wrapper'ında set mi?
- **SP THROW:** İş kuralı hataları 50000-59999 aralığında Türkçe mi?
- **`sp_ValidateStatusTransition`:** Status geçişi bu motoru kullanıyor mu?

## Kullanım

```
/review-pr                          # uncommitted + son commit
/review-pr HEAD~5..HEAD             # son 5 commit
/review-pr origin/main..HEAD        # PR'ın main'den farkı
```

## İlişkili

- `/security-check` — sadece güvenlik (3 agent dar scope)
- `/feature-dev` — yeni feature başlatma akışı
- `.claude/agents/*` — agent tanımları
