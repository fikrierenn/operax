# OPERAX — Sprint Detayları
> Bu dosya her sprint için kabul kriterlerini, bağımlılıkları ve ekran listesini içerir.
> Güncel durum için → `PLAN.md`

---

## SPRINT 0 — Foundation Fix
> Önkoşul: Yok
> Detay: `docs/SPRINT_0.md`
> Kabul: `dotnet build` → 0 hata, 0 uyarı

**Neden önce bu?**
19 derleme hatası ile uygulama ayağa kalkmıyor. Hiçbir özellik test edilemiyor.
Önce temel sağlam olmalı.

**Çıktılar:**
- Uygulama derleniyor ve çalışıyor
- Güvenlik açıkları kapatıldı
- Tüm null uyarıları temizlendi

---

## SPRINT 1 — M00 Platform Core Stabilize
> Önkoşul: S0
> Modül bağımlılığı: Yok (tüm modüllerin temeli)

### Amaç
Kullanıcı oturum açıyor, şirket seçiyor, rol bazlı yetkilendirme çalışıyor.
Admin ekranları tam işlevsel.

### Ekranlar

| Ekran | URL | Durum |
|---|---|---|
| Login | `/auth/login` | Mevcut — test edilecek |
| Şirket Yönetimi | `/admin/companies` | Mevcut — test |
| Kullanıcı Yönetimi | `/admin/users` | Mevcut — test |
| Rol Yönetimi | `/admin/roles` | Mevcut — test |
| Sözlük Yönetimi | `/admin/dictionary` | Mevcut — test |
| Parametre Yönetimi | `/admin/parameters` | Mevcut — test |
| Modül Aktivasyonu | `/admin/modules` | Mevcut — test |
| Durum Geçişleri | `/admin/status-transitions` | Mevcut — test |
| Denetim İzi | `/admin/audit-log` | **YOK — yazılacak** |

### Kritik Kontroller
- `CurrentCompany.Id` — login sonrası claim doğru set ediliyor mu?
- `[Authorize(Roles="...")]` — tüm admin sayfaları korunuyor mu?
- StatusTransition engine — belge durum geçişlerini kontrol ediyor mu?
- Seed data — `seed_core.sql` çalıştırıldı mı?

### UI Türkçe Kontrolü
Bu sprint'te taranacak ekranlar:
- Layout / menü / navigasyon
- Login sayfası
- Tüm admin alt ekranlar
- Hata sayfaları (404, 500)

### Kabul Kriteri
1. Admin kullanıcısı login → şirket seçimi → tüm admin ekranları çalışıyor
2. Yetkisiz kullanıcı admin sayfasına giremiyor
3. Hiçbir ekranda İngilizce UI metni yok
4. Denetim izi ekranı açılıyor ve log kayıtları listeleniyor

---

## SPRINT 2 — M01 Master Data
> Önkoşul: S1
> Modül bağımlılığı: M00

### Amaç
Ürün, müşteri/tedarikçi, depo ve lokasyon verileri sisteme girilebiliyor.
Bu veriler olmadan hiçbir işlem belgesi oluşturulamaz.

### Ekranlar

| Ekran | URL | Durum |
|---|---|---|
| Ürün Listesi | `/master/items` | Mevcut — test + Türkçe |
| Ürün Detayı | `/master/items/details` | Mevcut — UomId hatası → S0'da düzeltildi |
| Müşteri/Tedarikçi Listesi | `/master/accounts` | Mevcut — test |
| Müşteri/Tedarikçi Detayı | `/master/accounts/details` | Mevcut — test |
| Depo Listesi | `/warehouses` | Mevcut — test |
| Depo Detayı | `/warehouses/details` | Mevcut — test |
| Lokasyon (Bin) Listesi | `/master/locations` | Kontrol et — eksik olabilir |
| Birim (UOM) Yönetimi | `/admin/uom` | Kontrol et — eksik olabilir |

### Kritik İş Kuralları
- Her ürünün `BaseUOM` = EACH (değiştirilemez)
- UOM dönüşüm: 1 PACK = N EACH (ItemUOM tablosu)
- Barkod sisteme unique olmalı (ItemBarcode tablosu)
- Bin hiyerarşisi: Warehouse → Zone → Aisle → Rack → Level → Bin
- Lot takip: `IsLotTracked = true` ise mal kabulde lot zorunlu
- Seri takip: `IsSerialTracked = true` ise her birim için seri no zorunlu

