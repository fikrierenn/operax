# Alış Faturası Detayı

Tek bir alış faturasının incelendiği, tedarikçi belge bilgilerinin girildiği, kalem fiyatlarının düzenlendiği ve faturanın onaylandığı ekrandır.

## Ne işe yarar

Muhasebe ve satınalma ekibi bu ekranda tedarikçiden gelen kağıt/e-Fatura bilgilerini (fatura numarası, tarihi, ETTN) sisteme girer; kalem birim fiyatlarını doğrular veya düzeltir; ardından **Onayla** butonuyla faturayı cari hesaba işler. Onaylanan fatura için **Ödeme Yap** butonu ile ödeme sürecine geçilebilir. Faturanın kalemi mal kabul belgesindeki sipariş fiyatından farklıysa ekranın altında Fiyat Farkı tablosu çıkar ve yetkili kullanıcıdan gerekçeli onay istenir.

## Nasıl kullanılır

1. Alış Faturaları Listesinden bir satıra tıklayarak veya Mal Kabul Belgesi Detayından **Alış Faturası Oluştur** butonuyla bu ekrana gelin.
2. **Sol kart (Tedarikçi Belge Bilgisi)** alanında tedarikçi fatura numarasını, fatura tarihini, varsa e-Fatura ETTN/UUID'ini ve vade tarihini girin; **Bilgileri Kaydet** butonuna basın.
3. **Sağ kart (Fatura Kalemleri)** tablosunda mal kabulden kopyalanan kalemler görünür. Birim fiyatları doğrulayın; gerekiyorsa değiştirin. Fiyatsız (0 TL) kalemler sarı vurgulu görünür.
4. Tüm kalemler doğrulandıktan sonra **Kalem Fiyatlarını Kaydet** butonuna basın.
5. Fatura fiyatı sipariş fiyatından farklıysa alt kısımda **Sipariş Fiyat Farkları** tablosu görünür; her fark için gerekçe yazıp **Override (Gerekçeyle Onayla)** veya **Reddet** butonuna basın.
6. Hazır olduğunda sağ üstteki **Onayla** butonuyla faturayı kesinleştirin. Onay öncesi onay iletişim kutusu gelir.
7. Onaylı faturada **Ödeme Yap** butonu (Belge Zinciri bölümünde) görünür; bağlı ödeme ekranına yönlendirir.
8. Gerektiğinde **İptal Et** butonuyla faturayı iptal edebilirsiniz (cari defterde ters kayıt açılır).

## Alanlar ve butonlar

- **Tedarikçi Fatura No**: Tedarikçinin gönderdiği faturada yazan numara. Zorunludur; DRAFT'ta düzenlenebilir.
- **Tedarikçi Fatura Tarihi**: Faturanın üzerindeki gerçek tarih (VUK gereği bugünün tarihi değil tedarikçi belgesi tarihi girilmelidir). Zorunludur; DRAFT'ta düzenlenebilir.
- **e-Fatura ETTN/UUID**: e-Fatura alındıysa GİB ETTN kodu. Opsiyoneldir.
- **Vade Tarihi**: Ödeme vadesi. DRAFT'ta düzenlenebilir.
- **Bilgileri Kaydet**: Sol karttaki başlık bilgilerini kaydeder. Yalnızca DRAFT faturada aktiftir.
- **Kalemler tablosu**: Mal kabulden kopyalanmış kalemleri gösterir. Sütunlar: Ürün (kod + ad), Miktar (birimli), Birim Fiyat, Tutar (ara), KDV oranı, Toplam (KDV dahil).
- **Birim Fiyat alanı (DRAFT'ta)**: Her kalem için birim fiyat girilebilir. Fiyatsız (0 ₺) kalem sarı vurgulu görünür; **0 fiyatlı kalemle fatura onaylanamaz**.
- **Kalem Fiyatlarını Kaydet**: DRAFT'ta tüm kalem fiyatlarını tek seferde günceller; başlık toplamlarını yeniden hesaplar.
- **Bu kalemi düzelt (gerekçeli)**: **POSTED** faturada, yetkili roller için her kalemin altında gizli bir form açar. Doğru birim fiyat ve zorunlu gerekçe girilerek onaylanır; cari defterde ters + yeni satır yazılır, fatura POSTED kalır. Ödeme yapılmış kalemlerde bu seçenek çıkmaz.
- **Ara Toplam / KDV / Genel Toplam**: Kalem tablosunun altında özet; kalem fiyatları güncellenince otomatik yeniden hesaplanır.
- **Onayla**: Faturayı POSTED yapar; `sp_PurchaseInvoicePost` çalışır, cari borç ve ödeme planı oluşturulur. Yalnızca DRAFT'ta görünür.
- **İptal Et**: Faturayı iptal eder; `sp_PurchaseInvoiceReverse` cari deftere ters satır yazar. Yalnızca POSTED'da görünür.
- **Geri**: Alış Faturaları Listesine döner.
- **Belge Zinciri (Ödeme)**: POSTED faturada görünür; kalan tutar varsa **Ödeme Yap** bağlantısı sunar, tüm ödemeler yapılmışsa "Tam Ödendi" yazar.
- **Sipariş Fiyat Farkları tablosu**: Fatura fiyatı satınalma sipariş fiyatından saptıysa görünür. Her fark için beklenen ve gerçek fiyat, sapma yüzdesi, gerekçe giriş alanı ve AI değerlendirme sonucu görüntülenir.
- **Override (Gerekçeyle Onayla)**: Fiyat farkını kabul eder; gerekçe zorunludur. Yerel AI gerekçeyi denetler ve PLAUSIBLE / IMPLAUSIBLE / UNCHECKED kararını kaydeder (tavsiye niteliğindedir, işlemi bloke etmez).
- **Reddet**: Fiyat farkını reddeder; fatura fiyatı yine de geçerli kalır, yalnızca fark kaydının durumu REJECTED olur.

## İpuçları ve sık hatalar

- **Fiyatsız (0 ₺) kalemle fatura onaylanamaz**: Kalem oluşturulurken mal kabulden fiyat kopyalanmamışsa birim fiyatı elle girin ve kaydedin.
- **VUK gereği tedarikçi fatura tarihi**: Sisteme girilen tarih tedarikçinin belgesindeki tarihe uygun olmalıdır; farklı bir tarih girmek muhasebe uyumsuzluğuna yol açabilir.
- **POSTED faturada fiyat hatası varsa faturayı iptal etmenize gerek yoktur**: "Bu kalemi düzelt (gerekçeli)" seçeneği cari defterde ters + yeni satır açar; fatura numarası ve diğer bilgiler değişmez. Bu seçenek yalnızca ödeme yapılmamış kalemlerde ve yetkili rollerde görünür.
- **Fiyat farkı AI denetimi**: Yerel AI gerekçenin makul olup olmadığını kontrol eder. AI sonucu "IMPLAUSIBLE" çıksa dahi override işlemi tamamlanır; ancak bu durum denetim kaydında görünür.
- Fatura iptal edilince cari deftere ters satır yazılır (AccountMovement REVERSAL); iptal sonrası ödeme planı da otomatik CANCELLED olur.
- Ödeme yapılmış fatura iptal edilemez; önce ilgili ödemelerin tersine çevrilmesi gerekir.
