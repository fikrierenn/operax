---
name: mali-evrak-mevzuat
description: >
  Operax mali/lojistik evrak (fatura, irsaliye, iade, e-Belge, iptal/düzeltme) modülü
  yazılırken VUK + KDV Kanunu + TTK mevzuat doğrulama rehberi. İade faturası,
  e-Fatura/e-Arşiv/e-İrsaliye senaryoları, VUK tarih kuralları (sevk/düzenleme/kayıt
  + 7 gün), irsaliye↔fatura kardinalitesi (N irsaliye→1 fatura consolidation, 1 irsaliye→N
  fatura YASAK), yanlış fatura iptal/düzeltme akışı, GİB'e gitmiş vs sistem-içi evrak ayrımı,
  tevkifat, KDV iade, TTK ticari belge saklama. "iade faturası", "e-fatura senaryo",
  "fatura iptal", "fatura düzelt", "yanlış fatura", "fatura tarihi kuralı", "irsaliye fatura
  kardinalite", "toplu fatura", "kısmi faturalama", "mali evrak mevzuat", "VUK" denildiğinde
  veya M03/M04/M11/e-Belge modülü yazarken çağrılır.
  SALT-REHBER — mevzuatı dayatmaz, kod yazmadan önce doğrulanacak noktaları + kaynakları.
allowed-tools: Read, Grep, Glob, WebSearch, WebFetch
user-invocable: true
model: inherit
---

# Mali Evrak Mevzuat Rehberi (Operax — TR)

> **AMAÇ:** Operax'ta fatura/irsaliye/iade/e-Belge modülü yazılmadan ÖNCE TR mevzuat gereksinimlerini netleştir.
> **UYARI:** Bu skill hukuki görüş değil — tasarım girdisidir. `[DOC]` (resmi/birincil teyit), `[YORUM]`
> (entegratör/YMM, tek başına bağlayıcı değil), `DOĞRULANMADI` katmanları zorunlu. Production kararı öncesi
> GİB-yetkili kaynaktan (gib.gov.tr, efatura.gov.tr kılavuz, ilgili tebliğ) teyit et. Tahmin YASAK.

