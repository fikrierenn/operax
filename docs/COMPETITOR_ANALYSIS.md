# Operax — Rakip Analizi ve Özellik Matrisi

> Tarih: 2026-05-28
> Amaç: Operax'ın hedeflediği "Tek Platform ERP + WMS + Üretim + Finans (resmi muhasebe hariç)" kapsamında her modül için pazardaki rakip yazılımlarda standart sayılan özellikleri tek tek listelemek, eksiklerimizi netleştirmek ve modül modül geliştirme önceliklerini belirlemek.

---

## 1. Pazar Konumlandırma

Operax aynı anda dört farklı pazar segmentinde rekabet eder. Hiçbir rakip dört segmenti aynı anda tam ölçekli karşılamadığı için tek platform avantajı çok güçlüdür.

| Segment | Birincil Rakipler (TR) | Birincil Rakipler (Global) |
|---|---|---|
| ERP (Yönetim) | Logo Tiger, Mikro Fly, Netsis, Nebim V3, ETA SQL, Zirve | SAP B1, Odoo, NetSuite, Microsoft Dynamics 365 |
| WMS (Depo) | Hardware-driven entegratörler (Soft Bilgisayar, Tatva, BizenWare), Logo WMS | Manhattan SCALE, Blue Yonder WMS, Infor WMS, Körber, Reflex |
| MRP / Üretim | Logo Üretim, Mikro Üretim, IAS Canias | Plex, IQMS, IFS, ProShop, Fishbowl |
| Lojistik / Sevkiyat | Kargo entegratör yazılımları (UPS/MNG vb. API) | Cargo IQ, MercuryGate, Project44 |

Operax bunların hepsini tek veritabanı + tek arayüzde toplar. **Resmi muhasebe (e-defter, e-fatura çıktısı, KDV/Stopaj/Muhtasar beyannamesi, BA/BS formları, GİB entegrasyonları, Mali Müşavir transferi) Operax kapsam dışıdır**; bu blok M16 Integration Bridge üzerinden Logo/Mikro/Netsis/Luca/Bizimhesap'a yansıtılır.

---

## 2. Modül Bazlı Özellik Karşılaştırma Matrisi

> Her hücre: ✅ tam · ⚠️ kısmen · ❌ yok
>
> "Operax" sütunu mevcut durumu, "Plan" sütunu eklenmesi gereken (M11/M12 vs. olarak işaretli) özelliği gösterir.

### M01 — Ana Veri (Master Data)

| Özellik | Logo | Mikro | Nebim | Odoo | SAP B1 | Operax | Plan |
|---|---|---|---|---|---|---|---|
| Ürün kart yönetimi | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Çoklu UOM ve dönüşüm (EACH↔PACK↔CASE) | ✅ | ⚠️ | ✅ | ✅ | ✅ | ✅ | — |
| Çoklu barkod (UPC/EAN/QR/GS1) | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M01.B1 |
| Beden / Renk / Varyant matrisi | ✅ | ✅ | ✅✅ | ✅ | ✅ | ❌ | M01.V1 |
| Renk / desen / model bağlama | ⚠️ | ⚠️ | ✅ | ⚠️ | ⚠️ | ❌ | M01.V1 |
| GS1-128 etiket çözümleyici | ✅ | ⚠️ | ⚠️ | ✅ | ✅ | ❌ | M09.E1 |
| Müşteri/Tedarikçi cari kart | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Cari risk limit & ekstre | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M11.C1 |
| Çoklu adres (sevkiyat/fatura ayrı) | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M01.A1 |
| Vergi numarası doğrulama (GİB) | ✅ | ✅ | ✅ | ❌ | ⚠️ | ❌ | M16.V1 |
| Banka hesabı kart bağlama | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M11 |
| Kategori ağacı (sınırsız derinlik) | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M01.K1 |
| Marka / üretici yönetimi | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M01.M1 |
| Garanti süresi takibi | ✅ | ⚠️ | ⚠️ | ⚠️ | ✅ | ❌ | M12.G1 |
| Custom field (UDF/EAV) | ⚠️ | ⚠️ | ✅ | ✅✅ | ✅ | ✅ | — |
| Bağlı resim / dokümentasyon | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M01.D1 |

