# Standart Referans Tanımı Envanteri — Operax Baseline

> `referans-tanim-seed` skill çıktısı (2026-06-19, reference-researcher). Bir ERP'nin "shipped" gelmesi gereken referans-sözlük seti. Taşıyıcı: `DictionaryType` + `DictionaryValue` (CompanyId-scoped). Kanıt: `[OPERAX]` yerel, `[DOC]` mevzuat/rakip, `DOĞRULANMADI` teyit gerek.

## Kritik mimari ayrım — kod sabiti vs DictionaryValue
- **Kod sabiti (`Dtos.cs`) kalmalı** — SP/iş-mantığında dallanan, durum-makinesi taşıyan tipler: `DOC_STATUS`, `MOV_TYPE`, `SOURCE_DOC`, `CHEQUE_STATUS`, `PRICE_DIRECTION`, `PARTNER_TYPE`. Kullanıcı bunları değiştirirse SP kırılır → DictionaryValue'ya TAŞINMAZ.
- **DictionaryValue'da yaşamalı** — kullanıcı-genişletilebilir, iş-mantığı dallandırmayan: `UOM`, `TAX_RATE`, `CURRENCY`, `PAYMENT_METHOD`, `PAYMENT_TERM`, `PARTNER_CATEGORY`, sektörel öznitelikler.
- **Sınır vaka** — `ACCOUNT_TYPE`/`TRANSACTION_TYPE`: SP dallanması var → kod sabiti tercih, ama şu an hiçbir yerde sabit DEĞİL (hardcoded magic-string) → en azından `Dtos.cs`'e taşınmalı.

---

## (A) ÇEKİRDEK — her kurulum, sektörden bağımsız

