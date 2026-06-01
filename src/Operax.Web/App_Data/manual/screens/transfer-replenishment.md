# Raf Besleme (Replenishment)

Toplama raflarında kritik seviyenin altına düşen ürünleri ana stoktan besleyen sihirbaz ekrandır. Depo planlama sorumluları toplama raflarının dolu kalmasını sağlamak için kullanır.

## Ne işe yarar
Bu ekran, toplama (picking) raflarında minimum seviyenin altına inmiş ürünleri otomatik olarak tespit eder ve her biri için bir besleme önerisi sunar. Sistem, ürünün en dolu kaynak rafını bulur ve toplama rafına ne kadar stok taşınması gerektiğini hesaplar. Tek tıkla besleme emri (raf-içi transfer) oluşturursunuz; böylece toplama personeli boş rafla karşılaşmaz.

## Nasıl kullanılır
1. Ekran açıldığında besleme ihtiyacı olan ürünler kart kart listelenir; her kartta ürün, toplama rafı, mevcut/kritik seviye ve önerilen besleme miktarı görünür.
2. Bir ürünü beslemek için ilgili karttaki **Besleme Emri Oluştur** düğmesine tıklayın.
3. Sistem en uygun kaynak rafı bularak bir besleme transferi (taslak) oluşturur.
4. Liste güncellenir; beslenen ürün listeden düşer veya kalan ihtiyaç görünür.

## Alanlar ve butonlar
- **Toplama Rafı**: Beslenmesi gereken raf kodu.
- **Mevcut / Kritik**: Raftaki güncel miktar ile minimum (kritik) seviye.
- **Max Seviye**: Rafın hedeflenen maksimum doluluk miktarı.
- **Besleme İhtiyacı**: Sistemin önerdiği taşınması gereken adet.
- **Besleme Emri Oluştur**: En uygun kaynak raftan toplama rafına besleme transferi oluşturur.

## İpuçları ve sık hatalar
- Öneriler canlı analizdir; raf miktarları değiştikçe liste otomatik güncellenir.
- Oluşturulan besleme transferi **taslak** durumda açılır; fiziksel taşıma ve onay diğer transfer ekranlarından/terminalden yapılır.
- Sistem her ürün için en dolu rafı kaynak seçer; uygun kaynak raf yoksa veya stok yetersizse taşınacak miktar azaltılabilir.
- Hiç besleme ihtiyacı yoksa "Harika! Tüm raflarınız dolu." mesajı görünür; bu, tüm toplama raflarının yeterli olduğu anlamına gelir.
