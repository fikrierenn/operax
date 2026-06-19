# Üretim Emirleri
Açık ve tamamlanmış üretim (iş) emirlerini topluca izlediğiniz üretim planlama ekranıdır. Üretim planlamacıları ve yöneticiler kullanır.

## Ne işe yarar
Bu ekran, firmanın tüm üretim emirlerini en yeniden eskiye doğru listeler. Her emrin hedef ürünü, planlanan ve üretilen miktarını ve mevcut durumunu (RELEASED, IN_PROGRESS, COMPLETED) tek bakışta görürsünüz. Bir emrin hammadde ihtiyaçlarını, toplama görevini ve bitirme işlemlerini yönetmek için detayına geçilir.

## Nasıl kullanılır
1. Listede üretim emirlerini Emir No, Ürün, Hedef Miktar, Üretilen ve Durum sütunlarıyla inceleyin.
2. Durum rozetlerinden işin hangi aşamada olduğunu anlayın: yeşil=tamamlandı, mavi=devam ediyor, mor=başlatıldı.
3. Bir emrin hammadde, toplama ve bitirme işlemleri için ilgili satırdaki **Detay** butonuna tıklayın.

## Alanlar ve butonlar
- **+ Yeni İş Emri**: Yeni üretim emri başlatma butonu (üst sağda).
- **Emir No**: Üretim emrinin belge numarası.
- **Ürün**: Üretilecek bitmiş ürünün adı ve kodu.
- **Hedef Miktar**: Planlanan üretim adedi.
- **Üretilen**: Şu ana kadar tamamlanan adet.
- **Durum**: Emrin yaşam döngüsü aşaması (RELEASED → IN_PROGRESS → COMPLETED).
- **Detay**: Seçilen emrin detay ekranına gider.

## İpuçları ve sık hatalar
- Liste boşsa henüz üretim emri oluşturulmamış demektir; emirleri detayda yönetmeden önce oluşturulmuş olmaları gerekir.
- "Üretilen" miktarı, emir Bitir işlemiyle tamamlandığında dolar; devam eden emirlerde düşük/sıfır görünebilir.
- Emir durumu COMPLETED ise üretim tamamlanmıştır; düzeltme gerekiyorsa detay ekranından iptal (ters hareket) yapılır.
