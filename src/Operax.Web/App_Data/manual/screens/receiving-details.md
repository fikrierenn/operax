# Mal Kabul Belgesi Detayı

Tek bir mal kabul belgesinin oluşturulduğu, ürünlerin eklendiği ve stoğa aktarıldığı (onaylandığı) ekrandır.

## Ne işe yarar

Depo ekibi bu ekranda gelen malları sisteme kaydeder: hangi tedarikçiden hangi depoya, hangi ürünlerin, kaç adet ve hangi lot numarasıyla girdiğini girilir. Bir satınalma siparişi seçilirse depo ve tedarikçi alanları otomatik dolar. Bilgiler tamamlandıktan sonra **Stoğa Aktar (Onayla)** butonuyla belgeler kesinleşir, stok hareketleri işlenir ve ürünler depoya giriş yapar. Onaylanan belgeden bu ekranda **Belge Zinciri** üzerinden alış faturası oluşturulabilir.

## Nasıl kullanılır

1. Yeni bir belge açmak için listeden **Yeni Mal Kabul** butonunu, var olan belgeyi açmak için listeden **Aç** butonunu kullanın.
2. **Depo** dropdown'undan malın gireceği depoyu seçin.
3. **Tedarikçi** dropdown'undan tedarikçiyi seçin.
4. Opsiyonel olarak **Satınalma Siparişi** alanından ilgili siparişi seçin:
   - Tedarikçi seçili ise açık siparişler otomatik olarak o tedarikçiye göre filtrelenir.
   - Sipariş no arama kutusuna yazarak listede arama yapabilirsiniz.
   - Bir sipariş seçildiğinde depo ve tedarikçi alanları siparişten otomatik dolar.
5. **Taslak Kaydet** butonuna basarak belgeyi kaydedin. Evrak numarası otomatik atanır.
6. Sayfada yeniden yükleme sonrası **+ Ürün Ekle** butonu aktif olur.
7. **+ Ürün Ekle** butonuyla açılan modalda:
   - Sipariş seçildiyse sipariş satırlarından seçim yapılır; seçilmediyse tüm aktif ürünler listelenir.
   - Ürün seçildiğinde birim otomatik olarak ürünün temel birimi gelir.
   - Miktar ve opsiyonel lot numarası girilir, **Ürünü Kabul Et** ile kaydedilir.
8. Tüm ürünler girildikten sonra **Stoğa Aktar (Onayla)** (`Alt+P`) butonuyla belge kesinleştirilir.
9. Onaylanan belge için **Belge Zinciri → Alış Faturası Oluştur** butonuyla alış faturası açılır.

## Alanlar ve butonlar

- **Depo**: Malın gireceği depo. Sipariş seçilirse siparişten otomatik dolar.
- **Tedarikçi**: Malı getiren tedarikçi. Sipariş seçilirse siparişten otomatik dolar.
- **Satınalma Siparişi (Opsiyonel)**: Arama kutusuna sipariş numarası yazarak filtreleyebilirsiniz. Tedarikçi seçiliyse yalnızca o tedarikçiye ait açık siparişler görünür. Sipariş seçiminden vazgeçmek için "Doğrudan Kabul (Siparişsiz)" seçeneğini kullanın.
- **Belge Durumu**: DRAFT (turuncu) veya POSTED (yeşil) rozeti; düzenlenemez.
- **Mal Kabul Listesi**: Sayfanın üst solunda listeler ekranına döner.
- **Taslak Kaydet** (`Alt+S`): Başlık bilgilerini kaydeder; ilk kayıtta evrak numarasını atar.
- **Stoğa Aktar (Onayla)** (`Alt+P`): Belgeyi POSTED yapar; `sp_ReceivingPost` SP'si çalışarak stok hareketleri yazılır. Yalnızca DRAFT belgede görünür.
- **Sil**: Taslak belgeyi kalıcı olarak siler (soft-delete). Onaylanmış belgede görünmez.
- **Listeye Dön**: Mal Kabul Listesine döner.
- **+ Ürün Ekle**: DRAFT belgede ürün ekleme modalını açar.
- **Kalemler tablosu — Ürün / Miktar (Orijinal) / Miktar (Temel) / Parti (Lot) No**: Girilen kalemleri gösterir. "Miktar (Temel)" sütunu, girilen birimin ürünün temel birimine `fn_GetConversionRate` fonksiyonuyla çevrilen miktarıdır.
- **Çıkar butonu**: DRAFT belgede satırı tablodan kaldırır (geliştirme aşamasındadır).
- **Belge Zinciri (Alış Faturası)**: POSTED belgede görünür; bağlı alış faturası sayısını gösterir, yetkili kullanıcı yeni fatura oluşturabilir.

## İpuçları ve sık hatalar

- **Depo ve tedarikçi seçimi zorunludur**; bu alanlar boş bırakılarak kayıt yapılamaz.
- Her depoda **KABUL** adlı özel bir hücre vardır; mal kabulü belgesinin stok hareketi bu hücreye yazılır. Ürünü daha sonra doğru rafa aktarmak için Transfer ekranını kullanın.
- **Ürün seçildiğinde birim otomatik gelir** (ürünün temel birimi); farklı bir birimle giriş yapacaksanız dropdown'dan değiştirebilirsiniz.
- **Sipariş seçildiğinde**, o siparişin deposu ve tedarikçisi otomatik doldurulur; elle değiştirmeyin, aksi halde sipariş kapatma mantığı bozulabilir.
- **Taslak belge silinebilir**; POSTED belge silinemez, yalnızca iptal (ters hareket) işlemi yapılabilir.
- **Lot numarası**, parti takibi yapılan ürünler için önemlidir; FIFO/FEFO kuralı lot takibiyle çalışır.
- Belge onaylandıktan sonra miktar veya ürün değiştirilemez; hata varsa belgeyi iptal edin ve sıfırdan açın.
- **Açık sipariş seçicisi**: Sipariş no arama alanı sipariş listesini anlık filtreler; arama hem büyük hem küçük harfi yakalar.