### Kabul Kriteri
1. Ürün oluştur → UOM dönüşümü ekle → barkod ekle
2. Müşteri ve tedarikçi oluştur
3. Depo oluştur → bin ekle
4. Tüm ekranlar Türkçe

---

## SPRINT 3 — M02 Inventory Ledger
> Önkoşul: S2
> Modül bağımlılığı: M01

### Amaç
Anlık stok bakiyesi ve hareket geçmişi görülebiliyor.
Bu modül yazılmaz ama okunur — tüm hareketler başka modüller tarafından yazılır.

### Ekranlar

| Ekran | URL | Durum |
|---|---|---|
| Stok Bakiyesi | `/inventory` | **YOK — yazılacak** |
| Hareket Geçmişi | `/inventory/movements` | **YOK — yazılacak** |
| Bin Bazlı Bakiye | `/inventory/bin-balance` | **YOK — yazılacak** |

### StockMovement Hareket Tipleri (DictionaryValue.Code)
| Kod | Açıklama | Kaynak |
|---|---|---|
| `RECEIPT` | Mal kabul girişi | M03 Receiving |
| `ISSUE` | Sevkiyat çıkışı | M05 Shipping |
| `TRANSFER` | Bin-to-bin transfer | M07 Transfer |
| `CONSUMPTION` | Üretimde hammadde tüketimi | M10 Manufacturing |
| `PRODUCTION` | Üretimde mamul kabulü | M10 Manufacturing |
| `COUNT_ADJ` | Sayım fark düzeltmesi | M08 Cycle Count |

### Kritik Kontroller
- Negatif stok oluşmamalı (post öncesi bakiye kontrolü)
- FIFO için en eski hareketi bulmak: `ORDER BY CreatedAt ASC`
- CompanyId filtresi her sorguda zorunlu

### Kabul Kriteri
1. Stok bakiyesi ekranı açılıyor, anlık miktarlar görünüyor
2. Bir ürünün hareket geçmişi tarih sıralı listeleniyor
3. Bin bazlı bakiye: hangi binde ne kadar var
4. Filtreler çalışıyor (ürün, depo, tarih)
5. Tüm ekranlar Türkçe

---

## SPRINT 4 — M03 Receiving + M04 Purchase Orders
> Önkoşul: S3
> Modül bağımlılığı: M01, M02

### Amaç
Satın alma siparişi oluşturulup onaylanıyor.
Mal kabul belgesi açılıp ürünler sisteme girilebiliyor.
Her giriş StockMovement tablosuna RECEIPT hareketi olarak yazılıyor.

### Ekranlar — M04 Purchase Orders

| Ekran | URL | Durum |
|---|---|---|
| PO Listesi | `/purchase-orders` | Mevcut — test |
| PO Detayı | `/purchase-orders/details` | Mevcut — CS8601 → S0'da düzeltildi |

### Ekranlar — M03 Receiving

| Ekran | URL | Durum |
|---|---|---|
| Mal Kabul Listesi | `/receiving` | Mevcut — test |
| Mal Kabul Detayı | `/receiving/details` | Mevcut — CS8602 → S0'da düzeltildi |
| Mal Kabul Terminali | `/receiving/terminal` | Kontrol et — eksik olabilir |
| Raflama (Putaway) | `/receiving/putaway` | Kontrol et |

### Akış
```
PO Oluştur (DRAFT)
  → PO Onayla (APPROVED)
    → Receiving Header aç (PO'dan link)
      → Satır ekle (ürün + miktar + lot/seri)
        → Post et → RECEIPT StockMovement yazılır
          → Putaway Task oluşur
            → Ürün rafa yerleştirilir → stok binde görünür
```

### Kritik İş Kuralları
- Lot takipli ürün geliyorsa lot no zorunlu
- Seri takipli ürün geliyorsa her birim için seri no
- Mal kabulde QtyBase = QtyOriginal × ConversionRate (UOM dönüşümü)
- PO onaylandıktan sonra satır değiştirilemez

### Kabul Kriteri
1. PO oluştur → onayla → Receiving aç
2. Receiving satırı ekle → post → StockMovement tablosuna RECEIPT yazıldı
3. Stok bakiyesinde ürün görünüyor
4. Lot takipli üründe lot no zorunlu hata veriyor
5. Putaway task oluşuyor
6. Terminal ekranı barkod okuyucuyla çalışıyor
7. Tüm ekranlar Türkçe

