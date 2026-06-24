---
name: muhasebe-mevzuat
description: >
  Operax finans/muhasebe özelliği (çek/senet, cari hesap, banka/kasa, gider, GL
  muhasebeleştirme, mizan, dönem) yazılırken TÜRK MUHASEBE MEVZUATI doğrulama rehberi —
  TDHP (Tek Düzen Hesap Planı) hesap işleyişi (borç/alacak yönü), çek/senet muhasebe
  kaydı (101 Alınan Çekler / 103 Verilen Çekler, alış-anı cari kapama, karşılıksız iade),
  cari hesap (120 Alıcılar / 320 Satıcılar), VUK değerleme, şüpheli alacak (128/138).
  Muhasebenin 12 temel kavramı (MSUGT — dönemsellik, ihtiyatlılık, özün önceliği, belgelendirme…) lensi.
  SALT-REHBER + ONLINE-DOĞRULA: kesin hesap kodu/oran/özel durum gerektiğinde WebSearch ile
  yetkili kaynaktan (vergidosyasi, muhasebetr, GİB, mevzuat.gov.tr) teyit edip KAYNAK belirtir.
  "muhasebe kaydı", "hesap planı", "TDHP", "borç alacak hangi yön", "çek muhasebe", "karşılıksız
  çek", "cari kapama", "şüpheli alacak", "yevmiye fişi", "mizan", "muhasebeleştirme", "hesap kodu"
  denildiğinde veya M5/M6/M7/M8/M11 finans + GL özelliği yazarken çağrılır.
allowed-tools: Read, Grep, Glob, Bash, WebSearch, WebFetch
user-invocable: true
model: inherit
---

# Muhasebe Mevzuatı (Türk — TDHP/VUK) Rehberi

> **SALT-REHBER.** Mevzuatı koda gömmez — yazmadan önce hangi hesabın hangi yöne (borç/alacak)
> ve hangi ANDA hareket etmesi gerektiğini söyler. **Emin değilse ONLINE doğrular** (WebSearch
> yetkili kaynak) + kaynak belirtir. Operax SQL-First: kural SP'de yaşar, bu skill kuralın
> muhasebe-doğruluğunu garanti eder.

## Ne zaman tetiklenir
M5 Banka/Kasa · M6 Çek/Senet · M7 Kredi · M8 Gider · M11 Finans · GL muhasebeleştirme özelliği
yazarken/denetlerken. Bir hesabın yönü/anı belirsizse → önce buraya bak, gerekirse online doğrula.
Her finans/muhasebe kararında **muhasebenin 12 temel kavramı (§0)** lensini gözet (MSUGT — anayasa).

## Tamamlayıcılık (footprint-ladder — dup değil)
| Skill | Bakış |
|---|---|
| **muhasebe-mevzuat** (bu) | Muhasebe-kaydı doğruluğu (TDHP hesap/yön/an, çek-senet kaydı) |
| `mali-evrak-mevzuat` | Evrak/e-belge mevzuatı (fatura/irsaliye VUK tarih, e-Fatura/iade) |
| `mali-islem-akislari` | Operasyonel akış (mutabakat/varyans/kapanış işleyişi) |
| `erp-isleyis-danismani` | Statü/finansal-araç modelleme kararı |

## ONLINE-DOĞRULA disiplini (ZORUNLU)
- Kesin hesap kodu, KDV/tevkifat oranı, tebliğ no, özel durum (örn. dövizli çek değerleme, reeskont)
  → **WebSearch** yetkili kaynak (vergidosyasi.com, muhasebetr.com, alomaliye.com, GİB, mevzuat.gov.tr).
- Bulguyu **kaynak linkiyle** raporla; ezbere hesap kodu yazma. Şüphe = ara, uydurma.
- Mevzuat değişebilir (oran/tebliğ) → tarih-duyarlı; "2026 itibarıyla" diye not düş.

---

## 0. MUHASEBENİN 12 TEMEL KAVRAMI [DOC — MSUGT 1 Seri No'lu Tebliğ / ISMMMO]

Türk muhasebesinin anayasası. **Her finans/muhasebe SP'si, ekranı, ledger kararı bu 12 kavramı GÖZETİR** —
"kayıt doğru görünüyor" yetmez, hangi temel kavrama uyduğu/uymadığı sorgulanır. Operax SQL-First: kavram → SP/şema
tasarım kuralına iner.

