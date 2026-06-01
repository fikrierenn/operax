# İş Merkezleri
Üretim operasyonlarının yapıldığı makine/istasyonları ve bunların saatlik maliyet oranlarını yönettiğiniz ekrandır. Üretim ve maliyet sorumluları kullanır.

## Ne işe yarar
İş merkezleri, üretim rotasındaki operasyonların gerçekleştiği fiziksel istasyonlardır (örn. CNC Kesim Hattı). Her iş merkezi için işçilik, makine ve elektrik gibi saatlik maliyet oranları tanımlanır. Bu oranlar rota adımlarında ve üretim maliyet hesaplarında kullanılır. Üretim rotası oluşturabilmek için en az bir iş merkezi tanımlı olmalıdır.

## Nasıl kullanılır
1. Sağ üstteki **+ Yeni İş Merkezi** butonuna tıklayın; gizli ekleme formu açılır.
2. **Kod** ve **Ad** alanlarını doldurun (örn. CNC-01, CNC Kesim Hattı).
3. İşçilik, Makine ve Elektrik için saatlik maliyet tutarlarını (₺/saat) girin.
4. **Aktif** kutusunu işaretleyin ve **Kaydet** butonuna tıklayın.
5. Vazgeçmek isterseniz **İptal** butonuyla formu kapatın.
6. Tabloda her iş merkezinin maliyet oranlarını ve hesaplanan toplam saatlik maliyetini görün.

## Alanlar ve butonlar
- **+ Yeni İş Merkezi**: Ekleme formunu açar/kapatır.
- **Kod**: İş merkezinin kısa kodu (zorunlu).
- **Ad**: İş merkezinin açıklayıcı adı (zorunlu).
- **İşçilik Maliyeti (₺/Saat)**: Operatör/işçilik saatlik maliyeti.
- **Makine Maliyeti (₺/Saat)**: Makinenin saatlik amortisman/işletme maliyeti.
- **Elektrik Maliyeti (₺/Saat)**: Enerji saatlik maliyeti.
- **Toplam (₺/saat)**: Üç maliyet kaleminin tabloda otomatik toplamı.
- **Aktif**: İşaretliyse iş merkezi rotalarda seçilebilir.
- **Kaydet**: İş merkezini ekler veya günceller.
- **İptal**: Ekleme formunu kapatır.
- **Durum**: Aktif/Pasif rozetini gösterir.

## İpuçları ve sık hatalar
- Maliyet oranlarını saatlik girin; sistem bunları arka planda saniye bazına çevirip saklar, listede tekrar saatlik gösterir.
- Pasif iş merkezleri rota adımı eklerken seçim listesinde çıkmaz; kullanımdan kalkmış istasyonları pasife alın, silmek yerine pasif tutun.
- Maliyet oranlarını sıfır bıraksanız da iş merkezi oluşur, ancak üretim maliyeti hesabı eksik kalır.
