# Cari Kartı
Tek bir müşteri veya tedarikçinin tüm bilgilerini girdiğiniz ve mali geçmişini incelediğiniz ekrandır. Yeni cari açarken ve mevcut cariyi yönetirken kullanılır.

## Ne işe yarar
Cari kartı; firmanın kimlik ve iletişim bilgileri, mali ayarları (vade, kredi limiti, ödeme yöntemi, risk), sorumlu temsilcileri ve e-Belge bilgilerini tutar. Mevcut bir caride ayrıca ekstre/hareketler, siparişler, faturalar, çek/senet, mutabakat ve fiyat listeleri sekmelerinden cari ile olan tüm ilişkinizi tek yerden takip edersiniz.

## Nasıl kullanılır
1. "Genel" sekmesinde zorunlu alanları doldurun: Cari Tipi ve Cari Ünvanı.
2. İletişim, vergi no, adres ve notları girin.
3. "Mali Ayarlar" kartından vade politikası, vade gün sayısı, kredi limiti, ödeme yöntemi ve risk ayarlarını belirleyin.
4. Gerekirse satış/satınalma sorumlusu temsilcileri ve e-Belge bilgilerini ekleyin.
5. "Kaydet" butonuna basarak cariyi oluşturun. Cari kodu kayıt sırasında otomatik atanır.
6. Mevcut bir caride alanları değiştirmek için önce "Düzenle" butonuna basın; form açılır, değişiklik sonrası "Kaydet" veya "İptal" deyin.
7. Üstteki sekmelerden cari ile olan hareketleri inceleyin: Ekstre / Hareketler, Siparişler, Faturalar, Çek / Senet, Mutabakat, Fiyatlar.

## Alanlar ve butonlar
- **Cari Kodu**: Otomatik atanır, değiştirilemez.
- **Cari Tipi**: Tedarikçi, Müşteri veya Her İkisi. Zorunlu.
- **Cari Ünvanı**: Tam ticaret ünvanı. Zorunlu.
- **Vergi No / E-posta / Telefon / Adres / Notlar**: İletişim ve kimlik bilgileri.
- **Vade Politikası / Vade (Gün)**: Ödeme vadesinin başlangıcı ve gün sayısı.
- **Kredi Limiti (₺)**: Cariye tanınan azami açık tutar.
- **Limit aşımında bloke edilsin**: İşaretliyse limit aşılınca sipariş/sevkiyat engellenir.
- **Varsayılan Ödeme Yöntemi / Maks. Gecikme (Gün)**: Ödeme tercihi ve otomatik bloke eşiği.
- **Satış / Satınalma Sorumlusu**: Cariden sorumlu kullanıcılar.
- **e-Fatura mükellefi / Alias / İade IBAN**: e-Belge ve iade için banka bilgileri.
- **Aktif Cari / Risk Skoru / Risk Kategorisi**: Durum ve risk değerlendirmesi.
- **Düzenle / Kaydet / İptal / Geri**: Form modunu açar, değişikliği kaydeder veya vazgeçer.
- **Sekmeler**: Ekstre/Hareketler (bakiye + hareketler), Siparişler, Faturalar, Çek/Senet, Mutabakat, Fiyatlar.

## İpuçları ve sık hatalar
- Mevcut cariyi açtığınızda alanlar salt-okunur gelir; değişiklik için mutlaka önce "Düzenle" butonuna basın.
- Ekstre/Hareketler ve Siparişler sekmelerinde varsayılan tarih aralığı son 30 gündür; daha eski hareketler için tarih filtresini genişletin.
- Açık siparişler cari bakiyesini ve ekstreyi etkilemez; sadece bilgi amacıyla ayrı gösterilir. Bakiye yalnızca fatura ve ödeme/tahsilat hareketlerinden oluşur.
- Mutabakat sekmesinde bir tur başlatınca bakiye o anki haliyle dondurulur (snapshot); sonraki hareketler o turu değiştirmez.
- Risk skoru 0 gibi geçersiz bir değerle gelirse sistem otomatik 3'e çeker; kaydı bozmadan düzeltir.
