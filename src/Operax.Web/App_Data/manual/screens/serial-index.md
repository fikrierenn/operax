# Seri No Yönetimi
Tekil ürün bazında seri numarası takibini ve konum izlemeyi sağlayan listeleme ekranıdır. Depo, satış sonrası servis ve kalite personeli bir cihazın/ürünün nerede olduğunu sorgulamak için kullanır.

## Ne işe yarar
Bu ekran, şirketinize ait tüm seri numaralı ürünleri listeler. Her seri numarasının ürünü, durumu (Depoda / Sevk Edildi / Hurda / Karantina), bağlı lotu, güncel konumu ve sisteme giriş tarihi görünür. Belirli bir seri numarasının hangi durumda ve nerede olduğunu hızlıca bulmanızı sağlar. Seri numaraları bu ekrandan elle oluşturulmaz; mal kabul sırasında otomatik üretilir.

## Nasıl kullanılır
1. Sol menüden Seri No Yönetimi ekranını açın.
2. Üstteki kartlardan genel dağılımı görün: Toplam Seri No, Depoda, Sevk Edildi ve Karantina.
3. Arama kutusuna seri no, ürün kodu veya ürün adı yazıp "Ara" butonuna tıklayın.
4. Aramayı kaldırmak için "Temizle" butonunu kullanın.
5. Bir seri numarasının tüm yaşam döngüsünü görmek için ilgili satırdaki "Geçmiş" butonuna tıklayın.

## Alanlar ve butonlar
- **Toplam Seri No**: Kayıtlı toplam seri numarası sayısı.
- **Depoda**: Stokta bulunan (IN_STOCK) seri sayısı.
- **Sevk Edildi**: Müşteriye gönderilmiş seri sayısı.
- **Karantina**: Kontrol bekleyen seri sayısı.
- **Arama kutusu**: Seri no, ürün kodu veya ürün adıyla arama yapar.
- **Ara**: Aramayı çalıştırır.
- **Temizle**: Arama filtresini kaldırır (yalnızca arama yapılmışsa görünür).
- **Seri No**: Ürünün tekil seri numarası.
- **Durum**: Depoda, Sevk Edildi, Hurda veya Karantina.
- **Lot**: Seri numarasının bağlı olduğu parti (varsa).
- **Konum**: Seri numarasının bulunduğu depo/raf; yoksa "Konum yok".
- **Geçmiş**: Seri numarasının detay ve yaşam döngüsü ekranını açar.

## İpuçları ve sık hatalar
- Performans için en fazla 500 kayıt gösterilir; uyarı çıkarsa listeyi daraltmak için arama yapın.
- "Sevk Edildi" durumundaki seri artık depoda değildir; konumu "Depo dışı" olarak görünür.
- Karantina veya Hurda durumundaki seriler sevkiyatta kullanılmamalıdır.
- Bir seriyi bulamıyorsanız henüz sisteme girilmemiş olabilir; seriler mal kabulde oluşturulur.
