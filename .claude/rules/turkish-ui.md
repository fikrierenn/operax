# Türkçe UI ve Dil Kuralları

Bu dosya, Operax platformunun kullanıcı arayüzü (UI) dili ve yazılım dili standartlarını tanımlar. Kullanıcının gördüğü her şeyin tamamen Türkçe olması projenin en temel kurallarından biridir.

---

## 1. Kesin Dil Ayrımı Standartları

| Katman | Dil Standardı | Örnek |
|---|---|---|
| **Arayüz (UI) Metinleri** | **TAMAMEN TÜRKÇE** (UTF-8) | Sayfa başlıkları, butonlar, form label'ları, hata mesajları, toast bildirimleri. |
| **Veritabanı Şeması** | **İNGİLİZCE PASCALCASE** | Tablo ve kolon isimleri (`StockMovement`, `WarehouseId`, `QtyBase`). |
| **C# Kod Tanımlayıcıları** | **İNGİLİZCE** | Sınıf, metot, değişken ve property isimleri (`class ItemService`, `OnPostAsync()`). |
| **Kod İçi Yorum Satırları** | **TÜRKÇE** (UTF-8) | Metot başlarındaki açıklamalar, iş kuralı açıklamaları (`// Stok kontrolü yapılır`). |
| **Durum Sabitleri (Enums/Codes)** | **İNGİLİZCE SABİT** | `DocStatus.Draft`, `MovementType.Receipt`, `FIFO`. |

---

## 2. Türkçe UI Arayüz Standart Karşılıkları

Kullanıcı arayüzünde kullanılan buton, etiket ve hata mesajları için aşağıdaki standart Türkçe karşılıklar tavizsiz uygulanır:

| İngilizce Terim | Türkçe Karşılık | Kullanım Alanı |
|---|---|---|
| **Save** | **Kaydet** | Form submit butonları |
| **Cancel** | **İptal** | Form vazgeçme butonları |
| **Post / Approve** | **Onayla** | Belge kesinleştirme butonları (StockMovement tetikler) |
| **New / Create** | **Yeni** | Yeni kayıt ekleme butonları / Sayfa başlığı |
| **Edit / Update** | **Düzenle** | Kayıt güncelleme butonları / Sayfa başlığı |
| **Delete / Remove** | **Sil** | Satır veya kayıt silme butonları |
| **Add Line** | **Satır Ekle** | Detay tablosuna satır ekleme butonu |
| **Warehouse** | **Depo** | Form label ve tablo başlıkları |
| **Location / Bin** | **Lokasyon / Hücre** | Form label ve depo içi raf/alan başlıkları |
| **Quantity / Qty** | **Miktar** | Miktar sütunları ve alanları |
| **Actions** | **İşlemler** | Tablo sonundaki işlem sütunu başlığı |
| **Search...** | **Ara... / Arama yapın...** | Arama placeholder'ları |
| **Select...** | **Seçiniz...** | Dropdown default placeholder seçeneği |
| **No records found.** | **Kayıt bulunamadı.** | Boş durum (Empty state) metinleri |
| **Saved successfully.** | **Kaydedildi.** | İşlem başarılı toast bildirimi |
| **Error occurred.** | **Hata oluştu.** | Başarısız işlem bildirimi veya hata mesajı |
| **Back / Return** | **Geri** | Listeye veya önceki sayfaya dönüş butonları |
| **Details** | **Detay** | Kayıt detay görünümü linki veya başlığı |

---

## 3. Kodlama Sırasındaki UI Tarama Kuralı

*   Yeni bir ekran yazıldığında veya mevcut bir `.cshtml` / `.cshtml.cs` dosyası düzenlendiğinde, dosya içinde arayüze yansıyan İngilizce metin kalıp kalmadığı kontrol edilmelidir.
*   Butonlar, form label'ları, placeholder'lar, modal başlıkları, toast mesajları ve validation hata mesajları Türkçe standardına göre yazılmalıdır.
