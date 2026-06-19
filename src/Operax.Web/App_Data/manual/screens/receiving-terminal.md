# Mal Kabul Terminali

El terminali veya mobil cihazdan barkod okutarak mal kabul belgelerine satır girişi yapılan hafif, dokunmatik ekrandır.

## Ne işe yarar

Depo personeli bilgisayar başında değilken akıllı telefon veya el terminali tarayıcısından bu ekranı açarak bekleyen mal kabul belgelerinden birini seçer ve ürün barkodlarını okutarak miktarları kaydeder. Barkod okunduğunda ürün belgede zaten varsa miktarı güncellenir; yoksa yeni satır açılır. Her okutma sonrası sayfa yenilenerek satır durumu (Tamamlandı / Bekliyor) güncellenir.

## Nasıl kullanılır

1. Tarayıcıda `/Receiving/Terminal` adresine gidin; ekran mobil görünüme geçer.
2. **Bekleyen Belge Seç** listesinden çalışacağınız DRAFT durumundaki belgeyi seçin (belge no ve tedarikçi adı görünür).
3. Seçilen belgede **Barkod** alanı otomatik odak alır; barkod tarayıcıyı veya klavyeyi kullanarak ürün barkodunu girin.
4. **Lot No** alanı opsiyoneldir; parti takibi yapılan ürünlerde doldurun.
5. **Miktar** alanını kontrol edin (varsayılan 1); farklı bir miktar girecekseniz düzenleyin.
6. **Ekle ✓** butonuna basın ya da Enter'a basın; satır işlenir ve sayfa güncellenir.
7. Satırlar listesinde tamamlanan kalemlerin yanında "✓ Tamamlandı" ve yeşil renk görünür; bekleyenler turuncu renk alır.
8. İşlem bittikten sonra masaüstü ekranına geçerek belgeyi **Stoğa Aktar (Onayla)** ile kesinleştirin.
9. Farklı bir belgeye geçmek için sol üstteki **← Geri** bağlantısıyla belge seçim ekranına dönün.

## Alanlar ve butonlar

- **Belge seçim listesi**: DRAFT durumundaki tüm mal kabul belgeleri listelenir. Her kart; belge no, tedarikçi adı ve mevcut satır sayısını gösterir. Karta tıklayarak belge aktifleştirilir.
- **Aktif Belge başlığı (mavi kart)**: Seçilen belgenin numarası ve tedarikçi adını gösterir.
- **← Geri bağlantısı**: Belge seçim ekranına döner.
- **Barkod alanı**: Ürün barkodunu girmek için; sayfa açıldığında otomatik odak alır. Barkod ürün kayıtlarından `ItemBarcode` tablosunda aranır.
- **Lot No**: Opsiyonel parti numarası.
- **Miktar**: Okutulan birim miktarı; varsayılan 1, ondalık girilebilir.
- **Ekle ✓ butonu**: Barkod, lot ve miktarı işler; başarılı okutmada yeşil onay mesajı, hatalı okutmada kırmızı hata mesajı gösterilir.
- **Satırlar listesi**: Belgede kayıtlı kalemleri gösterir. Her satırda ürün kodu, adı, lot numarası (varsa), okutulan / beklenen miktar ve tamamlanma durumu görünür.

## İpuçları ve sık hatalar

- **Barkod bulunamazsa** kırmızı hata mesajı gösterilir ("Barkod bulunamadı: XYZ"); barkodun ürün kartında tanımlı olduğunu yöneticinizle kontrol edin.
- Mal kabul belgesini bu ekranla **onaylayamazsınız**; terminal yalnızca satır girişi yapar. Onaylama işlemi masaüstü **Mal Kabul Belgesi Detayı** ekranından yapılır.
- Terminale erişim için sisteme giriş yapılmış olması (oturum açık) gerekir; oturum sona ererse giriş ekranına yönlendirilirsiniz.
- **Aynı ürün barkodunu tekrar okuttuğunuzda** mevcut satırın miktarı eklenir (değiştirilmez); toplam miktar birikimli artar.
- Lot numarası girmeyi atlarsanız satır lot numarasız kaydedilir; daha sonra düzeltme yapmak için masaüstü detay ekranını kullanabilirsiniz.
- Zayıf Wi-Fi bağlantısında sayfa yenilemesi geç gelebilir; gönder butonuna tekrar basarak çift kayıt oluşturmayın.
- Yalnızca **DRAFT** belgeler listede görünür; onaylanmış veya iptal edilmiş belgeler terminalde gösterilmez.
