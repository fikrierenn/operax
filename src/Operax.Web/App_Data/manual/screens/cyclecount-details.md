# Sayım Belgesi

Tek bir fiziksel sayım oturumunun açıldığı, sayım kayıtlarının girildiği ve kesinleştirildiği ekrandır. Depo sorumluları envanteri sistem kayıtlarıyla karşılaştırmak için kullanır.

## Ne işe yarar
Bu ekranda yeni bir sayım oturumu başlatır, hangi raf-ürün için kaç adet fiziksel sayıldığını girer ve sistemin gösterdiği miktarla farkı görürsünüz. Her satırda **Sistem Miktarı**, **Sayım Miktarı** ve aradaki **Fark** yan yana gösterilir. Sayımı bitirdiğinizde belgeyi kesinleştirirsiniz; sistem farkları otomatik düzeltme hareketi olarak stoğa işler.

## Nasıl kullanılır
1. Yeni sayımda önce **Sayım Yapılacak Depo** listesinden depoyu seçin, sonra **Sayımı Başlat** düğmesine tıklayın.
2. Belge açıldıktan sonra **+ Sayım Kaydet** düğmesine tıklayın; açılan pencerede rafı, ürünü ve sayılan miktarı girip **Sayım Kaydını Ekle** ile satırı ekleyin.
3. Her satırda sistemin gösterdiği miktar, sizin saydığınız miktar ve fark otomatik görünür (artı fark yeşil, eksi fark kırmızı renkte).
4. Tüm kalemleri girdikten sonra **Sayımı Kesinleştir ve Kapat** düğmesine tıklayın; farklar stoğa düzeltme hareketi olarak yazılır ve belge **Tamamlandı** olur.
5. Hatalı kesinleştirilen bir sayımı geri almak için **Sayımı İptal Et** düğmesini kullanın; sistem ters düzeltme hareketi yazar.

## Alanlar ve butonlar
- **Sayım Yapılacak Depo**: Yeni sayımda fiziksel sayımın yapılacağı depo.
- **Sayımı Başlat**: Yeni taslak sayım belgesini oluşturur.
- **+ Sayım Kaydet**: Tek bir raf-ürün için fiziksel sayım satırı eklemek üzere pencere açar.
- **Sayım Yapılan Raf**: Sayılan ürünün bulunduğu raf/hücre.
- **Ürün**: Sayılan stok kalemi.
- **Sayım Miktarı (ADET)**: Fiziksel olarak saydığınız adet.
- **Sistem Miktarı**: Kayıt anında sistemin o raf-ürün için gösterdiği bakiye.
- **Fark**: Sayım ile sistem arasındaki sapma (+ fazla, − eksik).
- **Sayımı Kesinleştir ve Kapat**: Farkları stoğa işler, belgeyi tamamlar.
- **Sayımı İptal Et**: Tamamlanmış sayımı geri alır, ters düzeltme hareketi yazar.
- **İptal / Sayım Listesi**: Listeye geri döner.

## İpuçları ve sık hatalar
- Durum akışı: **Taslak** (oturum açıldı) → **Sayılıyor** (kayıt girilmeye başlandı) → **Tamamlandı** (kesinleştirildi).
- Sistem miktarı, satırı eklediğiniz andaki bakiyenin anlık fotoğrafıdır; bu yüzden sayımı kısa sürede tamamlayın.
- Sayımı kesinleştirmeden farklar stoğa işlenmez; **Sayımı Kesinleştir ve Kapat** adımını atlamayın.
- Kapatılmış bir sayımda hata fark ederseniz silme yapmayın; **Sayımı İptal Et** ile ters hareket yazdırın, sonra yeni sayım açın.
- İptal işlemi dönem kilidi gibi bir nedenle engellenebilir; bu durumda ekranda Türkçe hata mesajı görürsünüz.
