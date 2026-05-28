# Git ve Commit Disiplini Kuralları

Bu dosya, Operax projesinin Git versiyon kontrol sistemi kullanımını, commit disiplinini ve uncommitted dosya limitlerini tanımlar. Temiz bir kod geçmişi ve güvenli geri alma (rollback) noktaları oluşturmak için bu kurallar uygulanır.

---

## 1. Branch ve Commit Yaklaşımı

1.  **Branch-per-Ask (İş Başına Dal):**
    *   Kullanıcıdan gelen her bağımsız geliştirme talebi veya hata düzeltme isteği için yeni bir branch oluşturulabilir veya ayrıştırılabilir.
    *   Büyük risk taşıyan deneysel işler asla doğrudan ana dal (`main`/`master`) üzerinde yapılmaz.
2.  **Save-Point Commits (Güvenli Nokta Commit'leri):**
    *   Bir dosya veya alt özellik başarıyla yazıldığında ve derleme/testler yeşile döndüğünde, iş tamamen bitmemiş olsa bile hemen bir commit atılır.
    *   Bu commit'ler `WIP: [Modül] Açıklama` şeklinde isimlendirilir.
    *   **Amaç:** Olası bir yanlış koda girildiğinde veya tıkandığında, tüm işi kaybetmeden en son çalışan "güvenli noktaya" geri dönebilmektir.
3.  **Kullanıcı Onayı Olmadan Otomatik Commit Yasağı:**
    *   Kullanıcı açıkça talimat vermedikçe veya oturum sonu handoff protokolünde mutabık kalınmadıkça otomatik olarak uzak sunucuya commit/push yapılmaz.

---

## 2. 15 Dosya Eşiği ve Commit-Split Disiplini

1.  **Stop-the-World Eşiği (15 Dosya):**
    *   `git status --porcelain | wc -l` sonucu **15** veya daha fazla çıkarsa, bu durum "aşırı birikmiş uncommitted kod" anlamına gelir.
    *   **Kural:** Uncommitted dosya sayısı 15'i aştığında, yeni bir geliştirme veya görev kesinlikle başlatılamaz.
2.  **Planlı Commit-Split (Bölerek Commit):**
    *   Biriken değişiklikler mantıksal paketlere (bucket) bölünür.
    *   Örnek bölme sırası:
        1.  `db/` veya `sql/` şema değişiklikleri ve migrasyonlar.
        2.  `Lib/` altındaki ortak kütüphane ve DTO değişiklikleri.
        3.  Backend logic, PageModel (`.cshtml.cs`) değişiklikleri.
        4.  Arayüz, View (`.cshtml`) ve JS/CSS değişiklikleri.
        5.  Dokümantasyon, TODO ve PLAN güncellemeleri.
    *   Her paket sırayla, anlamlı commit mesajlarıyla commit edilir.
    *   Commit'ler tamamlanıp temiz bir çalışma alanı elde edildikten sonra yeni göreve başlanır.