| Tip | Değerler (Kod·Ad) | Taşıyıcı | Operax durumu |
|---|---|---|---|
| TAX_RATE | 0·%0 / 1·%1 / 10·%10 / 20·%20 | dict | ✅ var (yalnız 00000000 şirketinde) |
| UOM | EACH·Adet(C62) / KG·Kilogram(KGM) / GR·Gram(GRM) / LT·Litre(LTR) / MT·Metre(MTR) / M2·Metrekare(MTK) / M3·Metreküp(MTQ) / PACK·Paket / CASE·Koli / PALLET·Palet / BOX·Kutu / PR·Çift | dict | ❌ seed yorum bloğu (çalışmıyor) — **UN/ECE kod eşleme GAP (e-Belge zorunlu)** |
| CURRENCY | TRY·₺ / USD / EUR / GBP / CHF | dict | ❌ hardcoded "TRY" dağınık, merkezi yok |
| PAYMENT_METHOD | CASH·Nakit / EFT / HAVALE / CHEQUE·Çek / NOTE·Senet / CREDIT_CARD·Kredi Kartı / OFFSET·Mahsup | dict | ❌ hardcoded magic-string |
| PAYMENT_TERM | PESIN / NET7 / NET15 / NET30 / NET60 / NET90 / EOM·Ay Sonu | dict | ❌ tek parametre var, liste yok |
| WITHHOLDING (tevkifat) | GİB kodları: T10·Tekstil 7/10, T17·Demir-Çelik 5/10, T18·Hurda 7/10, T06·İşgücü 9/10… | dict | ❌ YOK — **e-Fatura zorunlu** (`DOĞRULANMADI` tam liste) |
| PARTNER_CATEGORY | BAYI / KURUMSAL / PERAKENDE + bölge | dict | ❌ yok (Mikro cari_grup/bölge muadili) |
| MOV_TYPE | RECEIPT/ISSUE/TRANSFER/COUNT_ADJ/PRODUCTION | **kod sabiti** | ✅ Dtos.cs. Eksik: Fire/Sarf/Açılış (Mikro sth_cins) |
| SOURCE_DOC | RECEIVING/SHIPPING/TRANSFER/CYCLE_COUNT/PRODUCTION | **kod sabiti** | ✅. Eksik: RETURN_IN/OUT, WASTE, OPENING_STOCK |
| DOC_STATUS / PARTNER_TYPE / PRICE_DIRECTION / DOC_PREFIX | (Dtos) | **kod sabiti** | ✅ |
| ACCOUNT_TYPE | CASH·Kasa / BANK·Banka / CREDIT_CARD / LOAN·Kredi / POS | kod sabiti olmalı | ❌ hardcoded (Dtos'a taşınmalı) |
| TRANSACTION_TYPE | INCOME / EXPENSE / TRANSFER_IN / TRANSFER_OUT | kod sabiti olmalı | ❌ hardcoded |
| CHEQUE_STATUS | PORTFOLIO/IN_BANK/COLLECTED/RETURNED/ENDORSED/PAID | kod sabiti | ✅. Eksik: Teminatta, Kısmen Ödendi |

---

## (B) SEKTÖREL KATMAN — UDF + DictionaryType (çekirdek şemaya kolon eklemeden)

**Tekstil:** SIZE (XS–XXL/36–48), COLOR, SEASON (SS26/FW26), FABRIC(UDF), tevkifat T10. ⚠️ Varyant matrisi (beden×renk SKU) = **yapısal GAP** (Item tek-seviye), ayrı plan.
**Kitap:** PUBLISHER, LANGUAGE, BINDING (Ciltli/Karton/E-Kitap), GENRE, ISBN (ItemBarcode mevcut). KDV %10 (`DOĞRULANMADI güncel`).
**Gıda:** ALLERGEN (EU 14), STORAGE (Oda/Soğuk+4/Donmuş-18), CERTIFICATE (Helal/Organik/ISO22000/HACCP), SHELF_LIFE. ⚠️ **Lot/SKT izi + FEFO** — StockMovement parti kolonu `DOĞRULANMADI` (şema kontrol gerek).

---

## (C) GAP özeti
- Dict altyapısı (DictionaryType/Value, IsSystem, CompanyId-scoped) sağlam — ideal taşıyıcı.
- TAX_RATE tek garantili seed ama **yalnız sistem şirketinde** (gerçek şirkete kopyalanmıyor).
- UOM seed commented (çalışmıyor); CURRENCY/PAYMENT/ACCOUNT/TRANSACTION hardcoded (magic-string ihlali, `architecture §3`).
- WITHHOLDING + UN/ECE birim eşleme yok (e-Belge bloklayıcı).

---

## (D) ÖNCELİK
- **P0 (açılış-kritik):** UOM seed + UN/ECE eşleme · TAX_RATE'i her şirkete kopyala · CURRENCY referansı · PAYMENT_METHOD/ACCOUNT_TYPE/TRANSACTION_TYPE → Dtos.cs sabiti (magic-string kapat).
- **P1 (e-Belge öncesi):** WITHHOLDING tevkifat listesi · PAYMENT_TERM listesi.
- **P2 (sektörel, pilote göre):** Tekstil varyant (yapısal) · Gıda lot/SKT+FEFO · Kitap dict'leri (düşük maliyet).

## DOĞRULANMADI (sonraki teyit)
1. Fresh DB "4 tip/13 değer" tam dökümü (yalnız TAX_RATE kod-kanıtlı; CLI query gerek).
2. GİB tevkifat tam/güncel liste.
3. Kitap KDV güncel oran.
4. StockMovement lot/SKT/parti kolonu varlığı.
5. COMPETITOR_ANALYSIS.md / REFERENCE_STUDY.md bu turda açılmadı — Logo/Netsis/SAP/Odoo/ERPNext katalogları genel ERP bilgisinden (repo-okuma değil).

## İlişkili
- `src/Operax.Web/Lib/Dtos.cs` (canonical) · `docs/sql/schema_M00.sql` (dict altyapı) · `setup_tax_dictionary.sql` (TAX seed) · `schema_M01_UOM.sql:30-37` (UOM commented) · `docs/reference/MIKRO_V16_ANALYSIS.md §12` · `.claude/skills/referans-tanim-seed/SKILL.md`.
