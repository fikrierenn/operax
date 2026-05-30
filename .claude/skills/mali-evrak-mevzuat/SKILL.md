---
name: mali-evrak-mevzuat
description: Operax mali/lojistik evrak (fatura, irsaliye, iade, e-Belge) modülü yazılırken TR mevzuat doğrulama rehberi. İade faturası, e-Fatura/e-Arşiv/e-İrsaliye senaryoları, VUK tarih kuralları (sevk/düzenleme/kayıt + 7 gün), tevkifat, KDV iade. "iade faturası", "e-fatura senaryo", "fatura tarihi kuralı", "mali evrak mevzuat" denildiğinde veya M03/M04/e-Belge modülü yazarken çağrılır. SALT-REHBER — mevzuatı dayatmaz, kod yazmadan önce doğrulanacak noktaları + kaynakları verir.
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
- İade faturası/irsaliyesi modülü (B17/E2, MASTER_EXECUTION_PLAN M-F2.2)
- e-Fatura / e-Arşiv / e-İrsaliye senaryo entegrasyonu (M04_EBelge, InvoiceEnvelope)
- Fatura tarih modeli / dönem kontrolü (B19 üç-tarih, plan 14 K4 guard)
- Periyodik GL muhasebeleştirme (K1/K2 — bu skill onun da ön koşulu)
- Tevkifat, KDV iade, gider pusulası, nihai tüketici iadesi

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
  → Operax: `sp_*InvoicePost`'ta `IF DATEDIFF(DAY,@DeliveryDate,@IssueDate)>7 THROW 51xxx Türkçe`.
- Dönem guard'ı (`sp_GuardPeriodOpen`) **fiili hareket tarihiyle** (MovementDate) çalışmalı, sistem tarihiyle değil.

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
  Tahsis **FIFO öner + manuel ezme** (en eski faturadan doldur, taşanı sonrakine; kullanıcı override). Kümülatif
  validasyon: aynı kaynak satıra toplam iade ≤ bakiye. Her satır kendi KDV oranını orijinalden taşır.
- **Nihai tüketici iadesi:** Tüketici fatura kesemez → gider pusulası VEYA satıcının e-Arşiv "iade bölümü". Hangisi
  zorunlu DOĞRULANMADI (GİB özelge teyidi gerek).
- **e-Arşiv iptal vs iade:** ~8 gün içinde iptal; süre geçince iade faturası. "7 vs 8 gün" kaynaklar arası çelişiyor → DOĞRULANMADI.

## 3. e-FATURA SENARYOLARI
- **Temel:** alıcıya ulaşınca kabul sayılır; ret yok → iade faturasıyla düzeltilir.
- **Ticari:** alıcı 8 gün içinde sistem üzerinden KABUL/RED (TTK 21 itiraz süresi).
- Temel'e sistem üzerinden RED dönülemez → itiraz KEP/noter + iade faturası.
- Operax: `InvoiceEnvelope.Scenario` (TEMEL/TICARI/IADE) + `ReferencedDocNo/Date/Uuid` alanları gerekli.

## 4. OPERAX MEVCUT DURUM (kod referans — değişebilir, doğrula)
- `schema_M04_EBelge.sql` — InvoiceEnvelope (Status ACCEPTED/REJECTED/RETURNING var; **Scenario + Referenced* YOK**)
- `schema_M04_SalesInvoice.sql:14` — `InvoiceDate` tek tarih (**IssueDate/DeliveryDate ayrımı YOK**)
- İade belge tipi **YOK** (sadece Variance — document-immutability.md). İade = ayrı belge (E2).
- `StockMovement` — `MovementDate` YOK (B19), sadece CreatedAt.

## 5. KAYIT PROTOKOLÜ
- Mevzuat bulgusu → `docs/REFERENCE_STUDY.md` backlog + ilgili `docs/MODULE_SPECS/M0X_*.md`'ye [DOC]/[YORUM] etiketli.
- Production-etkili karar → `plans/NN-*.md` + Fikri onayı.
- DOĞRULANMADI kalemler implementasyon öncesi GİB-yetkili kaynaktan teyit (bu skill tekrar çağrılır).

## 6. DOĞRULANACAK AÇIK NOKTALAR (GİB birincil kaynak gerek)
- [ ] 28.03.2025 alım-iade fatura-no zorunluluğunun GİB tebliğ/duyuru no (blog'lar [YORUM], birincil DOĞRULANMADI)
- [ ] e-İrsaliye iade "ayrı tip mi normal sevk mi" (e-İrsaliye kılavuzu PDF)
- [ ] e-Arşiv iptal süresi 7 mi 8 gün mü (iptal/itiraz kılavuzu)
- [ ] Nihai tüketici iadesi: gider pusulası mı e-Arşiv iade bölümü mü (GİB özelge)
- [ ] Tevkifatlı iade satır modeli + 2 No.lu KDV düzeltme

## Kaynaklar (başlangıç — her çağrıda güncelle)
- VUK 213 tam metin: https://www.mevzuat.gov.tr/MevzuatMetin/1.4.213.pdf (md.230 sevk, 231/5 7-gün, 219 kayıt)
- GİB: gib.gov.tr · efatura.gov.tr (UBL-TR kod listeleri kılavuzu, e-Fatura paketi)
- e-Fatura İptal/İtiraz Bildirim Kılavuzu (asmmmo.org.tr PDF)
- UBL-TR güncelleme duyurusu (PwC e-dönüşüm bülteni 2025)
- 7 gün karmaşası: alomaliye.com (M.B. Altaş YMM)

## İlişkili
- `docs/MIKRO_V16_ANALYSIS.md` §14 (üç tarih) · §12 (E1 irsaliye/fatura, E2 iade)
- `docs/REFERENCE_STUDY.md` B17/B19
- `docs/MASTER_EXECUTION_PLAN.md` M-F2.1/M-F2.2 (irsaliye/iade) · M-F0.4 (dönem guard)
- `plans/14-ledger-pk-immutability.md` (K4 dönem + MovementDate bağı)
- `.claude/rules/document-immutability.md` (iade = ayrı belge + ters-kayıt)
- **K2 ön koşul:** Periyodik GL modülü (K1) bu skill olmadan açılmaz.
