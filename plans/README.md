# Planlar (Plan-First Sistemi)

Tier 3 işler için zorunlu plan dokümanları. Kural detayı: [`.claude/rules/plan-first.md`](../.claude/rules/plan-first.md).

**Yön belgesi:** [`docs/MASTER_ROADMAP.md`](../docs/MASTER_ROADMAP.md) ve [`docs/COMPETITOR_ANALYSIS.md`](../docs/COMPETITOR_ANALYSIS.md). Yeni plan açmadan önce: STARTER kapsamında mı? Hangi rakip karşılaştırmasında? Faz sırası ne?

## Tier sistemi

| Tier | Plan? | Operax örnek |
|---|---|---|
| **1 — Trivial** | YOK | Typo, label çeviri, version bump |
| **2 — Standard** | TODO satırı yeterli | Tek SP düzeltme, tek view'a badge ekleme |
| **3 — Substantial** | TAM PLAN ZORUNLU | Yeni modül, schema migration, çoklu ekran refactor, STARTER paket aktivasyon |

## Klasör yapısı

```
plans/
├── README.md                # bu dosya
├── feature-template.md      # Tier 3 plan şablonu
├── NN-<slug>.md             # aktif planlar (NN sıralı)
└── archive/
    └── NN-<slug>.md         # tamamlanmış / iptal edilmiş planlar
```

## Workflow

### 1. Plan yaz (Tier 3)
```bash
ls plans/*.md | grep -E '^plans/[0-9]' | tail -1
cp plans/feature-template.md plans/NN-<slug>.md
```

Doldur: Problem, Scope, Alternatifler (en az 2 reddedilen), Riskler, Done criteria, Rollback, Adımlar.

### 2. Onay
Kullanıcıya göster, geri bildirim al. **Onay olmadan implement etme.**

### 3. Implementation
- Commit message: `feat(M11): hesap ekstre (plan: 03)`
- `docs/TODO.md`'de plan adımları (Faz X altında)

### 4. Tamamlanma
- `git mv plans/NN-*.md plans/archive/`
- Done criteria check
- Journal'da plan'in çıktı özeti

## Tier tespit hızlı kontrol

Sezgisel:
- 3+ klasöre dokunma → Tier 3
- Yeni dosya tipi (schema migration, yeni servis, yeni rule) → Tier 3
- Geri alınması zor (DB, security, FK) → Tier 3
- UI/UX/sidebar/route değişiklik → Tier 3
- Mevcut pattern + 1 dosya → Tier 1/2
- Sadece typo / label → Tier 1

Şüphede dururken kullanıcıya sor.

## STARTER kapsam dışı (plan açma)

- e-Belge gönderme (outbound) — ana ERP'ye bırakıldı, biz sadece sync
- Resmi defter/beyanname (Yevmiye, KDV, BA-BS) — M16 üzerinden dış muhasebe
- Üretim (M10), Servis (M12), Marketplace (M16.M1) — STARTER sonrası

## Stale plan disiplini

**14 gün** dokunulmamış aktif plan ya yeniden ısıt ya arşive taşı. Soğukta tutulan plan = soğutulan iş.

## İlişkili

- [`docs/MASTER_ROADMAP.md`](../docs/MASTER_ROADMAP.md) — modül öncelik sıralaması
- [`docs/COMPETITOR_ANALYSIS.md`](../docs/COMPETITOR_ANALYSIS.md) — rakip karşılaştırma matrisi
- [`docs/MODULE_SPECS/`](../docs/MODULE_SPECS/) — modül detay spec'leri
- [`.claude/rules/plan-first.md`](../.claude/rules/plan-first.md) — kural detay
- [`.claude/rules/commit-discipline.md`](../.claude/rules/commit-discipline.md) — Tier 3 commit referansı
