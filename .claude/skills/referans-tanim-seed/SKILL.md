---
name: referans-tanim-seed
description: Operax'ın çalışması için gereken TÜM baseline/referans tanımlarını (sözlük tipleri+değerleri, statü geçişleri, minimum master — depo/hücre/kategori, birim/KDV/ödeme/hareket/evrak tipleri) kodu tarayarak bulur, canlı DB ile farkını çıkarır ve eksiksiz idempotent per-company seed SQL üretip seed katmanına wire eder. "referans tanım", "baseline seed", "minimum kurulum verisi", "standart tanımları seed", "sözlük tara/oluştur", "çalışır kurulum", "fresh install eksik tanım" denildiğinde tetiklenir. DEMO/işlem verisi DEĞİL (o demo-veri-uret) — yalnız sistemin operable olması için gereken generic tanımlar.
allowed-tools: Read, Grep, Glob, Bash, Edit, Write, Agent, AskUserQuestion
user-invocable: true
model: inherit
---

# referans-tanim-seed — Baseline Referans Tanımı Tara + Seed Üret

> Fresh `migrate`+`seed` sonrası sistemin **operable** olması için gereken generic tanımları (referans sözlükler + minimum master) kodu kaynak alarak bulur ve idempotent seed üretir. Demo/işlem verisi DEĞİL (bkz. `demo-veri-uret`). FBUG-4 dersi: hardcoded FK-Id bağımlısı statik seed yazma — generic + idempotent.

## 1. TARA — neler gerekli (kaynak: kod, varsayma)
- **`src/Operax.Web/Lib/Dtos.cs` sabitleri** = canonical kod setleri: `MovementType` (RECEIPT/ISSUE/TRANSFER/COUNT_ADJ/PRODUCTION), `SourceDoc`/evrak (RECEIVING/SHIPPING/TRANSFER/CYCLE_COUNT/PRODUCTION + PO/SO/fatura), `DocStatus`, `PartnerType`, `PriceDirection`, `ReceivingMode`, `DocPrefix`. Magic-string yasağı: seed bu sabitlerle birebir aynı kodları kullanır.
- **Kod-referanslı dict tipleri:** `grep -rhoE "Code\s*=\s*'[A-Z_]+'" Features/ Lib/` + `DictionaryType.*'X'` → UI/sorgunun beklediği tipler (UOM, TAX_RATE, BRAND, CURRENCY, PAYMENT_METHOD…). Boş/eksik olan = gap.
- **StatusTransition:** her evrak tipi için en az DRAFT→POSTED (+ POSTED→CANCELLED). `sp_ValidateStatusTransition` kuralı yoksa her onay THROW eder → bu KRİTİK minimum.
- **FK-zorunlu minimum master:** en az 1 Warehouse + Bin (IsReceivingArea / IsPickingArea / sevk) — yoksa hiçbir stok akışı (mal kabul/sevk/transfer) çalışmaz. Birkaç jenerik Category.

## 2. DİFF — canlı DB ile karşılaştır (CompanyId-bazlı)
`operax-cli query` ile her şirket için: hangi DictionaryType var/boş, hangi değerler eksik, StatusTransition tam mı, Warehouse/Bin sayısı, Category. Tablo: gerekli vs mevcut → **gap listesi**.

## 3. ÜRET — idempotent baseline seed SQL
- **Per-company:** `FROM Company c WHERE NOT EXISTS(...)` veya `MERGE` ile her şirkete bir kez. Sistem-geneli vs şirket-bazlı ayrımına dikkat (dict CompanyId-scoped).
- **Idempotent:** `IF NOT EXISTS` / `WHERE NOT EXISTS` — re-run güvenli, mevcut değeri ezmez.
- **Türkçe NameTr** (turkish-ui), kod sabiti İngilizce (Dtos ile aynı).
- **Hardcoded FK-Id YOK** (FBUG-4): warehouse/bin/dict Id'leri `NEWID()` veya deterministic ama company'den türeyen; başka tabloya sabit-GUID bağımlılığı kurma.
- ALTER ADD sonrası `GO` (batch bağımlılığı — DocChain dersi).

## 4. WIRE — seed katmanına bağla
- Yeni `docs/sql/seed_reference.sql` → `operax-cli seed` listesine (`src/Operax.Cli/Program.cs` seed array). seed_core/setup_tax ile çakışmaz (idempotent). Sıra: core'dan sonra, demo'dan önce.
- (Opsiyon) bazıları gerçekten her kurulumda gerekiyorsa migrate addon listesine de eklenebilir — ama seed daha doğru yer (referans = veri, şema değil).

## 5. DOĞRULA — fresh test DB
- Ayrı `Operax_Test` DB'de `migrate`+`seed` → **operable** mı: Warehouse≥1, her gerekli dict dolu, StatusTransition tam, demo-veri-uret prerequisite'leri karşılanıyor mu (`OPERAX_CONN` env ile yönlendir). Gerçek dev DB'ye dokunma.
- Sonra `demo-veri-uret` bunun üstüne işlem verisi basar (zincirler).

## Guardrails
- ❌ Demo/işlem verisi (sipariş/fatura/stok hareketi) — o `demo-veri-uret`. Bu skill yalnız baseline referans + minimum master.
- ❌ Hardcoded FK-Id'ye bağlı statik seed (FBUG-4 kök sebebi).
- ❌ Mevcut değeri ezen non-idempotent INSERT.
- ❌ Magic string — Dtos sabit kodlarıyla birebir.
- ✅ Kod-kaynaklı (Dtos + grep), DB-diff'li, idempotent, per-company, fresh-DB doğrulanmış.

## İlişkili
- `.claude/skills/demo-veri-uret/SKILL.md` — üstüne işlem verisi (bu skill prerequisite'i kurar).
- `.claude/agents/demo-data-builder.md` — ağır SQL üretimi gerekirse delege edilebilir.
- `src/Operax.Web/Lib/Dtos.cs` — canonical kod sabitleri (tek doğruluk kaynağı).
- `docs/sql/seed_core.sql` / `setup_tax_dictionary.sql` — mevcut baseline seed (çakışma kontrolü).
- `docs/BUGS.md` FBUG-4 — statik seed FK-kırık dersi.
- `.claude/rules/ui-standard.md §1.5` (sıfır hardcoded veri — seed SQL meşru), `turkish-ui.md`.
