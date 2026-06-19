# Gider Faturası Detayı
Tek bir gider faturasını oluşturduğunuz, kalemlerini girdiğiniz, onayladığınız ve gerekirse iptal ettiğiniz ekrandır. Muhasebe ve satınalma sorumluları kullanır.

## Ne işe yarar
Bu ekranda yeni bir gider faturası başlığı açar, faturanın kalemlerini (gider tipi ve maliyet merkezi bazında) eklersiniz. Faturayı onayladığınızda sistem tutarı atomik olarak cari deftere işler ve fatura artık değiştirilemez. Onaylı bir fatura için ödeme oluşturabilir, gerekirse faturayı iptal edebilirsiniz.

## Nasıl kullanılır
1. **Yeni fatura için:** Başlık formunu doldurun — Fatura No, Tedarikçi, Para Birimi, Fatura Tarihi ve isteğe bağlı Vade Tarihi. **Kaydet** düğmesine tıklayın. Fatura Taslak olarak oluşur.
2. Fatura kaydedildikten sonra alt kısımda **Fatura Kalemleri** tablosu görünür. Buradan satır ekleyebilirsiniz.
3. Kalem eklemek için en alttaki formdan **Gider Tipi**, **Maliyet Merkezi**, **Miktar**, **Birim Fiyat** ve **KDV (%)** girin, **+ Kalem Ekle** düğmesine tıklayın. Toplam otomatik hesaplanır.
4. Yanlış eklediğiniz bir satırı kaldırmak için satır sonundaki **Sil** düğmesine tıklayın (yalnızca Taslak durumda).
5. Fatura hazır olduğunda sağ üstteki **Onayla** düğmesine tıklayın ve onay penceresini geçin. Fatura "Onaylandı" durumuna geçer.
6. Onaylı bir faturayı geri almak için **İptal Et** düğmesini kullanın.

## Alanlar ve butonlar
- **Fatura No**: Tedarikçinin fatura numarası (örn. FAT-2026-001).
- **Tedarikçi**: Faturanın geldiği cariyi seçtiğiniz alan.
- **Para Birimi**: TRY, USD veya EUR.
- **Fatura Tarihi / Vade Tarihi**: Düzenlenme ve ödeme son tarihleri.
- **Kaydet**: Başlık bilgilerini kaydeder (yalnızca Taslakta görünür).
- **Gider Tipi / Maliyet Merkezi**: Her kalemin hangi gidere ve hangi merkeze ait olduğunu belirler.
- **Miktar / Birim Fiyat / KDV (%)**: Kalem tutarını ve vergisini belirleyen alanlar.
- **+ Kalem Ekle**: Yeni fatura satırı ekler ve başlık toplamını günceller.
- **Sil**: Bir fatura satırını kaldırır.
- **Onayla**: Faturayı kesinleştirir, cari deftere işler.
- **İptal Et**: Onaylı faturayı iptal eder, cari deftere ters kayıt yazar.
- **Ödeme Yap**: Onaylı faturada görünür; tedarikçi ve tutar ön-dolu ödeme ekranını açar.

## İpuçları ve sık hatalar
- Durum akışı: **Taslak → Onayla → (gerekirse) İptal Et**. Kalem ekleme/silme yalnızca **Taslak** durumda mümkündür; onaylı faturaya "Onaylanmış faturaya satır eklenemez" uyarısı gelir.
- Onaylamadan önce tüm kalemleri ekleyin; toplam tutar kalemlerin KDV dahil değerinden hesaplanır.
- Fatura No olarak tedarikçinin numarasını girin; sistem ayrıca kendi iç kayıt numarasını otomatik üretir.
- Faturaya bağlı bir ödeme yapıldıysa veya e-Fatura gönderildiyse iptal reddedilebilir; bu durumda sistem Türkçe bir hata mesajı gösterir.
- Para birimini onaydan önce doğrulayın; onay sonrası değiştirilemez.
