---
name: session-handoff
description: Operax oturum sonu özeti yazar. Bugün yapılanları, build durumunu, yarım kalan işleri, yarına başlangıç noktasını docs/journal/YYYY-MM-DD.md dosyasına yazar. Yazım sonunda journal'i otomatik commit eder (sadece journal dosyası — başka dosyaya dokunmaz). Kullanıcı "handoff", "oturum sonu", "iyi geceler", "kaydet ve kapat", "günaydın özet" gibi ifadeler kullandığında veya /handoff çalıştırıldığında devreye gir.
allowed-tools: Read, Edit, Write, Bash, Grep, Glob
user-invocable: true
model: inherit
---

# Operax Oturum Devir Skill'i

## Amaç
Her oturum sonunda (veya başlangıçta özet almak için), gün içinde olanları kalıcı bir journal dosyasına yazar **ve journal'i otomatik commit eder**. Böylece:
- CLAUDE.md'ye session log yazılmaz (temizlik korunur)
- Yarınki Claude ne olduğuna bakar (SessionStart hook zaten okuyor)
- Geçmiş kararlar grep'lenebilir
- Journal sürekli uncommitted durumda asılı kalmaz

## Kaynak Dosya
`docs/journal/YYYY-MM-DD.md` — tarih format'ı `%Y-%m-%d`. Eğer dosya yoksa oluştur, varsa append.

## Çıktı Şablonu

```markdown
# Oturum Günlüğü — YYYY-MM-DD

## Ana Konu
<1-2 cümle: bu oturumda asıl hedef neydi>

## Tamamlananlar
- Madde 1 (dosya:line referansı varsa ekle)
- Madde 2
- ...

## Build / Test Durumu
- Build: yeşil / kırmızı / çalıştırılmadı
- Test: X yeşil, Y kırmızı / çalıştırılmadı
- Smoke test: yapıldı / yapılmadı / kırıldı

## Commit Durumu
- Uncommitted dosya sayısı: N
- Yeni commit'ler: <varsa liste>
- Commit beklemede: <varsa>

## Aktif Planlar
- plans/NN-<slug>.md — Faz X tamamlandı / Y kaldı

## Yarım Kalan / Yarın'a Bırakılan İşler
- Madde 1 — neden yarım, nereden devam
- Madde 2
- ...

## Kararlar
- <Bu oturumda alınan mimari/UX/teknik kararlar>
- <Plan dosyasına işlendi mi? Hangi plan?>

## Dikkat Edilmesi Gerekenler
- <Memory hatası, yanlış varsayım, düzeltme gerektirecek noktalar>

## Yarına Başlangıç Noktası
1. <En kritik 1. adım>
2. <2. adım>
3. <3. adım>
```

## Adım Adım

### Adım 1 — Bilgi Topla
```bash
date +%Y-%m-%d
git status --porcelain | wc -l
git log --since=midnight --oneline
find plans -maxdepth 1 -name '[0-9]*.md' -type f
```

### Adım 2 — Build Durumu
```bash
dotnet build src/Operax.Web/Operax.Web.csproj 2>&1 | tail -3
```

### Adım 3 — Journal Yaz
- `docs/journal/YYYY-MM-DD.md` dosyasını oluştur veya append
- Şablona göre 8 bölümü doldur
- Commit'leri post-commit-journal.sh zaten otomatik ekledi — yeni başlık ekleme

### Adım 4 — Aktif Plan Durumu Güncelle
- Her aktif `plans/NN-*.md` dosyasının "Adımlar" bölümünde yapılan adımları `[x]` işaretle
- Plan tamamlandıysa `git mv plans/NN-*.md plans/archive/`

### Adım 5 — Commit (sadece journal + plan)
```bash
git add docs/journal/$(date +%Y-%m-%d).md plans/
git commit -m "docs: oturum sonu özeti $(date +%Y-%m-%d)"
```

## Kurallar

1. **Sadece journal + plan dosyalarına dokun.** Diğer uncommitted dosyalar kullanıcının/önceki commit'lerin sorumluluğu.
2. **CLAUDE.md'ye log yazma.** Statik kimlik dosyasıdır.
3. **Build durumu yalandan "yeşil" deme.** Çalıştır, gerçek sonucu yaz.
4. **Plan referansları:** Aktif planı işaretliyorsan plan dosyası adını ve faz numarasını ver.

## İlişkili

- `.claude/hooks/session-start.sh` — yarınki oturum bu journal'ı okuyacak
- `.claude/skills/plan-tracker/SKILL.md` — plan + TODO sync
- `.claude/rules/session-protocol.md` — oturum protokol detay
- `.claude/rules/plan-first.md` — Tier 3 plan disiplini
