# Transfer Belgesi

Tek bir stok transferinin oluşturulduğu, kaynak/hedef deponun seçildiği ve taşınacak satırların eklendiği ekrandır. Depo sorumluları depolar veya raflar arası stok hareketi planlamak için kullanır.

## Ne işe yarar
Bu ekranda yeni bir transfer belgesi açar, kaynak ve hedef depoyu belirler ve hangi ürünün hangi raftan hangi rafa, kaç adet taşınacağını satır satır eklersiniz. Transfer tipi (Raf/Hücre Arası, Depolar Arası, Şubeler Arası), seçtiğiniz depolara göre otomatik belirlenir. Belge kaydedildikten sonra el terminalinden barkodla doğrulanıp onaylanır.

## Nasıl kullanılır
1. **Kaynak Depo** ve **Hedef Depo** listelerinden depoları seçin; üstteki **Transfer Tipi** rozeti otomatik güncellenir.
2. **Bilgileri Kaydet** düğmesine tıklayarak belgeyi oluşturun (durum **Taslak** olur).
3. **+ Hareket Ekle** düğmesine tıklayın; açılan pencerede ürünü, kaynak rafı, hedef rafı ve miktarı girip **Hareketi Ekle** ile satırı kaydedin.
4. Eklediğiniz tüm satırları tabloda (ürün, kaynak raf, hedef raf, miktar) görürsünüz.
5. Satırların fiziksel doğrulaması ve onayı Transfer Terminali'nden barkod okutularak yapılır.
6. Onaylanmış bir transferi geri almak için **Transferi İptal Et** düğmesini kullanın; sistem ters stok hareketi yazar.

## Alanlar ve butonlar
- **Kaynak Depo**: Stoğun çıkacağı depo.
- **Hedef Depo**: Stoğun gideceği depo.
- **Transfer Tipi**: Seçilen depolara göre otomatik türeyen tür rozeti (değiştirilemez, kendiliğinden belirlenir).
- **Bilgileri Kaydet**: Belge başlığını oluşturur/günceller.
- **+ Hareket Ekle**: Taşınacak tek bir ürün satırı eklemek için pencere açar.
- **Ürün / Kaynak Raf / Hedef Raf / Miktar**: Satır penceresindeki giriş alanları.
- **Transferi İptal Et**: Onaylanmış transferi geri alır, ters stok hareketi yazar.
- **Listeye Dön / Transfer Listesi**: Listeye geri döner.

## İpuçları ve sık hatalar
- Kaynak ve hedef raf listeleri seçtiğiniz depolara göre filtrelenir; satır eklemeden önce **Kaynak/Hedef Depo**'yu seçtiğinizden emin olun.
- Durum akışı: **Taslak** (oluşturuldu, düzenlenebilir) → onaylandıktan sonra stok hareketi yazılır.
- Taslak bir belge stok bakiyesini değiştirmez; gerçek hareket onaylandığında oluşur.
- İptal işlemi dönem kilidi gibi bir nedenle engellenebilir; bu durumda ekranda Türkçe hata mesajı görürsünüz.
- Aynı depoyu hem kaynak hem hedef seçerseniz transfer **Raf / Hücre Arası** olur; bu, depo içi raf değişimleri içindir.
