# Satınalma Siparişleri Listesi

Şirkete ait tüm satınalma siparişlerini duruma ve arama kriterine göre filtreli olarak listeleyen ana ekrandır.

## Ne işe yarar

Satınalma departmanının açtığı tüm sipariş evraklarını tek ekranda görürsünüz. Üst bilgi çubuğunda toplam evrak sayısı ve iptal dışındaki siparişlerin toplam tutarı anlık olarak gösterilir. Sekme filtreleriyle (Tümü / Taslak / Onaylı / İptal) hızlıca ilgili gruba geçebilirsiniz.

## Nasıl kullanılır

1. Soldaki menüden **Satınalma → Siparişler** yolunu izleyerek bu ekrana gelin.
2. Sekmelere tıklayarak duruma göre filtreleyin: **Taslak**, **Onaylı** veya **İptal**.
3. Arama kutusuna evrak numarası veya tedarikçi adının bir parçasını yazıp Enter'a basın; liste anında daralır.
4. Herhangi bir satıra tıklayarak siparişin detay ekranına geçin.
5. Sağ üstteki **Yeni Sipariş** butonuyla (veya `Alt+N` kısayoluyla) yeni sipariş formu açılır.

## Alanlar ve butonlar

- **Sekme rozeti (Taslak / Onaylı / İptal)**: Her sekmenin yanındaki sayı o durumdaki evrak adedini gösterir; tıklandığında liste filtrelenir.
- **Arama kutusu**: Evrak numarası veya tedarikçi adına göre arar; `?tab=...` parametresi korunur.
- **Tarih filtresi (chip)**: Son 30 gün filtresi; tıklanabilir (henüz ek seçenek panel geliştirme aşamasındadır).
- **Tedarikçi filtresi (chip)**: Tedarikçiye göre daraltma; tıklanabilir.
- **Tutar filtresi (chip)**: Tutar aralığına göre daraltma; tıklanabilir.
- **Sütunlar butonu**: Görünen sütunları özelleştirmek için (geliştirme aşamasındadır).
- **İçeri Aktar**: Toplu sipariş içe aktarma (geliştirme aşamasındadır).
- **Dışa Aktar**: Mevcut listeyi dışa aktarma (geliştirme aşamasındadır).
- **Yeni Sipariş** (`Alt+N`): Yeni satınalma siparişi detay ekranını açar.
- **Tablo satırı**: Evrak no, tedarikçi (avatar + şehir + VKN), tarih, vade, kalem sayısı, tutar ve durum rozeti gösterir. Satıra tıklamak detay ekranına gider.
- **Sayfalama (Önceki / Sonraki)**: En fazla 200 evrak tek seferde listelenir; çok sayıda evrak varken sayfalar arasında geçiş yapılır.

## İpuçları ve sık hatalar

- **Onaylı sipariş filtresinde hem POSTED hem APPROVED durumu** birlikte görünür; bu iki durum sisteminizde "onaylı" sayılmaktadır.
- Arama hem evrak numarasında hem tedarikçi adında eş zamanlı çalışır; ikisini ayırt etmek gerekmez.
- **"Aktif tutar"** hesabında iptal edilmiş siparişler dahil edilmez; sadece Taslak ve Onaylı evrakların toplam kalemleri sayılır.
- Listedeki vade tarihi tedarikçinin ödeme vadesinden hesaplanır; tedarikçi kaydında vade tanımlanmamışsa varsayılan 30 gün uygulanır.
- Eğer liste boş görünüyorsa ve filtre uygulanmışsa, arama metnini veya sekmeyi sıfırlayarak tekrar deneyin.
