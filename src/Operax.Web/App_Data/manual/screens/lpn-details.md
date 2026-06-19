# Palet Sorgulama
Tek bir paletin (LPN) güncel konumunu ve içindeki tüm ürünleri SKU/lot bazında gösteren sorgulama ekranıdır. Depo personeli bir paletin ne taşıdığını ve nerede olduğunu kontrol etmek için kullanır.

## Ne işe yarar
Bu ekran, seçtiğiniz paletin kodunu, durumunu ve güncel konumunu (depo/raf) gösterir. Altında, palet içindeki ürünleri ürün, lot ve miktar bazında listeler ve toplam ürün adedini özetler. Paleti raflama (putaway), sayım veya sevkiyat öncesi içerik doğrulaması yapmak için kullanılır.

## Nasıl kullanılır
1. Palet (LPN) Yönetimi listesinden bir paletin "Sorgula" butonuna tıklayarak bu ekrana gelin.
2. Üst satırdan palet kodunu, durumunu ve güncel konumunu kontrol edin.
3. "Palet İçeriği (SKU Bazlı)" tablosundan paletteki ürünleri, lotlarını ve miktarlarını inceleyin.
4. Alttaki özet kutusundan paletin toplam ürün adedini ve operasyonel durumunu görün.

## Alanlar ve butonlar
- **Palet Kodu**: Paletin tekil kodu (başlıkta gösterilir).
- **Durum rozeti**: Paletin güncel durumu.
- **Güncel Konum**: Paletin bulunduğu depo/raf.
- **Paleti Taşı**: Paleti başka bir konuma taşımak için tasarlanmış butondur.
- **Yazdır**: Palet etiketini yazdırmak için tasarlanmış butondur.
- **Palet İçeriği (SKU Bazlı)**: Ürün, lot ve miktar dökümü.
- **Kalem Mevcut**: Palet içindeki farklı ürün satır sayısı.
- **Palet Özeti**: Paletteki toplam ürün adedini gösteren özet kutusu.
- **Operasyonel Durum**: Paletin mevcut iş akışı durumunu açıklayan bilgi notu.

## İpuçları ve sık hatalar
- İçerik tablosu yalnızca bakiyesi sıfırdan farklı kalemleri gösterir; boşalan satırlar listelenmez.
- Palet içeriği boşsa "Palet içeriği boş." mesajı görünür.
- Sevkiyat veya transfer öncesi palet içeriğini bu ekrandan doğrulamak hataları önler.
- İçerik bilgisi paletteki güncel stok hareketlerinden hesaplanır; iptal edilmiş hareketler dahil edilmez.