---

## SPRINT 5 — M04 Sales Orders + M05 Shipping
> Önkoşul: S4
> Modül bağımlılığı: M01, M02, M03

### Amaç
Satış siparişi alınıp onaylanıyor.
Sevkiyat belgesi oluşturulup stok çıkışı gerçekleşiyor.
Her çıkış StockMovement tablosuna ISSUE hareketi olarak yazılıyor.

### Ekranlar — M04 Sales Orders

| Ekran | URL | Durum |
|---|---|---|
| SO Listesi | `/sales-orders` | Mevcut — test |
| SO Detayı | `/sales-orders/details` | Mevcut — CS8601+CS8602 → S0'da |

### Ekranlar — M05 Shipping

| Ekran | URL | Durum |
|---|---|---|
| Sevkiyat Listesi | `/shipping` | Mevcut — test |
| Sevkiyat Detayı | `/shipping/details` | Mevcut — IsNew → S0'da düzeltildi |
| Sevkiyat Terminali | `/shipping/terminal` | Kontrol et — eksik olabilir |

### Akış
```
SO Oluştur (DRAFT)
  → SO Onayla (APPROVED)
    → Shipping Header oluştur (SO'dan)
      → Satır ekle
        → Post et → ISSUE StockMovement yazılır (negatif QtyBase)
          → SO satırında QtyShipped güncellenir
            → Tüm satırlar sevk edildiyse SO → SHIPPED
```

### Kritik İş Kuralları
- Sevkiyat sonrası stok negatife düşmemeli (kontrol zorunlu)
- Kısmi sevkiyat: bir SO birden fazla sevkiyatta tamamlanabilir
- SO → Shipping otomatik link (TODO: Sales Order Notify Logic)

### Kabul Kriteri
1. SO oluştur → onayla → sevkiyat yap → post
2. StockMovement'ta ISSUE hareketi var, QtyBase negatif
3. Stok bakiyesi azaldı
4. SO satırında QtyShipped güncellendi
5. Tüm ekranlar Türkçe

---

## SPRINT 6 — M06 Picking + M07 Transfer
> Önkoşul: S5
> Modül bağımlılığı: M01, M02, M05

### Amaç
Toplama (pick) görevleri oluşturuluyor ve terminalde tamamlanıyor.
Bin-to-bin transfer ile ürünler depolar/raflar arasında taşınıyor.

### Ekranlar — M06 Picking

| Ekran | URL | Durum |
|---|---|---|
| Pick Task Listesi | `/picking` | Mevcut — test |
| Pick Task Detayı | `/picking/details` | Mevcut — CS8602 → S0'da |
| Picking Terminali | `/picking/terminal` | Kontrol et — eksik olabilir |

### Ekranlar — M07 Transfer

| Ekran | URL | Durum |
|---|---|---|
| Transfer Listesi | `/transfer` | Mevcut — test |
| Transfer Detayı | `/transfer/details` | Mevcut — FromBinId/ToBinId → S0'da |
| Putaway Ekranı | `/transfer/putaway` | Mevcut — ItemId → S0'da |
| Replenishment | `/transfer/replenishment` | Kontrol et |
| Transfer Terminali | `/transfer/terminal` | Kontrol et — eksik olabilir |

### Picking Akışı
```
Shipping belgesi oluşturulunca → PickTask otomatik oluşur (DRAFT)
  → Toplama başlar (IN_PROGRESS)
    → Her satır için FIFO/FEFO ile raf belirlenir
      → Terminalde ürün barkodu okutulur
        → Toplama tamamlanır (COMPLETED)
          → Shipping pick tamamlandı olarak işaretlenir
```

### FIFO / FEFO Mantığı
- FIFO: `ORDER BY CreatedAt ASC` — en eski giren ilk çıkar
- FEFO: `ORDER BY ExpiryDate ASC` — tarihi en yakın ilk çıkar
- Hangi strateji kullanılacağı `AllocationStrategy` parametresinden gelir

### Transfer Akışı
```
Transfer oluştur (DRAFT)
  → Satır ekle (ürün + kaynak bin + hedef bin + miktar)
    → Post et
      → Kaynak bin'den TRANSFER çıkışı (negatif QtyBase)
      → Hedef bin'e TRANSFER girişi (pozitif QtyBase)
      → Net: toplam stok değişmez, sadece lokasyon değişir
```

