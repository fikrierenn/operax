# Rota Detayı
Bir üretim rotasının operasyon adımlarını (sıra, operasyon, iş merkezi, süre ve maliyet) tanımladığınız ekrandır. Üretim planlamacıları kullanır.

## Ne işe yarar
Bu ekranda seçili rotaya sıralı operasyon adımları eklersiniz. Her adım bir operasyonu (örn. CNC Kesim), bunun yapılacağı iş merkezini, standart süreyi ve standart işçilik/makine maliyetini içerir. Adımlar otomatik olarak 10'ar artan sıra numarasıyla dizilir. Ekranın altında rotanın toplam süresi ve toplam maliyeti otomatik hesaplanır.

## Nasıl kullanılır
1. **Yeni Operasyon Adımı** formunda Operasyon Kodu (örn. KESIM) ve Operasyon Adı (örn. CNC Kesim) girin.
2. **İş Merkezi** listesinden adımın yapılacağı istasyonu seçin (zorunlu).
3. **Süre (dk)**, **İşçilik Maliyeti (₺)** ve **Makine Maliyeti (₺)** alanlarını doldurun.
4. **Adım Ekle** butonuna tıklayın; adım tablonun sonuna eklenir.
5. Operasyon Adımları tablosunda sıra, operasyon, iş merkezi, süre ve maliyetleri görün.
6. Yanlış eklenen bir adımı satırındaki **Sil** butonuyla kaldırın (onay sorulur).

## Alanlar ve butonlar
- **Operasyon Kodu**: Adımın kısa kodu (zorunlu, otomatik büyük harfe çevrilir).
- **Operasyon Adı**: Operasyonun açıklayıcı adı (zorunlu).
- **İş Merkezi**: Adımın yapılacağı aktif iş merkezi (zorunlu).
- **Süre (dk)**: Operasyonun standart süresi (dakika; sistem saniyeye çevirip saklar).
- **İşçilik Maliyeti (₺) / Makine Maliyeti (₺)**: Adımın standart maliyet kalemleri.
- **Adım Ekle**: Yeni operasyon adımını rotaya ekler.
- **Sıra**: Adımın rota içindeki sıra numarası (10'ar artar).
- **Sil**: Seçili adımı kaldırır (onay ister).
- **← Rota Listesi**: Üretim Rotaları listesine döner.
- **Toplam süre / Toplam maliyet**: Tüm adımların alt toplamı (tablo altında).

## İpuçları ve sık hatalar
- İş Merkezi listesinde yalnızca aktif iş merkezleri görünür; aradığınız istasyon yoksa önce İş Merkezleri ekranından aktif edin.
- Adımlar 10'ar numaralanır (10, 20, 30...); bu, ileride araya yeni adım ekleme esnekliği sağlar.
- Süreyi dakika cinsinden girin; toplam süre tabloda dakika olarak gösterilir.
- Rota başka şirkete aitse veya bulunamazsa ekran "Rota bulunamadı" uyarısı verir.
