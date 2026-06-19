#!/usr/bin/env bash
# SessionStart hook — Operax projesinde her oturum başında çalışır.
# Çıktısı stdout'a yazılır, Claude bunu additionalContext olarak görür.

set -e

REPO="${CLAUDE_PROJECT_DIR:-D:/Dev/Operax}"
cd "$REPO" 2>/dev/null || exit 0

echo "## Operax — Oturum Başı Özet"
echo ""

# Son 3 gün commit'ler
echo "### Son 3 gün commit'ler"
git log --since='3 days ago' --oneline 2>/dev/null | head -10
echo ""

# Uncommitted dosya sayısı + 15 eşik uyarısı
echo "### Uncommitted dosya sayısı"
count=$(git status --porcelain 2>/dev/null | wc -l | tr -d ' ')
echo "$count dosya"
if [ "$count" -gt 15 ]; then
    echo ""
    echo "UYARI: 15 dosya eşiği aşıldı. Yeni iş başlamadan önce commit-split gerek (.claude/rules/commit-discipline.md)."
fi
echo ""

# Aktif plan envanter (plan-first disiplini)
active_plans=$(find plans -maxdepth 1 -name '[0-9]*.md' -type f 2>/dev/null | wc -l | tr -d ' ')
archived_plans=$(find plans/archive -name '*.md' -type f 2>/dev/null | wc -l | tr -d ' ')
echo "### Planlar"
echo "- Aktif: $active_plans · Arşivlenmiş: $archived_plans"

# Stale plan kontrol (14+ gün dokunulmamış)
stale_plans=$(find plans -maxdepth 1 -name '[0-9]*.md' -type f -mtime +14 2>/dev/null)
if [ -n "$stale_plans" ]; then
    stale_count=$(echo "$stale_plans" | wc -l | tr -d ' ')
    echo "- 14+ gün stale: $stale_count plan — yeniden ısıt veya arşive taşı"
    printf '%s\n' "$stale_plans" | head -3 | sed 's/^/  /'
fi
echo ""

# Aktif TODO başlıkları
if [ -f docs/TODO.md ]; then
    echo "### Aktif TODO başlıkları (ilk 12)"
    grep -E '^### |^- \[ \]' docs/TODO.md 2>/dev/null | head -12
    echo ""
fi

# Kod sağlığı sinyalleri
echo "### Kod sağlığı"

# C# hard-limit ihlali (coding-discipline.md: >500 satır)
csharp_over=$(find src/Operax.Web -name '*.cs' -type f -exec wc -l {} + 2>/dev/null | awk '$1 > 500 && $2 != "total"' | sort -rn)
csharp_count=$(printf '%s\n' "$csharp_over" | grep -c '^[[:space:]]*[0-9]' || true)
if [ "$csharp_count" -gt 0 ]; then
    echo "- C# 500+ satır: $csharp_count dosya"
    printf '%s\n' "$csharp_over" | head -3 | sed 's/^/  /'
else
    echo "- C# hard-limit (500): temiz"
fi

# CSS parts size kontrolü (ui-standard.md: <200 satır)
css_over=$(find src/Operax.Web/wwwroot/css/parts -name '*.css' -type f -exec wc -l {} + 2>/dev/null | awk '$1 > 200 && $2 != "total"' | sort -rn)
css_count=$(printf '%s\n' "$css_over" | grep -c '^[[:space:]]*[0-9]' || true)
if [ "$css_count" -gt 0 ]; then
    echo "- CSS parts 200+ satır: $css_count dosya (split gerek)"
else
    echo "- CSS parts (200): temiz"
fi

# Inline style ihlal (ui-standard.md sıfır tolerans renk/font için)
inline_count=$(grep -ro 'style="' src/Operax.Web/Features 2>/dev/null | wc -l | tr -d ' ')
echo "- Inline style attr (.cshtml): $inline_count oluşum"
echo ""

# Son journal
echo "### En son journal girdisi"
last_journal=$(ls -t docs/journal/*.md 2>/dev/null | head -1)
if [ -n "$last_journal" ]; then
    journal_mtime=$(stat -c '%Y' "$last_journal" 2>/dev/null || echo 0)
    now=$(date +%s)
    journal_age=$(( (now - journal_mtime) / 86400 ))
    echo "Dosya: $last_journal ($journal_age gün önce)"
    if [ "$journal_age" -ge 2 ]; then
        echo ""
        echo "UYARI: Journal $journal_age gün eski. Stale claim olabilir — .claude/rules/todo-verification.md."
    fi
    echo ""
    tail -30 "$last_journal"
fi
echo ""

# Kritik dosyalar
echo "### Kritik kurallar"
echo "- Plan-First: .claude/rules/plan-first.md (Tier 3 işlerde plan zorunlu)"
echo "- Evrak bütünlük: .claude/rules/document-immutability.md"
echo "- UI standardı: .claude/rules/ui-standard.md"
echo "- Türkçe UI: .claude/rules/turkish-ui.md"
echo "- Commit disiplini: .claude/rules/commit-discipline.md"

exit 0
