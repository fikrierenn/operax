# Satınalma Siparişi Detayı

Tek bir satınalma siparişinin oluşturulduğu, düzenlendiği, kalem eklendiği ve onaylandığı ekrandır.

## Ne işe yarar

Satınalma ekibi bu ekranda tedarikçi seçer, sipariş kalemlerini girer ve siparişi onaylayarak mal kabul sürecini başlatır. Onaylanan sipariş salt-okunur hale gelir; düzeltme veya iptal gibi işlemler yine bu ekrandan yapılır. Sağ panelde belge aktivite akışı (kim ne yaptı, ne zaman) görüntülenir. Onaylı siparişe bağlı mal kabul belgesi de bu ekrandaki **Belge Zinciri** smart butonundan oluşturulabilir.

## Nasıl kullanılır

1. Listedeki bir siparişe tıklayarak veya **Yeni Sipariş** butonuyla bu ekrana gelin.
2. **Evrak Bilgileri** kartında tedarikçiyi ve depoyu seçin, gerekiyorsa not ekleyin; **Kaydet** butonuna basın.
3. Kaydedilen taslak belge yeniden yüklenir; artık **Sipariş Kalemleri** kartı aktifleşir.
4. **Kalem Ekle** butonuna basın: açılan satırda ürün seçin (birim fiyat otomatik gelir, değiştirebilirsiniz), miktar girin ve **Ekle**'ye basın.
5. İstediğiniz kadar kalem ekledikten sonra sağ üst köşedeki **Onayla** (`Alt+S`) butonuyla siparişi kesinleştirin.
6. Onaylı siparişin mal kabulünü başlatmak için sayfadaki **Belge Zinciri → Mal Kabul Oluştur** butonunu kullanın.
7. Gerektiğinde onaylı siparişi **İptal Et** butonuyla iptal edebilirsiniz (bağlı mal kabul varsa önce o iptal edilmelidir).

## Alanlar ve butonlar

- **Tedarikçi**: DRAFT'ta dropdown ile seçilir; POSTED'da salt metin olarak gösterilir. Listede yalnızca "Tedarikçi" veya "Her İkisi" tipindeki cari kartlar görünür.
- **Şehir**: Tedarikçinin kayıtlı şehri; otomatik doldurulur, düzenlenemez.
- **Para Birimi**: Sistem şu an yalnızca TRY (₺) desteklemektedir.
- **Vade Tarihi**: Sipariş tarihi + tedarikçi ödeme vadesi hesaplanarak gösterilir; tedarikçi kaydında vade yoksa 30 gün varsayılandır.
- **Depo**: DRAFT'ta dropdown ile seçilir; POSTED'da salt metin olarak gösterilir.
- **Notlar**: Serbest metin notu; DRAFT'ta düzenlenebilir.
- **Kaydet**: Başlık bilgilerini kaydeder. Yeni belge ise evrak numarasını otomatik atar (seri ayarlarından).
- **Kalem Ekle**: Yeni satır girişini açar. Ürün seçilince önerilen alış fiyatı (son hareketli ortalama maliyet) otomatik gelir; kullanıcı bu fiyatı değiştirebilir. Miktar zorunludur.
- **SKU / Ürün / Birim / Miktar / Birim Fiyat / KDV / Toplam**: Kalem tablosundaki sütunlar. KDV oranı her kalem için %20 uygulanmaktadır.
- **Ara Toplam / KDV / Genel Toplam**: Kalem tablosunun altındaki özet; tüm tutarlar TRY cinsindendir.
- **Onayla** (`Alt+S`): Siparişi POSTED durumuna geçirir, otomatik ödeme planı oluşturur. Yalnızca DRAFT belgelerde görünür; yeni (henüz kaydedilmemiş) belgede görünmez.
- **İptal Et**: Siparişi CANCELLED yapar. Yalnızca POSTED ve bağlı mal kabul belgesi olmayan siparişlerde görünür.
- **PDF**: Belgeyi dışa aktarır.
- **Geri**: Satınalma Siparişleri listesine döner.
- **Belge Zinciri (Mal Kabul)**: POSTED siparişte görünür; kaç mal kabul belgesi bağlı olduğunu gösterir, varsa listesine bağlantı verir; yetkili kullanıcı yeni mal kabul belgesi oluşturabilir.
- **Aktivite akışı**: Sağ panelde oluşturma, onaylama, satır ekleme, iptal gibi işlemlerin kaydını gösterir.
- **Fiyat Uyarı Bandı**: Kalem eklenirken fiyat tedarikçi liste fiyatından saptıysa sarı uyarı bandı gösterilir; **Fiyat Farkları** sayfasına bağlantı içerir.

## İpuçları ve sık hatalar

- **Ürün seçince otomatik gelen fiyat**, ItemCost tablosundaki son hareketli ortalama maliyettir; tedarikçi fiyat listesinden farklı olabilir. Siparişe girilen fiyatın tedarikçi fiyatından önemli ölçüde sapması halinde sistem bir fiyat farkı kaydı açar ve satınalmacıya uyarı gösterir.
- Kalem ekleyebilmek için belgenin önce **Kaydet** ile taslak olarak kaydedilmiş olması gerekir; "Yeni Sipariş" formunda kalem satırı görünmez.
- **POSTED sipariş düzenlenemez.** Bağlı mal kabul belgesi olmayan POSTED siparişi iptal edebilirsiniz; bağlı mal kabul varsa önce o belge iptal edilmelidir.
- Onayla butonuna tıklayınca sistem otomatik olarak tedarikçi ödeme planını (vadeye göre) oluşturur; bu plan Finans modülünde görünür.
- Aktivite akışında kullanıcı adı yerine "Sistem" görünüyorsa işlem otomasyon veya seed tarafından gerçekleştirilmiştir.
