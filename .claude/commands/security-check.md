---
description: "Operax güvenlik denetimi — security-reviewer + silent-failure-hunter + OWASP sweep paralel"
argument-hint: "[range] (örn. HEAD~5..HEAD veya boş = uncommitted)"
allowed-tools: ["Bash", "Glob", "Grep", "Read", "Task"]
---

# /security-check — Operax Güvenlik Denetimi

3 paralel agent ile son değişiklikleri `.claude/rules/security-principles.md` checklist'ine göre tarar. Range parametresi opsiyonel — verilmezse `HEAD~1..HEAD` + uncommitted scope alır.

**Range:** "$ARGUMENTS"

## Workflow

### 1. Range Tespiti

```bash
RANGE="$ARGUMENTS"
[ -z "$RANGE" ] && RANGE="HEAD~1..HEAD"

git log --oneline "$RANGE" 2>/dev/null
git diff "$RANGE" --name-only
git status --short
```

Çıktı:
- Commit listesi (kaç commit, hangi konular)
- Değişen dosya listesi (kategorize: PageModel / View / SP / Schema / Config)
- Uncommitted dosyalar

Scope **0 dosya** → "Taranacak değişiklik yok." dön + çık.
Scope **>150 dosya** → "Scope çok büyük. Daraltma öner:" + son 3 commit yaklaşımı sun.

### 2. Üç Paralel Agent — TEK MESAJDA

#### Agent 1 — `security-reviewer`

```
subagent_type: security-reviewer
description: Operax güvenlik denetimi
prompt:
  Operax son değişiklikleri range '<RANGE>' kapsamında güvenlik audit'i yap.
  Scope: git diff <RANGE> çıktısı.

  Odak (security-principles.md 12 mutlak kural):
  1. SQL injection (Dapper parametre, string concat yasak)
  2. XSS (@Html.Raw user input, inline JS interpolasyon)
  3. CSRF ([Authorize] + AntiForgery)
  4. Open redirect (Url.IsLocalUrl + StartsWith)
  5. Secret yönetimi (appsettings.json plain text)
  6. Cookie sertleştirme
  7. Exception (ex.Message user'a YASAK)
  8. CompanyId multi-tenant disiplini
  9. Password hashing (Identity default)
  10. NCalc (DataTable.Compute yasak)
  11. Mass assignment ([BindNever], DTO bind)
  12. Evrak bütünlüğü (POSTED kilitleme guard)

  Output: file:line + saldırı senaryosu + kanıt + fix snippet.
  Confidence ≥ 80 olan CRITICAL/HIGH döndür.
  0 bulgu varsa "0 CRITICAL / 0 HIGH" yaz.
```

#### Agent 2 — `silent-failure-hunter`

```
subagent_type: silent-failure-hunter
description: Silent failure denetimi
prompt:
  Operax son değişiklikleri range '<RANGE>' kapsamında silent failure
  ve uygunsuz error handling ara.

  Odak:
  - Boş catch block
  - catch swallow + log only (caller'a fail bilgisi yok)
  - .catch(() => {}) JS sessiz fail
  - Task döner exception yutar
  - Fallback / retry kullanıcıya bilgi vermeden
  - SqlException catch ama spesifik exception yok
  - ex.Message user'a sızıyor

  Kural referansı: .claude/rules/error-handling.md + security-principles.md §7

  Output: file:line + Hidden Errors listesi + User Impact + fix snippet.
```

#### Agent 3 — OWASP Sweep (`general-purpose` veya `code-reviewer`)

```
subagent_type: code-reviewer
description: OWASP checklist sweep
prompt:
  Operax son değişiklikleri range '<RANGE>' için OWASP Top 10 ve
  defansif security pratik checklist'i ile tara.

  Range: git diff <RANGE>
  Read first:
  - .claude/rules/security-principles.md (12 mutlak kural)
  - .claude/skills/code-quality-checklist/ (proaktif kurallar)
  - .claude/rules/document-immutability.md (evrak kilitleme)

  Operax özelinde dikkat:
  - Dapper SP çağrısı: commandType + parametre
  - Çift dil L.T() helper user mesajlarında
  - Single-tenant CompanyId disiplini
  - Hangfire job exception handling
  - sp_ValidateStatusTransition motoru

  Output:
  - CRITICAL (exploitable, kanıt + saldırgan + sömürü adımı)
  - HIGH (defense-in-depth)
  - MEDIUM (best practice)
  - POSITIVE (3-5 örnek)
  - Genel postür 1-3 cümle
  Max 1200 kelime.
```

### 3. Bulguları Birleştir

```markdown
# Security Check Özeti — <range>

**Scope:** N dosya, K commit
**Tarih:** YYYY-MM-DD

## Kesişen bulgular (2/3 veya 3/3 doğruladı)

### HIGH-1 · <başlık> (3/3)
- **File:line:** ...
- **Kanıt:** ...
- **Fix:** ...

## Tek agent bulguları

### HIGH-N · <başlık> (security-reviewer)
...

## MEDIUM
- file:line — kısa not

## POSITIVE
- file:line — doğru pratik

## Karar noktası
- **A)** En kritik N HIGH'i hemen fix — ~Xh
- **B)** Sadece kritik bir/iki acil
- **C)** Tüm bulguları docs/TODO.md'ye, başka iş yap
```

### 4. TodoWrite Takip

Kullanıcı fix etmeye karar verirse `TodoWrite` ile her bulgu ayrı item.

## Kullanım Örnekleri

```
/security-check
/security-check HEAD~5..HEAD
/security-check origin/main..HEAD
```

## Notlar

- **Paralel zorunlu.** 3 agent tek mesajda Task tool ile başlat
- **Range zorunlu doğrula.** Boş scope → çık. Çok büyük → sor
- **Sonuç birleştirilmeden çıkma.** 3 ayrı rapor → sentez → tek özet + karar noktası

## İlişkili

- `.claude/agents/security-reviewer.md`
- `.claude/agents/silent-failure-hunter.md`
- `.claude/agents/code-reviewer.md`
- `.claude/rules/security-principles.md` (anayasa)
- `.claude/skills/code-quality-checklist/`