| # | Kavram | Öz (MSUGT) | Operax tasarım karşılığı / kod-kontrolü |
|---|---|---|---|
| 1 | **Sosyal Sorumluluk** | Belli grup değil tüm toplum çıkarı; gerçeğe uygun, şeffaf raporlama | Denetlenebilir izler (`AuditLog`), doğru mali tablo; veri gizleme/çarpıtma yok |
| 2 | **Kişilik** | İşletme sahip/ortak/personelden AYRI tüzel kişilik | `CompanyId` izolasyonu; şirket kasası ≠ şahıs; ortak/patron cari **ayrı Partner** (şahsi harcama şirkete yazılmaz) |
| 3 | **İşletmenin Sürekliliği** | Faaliyet süresiz varsayılır (tasfiye değil) | Değerleme **maliyet esaslı** (likidasyon değeri değil); amortisman/itfa süreklilik varsayar |
| 4 | 🔴 **Dönemsellik** | Sınırsız ömür dönemlere bölünür; her dönem **bağımsız**; gelir/gider **ait olduğu döneme** (tahakkuk) | `AccountingPeriod` OPEN/CLOSED/LOCKED + `sp_GuardPeriodOpen`; hareket **MovementDate** ile döneme düşer (sistem tarihi değil); 7-gün/ay-sonu fatura kuralı; tahakkuk eden gelir/gider doğru döneme |
| 5 | **Parayla Ölçülme** | Yalnız para ile ölçülebilen olay kaydedilir; ortak ölçü TL | Tutarlar `DECIMAL(18,4)`; `Currency NVARCHAR(3)` + kur; ölçülemeyen değer (marka itibarı vb.) kaydedilmez |
| 6 | **Maliyet Esası** | Varlık/hizmet **elde etme maliyetiyle** muhasebeleşir (para/alacak hariç) | `ItemCost` moving-avg; stok maliyet ledger; piyasa değeriyle yukarı-yazma YOK (ihtiyatlılık istisnası ayrı) |
| 7 | 🔴 **Tarafsızlık ve Belgelendirme** | Kayıt **objektif belgeye** dayalı, gerçekçi, yöntem seçimi ön yargısız | Her `AccountMovement`/`StockMovement`/`FinancialTransaction` **`SourceDocType`+`SourceDocId` zorunlu** (belgesiz hareket yok); guard'lar |
| 8 | **Tutarlılık** | Seçilen muhasebe politikası dönemler arası **değişmeden** uygulanır | Maliyet yöntemi (FIFO/MA), değerleme, statü kümeleri sabit; değişirse açıklama + tarih |
| 9 | **Tam Açıklama** | Mali tablolar karar için yeterli/açık/anlaşılır | Detaylı ekran/rapor/dipnot; gizli netleştirme yok (örn. çift-sayım gizleme değil, kaynakta ayır) |
| 10 | 🔴 **İhtiyatlılık (Muhafazakârlık)** | Şüpheli gider/zarar **karşılık** ayrılır; **gerçekleşmemiş kâr yazılmaz** | Şüpheli alacak (128/138) karşılık; gerçekleşmemiş kur farkı temkinli; gelir gerçekleşince (tahsil/teslim) yazılır, sipariş anında değil → **açık sipariş ledger-dışı** (MEMORY: open-orders-not-in-ledger) |
| 11 | **Önemlilik** | Karara etki edebilen kalem atlanmaz/gizlenmez | Varyans maddiyet eşiği, yuvarlama toleransı; önemli fark gizlenmez (PriceVariance her sapmada DRAFT) |
| 12 | 🔴 **Özün Önceliği** | Şekil değil **finansal öz**; işlemin gerçek mahiyeti | Finansal-araç tipi gerçek işleve göre (EFT≠Havale, vadeli çek=alacak, `InstrumentType`≠`PaymentMethod`); belgenin adı değil işlevi modellenir (erp-isleyis-danismani ile) |

🔴 = en çok kod-bağlayıcı (dönemsellik, belgelendirme, ihtiyatlılık, özün önceliği). Yeni finans SP'si/ekranı
yazarken bu 4'ü açıkça kontrol et.

