# Fiyat Farkları

Satınalma siparişi kalemlerinde tedarikçi liste fiyatından sapan kalemleri listeleyen ve satınalma yöneticisinin onay ya da red kararını verdiği ekrandır.

## Ne işe yarar

Bir sipariş kalemine girilen fiyat, tedarikçinin tanımlı liste fiyatından saptığında sistem otomatik olarak bir "Fiyat Farkı" kaydı (DRAFT) açar ve satınalmacıya uyarı gösterir. Bu ekranda tüm bekleyen fark kayıtları görüntülenir; yetkili yönetici her birini ayrı ayrı onaylayabilir veya reddedebilir. Onaylandığında ilgili PO satırının fiyatı ve ürün maliyeti güncellenir; reddedildiğinde sipariş fiyatı değişmez, fark kayıtlı kalır.

## Nasıl kullanılır

1. Soldaki menüden **Satınalma → Siparişler** altında **Fiyat Farkları** bağlantısına veya Satınalma Siparişi Detayı'ndaki sarı uyarı bandındaki bağlantıya tıklayın.
2. Varsayılan **Bekleyen** sekmesinde onay bekleyen fark kayıtları listelenir.
3. Her satırda sipariş numarası, tedarikçi, ürün, liste fiyatı, girilen fiyat, fark tutarı ve sapma yüzdesi görünür.
4. **Onayla** butonuyla fark kabul edilir; sipariş satırı fiyatı ve ItemCost güncellenir.
5. **Reddet** butonuyla fark reddedilir; sipariş fiyatı değişmez, kaydın durumu REJECTED olur.
6. **Onaylı** ve **Reddedilen** sekmeleriyle geçmişe bakabilirsiniz.

## Alanlar ve butonlar

- **Bekleyen / Onaylı / Reddedilen sekmeleri**: Rozet sayısıyla birlikte durum bazlı filtreleme yapar.
- **Evrak No**: İlgili satınalma siparişinin numarası; tıklanınca siparişin detay ekranına gider.
- **Tedarikçi**: Sapmanın oluştuğu siparişteki tedarikçi.
- **Ürün**: SKU kodu ve ürün adı.
- **Liste Fiyatı**: Tedarikçi fiyat listesindeki beklenen birim fiyat.
- **Girilen Fiyat**: Sipariş kalemine girilen gerçek fiyat.
- **Fark**: Girilen fiyat − Liste fiyatı. Pozitif (kırmızı) daha pahalı, negatif (yeşil) daha ucuz anlamına gelir.
- **Sapma %**: Farkın liste fiyatına oranı. %10'u aşan sapmalar kırmızı, altındakiler turuncu rozet alır.
- **Tarih**: Fiyat farkı kaydının oluşturulma tarihi.
- **Onayla**: Farkı kabul eder; `sp_ApprovePriceVariance` SP'si çalışır, maliyet motoru güncellenir. Yalnızca Bekleyen sekmesinde görünür.
- **Reddet**: Farkı reddeder; kaydın durumu REJECTED olur, sipariş fiyatı korunur. Yalnızca Bekleyen sekmesinde görünür.

## İpuçları ve sık hatalar

- Fiyat farkı kaydı, sipariş kalemine **girilen fiyatın tedarikçi liste fiyatından sapması** durumunda otomatik oluşur; satınalmacı sipariş ekranına kalem eklerken bu süreci manuel tetiklemez.
- Onaylama işlemi, hem sipariş satırının fiyatını hem de ürünün hareketli ortalama maliyetini günceller; maliyete hassas raporlarda bu değişiklik yansır.
- Reddedilen kayıtlar silinmez; denetim amacıyla REJECTED durumunda saklanır.
- Bir fiyat farkı yalnızca DRAFT durumdayken onaylanabilir veya reddedilebilir; APPROVED ya da REJECTED kayıtlarda butonlar görünmez.
- Beklenen sepette çok sayıda kayıt birikirse tedarikçi kartında fiyat listesi güncellenmemiş olabilir; önce tedarikçi fiyat listelerini kontrol edin.
