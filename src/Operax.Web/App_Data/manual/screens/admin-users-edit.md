# Kullanıcı Düzenle
Mevcut bir kullanıcının e-posta, şifre ve yetki grubu bilgilerini güncellediğiniz ekrandır. Yalnızca Yönetici (Administrator) rolü kullanabilir.

## Ne işe yarar
Bir personelin erişim bilgilerini değiştirmek için kullanılır: e-posta adresini güncelleyebilir, şifresini sıfırlayabilir veya yetki grubunu değiştirebilirsiniz. Görevi değişen ya da şifresini unutan kullanıcılar için sık başvurulan ekrandır.

## Nasıl kullanılır
1. **Kullanıcı Adı** alanı bilgi amaçlıdır ve değiştirilemez (gri ve kilitli görünür).
2. **E-Posta** alanından giriş adresini güncelleyebilirsiniz.
3. Şifreyi değiştirmek istiyorsanız **Yeni Şifre** alanına yeni şifreyi yazın. Şifreyi değiştirmek istemiyorsanız bu alanı boş bırakın.
4. **Yetki Grubu** listesinden kullanıcının rolünü seçin veya rolü kaldırmak için "Kullanıcı (Rol Yok)" seçeneğini işaretleyin.
5. **Değişiklikleri Kaydet** butonuna tıklayın. İşlem başarılıysa kullanıcı listesine dönersiniz.
6. Vazgeçmek için **İptal** butonuna veya **Geri** bağlantısına tıklayın.

## Alanlar ve butonlar
- **Kullanıcı Adı**: Mevcut giriş adı; salt okunurdur, düzenlenemez.
- **E-Posta**: Güncellenebilir giriş adresi. E-posta değiştirildiğinde kullanıcı adı da bununla eşitlenir.
- **Yeni Şifre**: Yeni şifre. Boş bırakılırsa mevcut şifre korunur.
- **Yetki Grubu**: Kullanıcının rolü. Kaydederken eski roller kaldırılıp seçilen yeni rol atanır.
- **Değişiklikleri Kaydet**: Yapılan tüm değişiklikleri uygular.
- **İptal / Geri**: Değişiklik yapmadan listeye döner.

## İpuçları ve sık hatalar
- Yeni Şifre alanını boş bırakmak şifreyi silmez; sadece mevcut şifrenin korunması anlamına gelir. Şifreyi yalnızca değiştirmek istediğinizde doldurun.
- Yetki grubunu "Kullanıcı (Rol Yok)" yaparsanız, kullanıcı yönetici ekranlarına erişimini kaybeder. Yanlışlıkla kendi yöneticilik yetkinizi kaldırmamaya dikkat edin.
- E-posta veya şifre sistemin kurallarına uymuyorsa formun üstünde kırmızı uyarı çıkar ve kayıt yapılmaz.
- Kullanıcının bir şirket bağlantısı yoksa, kaydetme sırasında otomatik olarak sizin şirketinize bağlanır.
