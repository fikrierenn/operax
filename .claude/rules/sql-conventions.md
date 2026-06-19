# SQL ve Veritabanı Standartları

Bu dosya, Operax veritabanı şeması, T-SQL sorgu standartları, Stored Procedure kuralları ve veritabanı şema güncellemeleri standartlarını tanımlar.

---

## 1. Tablo ve Kolon İsimlendirme Standartları

*   **İngilizce PascalCase:** Tüm tablo, kolon ve index isimleri İngilizce PascalCase olarak tanımlanır.
    *   Doğru: `StockMovement`, `WarehouseId`, `QtyBase`, `CreatedAt`
    *   Yanlış: `stok_hareketleri`, `warehouse_id`, `QuantityBase`
*   **Zorunlu Kolonlar:** Her veritabanı tablosunda aşağıdaki sütunların bulunması zorunludur:
    *   `CompanyId` (UNIQUEIDENTIFIER)
    *   `IsDeleted` (BIT, default 0)
    *   `CreatedAt` (DATETIME2, default GETUTCDATE())
    *   `CreatedBy` (NVARCHAR(450))
    *   `UpdatedAt` (DATETIME2, NULL)
    *   `UpdatedBy` (NVARCHAR(450), NULL)
*   **Birincil Anahtar (PK):** PK kolonları her zaman `UNIQUEIDENTIFIER` tipinde olmalı ve default değeri `NEWID()` veya C# tarafından üretilen Guid olmalıdır.

---

## 2. T-SQL Sorgu Kuralları

1.  **Her Zaman Parametreli Sorgu (SQL Injection Koruması):**
    *   SQL injection açıklarını önlemek ve veritabanı plan cache'ini verimli kullanmak adına tüm sorgular Dapper parametreleri üzerinden geçirilmeli, string birleştirme (`+` veya `$""`) yapılmamalıdır.
2.  **SARGable Arama Koşulları:**
    *   `WHERE` koşullarında index kullanımını bozacak fonksiyonlar kullanılmamalıdır.
    *   Yanlış: `WHERE YEAR(CreatedAt) = 2026`
    *   Doğru: `WHERE CreatedAt >= '2026-01-01' AND CreatedAt < '2027-01-01'`
3.  **Büyük Sorgularda JOIN Optimizasyonu:**
    *   JOIN işlemleri yapılırken indeksli kolonlar üzerinden eşleşme yapılmalı, alt sorgu (subquery) yerine JOIN veya `WITH` (CTE) yapıları tercih edilmelidir.

---

## 3. Stored Procedure Standartları

*   **Kullanım Alanı:** Onay işlemleri (Post/Approve), çok adımlı veri yazma operasyonları ve atomik transaction gerektiren karmaşık iş mantıkları Stored Procedure (SP) ile yazılır.
*   **SP İçi Standartlar:**
    *   Her SP dosyasının en başında `SET XACT_ABORT ON;` ifadesi bulunmalıdır (hata durumunda transaction'ın otomatik rollback edilmesini garanti eder).
    *   SP içinde mutlaka `BEGIN TRY...END TRY` ve `BEGIN CATCH...END CATCH` blokları ile hata yönetimi yapılmalıdır.
    *   SP parametrelerinde `@HeaderId`, `@CompanyId` ve `@UserId` bulunması zorunludur.
*   **Konum:** Yazılan SP'ler `docs/sql/db_objects.sql` dosyası içerisine `CREATE OR ALTER` syntax'ı ile eklenir.

---

## 4. Şema Güncellemeleri (Migration)

*   **Migration Yönetimi:**
    *   Yeni bir tablo veya kolon eklendiğinde, ilgili SQL script'i `src/Operax.Web/` altındaki veya projenin ana şema klasöründeki migration listesine eklenmelidir.
    *   `operax-cli migrate` komutu çalıştırılarak sırasıyla şema ve nesne tanımları veritabanına uygulanır.