### Kabul Kriteri
1. Pick task oluştur → terminalde tamamla → shipping güncellendi
2. Transfer oluştur → post → kaynak bin azaldı, hedef bin arttı
3. Replenishment: dolum bölgesinden toplama bölgesine otomatik transfer
4. FIFO ve FEFO stratejileri parametre bazlı çalışıyor
5. Tüm ekranlar Türkçe

---

## SPRINT 7 — M08 Cycle Count + M09 Traceability
> Önkoşul: S6
> Modül bağımlılığı: M01, M02

### Amaç
Periyodik stok sayımı yapılıyor, farklar düzeltiliyor.
Lot ve seri numara takibi yapılıyor, ürün geçmişi izlenebiliyor.

### Ekranlar — M08 Cycle Count

| Ekran | URL | Durum |
|---|---|---|
| Sayım Listesi | `/cycle-count` | Mevcut — test |
| Sayım Detayı | `/cycle-count/details` | Mevcut — CS0103+CS8602 → S0'da |
| Sayım Terminali | `/cycle-count/terminal` | Kontrol et — eksik olabilir |

### Ekranlar — M09 Traceability

| Ekran | URL | Durum |
|---|---|---|
| LPN Listesi | `/lpn` | Kontrol et |
| LPN Detayı | `/lpn/details` | Kontrol et |
| Lot Listesi | `/lot` | Kontrol et |
| Lot Detayı (hareket) | `/lot/details` | Kontrol et |
| Seri No Listesi | `/serial` | Kontrol et |
| Seri No Detayı | `/serial/details` | Kontrol et |

### Sayım Akışı
```
Sayım başlat (DRAFT)
  → Sayım satırı ekle (bin + ürün)
    → QtySystem = anlık stok bakiyesi (snapshot)
      → Depo personeli sayar → QtyCounted girer
        → QtyDifference = QtyCounted - QtySystem
          → Post et
            → Fark varsa → COUNT_ADJ StockMovement yazılır
              → Sayım → COMPLETED
```

### Tolerans Kontrolü
- `CountTolerance` parametresi: örneğin %2
- Fark tolerans içindeyse uyarı yok
- Fark tolerans dışındaysa onay gerekebilir (ileride)

### Kabul Kriteri
1. Sayım oluştur → satır ekle → sayılan miktar gir → post
2. COUNT_ADJ StockMovement yazıldı
3. Stok bakiyesi güncellendi
4. Lot listesi: lot bazlı hareket geçmişi görünüyor
5. Seri no listesi: seri bazlı konum görünüyor
6. Tüm ekranlar Türkçe

---

## SPRINT 8 — M10 Manufacturing
> Önkoşul: S7
> Modül bağımlılığı: M01, M02, M03
> En karmaşık sprint — dikkatli planlanacak

### Amaç
Üretim iş emirleri oluşturuluyor.
BOM (reçete) üzerinden malzeme planlanıyor.
İş istasyonlarında aktiviteler takip ediliyor.
Mamul stoka alınıyor.

### Ekranlar

| Ekran | URL | Durum |
|---|---|---|
| İş Emri Listesi | `/production` | Mevcut — test |
| İş Emri Detayı | `/production/details` | Mevcut — ItemId → S0'da |
| Üretim Terminali | `/production/terminal` | Kontrol et |
| BOM Yönetimi | `/production/bom` | Kontrol et |
| İş Merkezi | `/production/work-centers` | Kontrol et |
| Rota Yönetimi | `/production/routes` | Kontrol et |

### Üretim Akışı
```
İş Emri oluştur (DRAFT) — ürün + hedef miktar
  → BOM'dan malzemeler hesaplanır (DynamicBomService)
    → Malzeme rezervasyonu
      → İş emrini serbest bırak (RELEASED)
        → İş istasyonunda başlat (IN_PROGRESS)
          → Hammadde tüket → CONSUMPTION StockMovement
            → Aktiviteyi bitir (maliyet hesaplanır)
              → Kalite kontrolü (PASS/FAIL/REWORK)
                → PASS: mamul stoka al → PRODUCTION StockMovement
                  → İş emri COMPLETED
```

### Kritik Güvenlik Notu
- `DynamicBomService.EvaluateFormula()` içindeki `DataTable.Compute()` GÜVENSİZ
- Bu sprint'te NCalc ile değiştirilecek
- NCalc: sandboxed, güvenli matematiksel ifade değerlendirici