### M02 — Stok / Envanter

| Özellik | Logo | Mikro | Nebim | Odoo | SAP B1 | Operax | Plan |
|---|---|---|---|---|---|---|---|
| Lot / Parti takibi | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Seri no takibi | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| FEFO (son kullanma) | ⚠️ | ❌ | ⚠️ | ✅ | ✅ | ⚠️ | M09.F1 |
| FIFO | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M02.C1 |
| Hareketli ağırlıklı ortalama maliyet | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M02.C2 (eklendi) |
| Standart maliyet | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M02.C3 |
| Negatif stok uyarısı/yasak | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M02.N1 |
| Stok rezervasyonu | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M02.R1 |
| Stok sayım (cycle / annual) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Blind / Open count modu | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ✅ | — |
| Tolerans bazlı otomatik onay | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ⚠️ | M08.T1 |
| Stok devir hızı raporu | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M15.S1 |
| Yaşlandırma (aging) raporu | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M15.S2 |
| ABC analizi | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ❌ | M15.S3 |
| Min/Max bazlı sipariş önerisi | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M02.S1 |
| Replenishment (raf dolum) | ⚠️ | ❌ | ⚠️ | ✅ | ✅ | ⚠️ | M07.R1 |
| Multi-warehouse | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Hücre/bin/raf yönetimi (3-boyutlu) | ⚠️ | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | — |

### M03 — Satınalma (Purchase Order)

| Özellik | Logo | Mikro | Nebim | Odoo | SAP B1 | Operax | Plan |
|---|---|---|---|---|---|---|---|
| Sipariş açma / onaylama | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Çoklu seviye onay (workflow) | ✅ | ⚠️ | ⚠️ | ✅ | ✅ | ❌ | M03.A1 |
| Fiyat listesi kontrolü (tedarikçi başına) | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M03.P1 |
| Fiyat farkı (variance) otomatik kaydı | ✅ | ✅ | ⚠️ | ✅ | ✅ | ⚠️ (şema var) | M03.P2 |
| Teklif yönetimi (RFQ) | ✅ | ⚠️ | ⚠️ | ✅ | ✅ | ❌ | M03.R1 |
| Çoklu teklif kıyaslama | ⚠️ | ❌ | ❌ | ✅ | ✅ | ❌ | M03.R2 |
| Kısmi mal kabul takibi | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Faturalı vs faturasız mal kabul | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M03.F1 |
| Hizmet kalemi (stoksuz) sipariş | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M03.S1 |
| MRP'den otomatik PO önerisi | ✅ | ✅ | ⚠️ | ✅ | ✅ | ❌ | M10.M1 |
| Vade & ödeme şartı | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M03.V1 |
| Çoklu döviz + kur farkı | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M03.D1 |
| Tedarikçi performans skoru | ⚠️ | ⚠️ | ⚠️ | ✅ | ✅ | ❌ | M03.T1 |
| Açık PO raporu (open balance) | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M15.P1 |
| Drop-ship / üçgen sevkiyat | ✅ | ⚠️ | ✅ | ✅ | ✅ | ❌ | M03.S2 |

### M04 — Satış (Sales Order + Invoice)

