# OPERAX Platform Master Döküman
**Modüler Kurulabilir ve Satılabilir Operasyon Platformu (WMS + Üretim + Ticari)**  
*Sürüm: v2.2-TR | Tarih: Mart 2026*

---

## 1. Bu Doküman Ne?
Bu doküman, OPERAX Platform’un modül modül kurulabilen ve lisanslanabilen bir ürün ailesi olarak tasarımını anlatır. Anlatım dili Türkçedir; tablo/kolon isimleri ve kod değerleri (Dictionary Code) İngilizce ERP/WMS literatürüne göre standarttır. Böylece hem saha ekibi Türkçe okur, hem de entegrasyon ve teknik katman global standarda oturur.

*   **Dinamik Reçete (Parametrik BOM):** Girilen ölçülere göre anlık reçete doğurma.
*   **Çok Aşamalı Rota (Routing):** Üretimin **Kesim -> Montaj -> Paketleme** gibi ardışık istasyonlarda işlem görmesi.
*   **Esnek İzlenebilirlik (Refakat Formu vs. Etiket):** İstasyonlar arası fiziksel etiket basmak yerine; tüm üretim emrini en başından sonuna kadar takip eden bir **Refakat Formu (Travel Card)** kullanılabilir. Alternatif olarak, her istasyon için **Dijital İş Kuyruğu** ekranları üzerinden "Kesim'den gelen işler" listelenerek etiketsiz akış da sağlanabilir.
*   **Mola & Kesinti Yönetimi:** Operatör işi her an durdurup (elektrik/işçilik maliyetini mühürleyip) sonra devam edebilir.
*   **Kümülatif Maliyet:** Her operasyon bittiğinde (Stop), hesaplanan maliyetler iş emrinin "Kumbarasına" add-on olarak eklenir.
*   **Hücresel WMS:** Stok `Warehouse` bazında değil `Location` (bin) bazında tutulur.
*   **UOM standardı:** Base UOM = `EACH` (adet). PACK/CASE barkod okutulsa bile base qty (EACH) yazılır.
*   **Kısmi sevk:** `SalesOrderLine` satırları birden fazla `Shipment` ile kademeli kapanabilir.

---

## 2. İsimlendirme Standardı (Schema / Code / UI)
Bu platformda üç ayrı isim katmanı vardır. Bu ayrımı bozmamak ürünleşmenin anahtarıdır:

| Katman | Dil | Örnek | Kural |
| :--- | :--- | :--- | :--- |
| **Schema** (tablo/kolon) | İngilizce | `SalesOrder` / `StockMovement` | ASCII, PascalCase, PK=Id |
| **Code** (dictionary) | İngilizce | `Status=DRAFT`, `UOM=EACH` | Sabit kodlar EN, değişmez anahtar |
| **UI** (görünen ad) | Türkçe | Durum=Taslak, Birim=Adet | Çok dilli gösterim: NameTr/NameEn |

---

## 3. Dictionary / Parameter Standardı (Hard-code Yasağı)
Operax’ta sistem davranışını belirleyen tüm tanımlar sözlük ve parametre tablolarındadır. Kod tarafında enum/if-else ile süreç sabitlenmez.

### 3.1 Sözlük tabloları
*   `DictionaryType`: Sözlük türü tanımlar (Status, UOM, DocumentType...)
*   `DictionaryValue`: Sözlük değerleri (DRAFT, EACH...)
*   `StatusTransition`: Belge bazlı durum geçişleri (Hangi roldeki kullanıcı hangi durumdan hangisine geçirebilir).

### 3.2 Parametre tablosu
*   `Parameter`: Şirket+modül bazlı ayarlar (Örn: `RequireBinScan`, `AllocationStrategy`).

---

## 4. Modül Kataloğu (SKU Mantığı)
OPERAX, çekirdek + opsiyonel modüller olarak satılabilir.

| Kod | Modül | Zorunlu mu? | Bağımlılık |
| :--- | :--- | :--- | :--- |
| **M00** | Platform Core | Evet | - |
| **M01** | Master Data | Evet | M00 |
| **M02** | Inventory Ledger | Evet | M00+M01 |
| **M03** | Procurement & Receiving | Opsiyonel | M00+M01+M02 |
| **M04** | Sales Order | Opsiyonel | M00+M01 |
| **M05** | Shipping | Opsiyonel | M00+M01+M02+M04 |
| **M06** | Picking | Opsiyonel | M05 (+M01 bin) |
| **M07** | Transfer & Replenishment | Opsiyonel | M00+M01+M02 |
| **M08** | Cycle Count | Opsiyonel | M00+M01+M02 |
| **M09** | LPN & Traceability | Opsiyonel | M01+M02 |
| **M10** | Manufacturing | Opsiyonel | M00+M01+M02 |
| **M15** | Dashboards | Opsiyonel | M00 + (hedef modül) |

---

## 5. Modül Detayları

### M00 — Platform Core
**Problem / Neden var?**  
Altyapı olmadan modüller satılamaz. Kimlik, yetki, sözlük, parametre, modül aktivasyonu ve audit/queue çekirdekte çözülmelidir.

**İşleyiş Hikayesi:** Belge oluşturulur (DRAFT) -> Satırlar girilir, validasyon yapılır -> Onay (POSTED) ile stok/olay etkileri üretilir -> AuditLog her adımı saniye bazlı mühürler.

---

