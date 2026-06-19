# Sevkiyat Terminali

El terminali (mobil/dokunmatik) için tasarlanmış, sevkiyat satırlarını barkod okutarak doğrulama ekranıdır. Depo paketleme personeli kullanır.

## Ne işe yarar
Depo personeli, bekleyen bir sevkiyat belgesini seçip ürünleri tek tek barkod okutarak doğrular. Bu sayede sevkiyata yanlış ürün veya eksik/fazla miktar girmesi önlenir. Ekran büyük dokunmatik butonlarla, el terminalinde rahat kullanılacak şekilde sadeleştirilmiştir.

## Nasıl kullanılır
1. Ekran açıldığında **Sevkiyat Belgesi Seç** listesinden çalışacağınız sevkiyatı seçin (her satırda belge numarası ve satır sayısı görünür).
2. Açılan ekranda **Barkod** kutusuna ürünün barkodunu okutun (alan otomatik odaklanır, okuyucu doğrudan yazar).
3. **Miktar** alanına okutulan ürünün adedini girin (varsayılan 1'dir).
4. **Onayla** butonuna basın. Ürün sevkiyatta varsa yeşil onay mesajı, yoksa kırmızı hata mesajı görürsünüz.
5. Sevkiyattaki tüm ürünleri okutana kadar tekrarlayın. Başka belgeye geçmek için **← Geri** butonuna basın.

## Alanlar ve butonlar
- **Sevkiyat Belgesi Seç**: Onay bekleyen (Taslak) sevkiyatların listesi; tıklayınca o belgeyle çalışmaya başlarsınız.
- **Barkod**: Ürün barkodunun okutulduğu alan. Barkod yoksa ürün kodu da kabul edilir.
- **Miktar**: Okutulan ürünün doğrulanacak miktarı.
- **Onayla**: Okutulan barkodu sevkiyat satırlarında arar ve doğrular.
- **← Geri**: Belge seçim ekranına döner.
- **Sevkiyat Satırları**: Seçili sevkiyatta sevk edilecek ürünleri, miktar ve birimleriyle listeler.

## İpuçları ve sık hatalar
- Yalnızca **Taslak (bekleyen)** durumdaki sevkiyatlar listede görünür; onaylanmış sevkiyatlar terminale gelmez.
- "Barkod bulunamadı" hatası, okutulan barkodun hiçbir ürünle eşleşmediğini gösterir; ürünün barkod tanımını kontrol edin.
- "Bu ürün sevkiyat belgesinde yok" hatası, ürünün geçerli olduğunu ama bu sevkiyata ait olmadığını gösterir; doğru belgede olduğunuzdan emin olun.
- Terminalde yapılan okutma doğrulama amaçlıdır; sevkiyatın stoğa yansıması için belge ayrıca Sevkiyat Belgesi ekranından "Sevkiyatı Tamamla (Onayla)" ile onaylanmalıdır.
