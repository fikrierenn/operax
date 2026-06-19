# Depolar ve Raflar
Fiziki depolarınızı listeleyen ve yeni depo eklediğiniz ekrandır. Stok alanlarının, rafların ve hücrelerin yönetimi buradan başlar.

## Ne işe yarar
Bu ekran işletmenizdeki tüm depoları tek listede toplar. Her deponun kodunu, adını ve aktiflik durumunu gösterir. Üstteki kartlar aktif depo sayısını, tanımlı toplam hücre/raf sayısını ve genel lokasyon doluluk oranını özetler. Stok hareketleri yalnızca tanımlı hücrelere yazılır, bu yüzden depo ve raf yapısı doğru kurulmalıdır.

## Nasıl kullanılır
1. Listede mevcut depoları görün; her satır bir depoyu temsil eder.
2. Belirli bir depo aramak için arama kutusuna kod veya ad yazın, ya da durum açılır menüsünden filtreleyin.
3. Bir deponun hücrelerini görmek ve düzenlemek için satıra tıklayın veya "Hücreler / Düzenle" butonuna basın.
4. Yeni depo eklemek için sağ üstteki "Yeni Depo" butonuna tıklayın (kısayol: Alt+N).

## Alanlar ve butonlar
- **Aktif Depolar**: Kullanımdaki fiziki depo sayısı.
- **Toplam Hücre / Raf**: Tüm depolardaki aktif tanımlı hücre sayısı.
- **Lokasyonel Doluluk**: Stok bulunan hücrelerin toplam hücreye oranı (yüzde).
- **Arama kutusu / Tüm Durumlar**: Listeyi kod-ad veya aktif/pasif duruma göre daraltır.
- **Depo Kodu / Depo Tanımı sütunları**: Deponun kısa kodu ve tam adı.
- **Durum sütunu**: AKTİF veya PASİF.
- **Yeni Depo / Hücreler / Düzenle**: Depo kartı sayfasını açar.

## İpuçları ve sık hatalar
- Bir depo oluşturmak tek başına yeterli değildir; içine en az bir hücre (raf) tanımlamadan stok hareketi yapılamaz.
- Doluluk oranı, stoğu olan ayrı hücrelerin toplam hücreye bölünmesiyle hesaplanır; tek bir hücrede çok stok olması doluluğu yükseltmez.
- Kullanımdan kaldırılacak depoyu pasif yapın; içinde stok varken depo yapısını bozmaktan kaçının.
