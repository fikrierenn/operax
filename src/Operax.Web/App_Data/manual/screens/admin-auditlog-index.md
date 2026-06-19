# Denetim Kayıtları
Sistemde gerçekleştirilen işlemlerin güvenli geçmişini gösterir. Kim, ne zaman, hangi kaydı oluşturdu/değiştirdi/sildi sorularının yanıtı buradadır. Yalnızca Yönetici (Administrator) rolü kullanabilir.

## Ne işe yarar
Denetim kayıtları, sistem üzerindeki tüm önemli işlemlerin değiştirilemez bir günlüğüdür. Bir kayıtta sorun olduğunda veya bir işlemin kim tarafından yapıldığını öğrenmeniz gerektiğinde bu ekran kullanılır. Kullanıcıya, aksiyona ve kayıt tipine göre filtreleme yaparak aradığınız işleme hızla ulaşırsınız.

## Nasıl kullanılır
1. Üstteki filtre panelinde aradığınız işlemi daraltın: **Kullanıcı**, **Aksiyon** ve/veya **Kayıt Tipi** alanlarına arama metni girin.
2. **Filtrele** butonuna tıklayın; liste girdiğiniz koşullara göre yenilenir.
3. Filtreleri sıfırlamak için **Temizle** butonuna tıklayın.
4. Listede her satır bir işlemi gösterir: tarih/saat, kullanıcı, aksiyon türü, kayıt tipi, kayıt no, detay ve IP adresi.
5. Detay metni uzunsa kısaltılır; tam metni görmek için fareyi detay hücresinin üzerine getirin.

## Alanlar ve butonlar
- **Kullanıcı (filtre)**: Belirli bir kullanıcının işlemlerini bulmak için kullanıcı adı yazın.
- **Aksiyon (filtre)**: İşlem türüne göre arar (örnek: CREATE, UPDATE, DELETE).
- **Kayıt Tipi (filtre)**: Belge/kayıt türüne göre arar (örnek: ReceivingHeader, Item).
- **Filtrele**: Girilen koşullarla listeyi yeniler.
- **Temizle**: Tüm filtreleri kaldırır.
- **Aksiyon rozetleri**: CREATE yeşil, UPDATE mavi, DELETE kırmızı, POST mor, LOGIN sarı renkte gösterilir.

## İpuçları ve sık hatalar
- Ekran her zaman en son 200 kaydı gösterir ve en yeni işlem en üsttedir. Daha eski bir işlemi arıyorsanız mutlaka filtre kullanın.
- Filtre alanları parça eşleşmesiyle çalışır; örneğin Aksiyon'a "CRE" yazmanız "CREATE" işlemlerini bulmaya yeter.
- Denetim kayıtları silinemez ve değiştirilemez; bu, güvenlik ve uyumluluk için tasarlanmıştır.
- Yalnızca kendi şirketinize ait kayıtlar listelenir.
