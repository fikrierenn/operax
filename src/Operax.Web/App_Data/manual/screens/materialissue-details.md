# Sarf Fişi Detayı
Tek bir sarf fişini oluşturduğunuz, kalemlerini girdiğiniz, onayladığınız ve gerekirse iptal ettiğiniz ekrandır. Depo ve stok sorumluları kullanır.

## Ne işe yarar
Bu ekranda iç tüketim için depodan düşülecek malzemenin fişini hazırlarsınız. Depoyu, tarihi ve isteğe bağlı masraf merkezini seçer, tüketilecek ürünleri kalem olarak eklersiniz. Fişi onayladığınızda sistem seçilen depodan stoğu fiilen düşer (stok hareketi yazar). İptal ederseniz stok geri alınır. Sarf iç tüketim olduğundan cari deftere kayıt yazılmaz, yalnızca stok etkilenir.

## Nasıl kullanılır
1. **Yeni fiş için:** Başlık formundan **Depo** seçin, **Tarih** ve isteğe bağlı **Masraf Merkezi** ile **Açıklama** girin. **Oluştur** düğmesine tıklayın.
2. Fiş kaydedildikten sonra alt kısımda **Sarf Kalemleri** bölümü açılır.
3. Kalem eklemek için **+ Kalem Ekle** düğmesine tıklayın; açılan formdan sarf edilecek **Ürün**'ü ve **Miktar**'ı girip **Ekle** düğmesine basın.
4. Yanlış eklenen satırı kaldırmak için satır sonundaki **Sil** düğmesine tıklayın (yalnızca Taslakta).
5. Fiş hazır olduğunda sağ üstteki **Onayla (Stoğa İşle)** düğmesine tıklayın ve onay penceresini geçin; stok düşülür.
6. Onaylı bir fişi geri almak için **İptal Et** düğmesini kullanın; stok geri alınır.

## Alanlar ve butonlar
- **Depo**: Malzemenin çıkılacağı depo (zorunlu).
- **Tarih**: Sarf tarihi (yeni fişte bugün gelir).
- **Masraf Merkezi**: Sarfın yükleneceği merkez (isteğe bağlı).
- **Açıklama**: Sarf gerekçesi.
- **Oluştur / Kaydet**: Başlık bilgilerini kaydeder (yalnızca Taslakta).
- **+ Kalem Ekle**: Kalem giriş formunu açar/kapatır.
- **Ürün**: Sarf edilebilir ürün (yalnızca stok ve sarf malzemesi listelenir; hizmet kalemleri çıkmaz).
- **Miktar**: Sarf edilecek miktar; ürünün temel biriminde işlenir.
- **Ekle**: Kalemi fişe ekler.
- **Sil**: Bir kalemi kaldırır.
- **Onayla (Stoğa İşle)**: Fişi onaylar ve depodan stoğu düşer.
- **İptal Et**: Onaylı fişi iptal eder ve stoğu geri alır.

## İpuçları ve sık hatalar
- Durum akışı: **Taslak → Onayla (Stoğa İşle) → (gerekirse) İptal Et**. Depo, tarih, masraf merkezi ve kalemler yalnızca **Taslak** durumda değiştirilebilir.
- Onaylamadan önce depoyu doğru seçtiğinizden emin olun; onaylanan fiş o depodan stoğu düşer ve başlık alanları kilitlenir.
- Ürün listesinde yalnızca stok ve sarf (consumable) türü ürünler görünür; hizmet ürünleri stoksuz olduğundan listelenmez.
- Stok yetersizse onay sırasında sistem Türkçe bir hata mesajı gösterir; miktarı veya depoyu kontrol edin.
- Yanlışlıkla onaylanan bir fişi **İptal Et** ile geri alabilirsiniz; iptal stoğu otomatik geri yükler.
