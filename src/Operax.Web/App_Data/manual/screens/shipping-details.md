# Sevkiyat Belgesi

Tek bir sevkiyatın (irsaliye) oluşturulduğu, ürün satırlarının eklendiği ve onaylanarak stok çıkışının yapıldığı ekrandır. Depo ve lojistik ekibi kullanır.

## Ne işe yarar
Bu ekranda yeni sevkiyat açar veya mevcut bir sevkiyatı görüntüler/düzenlersiniz. Çıkış deposu, taşıyıcı firma ve araç plakası gibi bilgileri girip sevkiyata açık satış siparişlerinden ürün satırları aktarırsınız. Sevkiyatı tamamladığınızda (onayladığınızda) stoktan çıkış hareketi yazılır, satış siparişi güncellenir ve ayara göre otomatik fatura oluşabilir.

## Nasıl kullanılır
1. Yeni sevkiyat için **Çıkış Deposu** seçin, isterseniz **Taşıyıcı Firma** ve **Araç Plakası** girin.
2. **Sevkiyatı Kaydet** butonuna (Alt+S) tıklayın. Kayıttan sonra sevkiyat numarası atanır ve durum "DRAFT" (Taslak) olur.
3. **Sevkiyat Satırları** bölümünde **+ Ürün Ekle** butonuna tıklayın. Açılan pencerede bir **Açık Satış Siparişi** seçin; siparişteki tüm açık kalemler otomatik olarak sevkiyata aktarılır.
4. Satırları kontrol edin. Gerekirse bir satırı **Çıkar** butonuyla silebilirsiniz (yalnızca Taslak iken).
5. Hazır olduğunuzda **Sevkiyatı Tamamla (Onayla)** butonuna (Alt+P) tıklayın; stok çıkışı bu adımda yazılır.
6. Onaylanmış bir sevkiyatı geri almak için **Sevkiyatı İptal Et** butonunu kullanın; onay sorulduktan sonra ters stok hareketi yazılır.

## Alanlar ve butonlar
- **Çıkış Deposu**: Ürünlerin çıkacağı depo (zorunlu).
- **Taşıyıcı Firma**: Sevkiyatı taşıyan kargo/nakliye firması (örn. Aras, Yurtiçi).
- **Araç Plakası**: Sevkiyatı taşıyan aracın plakası.
- **Sevkiyat Durumu**: Belgenin güncel durumunu (DRAFT/POSTED) gösteren etiket.
- **Sevkiyatı Kaydet**: Başlık bilgilerini kaydeder; yeni sevkiyatta numara üretir.
- **+ Ürün Ekle**: Açık satış siparişi seçerek kalemleri sevkiyata aktarır.
- **Çıkar**: Bir sevkiyat satırını listeden kaldırır (Taslak iken).
- **Sevkiyatı Tamamla (Onayla)**: Sevkiyatı onaylar, stok çıkış hareketini yazar (DRAFT → POSTED).
- **Sevkiyatı İptal Et**: Onaylı sevkiyatı iptal eder ve ters stok hareketi yazar.

## İpuçları ve sık hatalar
- Satır ekleme ve düzenleme **yalnızca Taslak (DRAFT)** durumunda mümkündür. Sevkiyat onaylandıktan sonra satırlar kilitlenir.
- "+ Ürün Ekle" penceresinde sipariş seçtiğinizde o siparişin **tüm açık kalemleri** tek seferde aktarılır; tek tek ürün seçmenize gerek yoktur.
- Onaylama (Tamamla) işlemi stoğu fiilen düşürür; bu nedenle satırların ve miktarların doğruluğunu onaylamadan önce mutlaka kontrol edin.
- İptal işlemi, sevkiyata bağlı fatura varsa veya dönem kilitliyse engellenebilir; bu durumda ekrana açıklayıcı bir hata mesajı gelir.
- Sevkiyatı tamamlamadan önce miktarları el terminalinden doğrulamak isterseniz Sevkiyat Terminali ekranını kullanabilirsiniz.