| Özellik | Logo | Mikro | Nebim | Odoo | SAP B1 | Operax | Plan |
|---|---|---|---|---|---|---|---|
| Sipariş açma | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Müşteri bazlı fiyat listesi | ✅ | ✅ | ✅✅ | ✅ | ✅ | ⚠️ (şema var) | M04.P1 |
| Kademeli/dönemsel iskonto | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M04.I1 |
| Kampanya / promosyon | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ❌ | M04.K1 |
| Müşteri kredi limiti kontrolü | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M04.L1 |
| Vade hesabı | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M04.V1 |
| Kısmi sevk + back-order | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Otomatik fatura (sevk sonrası) | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ (şema var) | M04.F1 |
| EOD toplu fatura | ✅ | ✅ | ⚠️ | ✅ | ⚠️ | ❌ | M04.F2 |
| e-Fatura / e-Arşiv (entegrasyon) | ✅ | ✅ | ✅ | ⚠️ | ⚠️ | ❌ | M16.E1 |
| İrsaliye | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ (Shipping) | M04.I1 |
| İade / RMA | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M12.R1 |
| Konsinye satış | ✅ | ⚠️ | ✅ | ✅ | ✅ | ❌ | M04.K2 |
| Çoklu para birimi | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M04.D1 |
| KDV/ÖTV/ÖİV ayrımı | ✅ | ✅ | ✅ | ⚠️ | ⚠️ | ⚠️ | M04.T1 |
| Hizmet kalemi satış | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M04.S1 |

### M05 — Sevkiyat & Lojistik

| Özellik | Logo | Mikro | Manhattan | Blue Yonder | Operax | Plan |
|---|---|---|---|---|---|---|
| Sevkiyat belgesi | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Wave (dalga) toplama | ⚠️ | ❌ | ✅ | ✅ | ❌ | M06.W1 |
| LPN / palet etiket basımı | ⚠️ | ❌ | ✅ | ✅ | ⚠️ | M09.L1 |
| Koli (carton) yönetimi | ⚠️ | ❌ | ✅ | ✅ | ❌ | M09.C1 |
| Sevk planlama (loading) | ⚠️ | ❌ | ✅ | ✅ | ❌ | M05.P1 |
| Araç & kapı (dock) yönetimi | ⚠️ | ❌ | ✅ | ✅ | ❌ | M05.D1 |
| Kargo entegratörü (UPS/MNG/Aras/Yurtiçi) | ✅ | ⚠️ | ✅ | ✅ | ❌ | M16.K1 |
| Tracking number alma & webhook | ⚠️ | ❌ | ✅ | ✅ | ❌ | M16.K2 |
| Çoklu kargo seçimi (kural bazlı) | ⚠️ | ❌ | ✅ | ✅ | ❌ | M05.K1 |
| Cross-docking | ❌ | ❌ | ✅ | ✅ | ❌ | M05.C1 |
| Yük optimizasyonu | ❌ | ❌ | ✅ | ✅ | ❌ | M05.O1 |

### M06 — Toplama (Picking)

| Özellik | Logo | Manhattan | Blue Yonder | Odoo | Operax | Plan |
|---|---|---|---|---|---|---|
| Pick task oluşturma | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Wave picking | ⚠️ | ✅ | ✅ | ✅ | ❌ | M06.W1 |
| Zone picking | ❌ | ✅ | ✅ | ⚠️ | ❌ | M06.Z1 |
| Cluster picking (multi-order) | ❌ | ✅ | ✅ | ⚠️ | ❌ | M06.C1 |
| Voice picking | ❌ | ✅ | ✅ | ❌ | ❌ | — (gelecek) |
| Pick-to-light | ❌ | ✅ | ✅ | ❌ | ❌ | — (gelecek) |
| FIFO/FEFO stratejisi | ⚠️ | ✅ | ✅ | ✅ | ⚠️ | M06.F1 |
| Slot optimizasyonu | ❌ | ✅ | ✅ | ⚠️ | ❌ | M06.S1 |
| Kısmi toplama (under-pick) | ✅ | ✅ | ✅ | ✅ | ⚠️ | M06.P1 |
| El terminali (mobil) | ⚠️ | ✅ | ✅ | ⚠️ | ⚠️ | M21.T1 |
| Barkod doğrulama (RequireBinScan) | ✅ | ✅ | ✅ | ✅ | ✅ | — |

### M10 — Üretim (Manufacturing)

