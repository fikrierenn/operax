# Statü Geçişleri
Belgelerin yaşam döngüsü kurallarını, yani bir durumdan hangi duruma geçebileceğini listeler. Yalnızca Yönetici (Administrator) rolü kullanabilir.

## Ne işe yarar
Sistemdeki belgeler (sipariş, mal kabul, fatura gibi) belirli durumlar arasında geçiş yapar; örneğin TASLAK durumundan ONAYLI durumuna. Bu ekran, her belge tipi için izin verilen geçişleri ve bu geçişe ait aksiyon adını gösterir. Böylece belgelerin hangi adımlardan geçeceğini ve iş akışını görebilirsiniz.

## Nasıl kullanılır
1. Tablo, tanımlı tüm statü geçişlerini listeler: belge tipi, kaynak statü, hedef statü ve aksiyon adı.
2. Her satırda kaynak statüden hedef statüye doğru bir ok, geçişin yönünü gösterir.
3. Bir geçişi kaldırmak için satırdaki **Sil** butonunu kullanın.
4. Yeni bir geçiş eklemek için sağ üstteki **Yeni Geçiş** butonunu kullanın (Alt+N ile vurgulanır).
5. Üst soldaki **Ayarlar** bağlantısı sizi Sistem Ayarları ekranına döndürür.

## Alanlar ve butonlar
- **Belge Tipi**: Geçiş kuralının uygulandığı belge türü.
- **Kaynak Statü**: Belgenin geçişten önceki durumu (örnek: DRAFT).
- **Hedef Statü**: Geçiş sonrası belgenin alacağı durum (örnek: POSTED).
- **Aksiyon Adı**: Geçişi tetikleyen işlemin Türkçe adı (örnek: Onayla).
- **Sil**: Seçili geçiş kuralını kaldırır.
- **Yeni Geçiş**: Yeni bir statü geçiş kuralı eklemek için kullanılır.

## İpuçları ve sık hatalar
- Tipik belge akışı TASLAK (DRAFT) → ONAYLI (POSTED) → İPTAL (CANCELLED) yönündedir; tanımladığınız geçişler bu mantıkla tutarlı olmalıdır.
- Bir geçişi silmek, o belge tipinde ilgili durum değişiminin yapılamaması anlamına gelebilir; silmeden önce iş akışına etkisini değerlendirin.
- Geçişler belge tipine ve sıra numarasına göre gruplanarak listelenir.
- Yalnızca şirketinize ait ve silinmemiş geçişler görünür.
