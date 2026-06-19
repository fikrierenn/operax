# Gider Raporu
Onaylı gider faturalarınızı gider merkezi ve gider tipi kırılımıyla özetleyen rapor ekranıdır. Muhasebe, finans ve yönetim kullanır.

## Ne işe yarar
Bu ekran, seçtiğiniz tarih aralığındaki onaylı gider faturalarını maliyet (gider) merkezi × gider tipi bazında gruplayarak gösterir. Hangi merkezin ne kadar harcadığını, bu harcamanın hangi gider tiplerine dağıldığını ve net/KDV/toplam tutarları görürsünüz. Bütçe takibi ve dönemsel gider analizi için kullanılır.

## Nasıl kullanılır
1. Ekran açıldığında varsayılan olarak içinde bulunulan yılın başından bugüne kadarki onaylı giderler listelenir.
2. Farklı bir dönem için üstteki **Başlangıç** ve **Bitiş** tarihlerini seçin.
3. **Raporla** düğmesine tıklayın; tablo seçtiğiniz aralığa göre yeniden hesaplanır.
4. Üstteki Net Toplam, KDV ve Genel Toplam kartlarından dönemin toplam giderini okuyun.
5. Tabloda her gider merkezi kalın başlık satırıyla gelir; altındaki satırlar o merkeze ait gider tiplerinin kırılımıdır.

## Alanlar ve butonlar
- **Başlangıç / Bitiş**: Raporun tarih aralığını belirleyen alanlar.
- **Raporla**: Seçilen tarih aralığıyla raporu yeniden çalıştırır.
- **Net Toplam**: KDV hariç toplam gider.
- **KDV**: Toplam KDV tutarı.
- **Genel Toplam**: KDV dahil toplam gider.
- **Gider Merkezi**: Harcamanın yapıldığı maliyet merkezi (gruplama başlığı).
- **Gider Tipi**: Merkez altındaki harcama kalemi türü.
- **Kalem**: O satırdaki fatura kalemi adedi.
- **Net / KDV / Toplam**: Satır ve grup bazında tutar kırılımı.

## İpuçları ve sık hatalar
- Rapora yalnızca **onaylı** gider faturaları girer; Taslak durumdaki faturalar toplamı etkilemez.
- Seçtiğiniz aralıkta veri yoksa "Seçilen aralıkta onaylı gider faturası bulunamadı." uyarısı gelir; tarih aralığını genişletin.
- Maliyet merkezi atanmamış kalemler "— Merkez Atanmamış —" başlığı altında toplanır; düzenli raporlama için faturalarda her kaleme merkez seçmeye özen gösterin.
- Bu rapor salt görüntülemedir; buradan fatura düzenleyemezsiniz, düzenleme için Gider Faturaları ekranını kullanın.
