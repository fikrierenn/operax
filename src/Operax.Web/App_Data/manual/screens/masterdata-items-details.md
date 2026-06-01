# Ürün Kartı
Tek bir ürünün tüm tanım bilgilerini girdiğiniz ve düzenlediğiniz ekrandır. Yeni ürün açarken ve mevcut ürünü güncellerken kullanılır.

## Ne işe yarar
Ürün kartı; ürünün kodu, adı, kategorisi, KDV oranı, kalem türü, lojistik boyutları, emniyet stoğu limitleri ve takip ayarları gibi tüm özelliklerini tutar. Ayrı sekmelerde birim dönüşümleri (örneğin 1 koli = 12 adet) ve alternatif barkodlar tanımlanır. Mevcut bir üründe stok miktarı ve hareket sayısı da özet kutucuklarda görünür.

## Nasıl kullanılır
1. "Genel Bilgiler" sekmesinde zorunlu alanları doldurun: Ürün Kodu, Ürün Adı, Baz Birim ve KDV Oranı.
2. İsteğe bağlı olarak kategori, kalem türü, açıklama, hacim, ağırlık, sıcaklık rejimi ve minimum/maksimum stok girin.
3. Sağdaki "Takip Ayarları" kartından ürünü Aktif yapın; gerekirse Lot Takibi ve/veya Seri No Takibi'ni açın.
4. "Kaydet" butonuna basarak ürünü oluşturun veya güncelleyin.
5. Ürün kaydedildikten sonra "Birim Dönüşümleri" sekmesine geçerek alternatif birim ve katsayı ekleyin ("Dönüşüm Ekle").
6. "Barkodlar" sekmesinde "Barkod Ekle" ile barkod numarası ve temsil ettiği birimi tanımlayın.
7. Mevcut bir üründe "Stokta Var" veya "Hareketler" kutucuğuna tıklayarak ilgili stok sayfalarına geçebilirsiniz.

## Alanlar ve butonlar
- **Ürün Kodu**: Ürünün benzersiz kodu (örn. SKU-1001). Zorunlu.
- **Baz Birim**: Stoğun tutulduğu temel birim (adet, kg vb.). Zorunlu.
- **Ürün Adı**: Tam ürün adı. Zorunlu.
- **Kategori / KDV Oranı (%)**: Ürünün sınıfı ve vergi oranı.
- **Kalem Türü**: Stok, Sarf Malzeme, Hizmet veya Sabit Kıymet.
- **Hacim / Ağırlık / Sıcaklık Rejimi**: Lojistik planlama bilgileri.
- **Minimum Stok (Emniyet) / Maksimum Stok**: Stok limitleri; minimum altına düşünce ürün kritik sayılır.
- **Aktif**: Açıkken ürün sistemde görünür ve hareket görebilir.
- **Lot Takibi / Seri No Takibi**: Açıkken her harekette parti veya seri numarası zorunlu olur.
- **Kaydet**: Tüm değişiklikleri yazar.
- **Pasif Yap**: Mevcut üründe görünen, ürünü kullanımdan kaldırma butonu.
- **Dönüşüm Ekle / Ekle**: Alternatif birim ve baz birim karşılığı katsayı tanımlar.
- **Barkod Ekle / Kaydet**: Yeni barkod ve eşlendiği birimi tanımlar.
- **Sil**: Birim dönüşümü veya barkod satırını siler (onay sorar).

## İpuçları ve sık hatalar
- Birim Dönüşümleri ve Barkodlar sekmeleri yalnızca ürün kaydedildikten sonra kullanılabilir; yeni üründe önce "Genel Bilgiler" sekmesinden kaydedin.
- Birim dönüşümünde katsayı, baz birim karşılığıdır: 1 koli = 12 adet ise koli birimi için katsayı 12 girilir.
- Lot veya Seri No takibini açtıktan sonra o ürünle yapılan her mal kabul/sevkiyatta numara girmek zorunlu olur; gereksiz yere açmayın.
- Hacim, ağırlık, sıcaklık ve stok limitleri açıklama alanı içinde JSON olarak saklanır; bu alanları el ile bozmayın, sadece form üzerinden girin.
