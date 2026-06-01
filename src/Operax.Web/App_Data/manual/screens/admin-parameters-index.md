# Sistem Parametreleri
Modül bazlı operasyonel kuralları ve global ayarları anahtar-değer çiftleri halinde yönetir. Yalnızca Yönetici (Administrator) rolü kullanabilir.

## Ne işe yarar
Sistem parametreleri, kod değişikliği gerektirmeden bazı davranışları ayarlamanızı sağlayan değerlerdir. Örneğin bir modülün fatura modunu veya bir eşik değerini buradan değiştirebilirsiniz. Her parametre bir modüle, bir koda ve bir değere sahiptir. Bu ekranda yeni parametre ekleyebilir, mevcutların değerini güncelleyebilir veya silebilirsiniz.

## Nasıl kullanılır
1. Yeni bir parametre eklemek için üstteki **Yeni Parametre** formunu doldurun: **Modül Kodu**, **Parametre Kodu**, **Değer** ve isteğe bağlı **Açıklama**.
2. **Ekle** butonuna tıklayın; parametre listeye eklenir ve üstte yeşil bir bildirim çıkar.
3. Mevcut bir parametrenin değerini değiştirmek için alttaki listede ilgili satırın **Değer** ve **Açıklama** alanlarını güncelleyin, ardından satırdaki **Kaydet** butonuna tıklayın.
4. Bir parametreyi kaldırmak için satırdaki **Sil** butonuna tıklayın ve onay penceresini doğrulayın.

## Alanlar ve butonlar
- **Modül Kodu**: Parametrenin ait olduğu modül (örnek: M11, SYS, M03). Boş bırakılırsa "SYS" olarak kaydedilir.
- **Parametre Kodu**: Parametrenin benzersiz kodu (örnek: InvoiceMode). Zorunludur; büyük harfe çevrilerek kaydedilir.
- **Değer**: Parametrenin değeri (örnek: INSTANT). Zorunludur.
- **Açıklama**: Parametrenin ne işe yaradığını anlatan isteğe bağlı not.
- **Ekle**: Yeni parametreyi kaydeder.
- **Kaydet (satırda)**: O parametrenin değer/açıklamasını günceller.
- **Sil**: Parametreyi kaldırır (onay ister).

## İpuçları ve sık hatalar
- **Parametre Kodu** ve **Değer** alanları zorunludur; boş bırakırsanız "Kod ve Değer zorunludur" uyarısı alırsınız.
- Aynı kodla ikinci bir parametre ekleyemezsiniz; kod zaten tanımlıysa kırmızı uyarı çıkar.
- Bir parametrenin değerini değiştirmek o modülün davranışını etkileyebilir; ne işe yaradığından emin değilseniz değiştirmeden önce sorumluya danışın.
- Silme işlemi onay penceresiyle korunur; yanlışlıkla bir parametreyi kaldırmamaya dikkat edin.
- Yalnızca şirketinize ait parametreler listelenir.
