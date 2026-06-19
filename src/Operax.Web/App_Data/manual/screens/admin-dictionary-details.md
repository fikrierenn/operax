# Sözlük Değerleri (Dictionary Detayı)

Seçili sözlük türünün (ölçü birimi, para birimi, kategori vb.) mevcut değerlerini listeleyen ve yeni değer eklemeye imkân tanıyan yönetici ekranıdır.

## Ne işe yarar

Operax'ın tüm modüllerinde dropdown listelerinde kullanılan sabit değerler (birimler, ülke kodları, kategori etiketleri vb.) bu sözlük tablolarından beslenir. Yöneticiler bu ekranda seçili sözlük türüne ait mevcut değerleri görüntüleyebilir ve yeni değer ekleyebilir. Sözlük değerleri tüm şirketler tarafından paylaşılan global sistem verisidir.

## Nasıl kullanılır

1. **Admin → Sözlük** menüsünden Sözlük Listesi ekranına gelin.
2. Düzenlemek istediğiniz sözlük türüne tıklayarak bu Detay ekranını açın.
3. Ekranın üst kısmında türün Türkçe adı ve sistem kodu (`Code`) gösterilir.
4. Tabloda bu türe ait tüm aktif değerler listelenir.
5. Yeni değer eklemek için sağ üstteki **+ Add Value** bağlantısına tıklayın; modal açılır.
6. Modalda **Value Code**, **Display Name (TR)**, **Display Name (EN)** ve **Sort Order** alanlarını doldurun; **Save Value** butonuyla kaydedin.
7. Değer eklendikten sonra modal kapanır ve liste yenilenir.
8. Listeden çıkmak için **Back to List** butonuyla sözlük listesine dönün.

## Alanlar ve butonlar

- **Tür adı (başlık)**: Sözlük türünün Türkçe adı (ör. "Ölçü Birimi").
- **Sistem kodu**: Türün İngilizce kod adı (ör. `UOM`); bu değer kod içinde referans alınır.
- **Back to List**: Sözlük Listesi ekranına döner.
- **+ Add Value**: Yeni değer ekleme modalını açar.
- **Code (tablo sütunu)**: Değerin İngilizce sistem kodu (ör. `KG`, `ADET`). Modül açılır listelerinde bu kod kullanılır.
- **Name (TR) (tablo sütunu)**: Kullanıcılara Türkçe arayüzde gösterilecek ad.
- **Name (EN) (tablo sütunu)**: İngilizce görüntüleme adı (ileride çok dilli destek için korunur).
- **Sort (tablo sütunu)**: Açılır listelerde bu değerin sıralama numarası; küçük numara üstte çıkar.
- **Del butonu**: Değeri siler (geliştirme aşamasındadır; şu an yalnızca görsel olarak gösterilmektedir).
- **Modal — Value Code**: Zorunlu; büyük harf İngilizce kod önerilir (ör. `KG`, `PAKET`).
- **Modal — Display Name (TR)**: Zorunlu; açılır listelerde Türkçe görünecek ad.
- **Modal — Display Name (EN)**: Zorunlu; İngilizce görüntüleme adı.
- **Modal — Sort Order**: Sıralama numarası; varsayılan 10'dur.
- **Modal — Save Value**: Yeni değeri kaydeder; aynı modal DictionaryType Id'si parametre olarak gönderilir.
- **Modal — Cancel**: Modalı kapatır, değişiklik yapmaz.

## İpuçları ve sık hatalar

- **Value Code benzersiz olmalıdır**: Aynı tür altında aynı koda sahip iki değer eklenemez; veritabanı benzersizlik kısıtlaması hata verir.
- Bu ekrana yalnızca **Administrator** rolüne sahip kullanıcılar erişebilir; yetersiz yetkide sayfa görünmez.
- Sözlük değerleri tüm şirketler tarafından paylaşılır; eklediğiniz değer tüm müşteri/lokasyon ortamlarını etkiler. Dikkatli olun.
- Mevcut bir değerin adını (TR/EN) değiştirme özelliği henüz geliştirilme aşamasındadır; düzenleme gerekirse sistem yöneticinize başvurun.
- **Ölçü Birimi (UOM)** sözlüğüne eklenen değerler, ürün kartlarındaki temel birim ve dönüşüm birim listelerinde anında görünür.
- Sıralama numarasını atlayarak büyük değer (ör. 100) verirseniz bu değer listenin en altına iner; sık kullanılan değerlere düşük numara verin.
