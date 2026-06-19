#!/usr/bin/env bash
# Pre-commit antipattern scan — Operax PreToolUse hook.
#
# Tetikleyici: settings.json PreToolUse, matcher = "Bash", command ~= "^git commit".
# Script git commit komutu tespit ederse staged .cs/.cshtml dosyalarda
# kritik antipattern'leri arar ve exit 2 ile commit'i bloklar.
#
# Cikis kodlari:
#   0 -> commit devam etsin (check passed veya git commit komutu degil)
#   2 -> commit BLOKLA (antipattern bulundu, stdout'taki mesaj user'a gider)

set -e
cd "$(git rev-parse --show-toplevel 2>/dev/null || echo "$PWD")"

input=$(cat)

# Tool command'u cek
if command -v jq >/dev/null 2>&1; then
  cmd=$(echo "$input" | jq -r '.tool_input.command // ""')
else
  cmd=$(echo "$input" | sed -n 's/.*"command"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
fi

# Sadece git commit komutlarini hedefle
if ! echo "$cmd" | grep -qE '(^|[[:space:]&;])git[[:space:]]+commit([[:space:]]|$)'; then
  exit 0
fi

# Staged dosyalari al
staged=$(git diff --cached --name-only --diff-filter=ACM 2>/dev/null || true)
[ -z "$staged" ] && exit 0

# Sadece C# + Razor + Config dosyalari
targets=$(echo "$staged" | grep -E '\.(cs|cshtml|json|sql)$' || true)
[ -z "$targets" ] && exit 0

found_issues=()

for f in $targets; do
  [ -f "$f" ] || continue

  # C# antipattern'leri
  if [[ "$f" == *.cs ]]; then
    # DateTime.Now -> DateTime.UtcNow
    if grep -Hn 'DateTime\.Now\b' "$f" 2>/dev/null | grep -v '^\s*//' | grep -v '/// '; then
      found_issues+=("$f: DateTime.Now -> DateTime.UtcNow kullan (timezone sorunu)")
    fi
    # async void
    if grep -Hn 'async void\b' "$f" 2>/dev/null | grep -v 'event' | grep -v '^\s*//'; then
      found_issues+=("$f: async void (event handler harici yasak)")
    fi
    # new HttpClient
    if grep -Hn 'new HttpClient()' "$f" 2>/dev/null | grep -v '^\s*//'; then
      found_issues+=("$f: new HttpClient() -> IHttpClientFactory")
    fi
    # ex.Message user'a sizinti
    if grep -HnE '(TempData\[.*\]\s*=\s*[\$"][^;]*ex\.Message|Json\(\s*new\s*\{[^}]*ex\.Message|return\s+[^;]*ex\.Message)' "$f" 2>/dev/null; then
      found_issues+=("$f: ex.Message user'a sizintili (logger'a yaz, user'a generic mesaj)")
    fi
    # Console.WriteLine
    if grep -Hn 'Console\.WriteLine' "$f" 2>/dev/null | grep -v '^\s*//' | grep -v 'Operax.Cli'; then
      found_issues+=("$f: Console.WriteLine production'da yasak (_logger.LogDebug kullan)")
    fi
    # Bare catch
    if grep -HnE '^\s*catch\s*\{|^\s*catch\s*\(\s*\)' "$f" 2>/dev/null; then
      found_issues+=("$f: Bare catch block (silent failure — logger zorunlu)")
    fi
    # DataTable.Compute (NCalc kullan)
    if grep -Hn 'DataTable\.Compute' "$f" 2>/dev/null; then
      found_issues+=("$f: DataTable.Compute yasak (formula injection — NCalc kullan)")
    fi
    # SQL string concat (basic detection)
    if grep -HnE '(ExecuteAsync|QueryAsync|QuerySingleAsync)\s*\(\s*"[^"]*"\s*\+' "$f" 2>/dev/null; then
      found_issues+=("$f: SQL string concat (SQL injection - Dapper parametre kullan)")
    fi
  fi

  # Razor antipattern'leri
  if [[ "$f" == *.cshtml ]]; then
    # @Html.Raw kullanıcı verisi (heuristic)
    if grep -HnE '@Html\.Raw\([^)]*(Request|TempData|Model\.\w+(Code|Note|Description|Text|Name))[^)]*\)' "$f" 2>/dev/null; then
      found_issues+=("$f: @Html.Raw user veri ile XSS riski (auto-encode kullan)")
    fi
    # Inline style with color/font
    if grep -HnE 'style="[^"]*(color|font-|background-color|border-color|box-shadow)' "$f" 2>/dev/null; then
      found_issues+=("$f: Inline style (renk/font) — semantic class kullan (ui-standard.md)")
    fi
  fi

  # Hardcoded sifre (tum dosya tipleri)
  if grep -HnE 'Password\s*=\s*[A-Za-z0-9!@#$%^&*+._-]{4,}' "$f" 2>/dev/null | grep -v 'Password=["'\'']?\s' | grep -v 'Password=\$' | grep -v '\.example'; then
    found_issues+=("$f: hardcoded sifre tespit (env var / User Secrets kullan)")
  fi

  # SQL antipattern'leri
  if [[ "$f" == *.sql ]]; then
    # Bare UPDATE (WHERE yok — kritik)
    if grep -HnE '^\s*UPDATE\s+\w+\s+SET' "$f" 2>/dev/null | grep -v 'WHERE'; then
      # Bu basit, multi-line UPDATE'leri yakalamaz ama yakaladığı kritik
      # Manuel kontrol gerek
      :
    fi
    # GETDATE() (UTC yok)
    if grep -HnE 'GETDATE\(\)' "$f" 2>/dev/null; then
      found_issues+=("$f: GETDATE() yerine GETUTCDATE() kullan (timezone)")
    fi
  fi
done

if [ ${#found_issues[@]} -gt 0 ]; then
  echo "=== PRE-COMMIT ANTIPATTERN SCAN: BLOKLANDI ===" >&2
  for issue in "${found_issues[@]}"; do
    echo "  X $issue" >&2
  done
  echo "" >&2
  echo "Commit iptal. Once issue'lari duzelt, sonra tekrar commit'le." >&2
  echo "Override gerekiyorsa: git commit --no-verify (kural ihlali — journal'a yaz)." >&2
  exit 2
fi

exit 0
