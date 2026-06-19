# Toplama Terminali

Depo personelinin el terminali / mobil cihaz üzerinden barkod okutarak ürün topladığı dokunmatik ekrandır. Sahadaki toplayıcı personel kullanır.

## Ne işe yarar
Bu ekran, sahadaki toplayıcının elindeki cihazdan adım adım toplama yapmasını sağlar. Sistem size bekleyen görevleri listeler; bir görev seçtiğinizde gidilecek raf adresini, toplanacak ürünü ve miktarı büyük ve okunaklı şekilde gösterir. Ürün barkodunu okutup onayladıkça satırlar tek tek tamamlanır ve tüm satırlar bitince görev otomatik kapanır. Yanlış ürün okutulduğunda sistem uyarır, böylece hatalı toplama önlenir.

## Nasıl kullanılır
1. Ekran açıldığında "Toplama Görevi Seç" listesinden çalışacağınız göreve dokunun; her görevin yanında ilerleme (örn. 2/5) ve çubuk gösterilir.
2. Görev açıldığında üstte görev numarası, altında **Git: Raf** başlığıyla gidilecek raf adresi ve depo kodu görünür.
3. Belirtilen rafa gidin; ekrandaki ürün kodu, adı ve toplanacak miktarı (× sayı) kontrol edin.
4. Ürünün barkodunu, ortadaki "Ürün barkodunu okut..." alanına okutun (alan otomatik odaklanır).
5. **Onayla ✓** butonuna basın. Barkod eşleşirse satır tamamlanır ve sistem otomatik olarak sıradaki satıra geçer.
6. Tüm satırlar bitince "🎉 Tüm Satırlar Tamamlandı!" ekranı çıkar; **Yeni Görev Seç** ile başka bir göreve geçebilirsiniz.
7. Görevi bırakıp listeye dönmek için sağ üstteki **← Geri** butonunu kullanın.

## Alanlar ve butonlar
- **Toplama Görevi Seç**: Bekleyen (TASLAK / ATANDI / DEVAM EDİYOR) görevlerin listesi; her birinde ilerleme oranı vardır.
- **Git: Raf**: O an toplanacak ürünün bulunduğu raf adresi (büyük puntoyla) ve depo kodu.
- **Ürün kodu / adı**: Toplanması gereken ürünün kodu ve açıklaması.
- **× (miktar)**: Bu satırda kalan toplanacak miktar.
- **Barkod alanı**: Ürün barkodunun okutulduğu giriş kutusu; ekran açılınca otomatik odaklanır.
- **Onayla ✓**: Okutulan barkodu doğrular ve eşleşirse satırı tamamlar.
- **← Geri**: Görev listesine döner.
- **Yeni Görev Seç**: Görev tamamlandıktan sonra yeni görev seçmeye götürür.

## İpuçları ve sık hatalar
- Okuttuğunuz barkod ekrandaki ürünle eşleşmezse kırmızı "Barkod eşleşmedi!" uyarısı çıkar ve beklenen ürün kodunu gösterir; doğru ürünü alıp tekrar okutun.
- Barkod alanı otomatik odaklanır; el terminali okuyucusuyla doğrudan okutabilirsiniz, ayrıca tıklamanıza gerek yoktur.
- İlk barkod onaylandığında görev TASLAK durumdaysa otomatik olarak DEVAM EDİYOR'a geçer ve göreve adınız atanır.
- Satırlar her zaman sabit bir sırayla (FIFO) gelir; sistem hangi rafa gideceğinizi söyler, sıra atlanamaz.
- Barkodu okutmadan boş onaylayamazsınız; alan zorunludur.
- Yeşil "Toplama onaylandı!" mesajı her başarılı satırda kısa süre görünür; bir sonraki satıra otomatik geçtiğini doğrulayın.