## Ne zaman tetiklenir
- İrsaliye↔fatura kardinalitesi / consolidation / kısmi faturalama (Receiving:Invoice = N:1 → §1.5)
- Fatura iptal / düzeltme / soft-delete kararı (GİB'e gitti mi, sistem-içi mi?)
- Yanlış girilen/kesilen fatura akışı
- İade faturası/irsaliyesi modülü (B17/E2, MASTER_EXECUTION_PLAN M-F2.2)
- e-Fatura / e-Arşiv / e-İrsaliye senaryo entegrasyonu (M04_EBelge, InvoiceEnvelope)
- Fatura tarih modeli / dönem kontrolü (B19 üç-tarih, plan 14 K4 guard)
- Periyodik GL muhasebeleştirme (K1/K2 — bu skill onun da ön koşulu)
- Tevkifat, KDV iade, gider pusulası, nihai tüketici iadesi
- Cari mutabakat, yaşlandırma, açık-kalem kapama (VUK/TTK)
- AccountMovement / FinancialTransaction immutability kararları

## 0. KRİTİK AYRIM — GİB'E GİTMEYEN vs GİTMİŞ FATURA

Bu ayrım Operax'ta her iptal/düzeltme kararının temelidir:

| Durum | Tanım | Operax Akışı |
|---|---|---|
| **Sistem-içi (DRAFT)** | Henüz basılmamış/gönderilmemiş, GİB'e iletilmemiş | Soft-delete/iptal serbest — VUK fiziksel basım öncesini belge saymaz [DOC] |
| **Kağıt/basılı** | Matbu fatura, GİB kaydı yok | Tüm nüshalar saklanır, üzerine "İPTAL" + imza, yeni kesilir; koçandan koparılmaz [YORUM] |
| **e-Arşiv (iletildi)** | GİB'e iletim tarihinden 8 gün içinde | GİB İptal Portalı + alıcı onayı zorunlu [DOC — 509 SN VUK GT] |
| **e-Arşiv (8+ gün geçti)** | İptal penceresi kapandı | Zorunlu iade faturası |
| **e-Fatura Temel** | GİB'e ulaştı, 8 gün içinde | İptal Portalı + alıcı onayı [DOC] |
| **e-Fatura Ticari** | Alıcıya iletildi, 8 gün içinde | Alıcı Red Yanıtı → satıcı yeni doğru fatura [DOC] |
| **e-Fatura Ticari (8+ gün)** | Alıcı yanıt vermedi | TTK md.18 harici itiraz (KEP/noter) + GİB bildirimi, aylık 20'ye kadar |

**Operax kod kuralı:** `InvoiceEnvelope.Status IN ('DRAFT','FAILED','CANCELLED')` → soft-delete/iptal OK.
`Status NOT IN ('DRAFT','FAILED','CANCELLED')` + `DirectionType='OUTBOUND'` → THROW, iade faturası gerekli.

## 1. ÜÇ (DÖRT) TARİH KURALI — en kritik
Bir mali/lojistik evrakta tarihler KARIŞTIRILMAZ (Operax B19 / MIKRO §14):

| Tarih | VUK | Operax kolon | Kural |
|---|---|---|---|
| **Sevk/teslim tarihi** | md.230 | irsaliye DocDate + `StockMovement.MovementDate` (B19 — eklenmeli) | Malın fiilen sevk/teslim edildiği gün |
| **Düzenleme tarihi** | md.231/5 | `SalesInvoice.IssueDate` (eklenmeli) | Fatura oluşma; e-Fatura'da **imza zamanı** [DOC] |
| **İşlenme (sistem)** | — | `CreatedAt` | Kaydın sisteme girildiği an |
| **Defter kayıt** | md.219 | (GL ertelendi K1) | Max 10 gün içinde deftere |

- **7 GÜN KURALI [DOC — VUK 231/5]:** Fatura, teslim/hizmet tarihinden **azami 7 gün** içinde düzenlenir;
  aşılırsa **"hiç düzenlenmemiş" sayılır** (özel usulsüzlük). Süre: teslim günü sayılmaz, 7. gün sonu biter (md.18).
- **🔴 AY-SONU KAPAĞI — 7 gün ay atlamaz [DOC — KDV dönemi aylık]:** Fatura, malın teslim edildiği **AY içinde**
  kesilmek ZORUNDA (KDV beyannamesi aylık → fatura teslimle aynı KDV dönemine düşmeli). Yani 7 gün bir sonraki
  aya taşıyorsa **ayın son gününe kadar** çekilir. Örnek: irsaliye **30 Ocak** → 7 gün = 6 Şubat AMA fatura
  **en geç 31 Ocak** (Şubat'a sarkamaz). İrsaliye **10 Ocak** → 7 gün = 17 Ocak, ay içinde, sorun yok.
  **Kesin formül:** `FaturaSonTarih = MIN(DATEADD(DAY, 7, @DeliveryDate), EOMONTH(@DeliveryDate))`.
  → Operax: `sp_*InvoicePost`'ta
  `IF CAST(@IssueDate AS DATE) > MIN-formül → THROW 51xxx N'Fatura tarihi yasal süreyi aştı (teslim ayı + 7 gün, ay sonunu aşamaz).'`
- Dönem guard'ı (`sp_GuardPeriodOpen`) **fiili hareket tarihiyle** (MovementDate) çalışmalı, sistem tarihiyle değil.

## 1.5 İRSALİYE ↔ FATURA KARDİNALİTESİ [DOC — VUK 230/231, GİB toplu fatura]

Sevk irsaliyesi (delivery note) ile fatura arasındaki ilişki YÖNLÜ ve asimetriktir. Her ikisi de **ayrı belge** —
irsaliye malın fiziksel sevkini, fatura ticari/mali değeri belgeler; biri diğerinin yerine geçmez (VUK md.230).

| Yön | İzin | Kural / Şart | Dayanak |
|---|---|---|---|
| **N irsaliye → 1 fatura** (consolidation) | ✅ EVET | Aynı alıcının birden çok sevk irsaliyesi tek faturada birleşir. Şart: **en ESKİ** irsaliyeye göre `MIN(en_eski + 7 gün, EOMONTH(en_eski))` — yani **ay atlamaz** (§1 ay-sonu kapağı). Faturaya birleştirilen TÜM irsaliye no'ları yazılır. Farklı aydaki irsaliyeler **aynı faturada birleşemez** (her ay ayrı KDV dönemi). | VUK 231/5 + GİB [DOC] |
| **1 irsaliye → N fatura** (split) | ❌ HAYIR | "Bir sevk irsaliyesi için birden fazla fatura kesilemez." Bir irsaliye tek faturada **tamamen** kapanır; kısmen faturalanıp kalanı başka faturaya bölünemez. | VUK uygulama [DOC] |
| **İrsaliyeli fatura** | ✅ EVET | Hem fatura hem sevk irsaliyesi şartlarını TEK belgede toplar; ayrıca sevk irsaliyesi aranmaz. | VUK md.230 [DOC] |

**Kısım kısım teslim istisnası:** Montaj/parçalı teslimde fatura, montajın **tamamlandığı** tarihi izleyen 7 gün
içinde düzenlenir; daha önce kesilen tüm irsaliye no'ları faturada belirtilir (GİB özelge 2016). Bu "1 irsaliye → N
fatura" DEĞİL; "N parça-irsaliye → 1 fatura"dır.

**Operax model sonucu (kod-bağlayıcı):**
- **`Receiving : PurchaseInvoice = N:1`** (satış tarafı `Shipping : SalesInvoice = N:1` aynı). Yani:
  - Bir Receiving (irsaliye) **en fazla 1** POSTED faturaya bağlanır → ikinci fatura üretimi **THROW** (51xxx Türkçe).
    Mevcut guard: `DocumentLock.ReceivingHasInvoiceAsync` (UI) — SP-seviyesi `sp_Create*InvoiceFromReceiving`
    girişinde de `IF EXISTS(POSTED/DRAFT invoice for ReceivingId) THROW` olmalı (defense-in-depth).
  - Bir fatura **birden çok** Receiving'i birleştirebilmeli (consolidation) → fatura↔irsaliye **çoklu** bağ
    (`PurchaseInvoiceLine.SourceReceivingLineId` zaten satır-bazlı; header tek-irsaliye varsayımı KIRILMAMALI).
- **PO ile karıştırma:** Bu kardinalite Receiving↔Invoice'tir. PO→Receiving hâlâ **1:N** (bir sipariş çok sevkiyat).
  Dolayısıyla PO-seviyesi forecast/PaymentPlan "ilk faturada iptal" KARARINI ETKİLEMEZ — PO estimate PO-seviyesinde
  kalır (bkz. plan 55, erp-isleyis-danismani forecast-only kararı).
- **7-gün + ay-sonu guard'ı consolidation'da en ESKİ irsaliyeye göre** hesaplanır (en yeni değil).

## 2. İADE FATURASI — güncel kurallar (28.03.2025 değişikliği)
- **Kim keser:** İadeyi yapan **ALICI** keser (satıcı "satış iade faturası" kesmez). [YORUM, çok kaynak]
- **🔴 28.03.2025 — GİB UBL-TR güncellemesi [DOC/YORUM]:** İade/alım-iade faturasında **iadeye konu fatura no+tarih
  ZORUNLU** — XML `BillingReference/InvoiceDocumentReference`, `DocumentTypeCode=RETURN`. Schematron kontrolü aktif;
  eksikse fatura geçersiz. → **Operax: `ReturnInvoice.SourceInvoiceId + SourceInvoiceNo + SourceInvoiceDate` zorunlu alan.**
- **Senaryo:** İADE faturası **kendi senaryosunu seçer** (Temel), gelen faturadan bağımsız. İADE tipine **uygulama
  yanıtı (kabul/ret) GÖNDERİLEMEZ** → Operax InvoiceEnvelope'da İADE ise yanıt-bekleme job'ı KAPALI.
- **İade irsaliyesi:** Fiziksel iade için ayrı irsaliye ("iade amacıyla düzenlenmiştir"); alıcı e-İrsaliye
  mükellefiyse e-İrsaliye iade, değilse kağıt. [YORUM]
- **🔴 OPERAX KARARI (2026-05-30) — SATIR BAZLI kaynak eşleme:** İade faturasında stok seçilince **hangi orijinal
  fatura satırından** iade edildiği seçilir (`ReturnInvoiceLine.SourceInvoiceLineId`). Mevzuat belge-düzeyi referans
  ister; Operax satır-düzeyi tutar (header referansı satırlardan türetilir) → kısmi iade + FIFO geri-açma (K7) +
  doğru KDV/tevkifat. **Kaçış valfi:** `SourceLinkType=UNLINKED` (faturasız/eski/açılış iadesi) — satır eşleme yok
  ama header mevzuat referansı yine zorunlu. Validasyon: iade miktarı orijinal bakiyeyi aşamaz. Detay: MIKRO §12.8.
- **🔴 ÇOK-KAYNAK TAHSİS (§12.8.1):** Bir iade miktarı birden çok kaynak faturaya yayılırsa (80 iade ↔ 28+18+50
  satılmış) → **TEK iade faturası, ÇOK satır** (her satır ayrı SourceInvoiceLineId, çoklu UBL-TR BillingReference).
  Tahsis **LIFO öner + manuel ezme** (en YENİ faturadan doldur, taşanı öncekine — elde kalan mal = son giren;
  kullanıcı override). ⚠️ İade kaynak seçimi LIFO; K7 FIFO satış COGS değerlemesi AYRI konu. Kümülatif
  validasyon: aynı kaynak satıra toplam iade ≤ bakiye. Her satır kendi KDV oranını orijinalden taşır.
- **Nihai tüketici iadesi:** Tüketici fatura kesemez → gider pusulası VEYA satıcının e-Arşiv "iade bölümü". Hangisi
  zorunlu DOĞRULANMADI (GİB özelge teyidi gerek).
- **e-Arşiv iptal vs iade:** ~8 gün içinde iptal; süre geçince iade faturası. "7 vs 8 gün" kaynaklar arası çelişiyor → DOĞRULANMADI.

## 3. e-FATURA / e-ARŞİV SENARYOLARI

### e-Fatura
- **Temel:** alıcıya ulaşınca kabul sayılır; ret yok → iade faturasıyla düzeltilir.
- **Ticari:** alıcı **8 gün** içinde sistem üzerinden KABUL/RED [DOC — TTK md.21/2].
- Temel'e sistem RED dönülemez → TTK md.18 itiraz (KEP/noter/telgraf) + GİB bildirimi aylık 20'ye kadar.
- Operax: `InvoiceEnvelope.Scenario` (TEMELFATURA/TICARIFATURA/IADE) + `ReferencedDocNo/Date/Uuid` alanları **EKSİK**.

### e-Arşiv İptal [DOC — 509 SN VUK GT, 526 SN GT]
- **İptal penceresi:** alıcıya **iletim tarihinden 8 gün** (düzenleme değil iletim).
- **Kanal:** GİB e-Arşiv İptal Portalı veya entegratör üzerinden → GİB'e bildirim **zorunlu**.
- **8 gün içi:** portal iptali + alıcı onayı.
- **8 gün sonrası:** zorunlu iade faturası.
- e-Arşiv'de alıcı onayı e-Fatura Ticari gibi state-machine değil — tek taraflı bildirim ama alıcı redde 8 günü var.
- Operax GAP: iptal kuyruğu, 8-gün deadline hesabı, GİB bildirim state'i yok.

### Yanlış Düzenlenen Fatura Düzeltme Akışı
- **DRAFT (gönderilmemiş):** VUK basım öncesini belge saymaz → soft-delete + doğru kayıt [DOC].
- **Kağıt (basılı, gönderilmemiş):** tüm nüshalar saklanır + "İPTAL" yazılır + koçandan koparılmaz + yeni kesilir [YORUM].
- **e-Belge (gönderilmiş, 8 gün içi):** GİB portal iptal → onay → yeni doğru fatura.
- **e-Belge (8 gün geçti):** iade faturası (alıcı) veya düzeltici fatura (satıcı iade) zorunlu.
- **Alış faturası (gelen e-Fatura yanlış):** Ticari → Red Yanıtı; Temel → harici itiraz + GİB bildirim → tedarikçi yeni keser. KDV indirimi yanlış faturada kullanılamaz [YORUM — özelge DOĞRULANMADI].

## 3.4 BİLDİRİM KANALI MEVZUATI — KEP / İYS / WhatsApp (Plan 20)

| Bildirim tipi | Kanal kuralı | Dayanak |
|---|---|---|
| **İhtar / itiraz / fesih / temerrüt** | KEP/noter/taahhütlü/telgraf ZORUNLU (4'ten biri) | TTK md.18/3 [DOC] |
| **Mutabakat mektubu** | KEP önerilir (delil), zorunlu değil | TTK md.18/3 dışı [YORUM] |
| **Fatura/bakiye/ödeme hatırlatma/sevkiyat** | İYS onayı GEREKMEZ (bilgilendirme istisnası) | 6563 md.6/3 [YORUM] |
| **Pazarlama/kampanya** | İYS onayı ZORUNLU | 6563 md.6 [DOC] |
| **WhatsApp** | yalnızca bilgilendirme; ihtar/itiraz YETERSİZ; delivery receipt resmi delil DEĞİL | [YORUM] |

- **Bilgilendirme + pazarlama karışırsa** (fatura + indirim) → ticari ileti sayılır, İYS gerekir.
- **KEP delivery receipt = resmi tebliğ delili** (zaman damgalı, ispat yükü tersine). İtiraz süresi
  **KEP teslim (DeliveredAt) tarihinden** başlar — gönderim/SentAt değil.
- **Saklama:** ihtar/mutabakat 10 yıl (TTK md.82) · İYS onay kaydı ≥1 yıl · KVKK kişisel veri amaçla-sınırlı + imha.
- Operax kod kuralı: `NotificationMessage.LegalChannelRequirement` (NONE/QUALIFIED) +
  `IsCommercialMessage` + `RetentionClass`. QUALIFIED → KEP/NOTER zorunlu, EMAIL THROW (Plan 20).

## 3.5 TTK md.65 — DEFTER SİLME YASAĞI [DOC]
- "Defterlere geçirilen kayıt kazımak, çizmek veya silmek suretiyle okunamaz hâle getirilemez."
- Düzeltme: yanlış satır **okunacak şekilde çizilir**, doğrusu yanına yazılır, paraflanır.
- Operax karşılığı: **append-only ledger + REVERSAL ters-kayıt** (`AccountMovement`, `StockMovement`).
- **ÇELİŞKİ:** sql-conventions.md §1 tüm tablolara zorunlu `IsDeleted BIT` dayatıyor, ama TTK md.65 defter tablolarında silme yasağı. Çözüm: ledger/POSTED fatura tablolarında `IsDeleted` by-design kullanılmaz — kural `document-immutability.md`'de belgelenmeli.
- `FinancialTransaction` = sistem-içi kasa kaydı (defter sınıfı değil) → soft-delete meşru.
- `AccountMovement` = cari subledger (defter sınıfı) → soft-delete YASAK, `IsDeleted` kaldırıldı ✅.
- `SalesInvoice/ExpenseInvoice` POSTED = deftere işlenmiş → soft-delete YASAK, REVERSAL gerekli.

## 4. OPERAX MEVCUT DURUM (kod referans — değişebilir, doğrula)
- `schema_M04_EBelge.sql` — InvoiceEnvelope (Status ACCEPTED/REJECTED/RETURNING var; **Scenario + Referenced* YOK**)
- `schema_M04_SalesInvoice.sql:14` — `InvoiceDate` tek tarih (**IssueDate/DeliveryDate ayrımı YOK**)
- İade belge tipi **YOK** (sadece Variance — document-immutability.md). İade = ayrı belge (E2).
- `StockMovement` — `MovementDate` YOK (B19), sadece CreatedAt.

## 5. KAYIT PROTOKOLÜ
- Mevzuat bulgusu → `docs/reference/REFERENCE_STUDY.md` backlog + ilgili `docs/MODULE_SPECS/M0X_*.md`'ye [DOC]/[YORUM] etiketli.
- Production-etkili karar → `plans/NN-*.md` + Fikri onayı.
- DOĞRULANMADI kalemler implementasyon öncesi GİB-yetkili kaynaktan teyit (bu skill tekrar çağrılır).

## 6. DOĞRULANACAK AÇIK NOKTALAR (GİB birincil kaynak gerek)
- [ ] 28.03.2025 alım-iade fatura-no zorunluluğunun GİB tebliğ/duyuru no (blog'lar [YORUM], birincil DOĞRULANMADI)
- [ ] e-İrsaliye iade "ayrı tip mi normal sevk mi" (e-İrsaliye kılavuzu PDF)
- [ ] ~~e-Arşiv iptal süresi 7 mi 8 gün mü~~ → **8 gün [DOC — 509 SN VUK GT]** (kapatıldı)
- [ ] Nihai tüketici iadesi: gider pusulası mı e-Arşiv iade bölümü mü (GİB özelge)
- [ ] Tevkifatlı iade satır modeli + 2 No.lu KDV düzeltme
- [ ] UBL-TR DocumentTypeCode kesin kod tablosu (01/03 ve UBL-2.1 380/381 eşleşmesi — efatura.gov.tr UBL-TR paketi kılavuzundan teyit)
- [ ] KDV indirim hakkı — yanlış faturada kullanılamaz özelge no (GİB mukteza, mevcut [YORUM])
- [ ] Kağıt fatura iptal prosedürü VUK'ta açık hüküm var mı (md.230/231 vs tebliğ — şu an [YORUM])

## Kaynaklar (araştırma sonrası güncellendi)
- VUK 213 tam metin: https://www.mevzuat.gov.tr/MevzuatMetin/1.4.213.pdf (md.230/231/232/65/219)
- GİB: gib.gov.tr · efatura.gov.tr (UBL-TR kod listeleri kılavuzu, e-Fatura paketi)
- **TÜRMOB e-Fatura İptal/İhtar/İtiraz Bildirim Kılavuzu [DOC]:** https://www.turmob.org.tr/arsiv/mbs/resmigazete/-e-Fatura_Iptal_Ihtar_Itiraz_Bildirim_Kilavuz.pdf
- **509 SN VUK Genel Tebliği [DOC]:** e-Arşiv iptal 8 gün + GİB bildirim zorunluluğu (vergiteknolojileri.com.tr doğrulama)
- **TTK md.65 [DOC]:** https://www.siviltoplum.gov.tr/defter-tutma (silme yasağı — çizgi+paraf)
- **VUK 231/5 7-gün [DOC]:** https://esasdenetim.com/2024-sirkuler/geriye-donuk-7-gunu-asan-surede-efatura-duzenlenmesi-halinde-usulsuzluk-cezasi-1771
- UBL-TR güncelleme: efatura.gov.tr kılavuz + PwC e-dönüşüm bülteni 2025
- TTK md.18/21 itiraz süresi (e-Ticari RED dayanaklı)

## İlişkili
- `docs/reference/MIKRO_V16_ANALYSIS.md` §14 (üç tarih) · §12 (E1 irsaliye/fatura, E2 iade)
- `docs/reference/REFERENCE_STUDY.md` B17/B19
- `docs/MASTER_EXECUTION_PLAN.md` M-F2.1/M-F2.2 (irsaliye/iade) · M-F0.4 (dönem guard)
- `plans/14-ledger-pk-immutability.md` (K4 dönem + MovementDate bağı)
- `.claude/rules/document-immutability.md` (iade = ayrı belge + ters-kayıt)
- **K2 ön koşul:** Periyodik GL modülü (K1) bu skill olmadan açılmaz.
