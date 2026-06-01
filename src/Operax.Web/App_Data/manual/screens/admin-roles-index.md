# Yetki Grupları
Sistemdeki rolleri (erişim seviyelerini) listeler. Buradan yeni rol oluşturabilir, mevcut rollerin modül izinlerini düzenleyebilir veya gereksiz rolleri silebilirsiniz. Yalnızca Yönetici (Administrator) rolü kullanabilir.

## Ne işe yarar
Yetki grupları, kullanıcıların hangi modüllere ne kadar erişebileceğini belirleyen rollerdir. Örneğin "Depo Personeli" rolü yalnızca depo ekranlarını görürken, "Muhasebe" rolü finans ekranlarına erişebilir. Bu ekran tüm rolleri tek listede toplar ve yönetmenizi sağlar.

## Nasıl kullanılır
1. Listede her satırda bir rol adı, yetki seviyesi açıklaması ve işlem butonları yer alır.
2. Yeni bir rol eklemek için sağ üstteki **Yeni Rol** butonuna tıklayın (Alt+N kısayolu da çalışır).
3. Bir rolün hangi modüllere eriştiğini ayarlamak için ilgili satırdaki **Modül İzinleri** butonuna tıklayın.
4. Artık kullanılmayan bir rolü kaldırmak için **Sil** butonuna tıklayın; çıkan onay penceresinde işlemi doğrulayın.
5. Üst soldaki **Ayarlar** bağlantısı sizi Sistem Ayarları ekranına döndürür.

## Alanlar ve butonlar
- **Rol Adı**: Yetki grubunun adı.
- **Yetki Seviyesi**: Rolün kapsamı hakkında kısa bilgi.
- **Modül İzinleri**: Rolün modül bazlı erişim seviyelerini düzenleme sayfasını açar.
- **Sil**: Rolü kaldırır (onay ister).
- **Sistem Rolü etiketi**: "Administrator" rolünde butonlar yerine bu etiket görünür; bu rol korumalıdır.
- **Yeni Rol**: Yeni bir rol oluşturma sayfasını açar.

## İpuçları ve sık hatalar
- **Administrator** rolü silinemez ve izinleri buradan düzenlenemez; sistemin temel yönetici rolüdür ve her zaman tüm yetkilere sahiptir.
- Bir rolü silmeden önce o role atanmış kullanıcı olup olmadığını kontrol edin; rolü kalkan kullanıcılar yetkilerini kaybeder.
- Silme işlemi onay penceresiyle korunur; yanlışlıkla silmemek için rol adını okuyup doğrulayın.
