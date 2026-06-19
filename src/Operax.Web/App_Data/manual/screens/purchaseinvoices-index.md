# Alış Faturaları Listesi

Tedarikçilerden gelen mal alım faturalarının tamamını listeleyen ve duruma/aramaya göre filtrelemeyi sağlayan ekrandır.

## Ne işe yarar

Satınalma ve muhasebe ekibinin tüm alış faturalarını görebildiği merkezi listedir. Ekranın üstündeki KPI kartları onay bekleyen taslak ve cari hesaba işlenmiş onaylı fatura sayısını gösterir. Listeyi tedarikçi fatura numarasına veya cari adına göre arayabilir, durum filtreleme yapabilirsiniz. Her satıra tıklayarak faturanın detay ekranına ulaşırsınız.

**Not:** Alış faturaları yalnızca mal/stok alımlarını kapsar. Hizmet ödemeleri ve sarf giderleri **Gider Faturası** modülünde tutulur.

## Nasıl kullanılır

1. Soldaki menüden **Satınalma → Alış Faturaları** yolunu izleyerek bu ekrana gelin.
2. KPI kartlarında taslak ve onaylı fatura sayısını görün.
3. Arama kutusuna tedarikçi adı veya tedarikçinin kendi fatura numarasından bir parça yazıp Enter'a basın.
4. **Durum** açılır listesinden Taslak / Onaylı / İptal filtresi uygulayın; seçim değişince liste otomatik güncellenir.
5. İstediğiniz satıra tıklayarak faturanın detay ekranına geçin.
6. Yeni alış faturası oluşturmak için **Mal Kabul Belgesi Detayı** ekranını açın ve **Alış Faturası Oluştur** butonunu kullanın (fatura bu listeden elle oluşturulamaz).

## Alanlar ve butonlar

- **KPI: Taslak**: Onay bekleyen fatura sayısı.
- **KPI: Onaylı**: Cari hesaba işlenmiş fatura sayısı.
- **Arama kutusu**: Tedarikçi fatura numarası (`SupplierInvoiceNo`) veya tedarikçi adında arama yapar.
- **Durum filtresi**: Tüm Durumlar / Taslak / Onaylı / İptal; seçim değiştiğinde form otomatik gönderilir.
- **Belge No**: Sistemin atadığı iç belge numarası.
- **Tedarikçi Fatura No**: Tedarikçinin gönderdiği faturada yazan numara (e-Fatura için ETTN/UUID da buraya kaydedilir).
- **Tedarikçi**: Faturayı düzenleyen cari.
- **Fatura Tarihi**: Tedarikçi fatura tarihi; girilmemişse sistemin atadığı tarih gösterilir.
- **Kalem**: Faturadaki ürün satırı sayısı.
- **Tutar**: KDV dahil genel toplam (₺).
- **Durum rozeti**: Taslak (sarı) / Onaylı (yeşil) / İptal (kırmızı).

## İpuçları ve sık hatalar

- Alış faturaları **Mal Kabul Belgesi** onaylanmadan oluşturulamaz; önce mal kabul belgesi POSTED durumuna getirilmelidir.
- Listede görünmesi için `sp_CreatePurchaseInvoiceFromReceiving` SP'sinin çalışmış olması gerekir; bu SP mal kabul detayındaki **Alış Faturası Oluştur** butonu tetiklediğinde otomatik çalışır.
- **Onaylı** faturalar cari hesaba (borç) işlenmiştir; bu faturalarda kalem fiyatını yalnızca gerekçeli düzeltme yöntemiyle değiştirebilirsiniz (bkz. Alış Faturası Detayı).
- Arama hem büyük hem küçük harfe duyarsızdır; kısmi eşleşme çalışır.
