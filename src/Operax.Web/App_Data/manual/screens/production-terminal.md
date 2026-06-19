# Üretim Terminali
Atölyedeki operatörlerin işlerini başlatıp bitirdiği, dokunmatik/el terminali için tasarlanmış basit ekrandır. Üretim operatörleri kullanır.

## Ne işe yarar
Bu ekran, sahadaki operatörün o an üzerinde çalıştığı işi ve sıradaki hazır işleri büyük dokunma butonlarıyla gösterir. Operatör bir işi başlattığında çalışma süresi (aktivite) kaydı açılır; işi bitirdiğinde kayıt kapanır. Aynı anda yalnızca bir iş aktif olabilir; yeni iş başlatıldığında önceki otomatik kapanır. Böylece kimin, hangi iş merkezinde, ne kadar süre çalıştığı izlenir.

## Nasıl kullanılır
1. Ekranın üstünde aktif bir işiniz varsa o işin emir no, iş merkezi ve operasyonu görünür.
2. Aktif işi tamamladığınızda büyük kırmızı **İşlemi Bitir** butonuna basın; aktivite kapanır.
3. Aktif iş yoksa "Şu an aktif bir işiniz yok" mesajı görünür.
4. **Hazır Bekleyen İşler** listesinden başlamak istediğiniz iş kartını bulun.
5. İlgili karttaki **İşi Başlat** butonuna basın; iş aktif hâle gelir ve emir durumu IN_PROGRESS olur.

## Alanlar ve butonlar
- **Şu An Devam Ediyor** kartı: Aktif işin emir no, iş merkezi, operasyon ve başlama saatini gösterir.
- **İşlemi Bitir**: Aktif aktiviteyi durdurur (bitiş zamanını kaydeder).
- **Hazır Bekleyen İşler**: Başlatılabilir (RELEASED veya IN_PROGRESS, açık aktivitesi olmayan) işlerin listesi.
- İş kartı: İş merkezi, emir no, ürün adı ve operasyon adını gösterir.
- **İşi Başlat**: Seçilen işi başlatır; varsa önceki aktif iş otomatik kapanır.

## İpuçları ve sık hatalar
- Aynı anda yalnızca bir iş aktif tutulur. Yeni iş başlattığınızda önceki işiniz otomatik kapandığı için, bitirmeden başka işe geçmeyin.
- "Tüm işler tamamlandı veya henüz hazır değil" mesajı, başlatılabilir iş olmadığını gösterir; işin RELEASED/IN_PROGRESS durumda ve rota adımının atanmış olması gerekir.
- Başlamadan önce kart üzerindeki emir no ve operasyonu kontrol edin; yanlış işi başlatırsanız bitirip doğru işi başlatın.