| Özellik | Logo | Mikro | IAS Canias | SAP B1 | Plex | Operax | Plan |
|---|---|---|---|---|---|---|---|
| BOM (reçete) yönetimi | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Çok seviyeli BOM | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M10.B1 |
| Parametrik BOM (formül bazlı) | ⚠️ | ⚠️ | ✅ | ⚠️ | ✅ | ✅ | — |
| Rota (routing) + iş istasyonu | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| İş emri (production order) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| WIP takibi (saniye hassasiyet) | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ✅ | — |
| Planlı vs fiili maliyet | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Varyans analizi | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ✅ | — |
| MRP (malzeme planlama) | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M10.M1 |
| CRP (kapasite planlama) | ⚠️ | ❌ | ✅ | ✅ | ✅ | ❌ | M10.C1 |
| Gantt çizelgesi | ⚠️ | ❌ | ✅ | ⚠️ | ✅ | ❌ | M10.G1 |
| Kalite kontrol (QC) | ✅ | ⚠️ | ✅ | ✅ | ✅ | ✅ | — |
| Defect kodları + Pareto | ⚠️ | ❌ | ✅ | ✅ | ✅ | ⚠️ | M10.D1 |
| Rework yönetimi | ⚠️ | ❌ | ✅ | ✅ | ✅ | ✅ | — |
| Subcontracting (fason) | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M10.F1 |
| OEE (overall equipment effectiveness) | ❌ | ❌ | ✅ | ⚠️ | ✅ | ❌ | M10.O1 |

### M11 — Finans (Kasa / Banka / Çek / Senet / Kredi / Kart)

