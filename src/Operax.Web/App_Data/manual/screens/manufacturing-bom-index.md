# Reçete (BOM) Yönetimi
Üretilen ürünlerin malzeme reçetelerini (BOM) yöneten ana ekrandır. Üretim planlamacıları ve teknik ofis kullanır.

## Ne işe yarar
Bu ekran, parametrik ürün modellerini ve formül tabanlı malzeme reçetelerini bir arada listeler. Örneğin "Duşakabin" gibi ölçüye göre değişen ürünlerde, sabit miktar yerine En/Boy gibi parametrelere bağlı formüllerle malzeme miktarı otomatik hesaplanır. Bir model tanımlandıktan sonra üretim emirlerinde bu model seçilip parametreler girilerek BOM otomatik üretilir.

## Nasıl kullanılır
1. Ekranın üstündeki KPI kutularından toplam, aktif ve pasif model sayısını görün.
2. Listede her modelin kodunu, adını, bağlı ana ürününü, parametre ve BOM satırı sayısını inceleyin.
3. Yeni bir reçete modeli oluşturmak için sağ üstteki **+ Yeni Model** butonuna tıklayın; Reçete Detayı ekranı açılır.
4. Mevcut bir modeli açmak veya düzenlemek için ilgili satırdaki **Düzenle** butonuna tıklayın.

## Alanlar ve butonlar
- **+ Yeni Model**: Boş bir reçete (ürün modeli) oluşturma ekranını açar.
- **Model Kodu**: Modelin kısa kodu (örn. DUS-KAB-V1).
- **Model Adı**: Modelin açıklayıcı adı.
- **Ana Ürün**: Modelin temsil ettiği bitmiş ürün (tanımlıysa kodu, değilse "—" görünür).
- **Parametre Sayısı**: Modelde tanımlı parametre (En, Boy vb.) adedi.
- **BOM Satırı**: Modele bağlı reçete bileşeni (malzeme) adedi; 0 ise gri rozet gösterilir.
- **Durum**: Modelin Aktif/Pasif olduğunu gösterir. Pasif modeller kullanım dışıdır.
- **Düzenle**: Seçilen modelin detay/düzenleme ekranına gider.

## İpuçları ve sık hatalar
- Bir model üretimde kullanılabilmesi için **Aktif** olmalı ve en az bir BOM satırı içermelidir. BOM satırı 0 olan model üretimde malzeme hesaplayamaz.
- Önce modeli oluşturun, ardından parametreleri ve formülleri Detay ekranında ekleyin. Parametre tanımlanmadan formül yazamazsınız.
- Listede görmek istediğiniz model yoksa, önce **+ Yeni Model** ile oluşturmanız gerekir.
