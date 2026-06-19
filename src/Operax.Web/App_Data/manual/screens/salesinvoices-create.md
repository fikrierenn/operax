# Sevkiyattan Fatura

Aynı müşteriye ait faturalanmamış sevkiyatları seçip tek faturada birleştirme ekranıdır. Satış ve muhasebe ekibi kullanır.

## Ne işe yarar
Bir müşteriye birden fazla sevkiyat yaptıysanız, bunları tek tek faturalamak yerine seçip tek bir faturada toplayabilirsiniz (N irsaliye → 1 fatura). Ekran, faturalanmamış sevkiyatı olan müşterileri listeler; bir müşteri seçtiğinizde o müşterinin faturalanmamış sevkiyatları ve kalan miktarları sağda görünür.

## Nasıl kullanılır
1. Sol taraftaki **Müşteriler** listesinden faturalandıracağınız cariyi seçin (her müşterinin sevkiyat ve kalem sayısı görünür).
2. Sağ tarafta o müşterinin faturalanmamış sevkiyatları sevkiyat bazında listelenir.
3. Faturaya dahil etmek istediğiniz sevkiyatların başındaki **onay kutusunu** işaretli bırakın; istemediklerinizin işaretini kaldırın.
4. **Seçilenleri Birleştir & Fatura Oluştur** butonuna tıklayın.
5. Fatura oluşturulunca otomatik olarak yeni faturanın detay sayfasına yönlendirilirsiniz.

## Alanlar ve butonlar
- **Müşteriler listesi (sol)**: Faturalanmamış sevkiyatı olan cariler; sevkiyat sayısı, kalem sayısı ve en eski sevkiyatın gün sayısı görünür.
- **Gün rozeti**: En eski sevkiyatın kaç günlük olduğunu gösterir; 7 günü aşarsa kırmızı renkle uyarır (VUK 7 gün kuralı).
- **Sevkiyat onay kutusu**: Faturaya hangi sevkiyatların dahil edileceğini seçer (varsayılan işaretli).
- **Sevkiyat satır tablosu**: Her sevkiyat için ürün, sevk miktarı, faturalanan ve kalan miktarı gösterir.
- **Seçilenleri Birleştir & Fatura Oluştur**: Seçili sevkiyatların kalan miktarlarından tek fatura üretir.

## İpuçları ve sık hatalar
- Birleştirmek için **en az bir sevkiyat** seçili olmalıdır; hiçbiri seçili değilse uyarı alırsınız.
- Yalnızca aynı müşteriye ait sevkiyatlar birleştirilebilir; farklı müşterilerin sevkiyatları tek faturada toplanamaz.
- Yedi günü aşan sevkiyatlar (kırmızı rozet) VUK açısından kritiktir; faturalamayı geciktirmeyin.
- "Kalan" miktarı 0 olan kalemler zaten faturalanmıştır; ekran yalnızca kalan miktarı olan satırları getirir.
- Birleştirme işlemi tek bir işlemde (atomik) yapılır; hata olursa fatura oluşmaz ve ekrana açıklayıcı mesaj gelir.
