# Lot / Parti Yönetimi
Parti (lot) bazlı stok takibini, son kullanma tarihlerini (SKT) ve FEFO yönetimini sağlayan listeleme ekranıdır. Depo, kalite ve satınalma personeli SKT'si dolan veya bloke edilmiş partileri izlemek için kullanır.

## Ne işe yarar
Bu ekran, şirketinize ait tüm lotları (partileri) listeler. Her lotun ürünü, parti numarası, son kullanma tarihi, mevcut miktarı ve durumu (Müsait / Karantina / Bloke) görünür. SKT'si geçmiş ve 30 gün içinde dolacak partiler renkli olarak öne çıkarılır; böylece FEFO (önce sonu gelen çıkar) yönetimi kolaylaşır. Lotlar bu ekrandan elle oluşturulmaz; mal kabul veya üretim sırasında otomatik üretilir.

## Nasıl kullanılır
1. Sol menüden Lot / Parti Yönetimi ekranını açın.
2. Üstteki kartlardan genel durumu inceleyin: Toplam Lot, SKT Geçmiş, 30 Günde Bitecek ve Bloke.
3. Tabloda kırmızı satırlar SKT'si geçmiş, sarı satırlar yakında dolacak partileri gösterir; bunları öncelikle işleyin.
4. Bir partinin konum dağılımını ve hareket geçmişini görmek için ilgili satırdaki "Detay" butonuna tıklayın.

## Alanlar ve butonlar
- **Toplam Lot**: Kayıtlı toplam parti sayısı.
- **SKT Geçmiş**: Son kullanma tarihi bugünden önce olan parti sayısı.
- **30 Günde Bitecek**: Önümüzdeki 30 gün içinde SKT'si dolacak parti sayısı.
- **Bloke**: Karantina veya bloke durumundaki parti sayısı.
- **Ürün**: Partinin ait olduğu ürün adı ve kodu.
- **Lot / Parti No**: Partinin tekil numarası.
- **Son Kullanma Tarihi**: SKT; geçmişse "SKT Geçti", yakında dolacaksa kalan gün rozeti gösterilir.
- **Mevcut Miktar**: Partiye ait güncel toplam stok.
- **Durum**: Müsait (kullanıma açık), Karantina (kontrol bekliyor) veya Bloke (kullanım dışı).
- **Detay**: Partinin konum dağılımı ve hareket geçmişi sayfasını açar.

## İpuçları ve sık hatalar
- SKT'si geçmiş (kırmızı) partiler stoktan otomatik düşülmez; bunları kalite kararına göre sevk etmeyin veya bloke edin.
- 30 Günde Bitecek (sarı) partileri FEFO kuralıyla önce sevk ederek fire önleyin.
- Karantina veya Bloke durumundaki partiler sevkiyatta kullanılmamalıdır.
- Mevcut miktarı sıfır olan partiler geçmiş olarak görünür; tarihsel takip için kayıtta kalırlar.
