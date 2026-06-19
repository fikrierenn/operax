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
| WITHHOLDING (tevkifat) | **GERÇEK GİB kodları 601-625** (web-doğrulandı): 601·Yapım 4/10, 606·İşgücü 9/10, 609·Fason Tekstil 7/10, 612·Temizlik 9/10, 615·Baskı 7/10, 617·Hurda Metal Külçe 7/10, 621·Metal/Plastik Hurda 9/10, 622·Pamuk/Yün/Deri 9/10… (tam liste §Sertleştirme) | dict | ❌ YOK — **e-Fatura zorunlu**. 2026 alt sınır 12.000₺ |
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
**Kitap:** PUBLISHER, LANGUAGE, BINDING (Ciltli/Karton/E-Kitap), GENRE, ISBN (ItemBarcode mevcut). **KDV %1** (matbu kitap/dergi/gazete I sayılı liste — web-doğrulandı; envanterdeki %10 YANLIŞTI).
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

## DOĞRULANMADI (kalan — derin tur sonrası)
- ✅ KAPANDI: tevkifat tam liste (601-625) · kitap KDV (%1) · UN/ECE eşleme (C62) · EU14 alerjen · ISO 4217 sembol/numeric.
- ❌ Kalan: (1) Fresh DB tam dict dökümü (CLI query). (2) StockMovement lot/SKT/parti kolonu (şema okuma). (3) Koli UN/ECE kesin kod (PC vs PK — resmi GİB PDF). (4) Odoo `res.currency`+payment-terms XML + ERPNext Mode-of-Payment fixture (raw erişilemedi). (5) GİB resmi tevkifat GUID listesi son-teyit.

## SERTLEŞTİRME — web-doğrulanmış değerler (2026-06-19, reference-researcher derin tur)

### UOM — ERPNext/UN-ECE deseni (P0 backlog)
UOM dict'e 2 kolon eklenmeli (ERPNext fixture deseni): **`UnEceCode`** (e-Belge zorunlu: Adet=**C62** [NIU geçersiz], Kg=KGM, Gram=GRM, Litre=LTR, Metre=MTR, m²=MTK, m³=MTQ, Çift=PR, Kutu=BX, Palet=PAL; Koli=PC/PK `DOĞRULANMADI`) + **`IsWholeNumber`** (Adet/Koli/Çift → kesirsiz miktar guard). Odoo dersi: kategori+referans-birim+`factor` ile item-bağımsız dönüşüm (kg↔g) SQL-side — `fn_GetConversionRate`'e kategori/factor kolonu opsiyonel genişleme.

### WITHHOLDING — GİB tevkifat tam liste (601-625, YÜKSEK güven)
601·Yapım+mühendislik 4/10 · 602·Etüt-proje-danışmanlık 9/10 · 603·Makine bakım-onarım 7/10 · 604·Yemek servis 5/10 · 605·Organizasyon 5/10 · 606·İşgücü temin 9/10 · 607·Özel güvenlik 9/10 · 608·Yapı denetim 9/10 · **609·Fason tekstil-konfeksiyon-deri dikim 7/10** · 610·Turistik mağaza 9/10 · 611·Spor kulübü yayın/reklam 9/10 · 612·Temizlik 9/10 · 613·Çevre-bahçe bakım 9/10 · 614·Servis taşımacılığı 5/10 · 615·Baskı-basım 7/10 · 616·Kurumlara diğer hizmet 5/10 · 617·Hurda metal külçe 7/10 · 618·Diğer külçe 7/10 · 619·Bakır/çinko/alüminyum ürün 7/10 · 620·İstisnadan vazgeçen hurda 7/10 · 621·Metal/plastik/kâğıt/cam hurda 9/10 · 622·Pamuk/tiftik/yün/deri 9/10 · 623·Ağaç/orman ürünü 5/10 · 624·Yük taşımacılığı 2/10 · 625·Ticari reklam 3/10. **2026 alt sınır: 12.000₺ (KDV dahil).** (GİB resmi GUID listesiyle son teyit önerilir.)

### CURRENCY — ISO 4217 (sembol + numeric)
TRY·₺·949 · USD·$·840 · EUR·€·978 · GBP·£·826 · CHF·Fr·756 · JPY·¥·392.

### Gıda ALLERGEN — EU 14 (Reg. 1169/2011 Annex II, kanonik sıra)
1 Glüten tahıl · 2 Kabuklu (crustacea) · 3 Yumurta · 4 Balık · 5 Yer fıstığı · 6 Soya · 7 Süt/laktoz · 8 Sert kabuklu yemiş · 9 Kereviz · 10 Hardal · 11 Susam · 12 Sülfit (>10mg) · 13 Acı bakla · 14 Yumuşakça.

### KDV oranları (2026) — kitap DÜZELTME
%0 ihracat/istisna · %1 temel gıda + **kitap/dergi/gazete** · %10 tekstil/giyim/restoran/mobilya (eski %8) · %20 genel (elektronik/yazılım/hizmet). Operax'ta %8 varsa %10'a güncelle.

### Tekstil beden
TR beden = EU beden (EN 13402). TR34=XS…TR42=XL (kadın); harf↔numerik eşleme cinsiyet/kategoriye kayar → SIZE dict serbest-değer, sabit eşleme dayatma.

## İlişkili
- `src/Operax.Web/Lib/Dtos.cs` (canonical) · `docs/sql/schema_M00.sql` (dict altyapı) · `setup_tax_dictionary.sql` (TAX seed) · `schema_M01_UOM.sql:30-37` (UOM commented) · `docs/reference/MIKRO_V16_ANALYSIS.md §12` · `.claude/skills/referans-tanim-seed/SKILL.md`.
