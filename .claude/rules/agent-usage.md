# Agent Kullanım Disiplini (Ana Ajan Delegasyonu)

Bu kural, ana ajanın (ben) işleri alt-ajanlara (subagent) NASIL dağıtacağını ve hangi model katmanını seçeceğini tanımlar. Amaç: doğru iş → doğru ajan → doğru model. `paths:` yok — compact sonrası da geçerli.

## 1. Temel İlke

**`general-purpose`'u varsayılan yapma.** Her iş için en dar kapsamlı, en uygun özelleşmiş ajanı seç. Özel ajan yoksa `general-purpose` kullan **ama model'i işe göre `model` parametresiyle elle ata** — varsayılana bırakma.

## 2. Model Katmanı Seçimi (ZORUNLU farklılaştırma)

| Model | Ne zaman | Örnek iş |
|---|---|---|
| **haiku** | Mekanik, deterministik, düşük-muhakeme; çok sayıda hızlı tarama | dosya/sembol arama, build/test çalıştır, şema diff, commit-split, basit grep raporu |
| **sonnet** | Dengeli analiz + üretim; orta muhakeme | mimari blueprint, kod review (kural uyumu), web çok-kaynak tarama, orta karmaşık keşif |
| **opus** | Derin muhakeme, çok-adımlı, yüksek-risk, ince hatalı sonuç pahalı | güvenlik review, silent-failure avı, SQL/SP iş-doğruluğu, dış-kaynak derin domain araştırması, mimari karar |

**Kural:** Bir Agent/Task çağrısında model'i bilinçli seç. Şüphedeysen göreve göre yukarı/aşağı kaydır; eşit harcama (her şeye opus / her şeye general-purpose) yasak.

## 3. İş → Ajan Matrisi

| İş türü | Ajan | Model |
|---|---|---|
| "X nerede / Y referansı / keşif" | `code-explorer` | haiku |
| Yeni feature/modül mimari blueprint | `code-architect` | sonnet |
| Uncommitted'i commit'lere böl | `commit-splitter` | haiku |
| Güvenlik denetimi (injection/XSS/CSRF/IDOR/secret) | `security-reviewer` | opus |
| Silent failure / error handling denetimi | `silent-failure-hunter` | opus |
| SP/şema iş-doğruluğu (transaction/THROW/ledger/immutability/PK) | `sql-sp-reviewer` | opus |
| Dış açık-kaynak repo + doküman derin domain araştırması | `reference-researcher` | opus |
| Kural-uyum kod review (Türkçe yorum, 80-satır, UI dili) | `code-reviewer` | sonnet |
| Build derle + hata/uyarı say | `build-validator` | haiku |
| Test çalıştır + raporla | `test-runner` | haiku |
| Şema dosyaları ↔ canlı DB farkı | `db-schema-checker` | haiku |
| T-SQL → PostgreSQL port (SP/TVF/View/sorgu) | `pgsql-porter` | opus |
| Demo/test verisi seed SQL üret (FK-tutarlı, ledger SP'den, demo CompanyId) | `demo-data-builder` | sonnet |
| Statü-vokabüler/yaşam-döngüsü/finansal-araç modelleme kararı (VT-kod uyumsuzluğu, kod kümesi tasarımı) | `erp-isleyis-danismani` | opus |
| Hiçbiri uymuyor (genel çok-adımlı) | `general-purpose` | işe göre elle ata |

## 4. Paralellik ve Fan-out

- Bağımsız işleri **tek mesajda paralel** başlat (birden çok Agent çağrısı).
- Aynı türden çok sayıda hedef (N dosya/N modül) varsa: her birine ayrı dar-kapsamlı haiku/sonnet ajan; sonucu ana ajan sentezler.
- Büyük çok-fazlı orkestrasyon (onlarca ajan) sadece kullanıcı açıkça "workflow" derse veya ultracode açıksa.

### Rol & Derinlik (pusula'dan uyarlandı)

Alt-ajan açarken **rol** yetki ve derinliği sınırlar:

| Rol | Yetki |
|---|---|
| **leaf** (varsayılan) | Salt-işçi. Kendi alt-ajanını AÇAMAZ. Tek görev, izole bağlam. Denetim/araştırma ajanları yazma-tool'suz (§5). |
| **orchestrator** | Alt-ajan açabilir ama derinlik-sınırlı; sonuçları sentezler. (`planner`, ana döngü) |

- **max_concurrent = 3** — aynı anda en fazla 3 paralel alt-ajan. Daha fazla hedef → dalga dalga (3'lü grup).
- **Derinlik ≤ 2** — orchestrator → leaf. Leaf alt-ajan açamaz (sonsuz fan-out engeli). 3. seviye gerekirse ana ajana dön.
- **İzolasyon:** alt-ajan geçmiş konuşmayı GÖRMEZ — yalnızca verilen görev. Parent child özetini bekler (senkron).

## 5. Salt-Okuma Disiplini

- Araştırma/denetim ajanlarına **yazma tool'u verme** (Edit/Write yok). reference-researcher, sql-sp-reviewer, security-reviewer, silent-failure-hunter, code-explorer, code-reviewer, db-schema-checker, build-validator, test-runner, pgsql-porter = read-only (+ Bash gerekiyorsa).
- Yalnızca commit-splitter yazma yapar (Edit + git), o da onayla.

## 6. Çıktı Beklentisi

- Her denetim ajanı **kanıt katmanı** + **confidence** raporlar; tahmin yasak, emin olmadığını "DOĞRULANMADI" der (`.claude/rules/todo-verification.md`).
- Ajan final mesajı ana ajana döner (kullanıcıya değil) → ana ajan özetler.

## 7. Yeni Ajan Eklerken

1. `.claude/agents/<name>.md` — YAML frontmatter ZORUNLU: `name`, `description`, `tools`, `model` (+ opsiyonel `color`).
2. Frontmatter olmadan dosya = ölü ajan (registry'ye girmez, invoke edilemez).
3. `description` "ne zaman çağrılır" + proaktif tetikleyici içersin.
4. Bu matrise (§3) satır ekle.

## İlişkili

- `.claude/rules/plan-first.md` — Tier 3 işlerde plan
- `.claude/rules/todo-verification.md` — kanıt disiplini
- `.claude/rules/commit-discipline.md` — commit-splitter bağlamı