**Kaynak:** [ISMMMO — Muhasebenin Temel Kavramları (MSUGT 1 Seri No'lu Tebliğ)](https://ismmmo.org.tr/Mevzuat/I-Muhasebenin-Temel-Kavramlari---4003)

---

## 1. TDHP Çekirdek Hesaplar (Operax kapsamı)

| Kod | Hesap | Karakter | Operax karşılığı |
|---|---|---|---|
| 100 | Kasa | Aktif (borç) | FinancialTransaction (Kasa hesabı) |
| 101 | Alınan Çekler | Aktif (borç) | `Cheque` Direction=Received, Status=PORTFOLIO |
| 102 | Bankalar | Aktif (borç) | FinancialTransaction (Banka hesabı) |
| 103 | Verilen Çekler ve Ödeme Emirleri (-) | Pasif düzenleyici | `Cheque` Direction=Issued |
| 120 | Alıcılar | Aktif (borç) | `AccountMovement` (müşteri cari, Borç bakiye) |
| 121 | Alacak Senetleri | Aktif (borç) | `PromissoryNote` Direction=Received |
| 128/138 | Şüpheli Ticari/Diğer Alacaklar | Aktif | (yasal takip — gelecek) |
| 153 | Ticari Mallar | Aktif | StockMovement / ItemCost |
| 320 | Satıcılar | Pasif (alacak) | `AccountMovement` (tedarikçi cari, Alacak bakiye) |
| 321 | Borç Senetleri | Pasif | `PromissoryNote` Direction=Issued |
| 191/391 | İndirilecek/Hesaplanan KDV | Aktif/Pasif | fatura KDV |
| 600/610 | Yurtiçi Satışlar / İade | Gelir | SalesInvoice |
| 770 | Genel Yönetim Gideri | Gider | ExpenseInvoice + CostCenter |

**Yön kuralı:** Aktif hesap artışı=Borç, azalışı=Alacak. Pasif tam tersi. Operax `AccountMovement`
bakiye = `SUM(Borç − Alacak)`; cari müşteri borç-bakiye (120), tedarikçi alacak-bakiye (320).

---

## 2. Çek/Senet Muhasebe İşleyişi (DOĞRULANMIŞ — 2026-06-23, kaynaklı)

**Alınan çek (müşteri çeki) — 3 an:**

| An | Kayıt (TDHP) | Operax |
|---|---|---|
| **Çek ALINDI** (portföye) | Borç **101** Alınan Çekler / Alacak **120** Alıcılar → **cari KAPANIR** | Cheque PORTFOLIO + **AccountMovement Credit** (partner alacaklanır) |
| **TAHSİL** (banka ödedi) | Borç **102** Bankalar / Alacak **101** → **cari'ye DOKUNMA** | FinancialTransaction INCOME + Cheque COLLECTED; **AccountMovement YAZMA** |
| **KARŞILIKSIZ / iade** | Borç **120** Alıcılar / Alacak **101** = **ters kayıt, cari geri açılır (iade)** + banka masrafı alıcıya | Cheque RETURNED + **AccountMovement Debit** (iade) + masraf |

- Yasal takibe geçilirse: 101 → **128** Şüpheli Ticari Alacaklar (esas faaliyet) / **138** (diğer).
- **Verilen çek** simetrik: verildiği an Borç **320** Satıcılar / Alacak **103** → payable cari kapanır.
- **Kritik hata deseni:** cari kapamayı TAHSİL anına bağlamak YANLIŞ — alış anına bağlanır (çek vadesi
  boyunca cari yanlış borçlu görünür, aging/risk şişer). Operax kodu bu hatayı taşıyordu (Plan 50 düzeltir).

**Senet (alacak/borç senedi):** 121/321; çek ile aynı mantık (alış-anı cari kapama).

**Kaynaklar:** [vergidosyasi — 101 Alınan Çekler](https://vergidosyasi.com/2017/11/14/101-alinan-cekler-hesabi-niteligi-isleyisi-ve-ornek-muhasebe-kayitlari/) · [muhasebetr — Çekler ve Karşılıksız Çekler](https://www.muhasebetr.com/yazarlarimiz/cumhurcetin/021/) · [muhasebedersleri — 101 işleyişi](https://www.muhasebedersleri.com/hesaplar/101-alinan-cekler.html)

---

## 3. Operax Eşleme + Disiplin

- **AccountMovement = cari alt-defter** (120/320). Çek portföyü = `Cheque` tablosu (101/103). GL fişi
  (yevmiye) **henüz yok** — periyodik GL muhasebeleştirme ayrı modül (subledger→GL posting-rule).
- Ledger append-only (`document-immutability.md`): düzeltme = ters kayıt (REVERSAL), silme yok.
- Dönem kilidi: her muhasebe hareketi `sp_GuardPeriodOpen` (LOCKED dönem = e-Defter berat → THROW).
- **Kod yazmadan önce:** hangi hesap, hangi yön, hangi an → 1-2 cümle + belirsizse online doğrula + kaynak.
- **12 temel kavram lensi (§0):** finans SP/ekran/ledger kararında 4 kod-bağlayıcı kavramı (🔴 dönemsellik,
  belgelendirme, ihtiyatlılık, özün önceliği) açıkça gözet — "kayıt doğru görünüyor" yetmez, hangi kavrama dayandığı.

## İlişkili
- `.claude/skills/mali-evrak-mevzuat/SKILL.md` — evrak/e-belge VUK (tamamlayıcı)
- `.claude/skills/mali-islem-akislari/SKILL.md` — operasyonel mutabakat/varyans/kapanış
- `.claude/rules/document-immutability.md` — ledger append-only + dönem kilidi
- `docs/reference/MIKRO_V16_ANALYSIS.md` §3.5 — posting-rule deseni (GL muhasebeleştirme)
