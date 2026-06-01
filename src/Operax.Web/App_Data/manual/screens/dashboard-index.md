# Anasayfa
Operax'a giriş yaptığınızda karşınıza çıkan yönetici görünümüdür. İşletmenizin satınalma, stok ve depo durumunu tek bir ekranda özetler.

## Ne işe yarar
Anasayfa, günlük operasyonun nabzını tutmanız için tasarlanmıştır. Açık satınalma tutarı, bu ay onaylanan evrak sayısı, depo doluluk oranı ve düşük stoklu ürün gibi temel göstergeleri (KPI) anlık olarak gösterir. Ayrıca son 6 ayın satınalma performansını, son hareketleri ve son satınalma siparişlerini listeler. Tüm veriler canlı veritabanından gelir; uydurma veya örnek rakam yoktur.

## Nasıl kullanılır
1. Üst kısımdaki dört KPI kartını inceleyerek işletmenin genel durumunu kavrayın (Açık Satınalma Tutarı, Bu Ay Onaylanan, Depo Doluluk, Düşük Stoklu SKU).
2. "Aylık Satınalma Performansı" grafiğinde son 6 ayın onaylı, taslak ve iptal tutarlarını renkli çubuklardan okuyun.
3. Sağdaki "Son Aktivite" panelinde kimin hangi stok hareketini yaptığını ve ne kadar zaman önce olduğunu görün.
4. "Son Satınalma Siparişleri" tablosunda bir satıra tıklayarak ilgili siparişin detayına gidin.
5. Yeni bir satınalma siparişi açmak için sağ üstteki "Yeni Sipariş" butonuna tıklayın (kısayol: Alt+N).
6. Ekran görüntüsünü/raporu yazdırmak için "Rapor İndir" butonunu kullanın.

## Alanlar ve butonlar
- **Açık Satınalma Tutarı**: İptal edilmemiş tüm satınalma siparişi satırlarının toplam tutarı.
- **Bu Ay Onaylanan**: Onaylı (POSTED) durumdaki sipariş sayısı; altında bekleyen taslak adedi yazar.
- **Depo Doluluk**: Stoklu hücrelerin toplam aktif hücreye oranı (yüzde).
- **Düşük Stoklu SKU**: Stok bakiyesi 10 birimin altına düşmüş ürün sayısı.
- **Aylık Satınalma Performansı**: Son 6 ayın çubuk grafiği. Mor = onaylanmış, sarı = taslak, kırmızı = iptal.
- **Son Aktivite**: En son 6 stok hareketi; renk hareket türüne göre değişir (giriş yeşil, çıkış kırmızı, transfer mavi).
- **Son Satınalma Siparişleri**: En yeni 5 sipariş; satıra tıklayınca detayına gider. "Hepsini gör" tüm sipariş listesine götürür.
- **Yeni Sipariş**: Yeni satınalma siparişi oluşturma sayfasını açar.
- **Rapor İndir**: Tarayıcının yazdırma penceresini açar.

## İpuçları ve sık hatalar
- KPI'lar sadece okunur özetlerdir; üzerlerinden işlem yapamazsınız. İşlem için ilgili modül sayfalarına geçin.
- "Düşük Stoklu Ürünler" kutusu şu an boş durum gösterir; bu liste henüz devreye alınmadığı için ürün gösterilmez (eksiklik değil, planlı).
- Veriler boşsa (örneğin yeni kurulum) "Henüz aktivite yok" gibi boş durum mesajları görmeniz normaldir; veri girdikçe dolar.
- Grafikteki tutarlar bine bölünmüş olarak (K) gösterilir; gerçek tutar için ilgili modüle bakın.
