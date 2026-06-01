# Depo Kartı
Tek bir deponun bilgilerini girdiğiniz ve içindeki hücreleri (rafları) yönettiğiniz ekrandır. Yeni depo açarken ve mevcut depoya hücre eklerken kullanılır.

## Ne işe yarar
Depo kartı; deponun kodunu, adını, bağlı olduğu şubeyi ve aktiflik durumunu tutar. Sağ tarafta o depoya ait tüm hücreler (raf adresleri) listelenir; her hücrenin bölgesi (zone) ve işlevi (toplama alanı / mal kabul alanı) tanımlanır. Stok hareketleri bu hücreler üzerinden gerçekleşir.

## Nasıl kullanılır
1. Sol kolonda zorunlu alanları doldurun: Depo Kodu ve Depo Adı.
2. Gerekirse depoyu bir şubeye bağlayın ("Bağlı Şube"); merkez depo için "Şubesiz / Merkez" bırakın.
3. "Aktif Durum" anahtarıyla deponun kullanıma açık olup olmadığını belirleyin.
4. "Kaydet" butonuna basarak depoyu oluşturun veya güncelleyin.
5. Depo kaydedildikten sonra sağdaki "Yeni Hücre Ekle" butonuna basın; açılan pencerede hücre kodu, bölge ve alan tipini girip "Kaydet" deyin.
6. Bir hücreyi kaldırmak için satır sonundaki çöp kutusu simgesine tıklayın (onay sorar).

## Alanlar ve butonlar
- **Depo Kodu**: Deponun kısa kodu (örn. W01). Zorunlu.
- **Depo Adı**: Deponun tam adı. Zorunlu.
- **Bağlı Şube**: Depoyu bir şubeye bağlar; boş bırakılırsa merkez sayılır.
- **Aktif Durum**: Açıkken depo kullanıma açıktır.
- **Kaydet**: Depo bilgilerini yazar.
- **Yeni Hücre Ekle**: Hücre tanımlama penceresini açar.
- **Hücre Kodu**: Raf adresi (örn. A-01-01). Zorunlu.
- **Bölge (Zone)**: Hücrenin bulunduğu bölge etiketi (örn. Raf Kat-1).
- **Toplama Alanı**: İşaretliyse hücre sipariş sevkiyatları (picking) için kullanılır.
- **Mal Kabul Alanı**: İşaretliyse hücre giriş/staging alanı olarak kullanılır.
- **Sil (çöp kutusu)**: Hücreyi pasif/silinmiş yapar (onay sorar).

## İpuçları ve sık hatalar
- Hücre yönetimi yalnızca depo kaydedildikten sonra açılır; yeni depoda önce sol formdan "Kaydet" deyin.
- Hücre işlevlerinde "T" rozeti toplama alanını, "M" rozeti mal kabul alanını gösterir. Bir hücre her ikisi de olabilir.
- Hücre kodlarını tutarlı bir düzende verin (örn. Koridor-Raf-Göz); bu, terminal taramalarını ve raporlamayı kolaylaştırır.
- Bir başka şirkete ait depo veya hücreye erişemezsiniz; sistem URL ile bile olsa bunu engeller.
