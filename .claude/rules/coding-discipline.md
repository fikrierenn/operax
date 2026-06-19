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