### Maliyet Hesaplama
- Malzeme maliyeti: BOM satırlarının birim maliyet × miktar
- İşçilik maliyeti: İş merkezi saatlik ücret × süre
- Enerji maliyeti: İş merkezi enerji katsayısı × süre
- Toplam: ProductionOrder.ActualTotalCost güncellenir

### Kabul Kriteri
1. İş emri oluştur → BOM'dan satırlar hesaplandı
2. İş emrini serbest bırak → malzeme rezervasyonu yapıldı
3. Aktivite başlat/bitir → CONSUMPTION StockMovement yazıldı
4. Kalite kontrolü PASS → PRODUCTION StockMovement yazıldı
5. Stok bakiyesinde mamul ürün görünüyor
6. Rework: kalite FAIL → iş emri REWORK statüsüne geçiyor
7. Tüm ekranlar Türkçe

---

## SPRINT 9 — Print Server (Zebra Etiket)
> Önkoşul: S7 (Lot/Serial hazır)
> Ayrı proje: `src/Operax.PrintServer`

### Amaç
Zebra yazıcılara ağ üzerinden ZPL etiket gönderilebiliyor.
Mal kabul, LPN ve lot oluşturmada otomatik etiket basılabiliyor.

### Bileşenler

| Bileşen | Açıklama |
|---|---|
| `ZebraService` | TCP 9100 raw ZPL gönderici |
| `PrintQueueService` | Hangfire job ile asenkron baskı kuyruğu |
| `LabelTemplates` | ZPL şablonları (Item, LPN, Lot, Bin, Carton) |
| `PrintApiController` | Operax.Web → PrintServer iletişimi |

### Etiket Tipleri
- **Item Barkod:** SKU + barkod + ürün adı
- **LPN:** LPN kodu + QR kod + içerik özeti
- **Lot:** Lot no + üretim tarihi + son kullanma
- **Bin:** Bin kodu + QR kod (depo lokasyonu)
- **Koli (Carton):** İçerik + ağırlık + hedef

### Kabul Kriteri
1. Zebra yazıcıya TCP üzerinden bağlanılabiliyor
2. Receiving sonrası etiket basma tetikleniyor (parametre ile)
3. LPN oluşturmada otomatik etiket

---

## SPRINT 10 — M15 Dashboard + Raporlar
> Önkoşul: S8
> Modül bağımlılığı: Hepsi

### Amaç
Yöneticiler anlık durumu görebiliyor.
Bekleyen işler, kritik stoklar ve günlük işlem özetleri tek ekranda.

### Ekranlar

| Ekran | URL | Durum |
|---|---|---|
| Ana Dashboard | `/` veya `/dashboard` | **YOK — yazılacak** |
| Stok Raporu | `/reports/inventory` | **YOK — yazılacak** |
| Hareket Raporu | `/reports/movements` | **YOK — yazılacak** |
| Üretim Raporu | `/reports/production` | **YOK — yazılacak** |

### KPI Kartları
- Bugünkü mal kabuller (adet + kalem)
- Bugünkü sevkiyatlar (adet + kalem)
- Aktif iş emirleri
- Açık pick task sayısı
- Kritik stok: son X günde hiç hareket görmemiş veya minimum altında

### Kabul Kriteri
1. Dashboard açılıyor ve gerçek zamanlı KPI gösteriyor
2. Bekleyen işler listesi çalışıyor
3. Stok raporu Excel/PDF çıktısı alınabiliyor (opsiyonel)
4. Tüm ekranlar Türkçe

---

## Gelecek Faz Detayları

### M18 — Gider Yönetimi
- Schema: `schema_M18_Expenses.sql` hazır
- Gider kategorileri, gider girişi, onay akışı, raporlama

### M19 — Bütçe Yönetimi
- Schema: `schema_M19_Budgeting.sql` hazır
- Bütçe tanımı, gerçekleşen vs bütçe karşılaştırması

### M12 — Servis / Bakım
- Schema: yok — tasarlanacak
- Ekipman kayıtları, bakım planları, arıza takibi

### M13 — Proje Yönetimi
- Schema: yok — tasarlanacak
- Proje bazlı maliyet ve stok takibi

### M16 — Entegrasyon Köprüsü
- ERP webhook'ları (SAP, Logo, Netsis vb.)
- REST API endpoint'leri
- Outbox pattern ile güvenli olay gönderimi
