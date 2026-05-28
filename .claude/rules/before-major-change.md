# Büyük Değişiklik Öncesi Kurallar

Bu dosya, Operax projesinde yapılacak büyük çaplı kod, rota veya veritabanı şeması değişikliklerinden (refactoring, dosya silme, yeniden adlandırma, tablo değişiklikleri vb.) önce yapılması gereken kontrolleri tanımlar. Beklenmeyen derleme ve çalışma zamanı hatalarını (runtime errors) önlemek için bu kurallara uyulması zorunludur.

---

## 1. Referans Arama ve Doğrulama

Büyük bir değişiklik yapmadan önce, etkilenecek tüm dosya ve referanslar `grep_search` kullanılarak taranmalıdır:

1.  **Metot / Sınıf Silme veya Yeniden Adlandırma (Rename):**
    *   Değiştirilecek sınıf veya metot adının tüm projede nerelerde kullanıldığı (`Features/`, `Lib/` klasörleri dahil) sorgulanmalıdır.
2.  **Model / DTO Değişiklikleri:**
    *   Veritabanı tablosundaki bir kolon adı veya DTO property'si değiştirildiğinde, bu property'yi kullanan tüm PageModel (`.cshtml.cs`) ve View (`.cshtml`) dosyaları bulunmalı ve güncellenmelidir.
3.  **Rota (Route) Kaldırma veya Değiştirme:**
    *   Razor Page URL yapısı veya sayfa yönlendirmesi (`RedirectToPage`) değiştirilmeden önce, eski rotayı çağıran form `action` etiketleri, `a href` linkleri ve yönlendirme komutları güncellenmelidir.

---

## 2. Şema ve Migration Değişiklikleri

Veritabanında yapılacak şema güncellemelerinden önce:

1.  **Geriye Dönük Uyumluluk (Backward Compatibility):**
    *   Silinecek veya değiştirilecek kolonun mevcut canlı sistemlerde veri kaybına yol açıp açmayacağı değerlendirilmelidir.
    *   Eğer bir kolon ikiye bölünecekse veya tipi değişecekse, veriyi dönüştüren geçici bir migration script'i tasarlanmalıdır.
2.  **Stored Procedure ve View Etkisi:**
    *   Tablo şeması değiştiğinde, o tabloyu okuyan Stored Procedure (`docs/sql/db_objects.sql`) ve SQL View tanımları güncellenmelidir. `CREATE OR ALTER` ile bu nesneler veritabanına yeniden yüklenmelidir.

---

## 3. Güvenli Aşamalı Geçiş Planı

Büyük çaplı değişikliklerde tek bir devasa commit yerine **aşamalara bölünmüş (incremental)** geçiş planı uygulanmalıdır:

1.  **Aşama 1 (Veritabanı):** Önce veritabanı şeması ve nesneleri güncellenir.
2.  **Aşama 2 (DTO & Core):** `Lib/` altındaki ortak sınıflar ve DTO'lar yeni şemaya göre güncellenir.
3.  **Aşama 3 (PageModels & Lojik):** Backend lojikleri ve PageModel sınıfları adapte edilir.
4.  **Aşama 4 (Views & Arayüz):** Arayüzler (`.cshtml`) ve JavaScript kodları yenilenir.
5.  **Aşama 5 (Doğrulama):** `dotnet build` çalıştırılarak 0 hata ve 0 uyarı alındığı doğrulanır.
