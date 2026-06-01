# Satış Faturası Detayı

Tek bir satış faturasının başlık bilgileri, kalemleri, e-Belge gönderim durumu ve tahsilat bilgisinin görüntülendiği ekrandır. Satış ve muhasebe ekibi kullanır.

## Ne işe yarar
Bu ekranda bir satış faturasının tüm ayrıntılarını görürsünüz: müşteri ve vergi bilgileri, fatura/vade tarihi, ödenen ve kalan tutar, kalem dökümü ve KDV hesabı. Sağ panelde e-Belge (e-Fatura/e-Arşiv) gönderim geçmişini izlersiniz. Onaylı faturalarda buradan tahsilat girişi başlatabilir veya faturayı iptal edebilirsiniz.

## Nasıl kullanılır
1. Fatura listesinden bir faturaya tıklayarak bu sayfaya gelin.
2. Üstteki rozetlerden faturanın durumunu ve e-Belge bilgisini kontrol edin.
3. **Fatura Bilgileri** kartında müşteri, tarihler, para birimi, ödenen ve kalan tutarı inceleyin.
4. **Fatura Kalemleri** tablosunda ürün bazında miktar, fiyat, KDV ve toplamları görün; altta genel toplam yer alır.
5. Henüz e-Belgeye dönüştürülmemiş faturayı **e-Fatura'ya Çevir** butonuyla işleyin.
6. Onaylı bir fatura için kalan tutar varsa, belge zinciri butonlarından **Tahsilat Al** ile ödeme girişi yapın.
7. Faturayı geçersiz kılmak için **İptal Et** butonuna tıklayın; onay sorulduktan sonra ters muhasebe kaydı yazılır.

## Alanlar ve butonlar
- **Durum rozeti**: Faturanın durumunu (Taslak/Onaylı/Tahsil/İptal) gösterir.
- **e-Belge rozetleri**: e-Belge tipini, durumunu ve varsa UUID kısaltmasını gösterir.
- **Fatura Bilgileri**: Müşteri (VKN, vergi dairesi), fatura tarihi, vade tarihi, para birimi, ödenen ve kalan tutar.
- **Fatura Kalemleri**: Ürün kodu, adı, birim, miktar, birim fiyat, ara toplam, KDV oranı/tutarı ve satır toplamı.
- **PDF**: Faturanın PDF çıktısını alır.
- **e-Fatura'ya Çevir**: Henüz e-Belge oluşturulmamış faturayı e-Fatura'ya dönüştürür.
- **Tahsilat Al**: Kalan tutar için tahsilat (ödeme) girişi ekranını açar.
- **İptal Et**: Onaylı faturayı iptal eder; ters muhasebe satırı yazılır.
- **e-Belge Gönderimleri (sağ panel)**: Gönderim tipi, durum, gönderim/kabul/red tarihleri ile geçmişi listeler.

## İpuçları ve sık hatalar
- e-Faturası gönderilmiş bir fatura artık iptal edilemez; iptal denenirse ekrana açıklayıcı hata mesajı gelir. Bu durumda iade faturası yolunu izleyin.
- **Tahsilat Al** butonu yalnızca fatura Onaylı durumdaysa ve kalan tutar varsa görünür; tutar tamamen tahsil edilince "Tam Tahsil Edildi" yazar.
- İptal işlemi yalnızca Onaylı (POSTED) faturalarda mümkündür; dönem kilitliyse de engellenebilir.
- "Kalan" tutar, genel toplamdan ödenen tutar düşülerek hesaplanır; tahsilat girdikçe bu değer azalır.
- e-Fatura'ya çevir butonu yalnızca daha önce e-Belge oluşturulmamış faturalarda görünür.
