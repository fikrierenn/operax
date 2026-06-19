#!/usr/bin/env bash
# Post-edit antipattern WARN — Operax PostToolUse hook (Edit|Write).
#
# Tetikleyici: settings.json PostToolUse, matcher = "Edit|Write|MultiEdit".
# Az önce yazılan .cs/.cshtml dosyasında kritik antipattern'leri tarar ve
# UYARIR (commit'i beklemeden hızlı geri-bildirim). BLOKLAMAZ — daima exit 0;
# commit-gate (pre-commit-antipattern.sh) bloklayan gerçek kapıdır.
#
# Env: CLAUDE_POSTEDIT_SKIP=1 -> taramayı atla.

set -e
[ "${CLAUDE_POSTEDIT_SKIP:-0}" = "1" ] && exit 0

input=$(cat)

# Yazılan dosya yolunu çek (Edit/Write/MultiEdit → tool_input.file_path)
if command -v jq >/dev/null 2>&1; then
  f=$(echo "$input" | jq -r '.tool_input.file_path // ""')
else
  f=$(echo "$input" | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
fi

[ -z "$f" ] && exit 0
[ -f "$f" ] || exit 0
case "$f" in
  *.cs|*.cshtml) ;;
  *) exit 0 ;;
esac

warns=()

if [[ "$f" == *.cs ]]; then
  grep -qnE 'DateTime\.Now\b' "$f" 2>/dev/null && warns+=("DateTime.Now -> DateTime.UtcNow (timezone)")
  grep -qnE '^\s*catch\s*\{|^\s*catch\s*\(\s*\)' "$f" 2>/dev/null && warns+=("Bare catch -> logger zorunlu (silent failure)")
  grep -qnE 'new HttpClient\(\)' "$f" 2>/dev/null && warns+=("new HttpClient() -> IHttpClientFactory")
  grep -qnE 'DataTable\.Compute' "$f" 2>/dev/null && warns+=("DataTable.Compute -> NCalc (injection)")
  grep -qnE 'ex\.Message' "$f" 2>/dev/null | head -1 >/dev/null && grep -qnE '(TempData\[[^]]*\]\s*=\s*[^;]*ex\.Message|return[^;]*ex\.Message)' "$f" 2>/dev/null && warns+=("ex.Message user'a sızıntı -> generic mesaj")
fi

if [[ "$f" == *.cshtml ]]; then
  grep -qnE 'style="[^"]*(color|font-|background-color|border-color|box-shadow)' "$f" 2>/dev/null && warns+=("Inline style (renk/font) -> semantic class")
fi

if [ ${#warns[@]} -gt 0 ]; then
  echo "uyari post-edit antipattern ($f):" >&2
  for w in "${warns[@]}"; do echo "  ~ $w" >&2; done
  echo "  (uyari — blok degil; commit-gate bloklar)" >&2
fi

exit 0
