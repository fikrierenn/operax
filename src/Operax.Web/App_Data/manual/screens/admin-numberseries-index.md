# Belge Serileri
Belgelerin otomatik numaralanma kurallarını yönetir: önek, ayraç, sonraki numara ve dolgu uzunluğu. Yalnızca Yönetici (Administrator) rolü kullanabilir.

## Ne işe yarar
Sistemdeki fatura, sipariş, cari kart, çek/senet gibi belgeler kaydedilirken otomatik bir numara alır. Bu numaranın nasıl üretileceğini (örneğin "FT-000001") burada belirlersiniz. Her belge tipi için ayrı bir seri tanımı bulunur ve her birini bağımsız olarak ayarlayabilirsiniz.

## Nasıl kullanılır
1. Tablo, tanımlı tüm belge tiplerini ve her birinin numaralama ayarlarını gösterir.
2. Bir satırda **Önek**, **Ayraç**, **Sonraki No** ve **Dolgu** alanlarını ihtiyacınıza göre değiştirin.
3. **Önizleme** sütununda, ayarlarınızla üretilecek numaranın nasıl görüneceğini anlık olarak görürsünüz.
4. Seriyi geçici olarak kapatmak isterseniz **Aktif** kutusunun işaretini kaldırın.
5. O satıra ait değişiklikleri uygulamak için satırdaki **Kaydet** butonuna tıklayın. Her satır bağımsız kaydedilir.

## Alanlar ve butonlar
- **Belge Tipi**: Numaralama kuralının uygulandığı belge türü (örnek: Satış Faturası, Alış Siparişi, Çek).
- **Önek**: Numaranın başına eklenen metin (örnek: FT). Boş bırakılamaz.
- **Ayraç**: Önek ile numara arasına konan karakter (örnek: "-").
- **Sonraki No**: Bir sonraki belgeye verilecek numara. En az 1 olmalıdır.
- **Dolgu**: Numaranın kaç haneye tamamlanacağı (örnek: 6 → 000001). 1 ile 9 arasında olur.
- **Önizleme**: Mevcut ayarlarla üretilecek örnek numara.
- **Aktif**: Serinin kullanımda olup olmadığını belirler.
- **Kaydet**: O satırın ayarlarını uygular.

## İpuçları ve sık hatalar
- **Önek zorunludur**; boş bırakıp kaydederseniz kırmızı bir hata uyarısı alırsınız.
- **Sonraki No** alanını geriye doğru değiştirmeyin; daha önce kullanılmış bir numaraya dönmek belge numaralarının çakışmasına yol açabilir.
- Dolgu değeri, numaranın baştaki sıfırlarla kaç haneye tamamlanacağını belirler; örneğin dolgu 6 iken numara 42 ise "000042" üretilir.
- Her satırın kendi Kaydet butonu vardır; bir satırda yaptığınız değişiklik diğer satırları etkilemez ve ayrı kaydedilmelidir.
- Tanımlı seri yoksa liste boş görünür; seriler kurulum (migrate) sırasında otomatik oluşturulur.
