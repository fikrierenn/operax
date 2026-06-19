# Toplama Detayı

Tek bir toplama görevinin satır satır incelendiği, personel atamasının yapıldığı ve toplama onaylarının verildiği detay ekranıdır. Depo sorumlusu masaüstünden kullanır.

## Ne işe yarar
Bu ekran, seçtiğiniz toplama görevinin tüm satırlarını (toplanacak ürünler, raf adresleri, istenen ve toplanan miktarlar) gösterir. Görevi bir depo personeline atayabilir, masaüstünden satır bazında toplama onayı verebilir ve hangi satırı kimin ne zaman topladığını izleyebilirsiniz. Tüm satırlar toplandığında görev otomatik olarak tamamlanır ve sevkiyat/paketleme aşamasına hazır hale gelir.

## Nasıl kullanılır
1. Üst başlıkta görev belge numarasını ve durum etiketini görün.
2. Görev henüz toplanmaya başlamadıysa (TASLAK veya ATANDI), sağ üstteki **Assign Task / Re-assign** butonuna gelin; açılan listeden bir depo personeli seçerek görevi atayın.
3. Tablodaki her satır için raf adresini (Bin), ürün kodunu, istenen ve toplanan miktarı kontrol edin.
4. Bir satırı masaüstünden toplandı olarak işaretlemek için satırın sağındaki **Confirm Pick** butonuna tıklayın; istenen miktar kadarı toplanmış sayılır.
5. Toplanan satırlarda kimin topladığı ve saat bilgisi "Operator Log" sütununda görünür.
6. Tüm satırlar toplandığında sayfanın altında yeşil "Picking Completed!" bilgi kutusu çıkar.
7. Listeye dönmek için sağ üstteki **Back** butonunu kullanın.

## Alanlar ve butonlar
- **Durum etiketi**: Görevin durumu (TASLAK / DEVAM EDİYOR / TAMAMLANDI).
- **Assign Task / Re-assign**: Görevi bir depo personeline atar veya atamayı değiştirir; sadece TASLAK ve ATANDI durumunda görünür. Atama sonrası durum ATANDI olur.
- **Bin (Target)**: Ürünün toplanacağı hedef raf adresi.
- **SKU / Item**: Toplanacak ürünün kodu ve adı.
- **Requested**: İstenen (toplanması gereken) miktar.
- **Picked**: O ana kadar toplanan miktar; istenen miktara ulaşınca yeşile döner.
- **Operator Log**: Satırı kimin ve hangi saatte topladığı; henüz toplanmadıysa "Waiting..." yazar.
- **Confirm Pick**: Satırı tek tıkla toplandı işaretler (stok hareketi yazılır). Satır tamamlandığında yerini "✓ DONE" alır.
- **Back**: Toplama görevleri listesine döner.

## İpuçları ve sık hatalar
- Atama dropdown'ı sadece TASLAK/ATANDI durumunda görünür; görev DEVAM EDİYOR veya TAMAMLANDI ise yeniden atama yapılamaz.
- **Confirm Pick** masaüstü hızlı onay içindir; sahada barkod doğrulaması gerekiyorsa Toplama Terminali ekranını kullanın.
- Bir satırın toplanan miktarı istenen miktara eşit olunca "✓ DONE" görünür ve o satıra tekrar onay verilemez.
- Atama listesinde "Warehouse" rolündeki personel önceliklidir; bu rolde kimse yoksa sistem genel kullanıcı listesini gösterir.
- Tüm satırlar toplanmadan görev tamamlanmaz; yeşil tamamlanma kutusu çıkmadıysa eksik satır vardır.