| Özellik | Logo | Mikro | Netsis | Odoo | SAP B1 | Operax | Plan |
|---|---|---|---|---|---|---|---|
| Kasa hesabı + hareket | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (eklendi) | — |
| Banka hesabı + hareket | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (eklendi) | — |
| Banka mutabakatı (reconciliation) | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M11.M1 |
| Çek portföyü (alınan/verilen) | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ (eklendi) | — |
| Çek statüleri (PORTFOLIO/IN_BANK/COLLECTED/RETURNED/ENDORSED/PAID) | ✅ | ✅ | ✅ | ⚠️ | ⚠️ | ✅ (eklendi) | — |
| Çek tahsile verme & alma | ✅ | ✅ | ✅ | ⚠️ | ⚠️ | ✅ (SP'ler eksik) | M11.C1 |
| Senet portföyü | ✅ | ✅ | ✅ | ⚠️ | ⚠️ | ✅ (eklendi) | — |
| Banka kredisi (anapara/faiz/taksit) | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ (eklendi) | — |
| Kredi taksit takvimi | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ (eklendi) | — |
| Kredi yapılandırma | ⚠️ | ⚠️ | ✅ | ❌ | ⚠️ | ❌ | M11.K1 |
| Kredi kartı (limit/ekstre/taksit) | ✅ | ✅ | ✅ | ❌ | ⚠️ | ✅ (eklendi) | — |
| POS entegrasyonu | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ❌ | M11.P1 |
| Ödeme planı (vadeli alış/satış) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (eklendi) | — |
| Otomatik virman (account transfer) | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M11.V1 |
| Çoklu para birimi + kur farkı | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M11.D1 |
| Nakit projeksiyon (cash flow) | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ⚠️ | M11.N1 |
| Mutabakat mektubu (BA/BS) | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | M16.B1 |
| Cari ekstre (account statement) | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M11.E1 |
| Yaşlandırma (alacak/borç aging) | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | M11.Y1 |

### M12 — Servis / RMA / Bakım

| Özellik | Logo | Nebim | Odoo | SAP B1 | Operax | Plan |
|---|---|---|---|---|---|---|
| Servis talep yönetimi (ticket) | ⚠️ | ✅ | ✅ | ✅ | ❌ | M12.T1 |
| SLA takibi | ❌ | ✅ | ✅ | ✅ | ❌ | M12.S1 |
| Saha servis (atama, rota) | ❌ | ⚠️ | ✅ | ⚠️ | ❌ | M12.F1 |
| Garanti yönetimi | ⚠️ | ✅ | ✅ | ✅ | ❌ | M12.G1 |
| RMA (iade) workflow | ✅ | ✅ | ✅ | ✅ | ❌ | M12.R1 |
| Servis parça stok | ⚠️ | ⚠️ | ✅ | ✅ | ❌ | M12.P1 |
| Bakım planlama (preventive) | ❌ | ❌ | ✅ | ✅ | ❌ | M12.B1 |
| MES — makine bakım | ❌ | ❌ | ⚠️ | ⚠️ | ❌ | M12.M1 |

### M14 — Prim / Komisyon

| Özellik | Logo | Nebim | Odoo | Operax | Plan |
|---|---|---|---|---|---|
| Satıcı prim hesabı | ⚠️ | ✅ | ✅ | ❌ | M14.S1 |
| Müşteri prim/iadesi | ⚠️ | ✅ | ⚠️ | ❌ | M14.M1 |
| Hedef bazlı bonus | ❌ | ✅ | ✅ | ❌ | M14.H1 |
| Çoklu komisyon kuralı | ⚠️ | ✅ | ✅ | ❌ | M14.K1 |
| Dönemsel kesinti / cap | ❌ | ✅ | ⚠️ | ❌ | M14.D1 |

### M16 — Entegrasyon Köprüsü

| Özellik | Açıklama | Plan |
|---|---|---|
| Logo Tiger / Mikro / Netsis dışa aktarım | XML/Excel/REST | M16.L1 |
| Luca / Bizimhesap / Quasar | API senkronizasyon | M16.L2 |
| GİB e-Fatura | UBL 2.1 + zarf | M16.G1 |
| GİB e-İrsaliye | UBL 2.1 | M16.G2 |
| GİB e-Arşiv | UBL 2.1 | M16.G3 |
| Banka POS/Sanal POS | İyzico/Param/Vakıf/PayTR | M16.B1 |
| Kargo: UPS / MNG / Yurtiçi / Aras / Sürat | Tracking + label | M16.K1 |
| Marketplace: Trendyol / Hepsiburada / Amazon | Stok+sipariş sync | M16.M1 |
| WhatsApp Business API | Sipariş onayı bildirim | M16.W1 |

### M15 — Dashboard / Raporlar

| Özellik | Logo | Mikro | Nebim | Odoo | SAP B1 | Operax | Plan |
|---|---|---|---|---|---|---|---|
| Yönetici paneli (KPI cards) | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ✅ | — |
| Operasyon paneli | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ⚠️ | M15.O1 |
| Pivot rapor üretici | ⚠️ | ✅ | ⚠️ | ✅ | ✅ | ❌ | M15.R1 |
| Drag-and-drop dashboard tasarımcısı | ❌ | ❌ | ⚠️ | ✅ | ⚠️ | ❌ | M15.D1 |
| Excel/PDF/CSV export | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | M15.E1 |
| Zamanlanmış e-posta raporu | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ❌ | M15.Z1 |
| Mobil rapor (PWA) | ❌ | ❌ | ⚠️ | ✅ | ✅ | ❌ | M21.R1 |

---

## 3. Önerilen Modül Öncelik Sırası

Mevcut gap'leri etki/efor matrisinde değerlendirip Operax'ın "tek platform" iddiasını en hızlı tamamlayacak sıraya koydum.

### Faz 2A (3-4 hafta) — Operasyonel Tamamlama
1. **M11 SP'ler** — Çek tahsil/iade, kredi taksit, kart ekstre ödeme SP'leri (şema hazır, SP eksik)
2. **M02.C** — FIFO + Standart maliyet motoru (şu an sadece Moving Avg şeması var)
3. **M03.P1** — Tedarikçi fiyat listesi kontrolü + PriceVariance otomatik kayıt (şema var, SP eksik)
4. **M04.F1** — Sevk POSTED → otomatik SalesInvoice + PaymentPlan üretimi (şema var, SP eksik)
5. **M11.E1 + M11.Y1** — Cari ekstre + yaşlandırma raporu

### Faz 2B (4-6 hafta) — Pazarlama Avantajı Kazandıran
6. **M04.L1** — Müşteri kredi limit kontrolü (SO açarken bloklama)
7. **M03.A1** — Çok seviyeli onay workflow (PO 50K üzeri → yönetici, 250K üzeri → genel müdür)
8. **M16.K1 + M16.K2** — Kargo entegrasyonu (UPS/MNG/Aras) + tracking webhook
9. **M16.E1** — e-Fatura/e-Arşiv (UBL 2.1) — entegratör seçimi sonrası

### Faz 2C (6-10 hafta) — Niş Pazar Açıcılar
10. **M12.T1 + M12.R1 + M12.S1** — Servis ticket + RMA + SLA (servis sektörü için)
11. **M10.M1** — MRP malzeme planlama (üretim firmaları için)
12. **M06.W1 + M06.Z1** — Wave + Zone picking (büyük depolar)
13. **M01.V1** — Beden/renk varyant matrisi (tekstil/giyim)
14. **M14** — Komisyon/prim modülü

### Faz 3 (gelecek) — Premium / Niş
15. **M10.O1** — OEE
16. **M16.M1** — Marketplace senkronizasyon
17. **M12.B1** — Preventive bakım planlama
18. **M11.P1** — POS entegrasyonu

---

## 4. Modül Spec Doküman Dizini

Her modülün detay tasarımı `docs/MODULE_SPECS/` altındadır. Bu dokümanlar şema değişiklikleri, SP listesi, ekran tasarımı ve test senaryolarını içerir.

| Modül | Spec Dosyası | Durum |
|---|---|---|
| M03 — Satınalma (genişletme) | `M03_Purchasing_Extended.md` | Yazıldı |
| M04 — Satış Faturası + Fiyat Politikası | `M04_SalesInvoice_Pricing.md` | Yazılacak |
| M11 — Finans (SP detay) | `M11_Finance_Procedures.md` | Yazılacak |
| M02 — Maliyetlendirme (FIFO + Std) | `M02_Costing_FIFO_Standard.md` | Yazılacak |
| M16 — e-Fatura ve Kargo | `M16_Integration_EInvoice_Carrier.md` | Yazılacak |
| M12 — Servis / RMA | `M12_Service_RMA.md` | Yazılacak |
| M14 — Komisyon | `M14_Commissions.md` | Yazılacak |

---

## 5. Kapsam Dışı (Resmi Muhasebe)

Aşağıdaki başlıklar Operax tarafından **üretilmez** — M16 üzerinden Logo/Mikro/Netsis/Luca/Bizimhesap'a aktarılır:

- e-Defter (Yevmiye + Kebir)
- KDV/Stopaj/Muhtasar Beyannamesi (XML/PDF üretimi ve GİB Defter-Beyan submission)
- BA/BS formları
- Geçici ve Kurumlar Vergisi
- Bilanço & Gelir Tablosu (resmi format VUK)
- Yeniden değerleme
- Amortisman defteri (resmi VUK formatı — yönetim amortismanı yapılabilir)

Operax tarafında üretilenler bu sistemlere "muhasebe fişi" olarak X-export formatında gönderilir (modülün spec'i `M16_Accounting_Export.md` dosyasında olacak).

---

## 6. Lisanslama Notu

Bu modüllerin tamamı tek platformda toplandığı için ücretlendirme paket bazlı tasarlanmıştır (M17 Packaging):

- **STARTER**: M00, M01, M02, M03, M04, M11 — küçük işletmeler
- **WMS_PRO**: + M05, M06, M07, M08, M09 — depo odaklı
- **MANUFACTURING**: + M10 — üretim firmaları
- **ENTERPRISE**: + M12, M14, M15, M16 (tam) — büyük ölçek
- **ULTIMATE**: tüm modüller

Detay `M17_Packaging.md` dosyasında.
