---
name: referans-tanim-seed
description: Operax'ın çalışması için gereken TÜM baseline/referans tanımlarını (sözlük tipleri+değerleri, statü geçişleri, minimum master — depo/hücre/kategori, birim/KDV/ödeme/hareket/evrak tipleri) kodu tarayarak bulur, canlı DB ile farkını çıkarır ve eksiksiz idempotent per-company seed SQL üretip seed katmanına wire eder. "referans tanım", "baseline seed", "minimum kurulum verisi", "standart tanımları seed", "sözlük tara/oluştur", "çalışır kurulum", "fresh install eksik tanım" denildiğinde tetiklenir. DEMO/işlem verisi DEĞİL (o demo-veri-uret) — yalnız sistemin operable olması için gereken generic tanımlar.
allowed-tools: Read, Grep, Glob, Bash, Edit, Write, Agent, AskUserQuestion
user-invocable: true
model: inherit
---

# referans-tanim-seed — Baseline Referans Tanımı Tara + Seed Üret

> Fresh `migrate`+`seed` sonrası sistemin **operable** olması için gereken generic tanımları (referans sözlükler + minimum master) kodu kaynak alarak bulur ve idempotent seed üretir. Demo/işlem verisi DEĞİL (bkz. `demo-veri-uret`). FBUG-4 dersi: hardcoded FK-Id bağımlısı statik seed yazma — generic + idempotent.

## 1. TARA — neler gerekli (İKİ kaynak: kod + rakip; sektör katmanlı)

### 1a. Kod-içi (canonical — varsayma, oku)
- **`src/Operax.Web/Lib/Dtos.cs` sabitleri** = canonical kod setleri: `MovementType` (RECEIPT/ISSUE/TRANSFER/COUNT_ADJ/PRODUCTION), `SourceDoc`/evrak (RECEIVING/SHIPPING/TRANSFER/CYCLE_COUNT/PRODUCTION + PO/SO/fatura), `DocStatus`, `PartnerType`, `PriceDirection`, `ReceivingMode`, `DocPrefix`. Magic-string yasağı: seed bu sabitlerle birebir aynı kodları kullanır.
- **Kod-referanslı dict tipleri:** `grep -rhoE "Code\s*=\s*'[A-Z_]+'" Features/ Lib/` + `DictionaryType.*'X'` → UI/sorgunun beklediği tipler (UOM, TAX_RATE, BRAND, CURRENCY, PAYMENT_METHOD…). Boş/eksik olan = gap.
- **StatusTransition:** her evrak tipi için en az DRAFT→POSTED (+ POSTED→CANCELLED). `sp_ValidateStatusTransition` kuralı yoksa her onay THROW eder → KRİTİK minimum.
- **FK-zorunlu minimum master:** en az 1 Warehouse + Bin (IsReceivingArea / IsPickingArea / sevk) — yoksa hiçbir stok akışı çalışmaz. Jenerik Category.

### 1b. Rakip + standart envanter (kapsam genişletme)
Kod sadece "şu an kullanılanı" verir; standart bir ERP'nin **shipped** referans setini vermez. Bu yüzden rakip/standart tara:
- **`competitor-analyst` skill + `reference-researcher` agent** ile: Logo · Mikro · Netsis · SAP B1 · Odoo hangi standart referans tanımlarını hazır getiriyor (UOM seti, ödeme tipleri, evrak tipleri, hareket tipleri, KDV/tevkifat kodları, ülke/döviz, ödeme koşulu, kargo/taşıyıcı tipi, birim grupları…).
- Kaynaklar: `docs/COMPETITOR_ANALYSIS.md`, `docs/reference/MIKRO_V16_ANALYSIS.md`, açık-kaynak (Odoo `uom`/`account` data XML'leri, ERPNext fixtures) — reference-researcher gerçeğinden okur.
- **TR mevzuat standartları:** KDV oranları (%0/1/10/20), tevkifat kodları, e-Belge birim kodları (UN/ECE — GİB), para birimleri (ISO 4217). `mali-evrak-mevzuat` skill'i ile doğrula.

### 1c. Sektör katmanı (ayrışma)
Referansları **iki katmana** ayır:
- **ÇEKİRDEK (her kurulum):** UOM temel, KDV, ödeme/hareket/evrak/statü tipleri, döviz — sektörden bağımsız.
- **SEKTÖREL (opsiyonel, kurulumda seçilir):** tekstil (Beden/Renk/Sezon/Kalite), kitap (Yayınevi/Dil/Cilt tipi), gıda (Alerjen/Saklama koşulu/Sertifika), vb. — UDF tanımı veya sektör-dict olarak.

## 2. DİFF — canlı DB ile karşılaştır (CompanyId-bazlı)
`operax-cli query` ile her şirket için: hangi DictionaryType var/boş, hangi değerler eksik, StatusTransition tam mı, Warehouse/Bin sayısı, Category. Tablo: gerekli vs mevcut → **gap listesi**.

## 2.5 ENVANTER — `docs/reference/REFERENCE_INVENTORY.md`
Tarama çıktısını kalıcı envantere yaz: her referans tipi için tablo — **Kod · Türkçe ad · Kaynak (Dtos/kod/rakip/mevzuat) · Katman (çekirdek/sektörel) · Operax'ta var mı · Rakip-parite notu**. Bu doküman seed'in tek doğruluk kaynağı + gelecekte diff için baz.

## 3. ÜRET — katmanlı idempotent seed SQL
- **Per-company:** `FROM Company c WHERE NOT EXISTS(...)` veya `MERGE` ile her şirkete bir kez. Sistem-geneli vs şirket-bazlı ayrımına dikkat (dict CompanyId-scoped).
- **Idempotent:** `IF NOT EXISTS` / `WHERE NOT EXISTS` — re-run güvenli, mevcut değeri ezmez.
- **Türkçe NameTr** (turkish-ui), kod sabiti İngilizce (Dtos ile aynı).
- **Hardcoded FK-Id YOK** (FBUG-4): warehouse/bin/dict Id'leri `NEWID()` veya deterministic ama company'den türeyen; başka tabloya sabit-GUID bağımlılığı kurma.
- ALTER ADD sonrası `GO` (batch bağımlılığı — DocChain dersi).

## 4. WIRE — seed katmanına bağla (katmanlı)
- `docs/sql/seed_reference.sql` (ÇEKİRDEK — her kurulum) → `operax-cli seed` listesine (`Program.cs` seed array). seed_core/setup_tax ile çakışmaz (idempotent). Sıra: core'dan sonra, demo'dan önce.
- `docs/sql/seed_reference_<sektor>.sql` (SEKTÖREL — opsiyonel) → varsayılan seed'e GİRMEZ; `operax-cli script <dosya>` ile kuruluma göre elle uygulanır (sektör seçimi).
- (Opsiyon) gerçekten her kurulumda gereken çekirdek migrate addon'a da konabilir — ama referans = veri, seed doğru yer.

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
