#!/usr/bin/env bash
# PreCompact hook — /compact öncesi aktif TODO + son commitler journal'a eklenir.
# Bağlamdan önce ne üzerinde çalışıldığının snapshot'ı otomatik kaydedilir.
# Tetik: settings.json PreCompact eventi.

set -e
cd "$(git rev-parse --show-toplevel 2>/dev/null || echo "$PWD")"

today=$(date +%Y-%m-%d)
journal="docs/journal/$today.md"
mkdir -p docs/journal

if [ ! -f "$journal" ]; then
  echo "# Oturum Günlüğü --- $today" > "$journal"
  echo "" >> "$journal"
fi

ts=$(date +%H:%M)

{
  echo ""
  echo "## Compact Snapshot --- $ts"
  echo ""
  echo "### Son 5 Commit"
  echo ""
  git log --oneline -5 2>/dev/null | sed 's/^/- /' || echo "- (git log başarısız)"
  echo ""
  echo "### Uncommitted Dosya Sayısı"
  echo ""
  count=$(git status --porcelain 2>/dev/null | wc -l | tr -d ' ')
  echo "- $count uncommitted dosya"
  echo ""
  echo "### Aktif Planlar"
  echo ""
  for plan in plans/[0-9]*.md; do
    [ -f "$plan" ] && echo "- $(basename "$plan")"
  done
  echo ""
  echo "### Aktif TODO (ilk 15 açık madde)"
  echo ""
  if [ -f docs/TODO.md ]; then
    grep '^\- \[ \]' docs/TODO.md 2>/dev/null | head -15 | sed 's/^/- /' || echo "- (TODO.md okunamadı)"
  else
    echo "- (docs/TODO.md yok)"
  fi
  echo ""
} >> "$journal"

exit 0
