# Kredi Kartı Detayı

Tek bir kredi kartının limit durumunu, ekstrelerini ve son slip harcamalarını gösteren; ekstre ödemesi yapabileceğiniz ekrandır. Finans sorumluları kart borç ve ödeme takibini buradan yürütür.

## Ne işe yarar
Bu ekran seçtiğiniz kartın limit, kullanılan ve kullanılabilir tutarlarını KPI kutularında özetler. Sol tarafta kartın dönemsel ekstreleri (borç, asgari ödeme, durum), sağ tarafta ise son slip işlemleri (harcamalar, taksitler) listelenir. Ödenmemiş bir ekstreyi seçtiğiniz banka hesabından buradan ödeyebilirsiniz.

## Nasıl kullanılır
1. Üstteki KPI kutularından kartın toplam limit, kullanılan, kullanılabilir tutarını ve doluluk yüzdesini görün.
2. **Ekstreler** tablosunda dönem, son ödeme tarihi, borç ve asgari ödeme tutarlarını inceleyin.
3. Ödenmemiş bir ekstreyi ödemek için: o satırdaki açılır listeden ödeme yapılacak banka hesabını seçin, ödeme tutarını kontrol edin (varsayılan kalan borç gelir) ve **Öde** butonuna tıklayın.
4. **Son Slip İşlemleri** tablosundan kartla yapılan harcamaları ve taksit bilgisini görün.
5. Listeye dönmek için **Geri** butonuna tıklayın.

## Alanlar ve butonlar
- **Geri**: Kredi Kartları listesine döner.
- **Toplam Limit / Kullanılan / Kullanılabilir**: KPI kutularında kartın limit durumu ve doluluk yüzdesi.
- **Ekstre / Son Ödeme**: Ekstrenin kesildiği ve son ödeme yapılacağı ayın günleri.
- **Ekstreler** tablosu:
  - **Dönem**: Ekstre dönem aralığı.
  - **Son Ödeme**: Ekstrenin son ödeme tarihi.
  - **Borç / Asgari**: Ekstre kapanış borcu ve asgari ödeme tutarı.
  - **Durum**: ÖDENDİ, BEKLİYOR veya AÇIK rozeti.
  - **Banka hesabı seçimi + tutar + Öde**: Ödenmemiş ekstreyi seçilen hesaptan öder.
- **Son Slip İşlemleri** tablosu:
  - **Tarih / İşyeri / Kategori / Tutar**: Kart harcama satırları. Taksitli işlemlerde taksit no/toplam rozeti çıkar.
- **Bağlı hesap**: Karta tanımlı varsayılan ödeme banka hesabı (varsa).

## İpuçları ve sık hatalar
- Ödeme yapabilmek için en az bir banka hesabı tanımlı olmalıdır; banka hesabı yoksa **Öde** formu görünmez.
- Ödeme tutarı alanında varsayılan olarak kalan borç gelir; kısmi ödeme yapacaksanız tutarı düşürebilirsiniz.
- Ekstre durumu ödeme sonrası otomatik güncellenir: borç tamamen kapandığında ÖDENDİ olur. Ekstre ödemesi seçtiğiniz banka hesabından gider olarak düşer.
- Slip işlemleri ve ekstreler kart sistemine işlendiği şekilde görüntülenir; bu satırlar burada elle değiştirilmez.
