# Mal Kabul Listesi

Şirkete ait tüm mal kabul belgelerini listeleyen ve yeni belge oluşturmaya giriş noktası olan ekrandır.

## Ne işe yarar

Depo ve satınalma ekibinin tedarikçilerden gelen malların giriş sürecini başlattığı merkezi listedir. Ekranın üst kısmındaki KPI kartları anlık olarak kaç belgenin taslak (onay bekliyor), kaç belgenin onaylı (depoya girilmiş) ve kaç belgenin iptal edilmiş olduğunu gösterir. Tabloda belge numarası, tarih, tedarikçi, depo kodu ve durum yan yana görünür.

## Nasıl kullanılır

1. Soldaki menüden **Lojistik → Mal Kabul** yolunu izleyerek bu ekrana gelin.
2. KPI kartlarından bekleyen işlemleri görün.
3. Tabloda arama kutusuna belge numarası veya tedarikçi adı yazarak filtreleme yapın; **Tüm Durumlar** açılır listesiyle yalnızca Taslak veya Onaylı belgeler gösterilebilir.
4. Bir satırdaki **Aç** butonuna veya tablonun herhangi bir yerine tıklayarak mal kabul belgesinin detay ekranına geçin.
5. Sağ üstteki **Yeni Mal Kabul** (`Alt+N`) butonuyla yeni bir mal kabul belgesi oluşturmaya başlayın.

## Alanlar ve butonlar

- **KPI: Bekleyen Kabuller**: DRAFT durumundaki belge sayısı; onay bekleyen girişleri gösterir.
- **KPI: Tamamlanan Girişler**: POSTED durumundaki belge sayısı; depoya stok olarak giren mal kabullerini gösterir.
- **KPI: İptal Edilenler**: CANCELLED durumundaki belge sayısı.
- **Arama kutusu**: Belge numarası veya tedarikçi adına göre eşleştirme yapar (geliştirme aşamasındadır; mevcut sürümde arama formun gönderilmesiyle sunucu taraflı çalışabilir).
- **Durum filtresi**: Tüm Durumlar / DRAFT / POSTED seçenekleri.
- **Belge No**: Her mal kabul belgesinin benzersiz numarası (ör. `RCV-20260601-00001`).
- **Tarih**: Belgenin oluşturulma tarihi.
- **Tedarikçi**: Malın alındığı tedarikçi.
- **Depo**: Malın girişinin yapıldığı deponun kısa kodu.
- **Durum rozeti**: Taslak (turuncu) / Tamamlandı (yeşil) / İptal (kırmızı) durumlarını gösterir.
- **Aç butonu**: Belgenin detay ekranını açar.
- **Yeni Mal Kabul** (`Alt+N`): Boş bir mal kabul detay formu açar.

## İpuçları ve sık hatalar

- Mal kabul belgelerinin büyük çoğunluğu **Satınalma Siparişi Detayı** ekranından otomatik oluşturulur; bu ekrandan elle girilen belgeler genellikle siparişsiz (doğrudan) girişler içindir.
- **POSTED** durumundaki belgeler stok hareketini tamamlamıştır; bu belgelerin miktarı veya tedarikçisi değiştirilemez. Bir hata varsa belgeyi iptal etmek ve yeniden açmak gerekir.
- Durum **Taslak** iken belge silinebilir; POSTED sonrası silme işlemi yapılamaz.
- Depo kodu tabloda kısaltılmış görünür; depunun tam adını görmek için belgeyi açın.
