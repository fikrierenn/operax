---
name: plan-tracker
description: TodoWrite in-session durumuyla docs/TODO.md ve plans/NN-*.md dosyalarını eş zamanlı senkronda tutar. Çok adımlı bir iş planlandığında maddeleri TODO.md ve plan dosyasına kalıcı yazar; her iş bitince [ ] → ✅ + commit hash ile işaretler. Kullanıcı "planla", "todo güncelle", "bu işleri kaydet" dediğinde devreye gir.
allowed-tools: Read, Edit, Write, Bash, Grep
user-invocable: true
model: inherit
---

# plan-tracker Skill (Operax)

## Amaç

TodoWrite **in-session geçici** durum tutar: `/clear`, oturum sonu, compact sonrası kaybolur. `docs/TODO.md` ve `plans/NN-*.md` **kalıcı** ama elle güncellemek zorunlu. Bu skill ikisini eş tutar.

## Ne zaman tetikle

**Devreye gir:**
- Kullanıcı 3+ adımlı plan tanımlıyor
- Bir madde tamamlandı (commit sonrası)
- Oturum başında ("günaydın", "nerede kaldık")
- Plan dosyası onaylandı → adımları TODO.md + TodoWrite'a aktar

**Devreye girme:**
- Tek adımlı trivial iş
- Soru-cevap akışı

## Üç Katman

1. **TodoWrite** — in-session, hız için
2. **plans/NN-<slug>.md** — Tier 3 plan dosyası, bölüm 7 "Adımlar"
3. **docs/TODO.md** — kalıcı master listesi

Üçü her zaman birlikte güncellenir.

## Adım Adım

### Mod 1: Plan onaylandı (Tier 3 start)

1. **plans/NN-<slug>.md** §7 Adımları oku
2. **TodoWrite** çağır: her adım için `{ content, activeForm, status: "pending" }`. Faz 1 ilk adımı `in_progress`.
3. **docs/TODO.md** "Aktif Planlar" bölümüne ekle:
   ```markdown
   ### Plan 01 — STARTER Paketinin Canlıya Alınması
   - [ ] Faz 1 · sp_CreateLoan multi-method (plan: 01)
   - [ ] Faz 2 · sp_ReceivingPost ItemCost wire (plan: 01)
   ...
   ```

### Mod 2: Adım tamamlandı (commit sonrası)

1. **Commit hash al:** `git rev-parse --short HEAD`
2. **TodoWrite** güncelle: o adımı `completed`
3. **plans/NN-*.md** §7'de o adımı `[x]` işaretle + commit hash ekle
4. **docs/TODO.md** o satırı `[x] ✅ commit <hash>` ile güncelle
5. Sonraki `pending` TodoWrite'da `in_progress` yap

### Mod 3: Yarım kaldı (oturum sonu)

- TodoWrite `in_progress` → kal
- plans/NN-*.md ve TODO.md'de `⏳` emoji + mevcut durum:
  ```
  - [⏳] Faz 3 · sp_CheckPriceVariance UI wire — kısmi: PageModel hazır, view yapılacak
  ```

### Mod 4: Plan tamamlandı

1. plans/NN-*.md §5 Done Criteria check
2. `git mv plans/NN-*.md plans/archive/`
3. docs/TODO.md "Aktif Planlar"'dan kaldır, "Tamamlanan Planlar" altına yaz
4. journal'a özet yaz (session-handoff skill'i çağrılabilir)

## Kurallar

1. **TodoWrite + plan + TODO.md birbirinden kopuk kalmaz**
2. **Commit hash ekleme:** her tamamlanan adımda zorunlu — git history'den detay grep'lenir
3. **Plan dosyası master:** çakışma varsa plan dosyası kazanır
4. **TODO.md commit etme:** skill TODO.md'yi otomatik commit etmez (`session-handoff` yapar)

## İlişkili

- `plans/README.md` — workflow
- `plans/feature-template.md` — şablon
- `.claude/rules/plan-first.md` — Tier sistemi
- `.claude/skills/session-handoff/SKILL.md` — oturum sonu özet
