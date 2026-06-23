# Kodlama Disiplini Kuralları

Bu dosya, Operax projesinin kod yazımı, kalitesi, güvenlik kuralları ve yorum satırı standartlarını tanımlar. Kodun temiz, okunabilir ve güvenli kalması için bu disiplin kurallarına uyulması zorunludur.

---

## 1. Kod İçi Türkçe Yorum Satırı Standardı (ZORUNLU)

Tüm `.cs` ve `.cshtml.cs` dosyalarında **Türkçe yorum** yazılması zorunludur.

### Yorum Yazılacak Alanlar ve Kurallar:
1.  **Metot Başları:** Her metodun en başında ne iş yaptığını açıklayan 1-2 satırlık açıklayıcı yorum bulunmalıdır.
2.  **İş Kuralları (Business Rules):** Kritik kontrol noktalarında hangi iş kuralının uygulandığı belirtilmelidir (`// İş kuralı: Negatif stok kontrolü yapılır`).
3.  **Karmaşık SQL Sorguları:** Sorgunun amacı ve varsa JOIN/FIFO öncelikleri açıklanmalıdır.
4.  **Transaction ve Guard Clause'lar:** Erken dönüşlerin (`return`) ve transaction bloklarının amacı belirtilmelidir.
5.  **İngilizce Yorum Yasağı:** Kod içinde İngilizce açıklama satırı yazılmamalıdır.

---

## 2. 80 Satır Eşiği ve Refactoring

1.  **Metot Uzunluk Sınırı (80 Satır):**
    *   Bir C# metodu (örneğin bir `OnPostAsync` veya helper metot) süslü parantezler dahil **80 satırı** aşmamalıdır.
    *   **Aksiyon:** 80 satırı aşan metotlar, mantıksal alt parçalara ayrıştırılarak `private` helper metotlara çıkarılmalıdır.
2.  **Drive-by Refactoring Yasağı:**
    *   Üzerinde çalışılan görevle ilgisi olmayan, spekülatif veya keyfi refactoring'ler (kod sadeleştirmeleri, altyapı değişiklikleri) yapılamaz.
    *   Yalnızca görev kapsamında değiştirilen kodlar temizlenir ve kurallara uygun hale getirilir.

---

## 3. Guard Clauses (Erken Dönüş)

*   Kod yazımında iç içe geçmiş `if` bloklarından (nested if) kaçınılmalıdır.
*   Geçersiz koşullar, yetki kontrolleri ve null durumlar metodun en başında **Guard Clause** kullanılarak elenmeli ve metottan erken dönülmelidir (`return`, `NotFound()`, `BadRequest()`).
*   Örnek:
    ```csharp
    // İş kuralı: Belge null ise işlem yapılmaz
    var header = await GetHeaderAsync(id);
    if (header == null) return NotFound();
    
    // İş kuralı: Belge onaylanmışsa tekrar onaylanamaz
    if (header.Status == DocStatus.Posted) return BadRequest("Belge zaten onaylanmış.");
    ```

---

## 4. Güvenlik ve NCalc Kullanımı

*   **DataTable.Compute() Yasağı:** Kullanıcı girdisi içeren matematiksel ve mantıksal formüllerin değerlendirilmesinde kesinlikle `DataTable.Compute()` kullanılmaz. Bu metot formula/SQL injection açıklarına sebep olur.
*   **NCalc Tercihi:** Formül değerlendirme işleri, tip-güvenli parametrelerle ve sandboxed ortamda çalışan **NCalc** kütüphanesi üzerinden yürütülmelidir.

---

## 5. Domain Uzmanı Skill'e Danışma (ZORUNLU — finans/muhasebe/mali-evrak)

Finans, muhasebe, mali-evrak veya stok-maliyet işleyişine dokunan kod/SP/şema yazmadan **ÖNCE** ilgili domain skill'e danışılır (kod yazmadan; hangi iş kuralı/mevzuat geçerli netleşsin diye). Bu skill'ler SALT-REHBER — kod yazmaz, doğrulanacak noktaları + kaynakları verir.

| Konu | Skill | Örnek tetik |
|---|---|---|
| TDHP hesap planı, borç/alacak yönü, çek/senet muhasebe kaydı, cari kapama, şüpheli alacak, yevmiye/mizan | **`muhasebe-mevzuat`** | "muhasebe kaydı", "hesap kodu", "çek muhasebe", "TDHP", GL · M5-M8/M11 |
| İade faturası, e-Belge senaryo, fatura iptal/düzelt, VUK tarih kuralı, irsaliye↔fatura, tevkifat, KDV iade | **`mali-evrak-mevzuat`** | "iade faturası", "e-fatura", "fatura iptal", "VUK" · M03/M04/M11/e-Belge |
| Mutabakat (GL↔subledger/banka/cari), varyans analizi, yevmiye doğruluğu, dönem kapanışı | **`mali-islem-akislari`** | "mutabakat", "varyans", "cari kapatma", "dönem kapanışı", "ters kayıt" · M11/M02 |

**Kural:** Bu konularda "kod doğru görünüyor" yetmez — mevzuat/işleyiş doğruluğu skill ile teyit edilmeden finans SP'si/ekranı yazılmaz. Statü kümesi / finansal araç tipi / evrak zinciri **modelleme** kararı için ayrıca `erp-isleyis-danismani` ajanı. (Bu skill'ler 2026-06-23 oturumunda üretildi.)
