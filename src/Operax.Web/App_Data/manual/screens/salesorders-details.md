# Satış Siparişi

Tek bir satış siparişinin oluşturulduğu, kalemlerinin girildiği ve onaylandığı ekrandır. Satış ekibi tarafından kullanılır.

## Ne işe yarar
Bu ekranda yeni bir satış siparişi açar veya mevcut bir siparişi görüntüler/düzenlersiniz. Müşteri, depo, teslim tarihi gibi başlık bilgilerini girip siparişe ürün kalemleri eklersiniz. Sipariş onaylandıktan (Onaylı) sonra üzerinden sevkiyat belgesi türetebilirsiniz. Sağ taraftaki aktivite akışı, sipariş üzerinde kimin ne zaman işlem yaptığını gösterir.

## Nasıl kullanılır
1. Yeni sipariş için listeden **Yeni Sipariş** ile bu sayfaya gelin. Üstte "Yeni Satış Siparişi" başlığı görünür.
2. **Evrak Bilgileri** kartında **Müşteri** ve **Depo** seçin, gerekiyorsa **Teslim Tarihi** ve **Notlar** girin.
3. **Kaydet** butonuna tıklayın. Kayıttan sonra evrak numarası otomatik atanır ve sipariş "Taslak" durumuna düşer.
4. **Sipariş Kalemleri** kartında **Kalem Ekle** butonuna tıklayın; açılan satırda ürünü seçip miktar ve birim fiyat girin, ardından **Ekle** deyin.
5. Tüm kalemleri ekledikten sonra sağ üstteki **Onayla** butonuna (Alt+S) tıklayarak siparişi kesinleştirin.
6. Onaylı bir siparişi geçersiz kılmak için **İptal Et** butonunu kullanın.

## Alanlar ve butonlar
- **Müşteri**: Siparişin ait olduğu cari (zorunlu). Yalnızca müşteri tipindeki cariler listelenir.
- **Depo**: Sevkiyatın yapılacağı çıkış deposu (zorunlu).
- **Teslim Tarihi**: Müşteriye teslim edilmesi planlanan tarih.
- **Notlar**: Siparişe ait serbest açıklama.
- **Kaydet**: Başlık bilgilerini kaydeder; yeni siparişte evrak numarasını üretir.
- **Kalem Ekle / Ekle**: Siparişe ürün satırı ekler. Ürünün temel ölçü birimi otomatik atanır.
- **Onayla**: Taslak siparişi Onaylı duruma geçirir (DRAFT → APPROVED).
- **İptal Et**: Onaylı siparişi iptal eder.
- **Sipariş Kalemleri tablosu**: SKU, ürün adı, birim, miktar, sevk edilen, açık miktar, birim fiyat ve toplamı gösterir. Ara toplam, KDV (%20) ve genel toplam altta hesaplanır.
- **Aktivite**: Sipariş üzerindeki oluşturma, onay, satır ekleme gibi işlemlerin geçmişini gösterir.

## İpuçları ve sık hatalar
- Müşteri, depo, teslim tarihi ve kalemler **yalnızca Taslak** durumunda düzenlenebilir. Sipariş onaylandıktan sonra alanlar salt okunur olur.
- Kalem eklemeden önce siparişi en az bir kez **Kaydet** etmeniz gerekir; "Kalem Ekle" butonu yeni (kaydedilmemiş) siparişte çıkmaz.
- Sarf malzemesi (CONSUMABLE) tipindeki ürünler satışa uygun değildir; bunlar listede görünmez ve eklenmeye çalışılırsa "satışa uygun değil" uyarısı alırsınız.
- "Açık" sütunu, sipariş edilen miktardan henüz sevk edilmemiş kısmı gösterir; sevkiyat planlamasında bu değere bakın.
- Sevkiyat oluşturma seçeneği sipariş **Onaylı** olduğunda ve yetkiniz varsa belge zinciri butonlarında belirir.
