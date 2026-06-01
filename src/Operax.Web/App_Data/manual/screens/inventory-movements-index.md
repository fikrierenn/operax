# Stok Hareketleri
Depolarınızda gerçekleşen tüm stok giriş, çıkış ve transfer hareketlerini kronolojik olarak gösteren rapor ekranıdır. Depo ve muhasebe personeli stok hareketlerini izlemek ve denetlemek için kullanır.

## Ne işe yarar
Bu ekran, aktif şirketinize ait son stok hareketlerini en yeniden eskiye doğru listeler. Her hareketin tarihi, tipi (giriş/çıkış/transfer), ürünü, lokasyonu, miktarı, işlemi yapan operatörü ve kaynak belgesini görürsünüz. Hangi belgeden hangi stok hareketinin oluştuğunu takip etmenizi sağlar. Hareketler belgeler onaylandığında (mal kabul, sevkiyat, transfer, sayım) otomatik olarak oluşur; bu ekran salt görüntülemedir.

## Nasıl kullanılır
1. Sol menüden Raporlar & Analiz > Stok Hareketleri yolunu izleyerek ekranı açın.
2. Üstteki kartlardan günlük hacmi gözden geçirin: Son 24 Saat, Toplam Giriş Hacmi ve Toplam Çıkış Hacmi.
3. Belirli bir güne ait hareketleri görmek için tarih kutusunu kullanın.
4. Hareket tipine göre süzmek için "Tüm Hareketler" açılır listesinden RECEIPT (Giriş), ISSUE (Çıkış) veya TRANSFER seçin.
5. Belirli bir ürün, lot veya belge aramak için "SKU, Lot veya Belge ara..." kutusunu kullanın.
6. Tablodan hareket satırını okuyun; yeşil satırlar giriş, kırmızı satırlar çıkış hareketidir.

## Alanlar ve butonlar
- **Son 24 Saat**: Son bir gün içinde gerçekleşen işlem sayısını gösterir.
- **Toplam Giriş Hacmi**: Depoya giren toplam baz miktarı gösterir.
- **Toplam Çıkış Hacmi**: Depodan çıkan toplam baz miktarı gösterir.
- **Tarih kutusu**: Hareketleri belirli bir güne göre süzer.
- **Tüm Hareketler (açılır liste)**: Hareket tipine göre süzme (Giriş / Çıkış / Transfer).
- **SKU, Lot veya Belge ara...**: Ürün kodu, lot veya kaynak belge numarasıyla arama yapar.
- **İşlem Tipi**: Hareketin türü — Giriş, Çıkış veya Transfer rozeti.
- **Miktar**: Baz birimde değişim; girişlerde + (yeşil), çıkışlarda − (kırmızı) gösterilir.
- **Operatör**: Hareketi oluşturan kullanıcı; sistem otomatik oluşturduysa "Sistem" yazar.
- **Kaynak Belge**: Hareketin kaynaklandığı belge numarası ve türü.

## İpuçları ve sık hatalar
- Performans için en fazla son 500 hareket listelenir; daha eski kayıtları görmek için tarih veya arama ile süzme yapın.
- Stok hareketleri bu ekrandan elle eklenemez veya silinemez; her hareket bir belge onayından doğar.
- İptal edilen hareketler bakiye hesabına dahil edilmez; rapor canlı belge durumunu yansıtır.
- Miktarın önündeki + veya − işareti hareketin yönünü gösterir: + giriş, − çıkış.
