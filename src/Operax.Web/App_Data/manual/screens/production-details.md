# Üretim Emri Detayı
Tek bir üretim emrinin hammadde ihtiyaçlarını yüklediğiniz, toplama görevi açtığınız ve üretimi bitirdiğiniz ekrandır. Üretim sorumluları kullanır.

## Ne işe yarar
Bu ekran bir üretim emrinin tüm yaşam döngüsünü yönetir: önce reçeteden (BOM) hammadde ihtiyaçları yüklenir, sonra hammaddeler için depo toplama görevi açılır, hammaddeler toplandıktan sonra üretim bitirilerek mamul stoğa girer. Hammadde satırlarında her kalemin gerekli ve toplanan (issued) miktarı ile hazır durumu görünür. Yanlış bir tamamlama yapılırsa emir iptal edilip ters stok hareketi yazılabilir.

## Nasıl kullanılır
1. Emrin hammadde ihtiyaçları henüz yoksa **Load BOM Requirements** butonuna tıklayarak reçeteden malzeme listesini yükleyin.
2. Hammadde satırları yüklendikten sonra **Create Picking Task for Raw Materials** butonuyla depo toplama görevi oluşturun; sistem sizi toplama ekranına yönlendirir.
3. Toplama görevi varsa **View Raw Material Pick Task** butonuyla ilgili toplama görevini görüntüleyin.
4. Tüm hammaddeler toplandığında (her satır READY olduğunda) **Finish Production** butonu görünür; tıklayın.
5. Açılan pencerede üretilen miktarı girip **Confirm & Post** ile onaylayın; mamul hedef depoya girer ve emir COMPLETED olur.
6. Tamamlanmış bir emri geri almak için **Üretim Emrini İptal Et** butonuna tıklayın (onay sorulur).

## Alanlar ve butonlar
- **Load BOM Requirements**: Reçeteden hammadde ihtiyaç satırlarını üretim emrine yükler (yalnızca satır yokken görünür).
- **Create Picking Task for Raw Materials**: Hammaddeler için depo toplama görevi açar.
- **View Raw Material Pick Task**: Mevcut hammadde toplama görevini açar.
- **Finish Production**: Üretimi bitirme penceresini açar (tüm satırlar hazır olunca görünür).
- **Produced Quantity (EACH)**: Bitirme penceresinde üretilen gerçek miktar.
- **Confirm & Post**: Üretimi onaylar, mamulü stoğa girer, emri tamamlar.
- **Üretim Emrini İptal Et**: Tamamlanmış emri iptal eder, hammadde sarfı ve mamul girişini ters hareketle kapatır.
- **Back**: Üretim Emirleri listesine döner.
- **Required / Issued / Status**: Her hammadde satırı için gerekli miktar, toplanan miktar ve hazır (READY) / toplama bekliyor (AWAITING PICK) durumu.

## İpuçları ve sık hatalar
- Adımları sırasıyla yapın: önce BOM yükle, sonra toplama görevi aç, hammaddeler toplandıktan sonra bitir. Hammaddeler toplanmadan (satırlar READY değilken) **Finish Production** butonu görünmez.
- **Üretim Emrini İptal Et** yalnızca COMPLETED emirlerde çalışır; dönem kilidi gibi bir engel varsa sistem Türkçe bir hata mesajı gösterir ve işlem yapılmaz.
- İptal işlemi geri alınamaz bir ters stok hareketi yazar; gerçekten gerekli olduğundan emin olun.
