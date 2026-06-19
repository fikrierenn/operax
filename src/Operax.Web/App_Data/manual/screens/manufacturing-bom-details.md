# Reçete Detayı
Bir ürün modelinin bilgilerini, parametrelerini ve formüllü reçete bileşenlerini tanımladığınız ekrandır. Teknik ofis ve üretim planlamacıları kullanır.

## Ne işe yarar
Bu ekranda parametrik bir reçete kurarsınız: önce model bilgilerini kaydeder, sonra ürüne ait parametreleri (En, Boy, Cam Tipi vb.) ve her malzeme için miktar formüllerini tanımlarsınız. Formüller NCalc ile değerlendirilir; üretim emrinde parametre değerleri girildiğinde her bileşenin miktarı otomatik hesaplanır. Fire oranı ve koşul formülü ile gerçek üretim senaryolarını modelleyebilirsiniz.

## Nasıl kullanılır
1. Yeni modelde **Model Kodu** ve **Model Adı** alanlarını doldurun; isterseniz bir **Ana Ürün** seçin ve **Aktif** kutusunu işaretleyin.
2. **Kaydet** butonuna tıklayın. Model kaydedildikten sonra Parametreler ve Reçete Bileşenleri bölümleri görünür.
3. **Parametreler** bölümünün altındaki formdan Kod (örn. WIDTH), Ad, Tip (Sayı/Metin/Seçenek), Varsayılan ve Birim girip **+ Parametre Ekle** ile parametre tanımlayın.
4. **Reçete Bileşenleri** bölümünde Bileşen Ürün seçin, Miktar Formülü yazın (örn. `[WIDTH] * [HEIGHT] / 1000000`), gerekirse Fire Oranı ve Koşul Formülü girip **+ BOM Satırı Ekle** butonuna tıklayın.
5. Yanlış girilen parametre veya bileşeni satırındaki **Sil** butonuyla kaldırın (onay sorulur).

## Alanlar ve butonlar
- **Model Kodu / Model Adı**: Modelin zorunlu kimlik bilgileri.
- **Ana Ürün**: Modelin temsil ettiği bitmiş ürün (opsiyonel).
- **Aktif**: İşaretliyse model üretimde kullanılabilir.
- **Kaydet**: Model başlık bilgilerini ekler veya günceller.
- **Parametre formu (Kod / Ad / Tip / Varsayılan / Birim)**: Formüllerde `[KOD]` olarak kullanılacak parametreleri tanımlar.
- **+ Parametre Ekle**: Yeni parametre satırı ekler. Kod otomatik büyük harfe çevrilir.
- **Bileşen Ürün**: Reçeteye girecek hammadde/yarı mamul.
- **Miktar Formülü**: Bileşen miktarını hesaplayan zorunlu formül.
- **Fire Oranı (%)**: Üretimde oluşacak kayıp/fire yüzdesi.
- **Koşul Formülü**: Bileşenin yalnızca belirli koşulda kullanılacağını belirtir (örn. `[CAM_TIPI] == "BUZLU"`).
- **+ BOM Satırı Ekle**: Reçeteye yeni bileşen ekler.
- **Sil**: Parametre veya BOM satırını siler (onay ister).
- **← Listeye Dön**: Reçete listesine geri döner.

## İpuçları ve sık hatalar
- Formüllerde parametreyi mutlaka köşeli parantezle yazın: `[WIDTH]`. Parametre tanımlamadan formülde kullanırsanız hesaplama başarısız olur.
- Formül yardımı (ekranın altında): çarpma `[WIDTH] * [HEIGHT]`, bölme `[QTY] / 1000`, yukarı yuvarlama `Ceiling([WIDTH] / 600)`, koşul `If([TIP] == 'A', 2, 3)`.
- Model başlığını kaydetmeden parametre/BOM bölümleri görünmez; önce **Kaydet** deyin.
- Parametre kodları otomatik büyük harfe çevrildiği için formülde de büyük harf kullanın.
