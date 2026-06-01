# Stok Bakiyesi
Tüm depolarınızdaki anlık stok seviyelerini raf (hücre) ve ürün bazında tek ekranda gösterir. Depo sorumluları ve satınalma personeli güncel stok durumunu kontrol etmek için kullanır.

## Ne işe yarar
Bu ekran, aktif şirketinize ait tüm depolardaki canlı stok bakiyesini listeler. Hangi ürünün hangi rafta ne kadar bulunduğunu, toplam stok miktarını ve kritik (minimum stok altına düşmüş) ürün sayısını görürsünüz. Sıfır bakiyeli kayıtlar otomatik olarak gizlenir; sadece içinde stok bulunan konumlar gösterilir. Veriler stok hareketlerinden (mal kabul, sevkiyat, transfer, sayım) otomatik hesaplanır.

## Nasıl kullanılır
1. Sol menüden Stok Kontrol > Anlık Bakiyeler yolunu izleyerek ekranı açın.
2. Üstteki üç kart ile genel durumu gözden geçirin: Toplam Stok Miktarı, Kritik SKU Sayısı ve Aktif Depolama Hücreleri.
3. Aradığınız ürünü bulmak için "Ürün kodu veya adı ile ara..." kutusunu kullanın.
4. Belirli bir depoya odaklanmak için "Tüm Depolar" açılır listesinden depo seçin.
5. Tablodan ilgili ürünün hangi rafta (konumda) ne kadar bulunduğunu okuyun.

## Alanlar ve butonlar
- **Toplam Stok Miktarı**: Aktif şirkete ait tüm depolardaki toplam stok adedini gösterir.
- **Kritik SKU Sayısı (Min Stok)**: Tanımlı minimum stok seviyesinin altına düşmüş ürün sayısını gösterir; sıfırsa "Güvenli Seviye", varsa "Emniyet Altı" uyarısı çıkar.
- **Aktif Depolama Hücreleri**: İçinde stok bulunan farklı raf (hücre) sayısını gösterir.
- **Tüm Depolar (açılır liste)**: Listeyi belirli bir depoya göre süzmek içindir.
- **Ürün kodu veya adı ile ara...**: Tabloda ürün araması yapmanızı sağlar.
- **Raf / Konum**: Stoğun bulunduğu hücre kodu; atanmamışsa "ATANMAMIŞ" görünür.
- **SKU / Ürün**: Ürün kodu ve adı.
- **Kullanılabilir / Rezerve / Toplam Stok**: Rafta kullanılabilir ve toplam miktarı gösterir.

## İpuçları ve sık hatalar
- Stok bakiyesi bu ekrandan elle değiştirilemez. Miktarı düzeltmek için ilgili belgeyi (mal kabul, sevkiyat, transfer veya sayım) kullanmanız gerekir.
- Bir ürünü göremiyorsanız bakiyesi sıfırdır; sıfır bakiyeli kayıtlar listede gösterilmez.
- "ATANMAMIŞ" konum, stoğun belirli bir rafa adreslenmediği anlamına gelir.
- Kritik SKU sayısı yalnızca minimum stok seviyesi tanımlı ürünleri sayar; tanımı olmayan ürünler bu sayıma dahil edilmez.