### M01 — Master Data
**Problem / Neden var?**  
Kartlar ve UOM/barkod standardı oturmazsa WMS/üretim çöker.  
**Özellikler:**
*   **Item:** Ürün kartı (BaseUOM=EACH)
*   **ItemUOM:** Birimler arası otomatik dönüşüm (1 CASE = 24 EACH).
*   **ItemBarcode:** Bir ürünün birden fazla barkodu (Adet barkodu, Koli barkodu) olabilir.
*   **Location (Bin):** Hücresel WMS altyapısı. Raf, göz, kat bazlı adresleme.

---

### M02 — Inventory Ledger (Envanter Hareketleri)
**Problem / Neden var?**  
Birden çok yerde stok tutarsan sistem çürür. Tek stok gerçeği defterdir.  
**İzlenebilirlik:** "Kim, Nereden, Ne Zaman, Ne Aldı?" Bu ekran artık her hareketi yapan **Operatör** (User) bilgisiyle tutar. El terminalinden yapılan her işlem burada saniye hassasiyetinde loglanır.

---

### M03 — Procurement & Receiving (Satın Alma ve Mal Kabul)
**Problem / Neden var?**  
Depoya giriş düzensizse devamı bozulur.  
**İşleyiş:** Sipariş (PO) açılır -> Mal kabulde gelen miktar girilir -> Sistem otomatik olarak `RECEIPT` hareketi oluşturur.

---

### M04 — Sales Order (Satış Siparişi)
**Problem / Neden var?**  
Kısmi sevk gerçek hayattır. Satır bazlı takip yoksa sipariş yönetilemez.

---

### M05 — Shipping (Sevkiyat)
**Problem / Neden var?**  
Sevk, siparişi gerçek dünyaya çıkarır. Stok düşümü (`ISSUE`) burada onaylanır.

---

### M06 — Picking (Toplama - Akıllı Emir Yönetimi)
**Problem / Neden var?**  
Sevkiyat ‘ne çıkacak’ der; Picking ‘nereden topla’ der.  
**Detaylar:**
*   **İzlenebilirlik:** Her toplama satırı operatör bazlı loglanır.
*   **Akıllı Stok Kontrolü:** Toplama emri oluşturulurken stok kontrol edilir. Stok varsa **Pick Task**; yoksa otomatik **M09/M10 Planlama/Üretim** tetiklenir.
*   **Bin Integration:** Operatör terminalde belirtilen raf adresinden ürünü onaylayarak alır.

---

### M09 — Production Planning (Üretim Planlama)
**Problem / Neden var?**  
Üretim bir emir değil önce bir planlamadır.  
**İşleyiş:**
*   Sevkiyattan (M05) stok yetersizliği ile tetiklenen ihtiyaçlar planlama havuzuna düşer.
*   **BOM Entegrasyonu:** Mamülün reçetesinden hammadde ihtiyaçları çıkarılır.
*   **Picking for Production:** Üretim için gerekli hammaddeler depodan 'Toplama Emri' ile toplatılır.

---

### M10 — Manufacturing (Üretim Yönetimi)
**Problem / Neden var?**  
Üretimde sarf görünmezse maliyet körlüğü olur.  
*   **Kalite Kontrol & Hata Yönetimi (Inspection Gate):** 
    *   **PASS (Onay):** Ürün mamül stokuna girer, üretim tamamlanır.
    *   **FAIL - REWORK (Esnek Yeniden İşlem):** Bu sadece bir "geri dön" butonu değildir. Mobilya gibi işlerde **"Ayağını değiştir"**, **"Aynasını yenile"** gibi komutlarla ek hammadde sarfiyatı (Extra Consumption) tetiklenebilir. Sistem, o ürün için harcanan *normal* malzemenin üzerine bu *extra* tamir parçalarını ve tamir için geçen ek zamanı (saniye) kümülatif maliyet olarak ekler.
    *   **FAIL - SCRAP (Hurda):** Ürün kurtarılamazsa imha edilir; fire maliyeti zarar hanesine yazılır.
    *   **FAIL - CANCEL (İptal):** Kritik hata durumunda üretim emri durdurulur. Eğer bu üretim bir satış siparişine bağlıysa, satış temsilcisine "Sipariş gecikecek veya iptal edilmeli" uyarısı gider.
*   **Maliyet Etkisi:** Rework gören bir ürün, "İlk seferde doğru" yapılan bir üründen daha pahalıya mal olur (Ek malzeme + Ek İşçilik). Sistem bu varyansı anlık raporlar.
**İşleyiş:** Hammaddeler toplanır (`CONSUMPTION`) -> Üretim biter -> Son Kontrol -> Karar (Onay/Tamir/Hurda/İptal) -> Stok Girişi veya Süreç Tekrarı. -> Mamül depoya girer (`PRODUCTION`) -> Bekleyen sevkiyat otomatik "Toplanabilir" hale gelir.

---

## 6. Teknik Standartlar
*   **Framework:** .NET 10, ASP.NET Core Razor Pages
*   **Data:** Dapper (High Performance SQL)
*   **Security:** ASP.NET Core Identity (Audit Trail Support)
*   **UI:** Tailwind CSS v4 (Modern & Premium Design)

---

## 7. Paketleme (SKU Önerisi)
*   **STARTER:** M00, M01, M02, M03, M04, M05
*   **WMS_PRO:** STARTER + M06 + M07 + M08
*   **ENTERPRISE:** WMS_PRO + M09 + M10 + M15
