# Modül İzinleri
Seçilen bir rolün hangi modüllere hangi seviyede eriştiğini belirlediğiniz ekrandır. Yalnızca Yönetici (Administrator) rolü kullanabilir.

## Ne işe yarar
Bir rolü oluşturduktan sonra, o role sistemdeki her modül için ayrı ayrı erişim seviyesi atarsınız. Bir modülü hiç görmemesini, sadece görüntülemesini veya düzenleyebilmesini seçebilirsiniz. Böylece personelin sorumluluğuna uygun, sınırlı ve güvenli bir erişim profili tanımlarsınız.

## Nasıl kullanılır
1. Sayfa başlığında düzenlediğiniz rolün adı görünür; tablo sistemdeki tüm modülleri listeler.
2. Her modül satırındaki **Erişim Seviyesi** listesinden uygun seviyeyi seçin: "Yok", "Görüntüle" veya "Düzenle".
3. Tüm modüller için seçimlerinizi yaptıktan sonra **Kaydet** butonuna tıklayın.
4. Kaydedince aynı sayfada kalırsınız ve üstte yeşil bir "güncellendi" bildirimi görürsünüz.
5. Vazgeçmek için **İptal** butonuna veya üstteki **Yetki Grupları** bağlantısına tıklayın.

## Alanlar ve butonlar
- **Modül**: Sistemdeki modülün kodu/adı.
- **Erişim Seviyesi**: Her modül için seçim listesi:
  - **Yok**: Rol bu modülü hiç göremez.
  - **Görüntüle**: Rol bu modülü açıp veriyi okuyabilir, ama değiştiremez.
  - **Düzenle**: Rol bu modülde veri ekleyip değiştirebilir.
- **Kaydet**: Tüm modüllerin seçilen seviyelerini uygular.
- **İptal / Yetki Grupları**: Değişiklik yapmadan rol listesine döner.

## İpuçları ve sık hatalar
- Değişiklikler ancak **Kaydet** butonuna bastığınızda geçerli olur; sadece listeyi değiştirmek yetmez.
- Bir modülü "Yok" seçtiğinizde o modülün izin kaydı tamamen kaldırılır; "Görüntüle" veya "Düzenle" seçince yeniden eklenir.
- İzinler kaydedildiğinde rolün yetki haritası anında yenilenir; o role sahip kullanıcılar değişikliği bir sonraki işlemlerinde hisseder.
- Bu ekran "Administrator" rolü için kullanılmaz; o rol her zaman tam yetkilidir.
