---
name: yetenek-uret
description: Operax çatısına yeni yetenek (skill / agent / hook / rule) SİSTEMATİK üretir veya mevcut olanı GÜNCELLER. footprint-ladder en-dar-basamak seçimi + convention-uyumlu scaffold + kayıt (hook→settings.json, agent→agent-usage matrisi) + test + onay. "yeni skill yap", "bu skill'i güncelle/iyileştir", "X için agent", "hook ekle/oluştur", "yetenek üret", "kendini geliştir", "çatıyı geliştir", "/yetenek-uret" denildiğinde tetiklenir. İNSAN-TETİKLİ (otonom değil). Her üretim/güncelleme kullanıcı onayı + git-revert'lenebilir.
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Agent, AskUserQuestion
user-invocable: true
model: inherit
---

# Yetenek Üret — Çatı Self-Development (insan-tetikli)

> pusula'dan uyarlandı. Otonom DEĞİL: sen tetikleyince yeni yeteneği DOĞRU basamakta, convention-uyumlu, kayıtlı ve test'li üretir/günceller. Üçlü döngünün "üret/güncelle" ayağı: `/learn` (sorun→kural) + **yetenek-uret** (üret/güncelle) + memory consolidation (bakım/budama).

## 0. İlk Soru — Gerçekten Yeni Yüzey mi? (footprint-ladder ZORUNLU)

Üretmeden ÖNCE merdiveni aşağıdan sor (`.Codex/rules/footprint-ladder.md`):
1. Mevcut SP/rule/skill/servisi **genişletmek** çözüyor mu? → yeni dosya AÇMA.
2. Çözmüyorsa: skill < rule < agent < hook < SP/şema < Razor sayfa — EN DAR basamak.
3. Şüphede → kullanıcıya sor: "Bu yeni X mi, yoksa mevcut Y'yi genişletme mi?"

**Anti-pattern:** her ihtiyaca yeni skill/agent refleksi. Tek-kullanımlık iş → inline çöz.

## 1. İhtiyaç Netleştir
- Ne tetikleyecek? (kullanıcı ifadesi / olay / dosya türü)
- Tek seferlik mi tekrar eden mi? (tek seferlik → skill DEĞİL)
- Hangi tip: **skill** (tekrarlı iş akışı) · **agent** (özelleşmiş salt-okuma/üretim alt-iş) · **hook** (mekanik olay-tetikli enforcement) · **rule** (kalıcı davranış).

## 2. Tipe Göre Scaffold

### Skill → `.Codex/skills/<ad>/SKILL.md`
- YAML frontmatter: `name` (kebab-case), `description` (NE yaptığı + TETİKLEYİCİ ifadeler — recall için kritik), `allowed-tools`, `user-invocable`, `model: inherit`.
- Gövde: amaç, adımlar, anti-pattern, İlişkili. Türkçe (`turkish-ui.md`), kısa (`response-style.md`).
- Tetik ifadelerini description'a AÇIK yaz (skill bunlarla bulunur).

### Agent → `.Codex/agents/<ad>.md`
- Frontmatter ZORUNLU (`agent-usage.md §7`): `name`, `description` (ne zaman + proaktif tetik), `tools`, `model` (+ opsiyonel `color`). Frontmatter yoksa = ölü ajan.
- **Model tier** (`agent-usage.md §2`): haiku (mekanik/tarama) · sonnet (analiz/review) · opus (derin/yüksek-risk). Bilinçli seç.
- Salt-okuma denetçi → Edit/Write VERME (`agent-usage.md §5`).
- Prompt disiplini: Görev / Scope / YAPMAYACAKLARIN / Done / Raporla (kanıt+confidence).
- `.Codex/rules/agent-usage.md §3` iş→ajan matrisine satır ekle.

### Hook → `.Codex/hooks/<ad>.sh` + `.Codex/settings.json` WIRE
- Event: SessionStart (bağlam enjekte) · PreToolUse (blok/gate) · PostToolUse · PreCompact · Stop (dikkat: her turda fire, gürültü).
- Script: `set -e`, `cd "$(git rev-parse --show-toplevel)"`, stdin JSON (jq + sed fallback), exit kodları (0=geç, 2=blok+stderr).
- **Operax-adapte ZORUNLU** (jenerik şablonu körü körüne wire ETME): güvenlik (şifre/`ex.Message`/boş catch) = BLOK; stil/konvansiyon (inline-style, magic-string) = UYAR. `DateTime.Now` Operax'ta yasak (timezone) → bunu BLOK değil belirgin uyar.
- settings.json'a ekle (event matcher). **Wire etmeden hook ölüdür.**

### Rule → `.Codex/rules/<konu>.md`
- `paths:` YOK (compact-survival).
- Başına katman etiketi: "Rule katmanı: core (her oturum) / on-demand (konu tetiklenince)".
- core ise AGENTS.md hızlı-referansına + ilişkili rule'lara çapraz-ref.

## 3. Kayıt + Keşfedilebilirlik
- Hook → settings.json (yoksa fiilen çalışmaz).
- Skill/agent → otomatik keşfedilir (dosya yeterli) ama description tetikleyici-zengin olmalı.
- Yeni core rule → AGENTS.md'ye 1-satır pointer (AGENTS.md sadece kimlik+indeks, log YAZMA).
- Yeni agent → `agent-usage.md §3` matrisine satır.

## 4. Test (`test-discipline.md`)
- Hook: `bash -n` syntax + davranış (git-commit-dışı→exit 0, hedef olay→beklenen).
- Kod üreten agent/skill → `build-validator` ile derle (0 hata).
- Skill/rule: tetik ifadesiyle bir kuru-koşu (doğru yükleniyor mu).

## 5. Mevcut Yeteneği GÜNCELLE (self-update)
- Tetik: "bu skill eksik/yanlış", kullanıcı feedback, stale bulgu.
- Aynı workflow: oku → eksiği bul → minimal düzelt (surgical, `coding-discipline.md`) → test → onay.
- Çelişen/eskiyen kural → düzelt veya `note: süperseded`. Silme yerine güncelle.

## 6. Onay + Commit
- Üretim/güncelleme **kullanıcı onayı** olmadan kalıcılaşmaz (footprint maliyeti).
- Commit: `feat(framework): yeni <tip> <ad>` veya `chore(framework): <ad> güncelle`. git-revert'lenebilir.
- `docs/TODO.md`/journal'a not (ne eklendi, neden).

## Guardrails
- ❌ Otonom (onaysız) üretim/değiştirme YOK.
- ❌ Aynı işi yapan ikinci skill/agent (footprint-ladder dup yasağı).
- ❌ Jenerik hook'u Operax-adapte etmeden wire.
- ✅ En dar basamak, convention-uyumlu, test'li, geri-alınabilir.

## İlişkili
- `.Codex/rules/footprint-ladder.md` — basamak seçimi (ZORUNLU ilk adım).
- `.Codex/rules/agent-usage.md` — agent model/rol/prompt + iş→ajan matrisi.
- `.Codex/commands/learn.md` — sorun→kural (kardeş: çıkarım).
- `.Codex/skills/impl-spec/SKILL.md` — büyük basamak (SP/UI) için dosya-dosya spec.
- `.Codex/rules/phase-review-gate.md` — kod üreten yetenek sonrası kapı.
- `.Codex/settings.json` — hook kayıt yeri.
