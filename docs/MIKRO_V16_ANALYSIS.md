# Mikro V16/V17 Veritabanı Şeması — Operax Referans Çalışması

> **Kaynak:** Mikro V17 tablo dokümantasyonu mirror'ı — `https://www.ozgurguler.net/blog/MikroV17/Tablolar/<tablo>.htm`
> (V16 mirror perteknoloji.com bazı detay sayfalarında 404 verdiği için V17 mirror kullanıldı; çekirdek
> kolonlarda V16↔V17 yapısal fark beklenmez, V17 ekleri çoğunlukla e-Belge/e-Ticaret kolonları.)
> **İnceleme:** (1) `reference-researcher` ajanı (opus) 6 tabloyu 2+ tutarlı WebFetch ile okudu; (2) Fikri 4 tabloyu
> **resmi Mikro dokümanından tam kolon** yapıştırdı (en yüksek güven) — Tablo 1 MUHASEBE_HESAP_PLANI, 2 MUHASEBE_FISLERI,
> 3 SORUMLULUK_MERKEZLERI, 5 STOK_MUHASEBE_GRUPLARI, 74 CARI_HAREKET_BORC_ALACAK_ESLEME, 164 STOK_HAREKET_MALIYET_DETAYLARI.
>
> **Kanıt katmanı:** `[REPO-HTM]` = tablonun .htm sayfası kolon kolon okundu (URL belirtildi) · `[OPERAX]` =
> yerel dosya:satır veya doğrulanmış karar · `DOĞRULANMADI` = sayfada görülmedi/erişilemedi (tahmin YASAK).
>
> **⚠️ HALÜSİNASYON TEMİZLİĞİ (2026-05-30):** Bu dosyanın ilk taslağındaki kolon iddiaları ("cha_Kod int
> IDENTITY", "msf_borc/msf_alacak", "320 tablo/8945 kolon", "tableData JSON parse") tek bir WebFetch
> halüsinasyonuna dayanıyordu, GERÇEĞE AYKIRIYDI ve SİLİNDİ. Gerçek: PK her tabloda `<önek>_Guid
> uniqueidentifier`; cari borç/alacak tek `cha_meblag`+`cha_tip` flag; GL meblağ `fis_meblag0..6`; JSON yok.

## 0. Yöntem / Güven
Her tablo 2+ WebFetch ile çekildi; kolon adı/tip düzeyinde tutarlı döndü (halüsinasyon riski düşük). Mirror resmi
`CREATE TABLE` DDL'i DEĞİLDİR; PK'nin fiziksel CLUSTERED niteliği ve index detayı sayfalarda kısmen ("Unique
Index NDX_..._00 on _Guid", "Primary Key Index NDX_..._02 on _kod") → fiziksel ayrıntı "kısmen / DOĞRULANMADI".

## 0.5 🔴 EN ÖNEMLİ MİMARİ DESEN — Tek Hareket Tablosu + Tip Ayrımı (polymorphic ledger)

**Mikro'da her evrak tipi için AYRI tablo YOK.** Tüm hareketler **tek geniş tabloda**, **tip kolonlarıyla** ayrışır:
- **Stok tarafı:** TEK `STOK_HAREKETLERI` — alış/satış/transfer/sayım/üretim/iade/fire hepsi burada.
  Ayrım: `sth_tip` (giriş/çıkış) + `sth_cins` (~16 cins: alış faturası, satış irsaliyesi, sayım fazla/eksik, sarf,
  fire, depo transfer…) + `sth_evraktip` (evrak türü) + `sth_normal_iade`. Evrak = aynı `sth_evrakno_seri +
  sth_evrakno_sira`'yı paylaşan satır kümesi (header ayrı tablo değil; başlık alanları satırda tekrarlı).
- **Cari tarafı:** TEK `CARI_HESAP_HAREKETLERI` — fatura/tahsilat/ödeme/çek/mahsup/açılış hepsi burada.
  Ayrım: `cha_evrak_tip` + `cha_tip` (borç/alacak) + `cha_cinsi` + `cha_kaynak` (hangi modülden geldi).
- **Finans/kasa-banka:** benzer şekilde tip kolonlu tek hareket tablosu mantığı.

**Operax ile kıyas (kritik fark — iki katmanı ayır):**

| Katman | Mikro | Operax | Sonuç |
|---|---|---|---|
| **Ledger (stok/cari etki)** | tek tablo + tip (`STOK_HAREKETLERI`, `CARI_HESAP_HAREKETLERI`) | tek tablo + tip (`StockMovement`+MovementType, `AccountMovement`) | **AYNI desen** — Operax ledger kararı Mikro tarafından DOĞRULANIR |
| **Belge başlığı/satırı** | AYRI tablo YOK — evrak = `STOK_HAREKETLERI` içinde evraktip+seri+sıra | AYRI tablolar: ReceivingHeader/Line, ShippingHeader/Line, SalesOrderHeader/Line, TransferLine… | **FARKLI** — Operax belge katmanını normalize ayrıştırmış |

**Ders:**
- **(a) ÇALINIR (ledger):** Operax'ın "tüm stok etkisi tek StockMovement'a tip ile, tüm cari etki tek
  AccountMovement'a" kararı = Mikro'nun 30 yıllık kanıtlı deseni. Yeni belge tipi eklemek **yeni ledger tablosu
  GEREKTİRMEZ** — sadece yeni MovementType/SourceDocType. Bu kararı sağlamlaştırır; ledger tablosu çoğaltma anti-pattern.
- **(b) GÖRMEZDEN GEL (belge başlığı):** Mikro'nun **belge başlığını da tek hareket tablosuna gömme** deseni
  (header alanlarını her satırda tekrar etme, ayrı Header tablosu olmaması) KOPYALANMAZ. Operax'ın ayrı
  ReceivingHeader/ShippingHeader yaklaşımı daha normalize, daha az tekrar, evrak-bütünlüğü (immutability) ve
  durum makinesi (DRAFT/POSTED) için gerekli. Mikro tek-tablo başlık deseni denormalize + Delphi-çağı tercihi.
- **(c) OPERAX GAP / DİKKAT:** Operax çok sayıda belge tablosuna sahip (ReceivingHeader, ShippingHeader, Transfer,
  CycleCount, ProductionOrder…). Bunlar **belge katmanı** — doğru. Ama ledger tarafında **yeni hareket tablosu
  AÇMAMA** disiplini korunmalı: her stok etkisi StockMovement'a, her cari etki AccountMovement'a tip ile yazılır;
  modül-bazlı ayrı "XStokHareket" tablosu açma riski varsa engellenir (mevcut durumda Operax doğru — bu bir uyarı notu).

> **Özet:** Operax = "belge katmanı ayrışık (normalize) + ledger katmanı birleşik (polymorphic)". Mikro = "ikisi de
> birleşik". Operax'ın hibriti **daha iyi** (ledger faydasını alır, belge normalizasyonunu da). Mikro deseni ledger
> kararını doğrular, belge-tek-tablo kısmını reddederiz.

## 1. STOK_HAREKETLERI — EN KRİTİK
**Kanıt:** [REPO-HTM] https://www.ozgurguler.net/blog/MikroV17/Tablolar/stok_hareketleri.htm (~155 kolon)

- **PK:** `sth_Guid uniqueidentifier` (CLUSTERED mı DOĞRULANMADI). Paralel artan: `sth_SpecRECno integer`, `sth_Hash bigint`.
- **İzolasyon:** `sth_firmano integer` + `sth_subeno integer` — her satırda.
- **Yön/iade:** `sth_tip tinyint` (giriş/çıkış/transfer) · `sth_cins tinyint` · `sth_normal_iade tinyint` (0:Normal/1:İade) · `sth_evraktip tinyint`. Miktar `sth_miktar float` işaretsiz; yön tip kolonundan. İade ayrı satır.
- **İptal:** `sth_iptal bit` (soft-cancel; silme değil).
- **Evrak/tarih:** `sth_evrakno_seri`+`sth_evrakno_sira int`+`sth_satirno int` · `sth_belge_no`+`sth_belge_tarih` · `sth_tarih` · `sth_fis_tarihi`+`sth_fis_sirano` (muhasebe köprüsü).
- **Miktar/maliyet:** `sth_miktar`, `sth_miktar2`, `sth_birimfiyat`, `sth_tutar`, `sth_maliyet_ana/alternatif/orjinal float`.
- **🔴 K6 KANITI:** Running-balance / "kalan" / "after" / kümülatif-StockValue snapshot kolonu **YOK**. `sth_maliyet_*` satır-anı valuation; kümülatif değil. → **K6 (snapshot reddi) DOĞRULANDI.** (ERPNext SLE snapshot tutuyordu — Mikro karşı-kanıt, K6'yı güçlendirir.)
- **K7:** maliyet harekette + parti/lot `sth_parti_kodu nvarchar(25)` / `sth_lot_no int`. Ayrı maliyet-detay tablosu → §1.5.
- **Audit:** `sth_create_user/date`, `sth_lastup_user/date`.
- **Görmezden gel (Delphi şişkinliği):** `sth_isk_mas1..10`+`sth_sat_iskmas1..10`+`sth_iskonto1..6`+`sth_masraf1..4` (40+ kolon); `sth_Olcu1..5`; sistem kolonları (`_DBCno/_fileid/_checksum/_Hash/_special1..3`).

## 1.5 STOK_HAREKET_MALIYET_DETAYLARI — 🔴 FIFO katman tüketim eşleme (K7 — kritik)
**Kanıt:** [REPO-HTM] Tablo No 164 (kullanıcı sağladı, 34 kolon) — Güncelleme 27.11.2023

- **PK:** `shd_Guid uniqueidentifier` (CONSTRAINT PRIMARY KEY) · ikincil index `shd_stok_hareket_uid` (NDX_..._02).
- **Çekirdek (5 iş kolonu):**
  - `shd_stok_hareket_uid uniqueidentifier` → ÇIKIŞ hareketine FK (`STOK_HAREKETLERI.sth_Guid`).
  - `shd_tuketim_stok_giris_uid uniqueidentifier` → tüketilen GİRİŞ hareketine FK (hangi giriş katmanından).
  - `shd_tuketim_stok_miktari float` → o girişten tüketilen miktar.
  - `shd_yuklenen_maliyet_ana/alt/orj float` → yüklenen maliyet (üç döviz paralel).
  - `shd_tipi tinyint` (0:Malzeme 1:Operasyon) · `shd_hesap_kodu` · `shd_srm_merkezi` (sorumluluk merkezi).
- **Ne yapar:** Bir çıkış hareketinin **hangi giriş katmanlarından ne kadar tükettiğini** satır satır tutar →
  bu **kalıcı FIFO eşleme tablosu** (ERPNext `stock_queue` JSON'unun normalize/kalıcı hali).

- **🔴 K7 İÇİN KRİTİK — KISMİ KARŞI-KANIT:** Operax K7 kararı = "FIFO snapshot'sız, **SP içi anlık kuyruk**, ayrı
  CostLayer tablosu **GEREKMEZ**." Mikro ise FIFO katman-tüketimini **kalıcı ayrı tabloda** (çıkış↔giriş eşleme
  satırları) tutuyor → "ayrı tablo gerekmez" varsayımını **desteklemiyor.** TR'nin yaygın ERP'si FIFO'yu kalıcı
  eşleme tablosuyla çözmüş.
  - **✅ KARAR (Fikri, 2026-05-30 — K7 revize):** **Kalıcı eşleme tablosu** seçildi. Operax FIFO'yu Mikro-stili
    kalıcı `StockCostConsumption(CikisMovementId, GirisMovementId, Miktar, Maliyet)` tablosunda tutacak (snapshot
    DEĞİL — çıkış↔giriş katman tüketim izi). "Ayrı tablo gerekmez" varsayımı İPTAL. Snapshot reddi (K6) hâlâ geçerli.
  - **(a) ÇALINDI:** çıkış↔giriş eşleme satırı deseni alındı; FIFO denetlenebilir + iade/düzeltmede katman
    geri-açılabilir. Operax `StockCostConsumption` tablosu bu desende kurulacak (uygulama roadmap/B5).
- **Görmezden gel:** `shd_DBCno/_fileid/_checksum/_Hash/_special1..3/_Mikro*` sistem kolonları.

## 2. CARI_HESAP_HAREKETLERI — cari subledger defteri
**Kanıt:** [REPO-HTM] https://www.ozgurguler.net/blog/MikroV17/Tablolar/cari_hesap_hareketleri.htm (~200 kolon)

- **PK:** `cha_Guid uniqueidentifier` (clustered DOĞRULANMADI). `cha_SpecRecNo integer`.
- **İzolasyon:** `cha_firmano`+`cha_subeno integer`. **İptal:** `cha_iptal bit`.
- **🔴 Borç/Alacak (K1/K3):** TEK tutar `cha_meblag float` + yön `cha_tip tinyint` (Borç/Alacak) + `cha_cinsi`, `cha_normal_Iade`, `cha_tpoz`. → Operax'ın iki-kolon `AccountMovement(Borc,Alacak)` desenine ZIT kodlama; ikisi de geçerli, Operax kararı değişmez.
- **Tarih/evrak/cari:** `cha_tarihi` · `cha_belge_no`+`cha_belge_tarih` · `cha_evrakno_seri/sira/satir_no` · `cha_vade integer` · `cha_kod nvarchar(25)` (cari iş anahtarı — GUID FK değil) · `cha_fis_tarih`+`cha_fis_sirano` (GL köprü) · `cha_uuid nvarchar(40)` (e-Fatura UUID).
- **Görmezden gel:** `cha_vergi1..20`+`cha_ilave_edilecek_kdv1..20` = 40 vergi kolonu; `cha_isk_mas1..10`+`cha_sat_iskmas1..10`+`cha_ft_iskonto1..6`+`cha_ft_masraf1..4`. → Operax JSON AdditionalFields/detay tablosu kullanır; bu denormalize N'li kolon deseni KOPYALANMAZ.

## 2.5 CARI_HAREKET_BORC_ALACAK_ESLEME — 🔴 ödeme-kapama (açık kalem matching)
**Kanıt:** [REPO-HTM] Tablo No 74 "Cari Hesap Kapama" (kullanıcı sağladı, 37 kolon) — Güncelleme 29.11.2023

- **PK:** `chk_Guid` (PRIMARY KEY) · index'ler `(chk_ChCinsi, chk_ChKodu, chk_Borc_uid)` ve `(…, chk_Alc_uid)`.
- **Çekirdek (eşleme):**
  - `chk_Borc_uid uniqueidentifier` ↔ `chk_Alc_uid uniqueidentifier` → bir **borç hareketini bir alacak hareketine** bağlar (hangi tahsilat hangi faturayı kapattı).
  - `chk_Tutar float` → kapanan tutar (kısmi kapama destekli; bir fatura çok satırla kapanır) · `chk_OrjBorcTutar/OrjAlacakTutar` (döviz).
  - `chk_BorcVade`+`chk_Alacakvade datetime` → vade eşleşmesi (yaşlandırma/vade analizi için).
  - `chk_HangiTutar tinyint` (0:Ara toplam 1:Masraf 2:Vergi) → tutarın hangi bileşeni kapanıyor.
- **🔴 POLYMORPHIC (yine tek tablo + tip):** `chk_ChCinsi tinyint` 11 hesap cinsi (0:Cari 1:Cari personel 2:Banka
  3:Hizmet 4:Kasa 5:Gider 6:Muhasebe 7:Personel 8:Demirbaş 9:İthalat 10:Finansal sözleşme). **Tek kapama tablosu
  tüm hesap tiplerini** kapatıyor — §0.5 "tek tablo + tip" deseninin kapama katmanındaki tekrarı.
- **Ders:**
  - **(a) ÇALINIR — OPERAX GAP:** Operax'ta **açık-kalem kapama/eşleme yok.** AccountMovement bakiyeyi net
    tutuyor (borç−alacak SUM) ama "hangi tahsilat hangi faturayı kapattı" izi yok. Vade-bazlı yaşlandırma
    (`tvf_PaymentPlanAging`) PaymentPlan üzerinden ama **hareket-seviyesi kapama** (FIFO/spesifik fatura eşleme)
    yok. Mikro deseni: ayrı `AccountReconciliation(BorcMovementId, AlacakMovementId, Tutar, Bilesen)` tablosu.
    Etki: orta-yüksek (doğru yaşlandırma + "açık fatura" raporu) / Maliyet: orta (yeni tablo + kapama SP'si).
  - K9 (cari mutabakat freeze) ile ilişki: kapama tablosu varsa "X tarihine kadar kapatılmış kalemler" doğal
    olarak mutabakat kapsamı → K9 guard'ı kapama tablosunu da okuyabilir.
- **Görmezden gel:** sistem kolonları (`_DBCno/_checksum/_Hash/_special*`).

## 3. MUHASEBE_FISLERI — GL fiş satırı (TAM KOLON — kullanıcı sağladı)
**Kanıt:** [REPO-HTM] Tablo No 2, 62 kolon tam liste (kullanıcı yapıştırdı) — Güncelleme 16.08.2023

- **Yapı:** Fiş satırı = TEK satır = TEK hesap (ERPNext GL Entry gibi); fiş = `fis_tarih`+`fis_sira_no`'yu paylaşan N satır (`fis_satir_no`). Ayrı header tablosu YOK.
- **PK/izolasyon:** `fis_Guid uniqueidentifier` (PK) · `fis_firmano integer`+`fis_subeno integer` · `fis_maliyil integer` (Mali Yıl) · `fis_iptal bit`.
- **🔴 UNIQUE index'ler (resmi defter bütünlüğü):** `(fis_firmano, fis_maliyil, fis_yevmiye_no, fis_satir_no)` + `(fis_tarih, fis_sira_no, fis_satir_no)`. → Yevmiye no firma+mali yıl bazında tekil; resmi defter sırası garanti.
- **Anahtarlar:** `fis_tarih`, `fis_sira_no`, `fis_satir_no`, `fis_tur tinyint` (0:Mahsup 1:Tahsil 2:Tediye 3:Açılış 4:Kapanış), `fis_hesap_kod nvarchar(25)` (→ MUHASEBE_HESAP_PLANI), `fis_yevmiye_no integer` (resmi defter sırası), `fis_aktif_pasif tinyint`, `fis_proje_kodu`, `fis_sorumluluk_kodu` (masraf merkezi → SORUMLULUK_MERKEZLERI).
- **🔴 Meblağ/döviz (K1) — DÜZELTME:** Borç/alacak ayrı kolon DEĞİL, **işaret-bazlı tek kolon**: `fis_meblag0 float` (yerli döviz) — **>0 ise Borç, <0 ise Alacak**. `fis_meblag1` (alt döviz), `fis_meblag2` (orj döviz) — aynı işaret kuralı, paralel 3 döviz. `fis_meblag3..6` stok hesabıysa 1./2./3./4. birim miktarı (yine işaretli). → Operax `AccountMovement(Borc, Alacak)` iki-kolon deseninden FARKLI (Mikro işaret, Operax iki kolon); ikisi de geçerli, Operax kararı değişmez.
- **🔴 Ticari↔GL köprü (K1/K3):** `fis_ticari_tip tinyint` (0:İlişki yok 1:Stok 2:Cari 3:Sipariş 4:Personel 5:Akaryakıt 6:Demirbaş 7:SMM) · `fis_ticari_uid uniqueidentifier` (ilgili subledger kaydı) · `fis_ticari_evraktip` · `fis_tic_evrak_seri/sira/belgeno/belgetarihi` · index `(fis_ticari_tip, fis_ticari_uid)`. → GL satırı kaynağını geri-izler; subledger ile GL **ayrı, gevşek bağlı, aktarım adımı var (perpetual DEĞİL).** → **K1/K3 DOĞRULANDI (tam kolonla teyit).**
- **🔴 Mahsup tipi zenginliği (`fis_fmahsup_tipi`, 23 değer):** Standart / Yansıtma açılış-kapanış / Dönem kar-zarar / Vergilendirme / **SMM (Satılan Mal Maliyeti) mahsubu** / **Kur farkı mahsubu** / Maliyet dağıtım / Enflasyon farkı / Şüpheli alacak… → periyodik muhasebeleştirmede üretilen fiş türleri kataloğu. **K1 GL modülü için doğrudan referans** (hangi otomatik mahsuplar gerekir).

## 3.5 🔴 GL ALTYAPISI (K1/K2 PERİYODİK MUHASEBELEŞTİRME MODÜLÜ İÇİN TAM İSKELET)
**Kanıt:** [REPO-HTM] Tablolar 1, 3, 5 tam kolon (kullanıcı sağladı). Bu üç tablo, ertelenmiş K1/K2 modülünün doğrudan referans tasarımı.

### 3.5.1 MUHASEBE_HESAP_PLANI (Tablo 1) — kebir hesap planı
- **PK/anahtar:** `muh_Guid` (PK) · `muh_hesap_kod nvarchar(25)` **UNIQUE** (iş anahtarı, tekdüzen hesap planı kodu) · `muh_grupkodu nvarchar(4)` + index `(grupkodu, hesap_kod)` (hiyerarşi).
- **🔴 Hesap tipi:** `muh_hesap_tip tinyint` — **0:Aktif 1:Pasif 2:Gelir 3:Gider 4:Nazım** (bilanço/gelir-tablosu sınıfı).
- **🔴 Çalışma şekli:** `muh_calisma_sekli tinyint` — 0:Borç 1:Alacak 2:Borç-Alacak (hesabın doğal bakiye yönü).
- **🔴 Hesap-bazlı kilit:** `muh_kilittarihi datetime` — bu hesaba kadar tarihli kayıt kilidi. → **K4 dönem kilidiyle ilişki:** Mikro hem dönem (firma+mali yıl) hem **hesap-bazlı** kilit tutuyor; Operax K4 sadece dönem-bazlı. Hesap-bazlı kilit Operax kapsamında YOK (not).
- **Boyut zorunluluğu:** `muh_sorum_merk tinyint` (0:Serbest 1:Gereksiz 2:Gerekli) + `muh_proje_detayi` (aynı) → masraf merkezi/proje boyutu hesap bazında zorunlu kılınabilir.
- **Maliyet/KDV:** `muh_maliyet_dagitim_sekli` (13 yöntem), `muh_kdv_tipi`, `muh_kdv_dagitim_sekli`, `muh_kurfarki_fl` (kur farkı hesabı mı), `muh_kesin_mizan_hesap_kodu`.
- **(a) ÇALINIR (K1 modülü):** Hesap planı çekirdek modeli = `HesapKod (unique) + HesapTip(Aktif/Pasif/Gelir/Gider/Nazım) + CalismaSekli + GrupKodu hiyerarşi + boyut-zorunluluk(masraf merkezi/proje)`. Tekdüzen Hesap Planı (Türkiye) bu yapıyla kurulur.

### 3.5.2 SORUMLULUK_MERKEZLERI (Tablo 3) — masraf/kar merkezi (cost center)
- **PK/anahtar:** `som_Guid` (PK) · `som_kod nvarchar(25)` UNIQUE · `som_isim`.
- **Tip:** `som_tipi tinyint` (0:Genel masraf mrk 1:Genel kar mrk 2:Doğrudan üretim masraf 3:Dolaylı üretim masraf 4:Satış kar 5:Kampanya satış kar 6:Yatırım 7:Ödenmeyen değerli kağıt).
- **Maliyet dağıtım:** `som_MaliyetDagitimSekli` (13 yöntem), `som_DagAnahKodu` (dağıtım anahtarı), `som_MasrafNereyeYuklenecek` (iş merkezi/iş emri/ürün/operasyon/kalıp).
- **(c) OPERAX GAP:** Operax'ta masraf merkezi / cost center YOK. K1 GL modülünde gelir; bugün kapsam dışı (K2 ertelendi). Üretim maliyet dağıtımıyla (M10) da bağlantılı.

### 3.5.3 STOK_MUHASEBE_GRUPLARI (Tablo 5) — 🔴 POSTING-RULE EŞLEME (en kritik K1 dersi)
- **PK/anahtar:** `stmuh_Guid` (PK) · `stmuh_kod` UNIQUE · `stmuh_ismi`.
- **🔴 İçerik:** Her stok muhasebe grubu için **~45 muhasebe-hesap-kodu eşleme kolonu**: `stmuh_muh_kod` (stok hesabı), `stmuh_iade_muh_kod`, `stmuh_YurtIciSatMuhK` (yurtiçi satış), `stmuh_SatIadeMuhKod`, `stmuh_SatIskMuhKod` (satış iskonto), `stmuh_Al_IskMKod` (alış iskonto), `stmuh_SatMalMuhKod` (**satış maliyeti = SMM**), `stmuh_YurtDisiSatMuh` (ihracat), `stmuh_depsatmuhkod` (depolar arası), `stmuh_bagortsat*` (bağlı ortaklık), + her biri için **UFRS fark karşılığı** (`*_ufrsfark_kod`, ~25 kolon).
- **🔴 BU NEDİR — K1 "posting-rule sahipliği" cevabı:** Bu tablo, **operasyon hareketinin hangi muhasebe hesabına gideceğini** belirler. Satış faturası → `YurtIciSatMuhK`; SMM mahsubu → `SatMalMuhKod`; iade → `iade_muh_kod`. Periyodik muhasebeleştirme SP'si stok hareketini okur, ürünün muhasebe grubuna bakar, doğru kebir hesabını bu tablodan çeker, MUHASEBE_FISLERI'ne yazar.
- **(a) ÇALINIR (K1 modülü — kritik):** Operax periyodik GL modülü açıldığında **posting-rule = "kaynak hareket tipi + ürün/cari muhasebe grubu → kebir hesap kodu" eşleme tablosu** olmalı. Bu, K1 kararındaki "posting-rule sahipliği o zaman kararlaştırılacak" açık sorusunun **somut desen cevabı**: eşleme grup bazında (her ürüne tek tek değil), yön bazında (satış/iade/iskonto/maliyet) ayrı hesap.
- **(b) GÖRMEZDEN GEL:** ~25 UFRS-fark kolonu + bağlı-ortaklık/ihraç-kayıtlı varyasyonları — TR çok-defter (VUK+UFRS paralel) gereksinimi; Operax tek-defter başlar, UFRS ileride. N'li düz kolon yerine Operax `PostingRule(GroupId, MovementType, AccountCode)` satır-bazlı normalize tablo kullanmalı (45 kolon değil).

> **K1/K2 NET ÇIKARIM:** Periyodik GL modülü 3 yapı taşı ister (Mikro doğruladı): **(1)** Hesap Planı (tip+çalışma şekli+hiyerarşi) · **(2)** Posting-Rule eşleme (hareket tipi + muhasebe grubu → hesap kodu; normalize) · **(3)** masraf merkezi/proje boyutu (opsiyonel). Muhasebeleştirme SP'si: subledger hareketi oku → grup+yön→hesap → işaretli meblağla MUHASEBE_FISLERI'ne yaz, `fis_ticari_uid` ile geri-bağla. Mevzuat skill'i (K2 ön koşulu) hesap planı standardı + mahsup türlerini (`fis_fmahsup_tipi` 23 değer) tanımlar.

## 4. CARI_HESAPLAR — cari master
**Kanıt:** [REPO-HTM] https://www.ozgurguler.net/blog/MikroV17/Tablolar/cari_hesaplar.htm (~190 kolon)

- **PK:** `cari_Guid` (PRIMARY KEY) · **iş anahtarı** `cari_kod nvarchar(25)` UNIQUE · `cari_DBCno smallint`.
- **Anahtarlar:** `cari_unvan1/2`, `cari_baglanti_tipi tinyint` (müşteri/tedarikçi/iştirak 0-8), `cari_Ana_cari_kodu` (parent/grup hiyerarşi), `cari_grup_kodu`, `cari_bolge_kodu`.
- **Risk:** `cari_KrediRiskTakibiVar_flg bit` (sadece BAYRAK) + `cari_b/a_bakiye_degerlendirilmesin_fl bit ×6`.
- **🔴 BULGU:** Master'da anlık **bakiye/risk-limiti TUTARI kolonu görülmedi** → bakiye hareketlerden türetiliyor (Operax `tvf_AccountBalance` deseni DOĞRULANDI). Limit tutarı ayrı tabloda olabilir → DOĞRULANMADI.
- **Görmezden gel:** `cari_banka_*1..10` (10× banka hesabı), muhasebe-kodu şişkinliği.

## 5. STOKLAR — stok master
**Kanıt:** [REPO-HTM] https://www.ozgurguler.net/blog/MikroV17/Tablolar/stoklar.htm (~250+ kolon)

- **PK:** `sto_kod nvarchar(25)` PRIMARY KEY (NDX_STOKLAR_02) · `sto_Guid` UNIQUE (NDX_STOKLAR_00) · `sto_plu_no integer IDENTITY` (artan).
- **Anahtarlar:** `sto_isim`, `sto_cins tinyint`, `sto_detay_takip tinyint` (parti/lot/seri/beden), `sto_min_stok/siparis_stok/max_stok float`, `sto_standartmaliyet float`, `sto_birim1..4_ad/katsayi` (4 birim UOM).
- **🔴 BULGU (K6):** Master'da anlık OnHand kolonu YOK; sadece eşik + standart maliyet → eldeki miktar/değer hareketlerden. Snapshot reddini destekler. `sto_kod` PRIMARY KEY, `sto_Guid` UNIQUE: Mikro iş-anahtarını PK yapmış (CLUSTERED nitelemesi DOĞRULANMADI).

## 6. ODEME_EMIRLERI — çek/senet kartı
**Kanıt:** [REPO-HTM] https://www.ozgurguler.net/blog/MikroV17/Tablolar/odeme_emirleri.htm (~75 kolon)

- **PK/izolasyon:** `sck_Guid uniqueidentifier` · `sck_firmano`+`sck_subeno` · `sck_iptal bit`.
- **Anahtarlar:** `sck_tip tinyint` (0-13), `sck_no nvarchar(25)`, `sck_vade datetime`, `sck_tutar float`, `sck_odenen float` (kısmi tahsilat).
- **🔴 Durum:** `sck_sonpoz tinyint (0-10)` — TEK pozisyon/durum enum (portföy→bankada→tahsil→karşılıksız→ciro). Operax çek statü makinesi (PORTFOLIO/IN_BANK/COLLECTED/RETURNED/ENDORSED) karşılığı. Geçiş izi ayrı tabloda görülmedi → DOĞRULANMADI.
- **🔴 "Nerede" izleme:** `sck_sahip_cari_cins/kodu` (kimden) + `sck_nerede_cari_cins/kodu` (kimde/hangi banka). → Çekin sahiplik/konum izi cari koduyla; **Operax'ta GAP.**

## 7. Operax Kararları — Doğrulama Sonucu
| Karar | Mikro Bulgusu (kanıt) | Sonuç |
|---|---|---|
| **R4** clustered int+GUID | `_Guid uniqueidentifier` PK + artan int (`_SpecRECno`, `sto_plu_no IDENTITY`); CLUSTERED nitelemesi harf-harf yok | **KISMEN / fiziksel DOĞRULANMADI** |
| **K6** snapshot reddi | stok_hareketleri tam listede running-balance/StockValue snapshot YOK; stoklar'da OnHand YOK | **DOĞRULANDI** |
| **K7** FIFO — kalıcı eşleme tablosu (revize) | Mikro `STOK_HAREKET_MALIYET_DETAYLARI` (çıkış↔giriş+miktar+maliyet) §1.5 | **✅ ÇALINDI — K7 revize: `StockCostConsumption` kalıcı tablo (Fikri 2026-05-30). Snapshot reddi (K6) korunur.** |
| **K1/K3** subledger≠GL | cari_hesap_hareketleri ≠ muhasebe_fisleri; `fis_ticari_uid`+`fis_ticari_evraktip` köprü | **DOĞRULANDI** |
| **K10/plan12** her satır izolasyon | `_firmano`+`_subeno` her hareket satırında int; global filter yok | **DOĞRULANDI** |

## 8. NE ÇALINIR / GÖRMEZDEN GELİNİR / OPERAX GAP
**(a) ÇALINIR:** **polymorphic ledger — tek hareket tablosu + tip (yeni belge tipi yeni ledger tablosu gerektirmez; §0.5)**; snapshot'sız hareket defteri (K6); subledger↔GL gevşek bağ (`fis_ticari_uid`); çek tek-poz enum + kısmi ödeme + "nerede cari"; parti/lot harekette (K7); mali yıl+yevmiye no (`fis_maliyil`/`fis_yevmiye_no`, plan 14 AccountingPeriod ile örtüşür); 4-birim UOM deseni.
**(b) GÖRMEZDEN GELİNİR:** **belge başlığını ledger tablosuna gömme** (Mikro'da ayrı Header tablosu yok, başlık satırda tekrarlı — Operax ayrı Header/Line tutar, §0.5); sabit N'li kolonlar (`cha_vergi1..20`, 10× banka); önek+Türkçe-kısaltma kolon adları; Delphi sistem kolonları; `_iptal bit` soft-cancel (Operax reversal kullanır — append-only); Delphi/VCL stack.
**(c) OPERAX GAP:** çek konum izleme (`sck_nerede_cari_kodu`); subledger→GL köprü kolonları (K1 modülü); çok-döviz paralel tutar (`fis_meblag0/1/2`); satır-bazlı mali yıl (`fis_maliyil`); **açık-kalem kapama/eşleme tablosu** (§2.5 — hangi tahsilat hangi faturayı kapattı; Operax'ta yok); **FIFO katman-tüketim iz tablosu** (§1.5 — K7 kararına bağlı).

## 9. Özet — Mikro ↔ Operax Yan Yana
| Kavram | Mikro V17 [REPO-HTM] | Operax [OPERAX] | Fark/Sonuç |
|---|---|---|---|
| Stok hareketi PK | `sth_Guid` + `sth_SpecRECno` int | StockMovement PK GUID NEWID (REFERENCE_STUDY §1) | İkisi GUID; plan 14 NEWID fragmentasyonunu düzeltecek |
| Stok bakiye snapshot | YOK (SUM) | YOK (SUM(QtyBase) WHERE IsCancelled=0) — K6 | AYNI. K6 doğrulandı |
| Maliyet konumu | harekette `sth_maliyet_*`+parti/lot | FIFO SP içi snapshot'sız — K7 | Aynı felsefe; ayrı CostLayer ikisinde de hedeflenmiyor |
| Firma izolasyonu | `_firmano`+`_subeno` her satır (int) | CompanyId her satır (guid) — K10 | AYNI desen (Operax guid, Mikro int) |
| Cari borç/alacak | tek `cha_meblag`+`cha_tip` flag | AccountMovement(Borc,Alacak) iki kolon | Farklı kodlama; ikisi geçerli; Operax kararı değişmez |
| Subledger vs GL | cari_hareket ≠ muhasebe_fisleri (`fis_ticari_uid`) | AccountMovement subledger; GL ileri modül — K1/K3 | AYNI ayrım. Doğrulandı |
| İptal/immutability | `_iptal` bit (soft-cancel) | reversal ters kayıt; IsDeleted kaldırılacak — plan 14 | Operax daha sıkı; Mikro `_iptal` AYKIRI, alınmaz |
| Çek durumu | `sck_sonpoz` 0-10 + `sck_odenen` + nerede cari | Cheque statü makinesi — document-immutability §2.4 | Benzer; "nerede cari" GAP |
| Vergi modeli | sabit N'li kolon (vergi1..20) | JSON AdditionalFields/detay tablo | Operax modern; Mikro deseni alınmaz |
| Master bakiye | denormalize bakiye/limit YOK | tvf_AccountBalance (türetilmiş) | AYNI. Snapshot yok |
| **Ledger tablo sayısı** | TEK `STOK_HAREKETLERI` + tip; TEK `CARI_HESAP_HAREKETLERI` + tip | TEK StockMovement + MovementType; TEK AccountMovement | **AYNI** (polymorphic ledger) — yeni belge tipi ledger tablosu gerektirmez |
| **Belge başlığı** | ayrı tablo YOK (evraktip+seri+sıra tek harekette) | ayrı Header/Line tablolar (Receiving/Shipping/SO…) | **FARKLI** — Operax normalize belge katmanı (immutability+durum makinesi için doğru) |

## 10. Confidence / DOĞRULANMADI
**Yüksek (2+ tutarlı [REPO-HTM] veya kullanıcı-sağlı tam kolon):** 8 tablonun kolon adı/tipi; snapshot YOKLUĞU (K6); `_firmano/_subeno` izolasyon (K10); `cha_meblag`+`cha_tip`; subledger↔GL ayrımı+köprü (K1/K3); `sck_sonpoz`+`sck_odenen`; **FIFO eşleme tablosu (§1.5, Tablo 164)**; **ödeme-kapama tablosu (§2.5, Tablo 74)**.
**DOĞRULANMADI:** PK fiziksel CLUSTERED/NONCLUSTERED (R4 fiziksel); cari risk-limiti TUTARI kolonu; çek statü-geçiş iz mekanizması; V16↔V17 fark (V16 mirror erişilemedi).
**KAPANAN KARAR:** K7 — ✅ Fikri (2026-05-30): kalıcı `StockCostConsumption` eşleme tablosu (Mikro-stili). "Ayrı tablo yok" varsayımı iptal; snapshot reddi (K6) korunur.
**SİLİNEN halüsinasyonlar:** "cha_Kod int IDENTITY", "msf_borc/msf_alacak", "320 tablo/8945 kolon", "tableData JSON", "sth_GUID kesin clustered".

## 12. 🔴 EVRAK/HAREKET TİPİ KARŞILAŞTIRMA — Mikro var / Operax yok (lazım mı analizi)

**Kaynak:** Mikro tip enum'ları [REPO-HTM] (reference-researcher, V17 mirror, 2+ tutarlı çekim) ↔ Operax envanteri
[OPERAX] (`Lib/Dtos.cs`, code-explorer). Enum kodu yüksek güven; `sth_cins` 14/15 + `cha_evrak_tip` 51-137 DOĞRULANMADI.
**Lazım sütunu:** EVET (üretilmeli) / OLUR (sektöre/ileriye bağlı) / HAYIR-ŞİMDİ (ertelenmiş/kapsam dışı).

### 12.1 Operax MEVCUT (kesin, Dtos.cs)
- **MovementType (5):** RECEIPT · ISSUE · TRANSFER · COUNT_ADJ · PRODUCTION
- **SourceDoc (6):** RECEIVING · SHIPPING · TRANSFER · COUNT · PRODUCTION · PICKING
- **AccountMovementType (9):** SALES_INVOICE · PURCHASE_INVOICE · PAYMENT · COLLECTION · CHEQUE_IN · CHEQUE_OUT · OPENING · VARIANCE · REVERSAL
- **TransactionType (4):** INCOME · EXPENSE · TRANSFER_IN · TRANSFER_OUT
- **Cheque statü (6):** PORTFOLIO · IN_BANK · COLLECTED · RETURNED · ENDORSED · PAID
- **DocPrefix (9):** PO·SO·RCV·SHP·TRF·CNT·PRD·PCK·REP

### 12.2 STOK HAREKETİ — Mikro `sth_cins` (0-13) ↔ Operax
| Mikro sth_cins | Operax karşılığı | Lazım? | Not |
|---|---|---|---|
| 0:Toptan · 1:Perakende | RECEIPT/ISSUE (SourceDoc ile) | **VAR** | Perakende-POS ayrı değil (E3) |
| 2:Dış Ticaret · 12:İthalat/İhracat | YOK | OLUR | İhracat/ithalat + GTİP/gümrük |
| 3:Stok Virman | YOK (TRANSFER depo-içi) | OLUR | Aynı depo birim/lot düzeltme |
| **4:Fire** | YOK (COUNT_ADJ'a karışır) | **EVET** | Fire/zayi ayrı sebep → maliyet+vergi (E4) |
| **5:Sarf** | ISSUE (üretim) — ayrı tip yok | **EVET** | Üretim dışı sarf/gider sarfı (E9) |
| 6:Transfer | TRANSFER | **VAR** | |
| 7:Üretim | PRODUCTION | **VAR** | |
| 8:Fason | YOK | OLUR | Fason üretim |
| 9:Değer Farkı | VARIANCE (cari) — stokta yok | OLUR | Stok değerleme/maliyet düzeltme |
| 10:Sayım | COUNT_ADJ | **VAR** | Fazla/eksik ayrımı yok (E5) |
| **11:Stok Açılış** | YOK | **EVET** | Dönem başı/go-live stok yükleme (E7) |
| 13:Hal · 14:Müstahsil(?) | YOK | HAYIR-ŞİMDİ | Niş sektör (hal/tarım), 14/15 DOĞRULANMADI |

### 12.3 STOK EVRAK TİPİ — Mikro `sth_evraktip` (0-18) ↔ Operax
| Mikro | Operax | Lazım? | Not |
|---|---|---|---|
| **1:Çıkış İrsaliyesi · 13:Giriş İrsaliyesi** | Receiving/Shipping (irsaliye≈) | **EVET** | İrsaliye↔Fatura ayrımı yok (E1); VUK: mal=irsaliye, mali=fatura |
| **3:Giriş Faturası · 4:Çıkış Faturası** | EI / SI | **VAR** ama | İrsaliye→fatura dönüşüm zinciri yok (E1) |
| 0:Depo Çıkış · 12:Depo Giriş Fişi | ADJUST (sebepsiz) | OLUR | Serbest depo giriş/çıkış fişi |
| 2:Depo Transfer · 11/17:Antrepo/Nakliye | TRANSFER | **VAR** kısmi | |
| 15:Depolar Arası Satış Fişi | YOK | OLUR | Şubeler arası satış (intercompany, VISION §7.5) |
| 5-10:İthalat masraf/maliyet yedirme | YOK | OLUR | Landed cost (ithalat masraf dağıtımı) |
| 18:Demirbaşa Virman | YOK | HAYIR-ŞİMDİ | Demirbaş modülü yok |

### 12.4 CARİ HAREKET — Mikro `cha_cinsi`(0-41)/`cha_evrak_tip` ↔ Operax
| Mikro | Operax | Lazım? | Not |
|---|---|---|---|
| Toptan/Perakende/Hizmet Faturası | SALES/PURCHASE_INVOICE | **VAR** | |
| Tahsilat Makbuzu · Tediye/Ödeme | COLLECTION/PAYMENT | **VAR** | |
| Çek/Senet Giriş-Çıkış Bordrosu | CHEQUE_IN/OUT + Cheque/Note | **VAR** kısmi | |
| Cari Açılış | OPENING | **VAR** | |
| **10:Vade Farkı Faturası** | YOK | **EVET** | Geç ödeme vade farkı (TR yaygın, E12) |
| **11:Kur Farkı Faturası** | YOK | OLUR | Dövizli çalışınca (E10) |
| **Genel Virman Dekontu (cari↔cari)** | YOK (Plan 11 başlamadı) | **EVET** | Virman evrakı (E11) |
| Borç/Alacak Dekontu | YOK (sadece VARIANCE) | **EVET** | Serbest borç/alacak dekontu (E12) |
| Gelen/Gönderilen Havale | TransactionType var, evrak yok | OLUR | Banka havale/EFT evrakı |
| 33:Avans Makbuzu | YOK | OLUR | Müşteri/tedarikçi avansı |
| Teminat Mektubu/Depozito · SMM · Müstahsil · Gümrük | YOK | HAYIR-ŞİMDİ / OLUR | İleri finans / sektör-mevzuat bağlı |

### 12.5 ÇEK/SENET DURUMU — Mikro `sck_sonpoz`(0-10) ↔ Operax Cheque statü(6)
| Mikro sck_sonpoz | Operax | Lazım? |
|---|---|---|
| 0:Portföyde | PORTFOLIO | **VAR** |
| 1:Ciro | ENDORSED | **VAR** |
| 2:Tahsilde | IN_BANK | **VAR** |
| 10:Ödendi/tahsil | COLLECTED/PAID | **VAR** |
| 4:İade · 7:Ödenmedi İade | RETURNED | **VAR** kısmi |
| **3:Teminatta** | YOK | **EVET** (çek/senet teminata verme — TR yaygın) |
| **9:Kısmen Ödendi** | YOK | **EVET** (kısmi tahsilat) |
| 8:İcrada | YOK | OLUR (karşılıksız→icra) |
| 6:Ödenmedi Portföyde | YOK | OLUR (vade geçti elde) |

### 12.6 EKSİK BELGE ÖZET (E1–E13, öncelik)
| # | Eksik | Lazım? | Çözüm (Operax felsefesi) |
|---|---|---|---|
| **E1** | İrsaliye↔Fatura ayrımı + dönüşüm | **EVET-YÜKSEK** | Belge zinciri: irsaliye(stok)→fatura(mali); ayrı SourceDocType |
| **E2** | Alış/Satış İade (ayrı belge) | **EVET-YÜKSEK** | İade belgesi→orijinale bağ + ters-kayıt (immutability) |
| **E4** | Fire/Zayi/İmha | **EVET-YÜKSEK** | ADJUST + `AdjustReason=WASTE/SCRAP` |
| **E11** | Virman (kasa↔kasa, cari↔cari) | **EVET-YÜKSEK** | Plan 11 (başlamadı); TransactionType TRANSFER var |
| E5 | Sayım Fazla/Eksik ayrımı | EVET-ORTA | COUNT_ADJ + işaret/sebep |
| E7 | Stok Açılış/Devir fişi | EVET-ORTA | SourceDocType=OPENING_STOCK |
| E12 | Vade Farkı / Borç-Alacak Dekontu | EVET-ORTA | AccountMovementType + dekont belgesi |
| E3 | Perakende/POS | OLUR | Sektöre bağlı |
| E6 | Konsinye Giriş/Çıkış | OLUR | Mülkiyet geçmeyen hareket |
| E8 | Fason | OLUR | Fason üretim |
| E10 | Kur Farkı (stok+cari) | OLUR | Dövizli çalışınca |
| E13 | GL Mahsup/Açılış/Kapanış | HAYIR-ŞİMDİ | K1/K2 ertelenmiş GL modülü |

### 12.7 Net Çıkarım + Çözüm Deseni (§0.5 uyumlu)
- **Yeni ledger tablosu AÇMA.** Polymorphic ledger doğru (§0.5). Eksik tipler 3 mekanizmayla:
  1. **`SourceDocType` kataloğu genişlet:** RETURN_IN, RETURN_OUT, WASTE, CONSIGNMENT_IN/OUT, OPENING_STOCK, FASON…
  2. **ADJUST'a `AdjustReason`:** COUNT_PLUS / COUNT_MINUS / WASTE / SCRAP / OPENING / REVALUATION.
  3. **Belge zinciri (Header/Line):** irsaliye, iade, dekont, virman ayrı belge (immutability + durum makinesi); ledger'a tip ile yazar.
- **En yüksek 4 (üretilmeli):** E1 irsaliye↔fatura · E2 iade · E4 fire · E11 virman (Plan 11).
- **Çek statü genişlet:** TEMİNAT + KISMİ ÖDEME (sck_sonpoz 3,9) → `document-immutability.md` §2.4.
- Mikro tam enum (`sth_cins` 14/15, `cha_evrak_tip` 51-137) DOĞRULANMADI — resmi DDL ile kesinleştir.

## 11. İlişkili
- `docs/REFERENCE_STUDY.md` — ERPNext/Smartstore/nop vb. ana referans çalışması (R0–R4, B1–B17)
- `plans/14-ledger-pk-immutability.md` — R4 clustered PK (Mikro §1 GUID PK gözlemi)
- `plans/12-data-isolation-guard.md` — CompanyId izolasyon (Mikro §1-6 firmano/subeno)
- `docs/VISION.md` §7.7 — muhasebe katman stratejisi (Mikro §3 subledger/GL gevşek bağ)
