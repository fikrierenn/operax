---
name: sql-migration-writer
description: Operax için idempotent SQL migration yazar (docs/sql/schema_M*.sql veya db_objects.sql). CREATE TABLE / ALTER / SP CREATE OR ALTER pattern'lerini takip eder. Yedek almadan silme YASAK kuralını uygular.
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
user-invocable: true
model: inherit
---

# SQL Migration Writer (Operax)

## Ne zaman tetiklenir

- "Yeni schema yaz X modülü için"
- "Tablo X'e Y kolonu ekle (migration ile)"
- "Yeni SP yaz"
- Plan dosyasında "şema değişikliği" geçtiğinde
- Yeni feature için DB seed gerektiğinde

## Önkoşullar (her zaman)

1. **Mevcut tablo + kolon kontrolü:**
   ```bash
   dotnet run --project src/Operax.Cli -- query "SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='X'"
   ```

2. **UPDATE öncesi SELECT + WHERE zorunlu:**
   - Önce `SELECT` mevcut state'i oku
   - `UPDATE` daima `WHERE` ile
   - Bare update = veri felaket

3. **Yedek almadan silme YASAK:**
   - `DROP TABLE` / `TRUNCATE` / kapsamlı `DELETE` öncesi backup
   - Kullanıcı "yap" dese bile sor + doğrula

4. **Yeni migration için dosya konumu:**
   - Yeni tablo grubu: `docs/sql/schema_M<NN>_<konu>.sql`
   - Kolon ekleme: mevcut `schema_*.sql`'e idempotent IF NOT EXISTS bloğu
   - SP / View / TVF: `docs/sql/db_objects.sql` veya `db_objects_starter.sql`

## Şablon kütüphanesi

### Şablon 1 — Yeni tablo (idempotent)

```sql
-- =============================================================================
-- M<NN> — <Modül adı>: <Tablo amacı>
-- Tarih: YYYY-MM-DD
-- Plan: plans/NN-<slug>.md
-- Idempotent: çoklu çalıştırma güvenli
-- =============================================================================
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '<TabloAdi>')
BEGIN
    CREATE TABLE <TabloAdi> (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        -- ... domain alanları ...
        IsDeleted   BIT DEFAULT 0,
        CreatedAt   DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy   UNIQUEIDENTIFIER,
        UpdatedAt   DATETIME2 NULL,
        UpdatedBy   UNIQUEIDENTIFIER,
        CONSTRAINT FK_<TabloAdi>_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    CREATE INDEX IX_<TabloAdi>_Company ON <TabloAdi>(CompanyId) WHERE IsDeleted = 0;
    PRINT '<TabloAdi> tablosu olusturuldu.';
END
GO
```

**Kurallar:**
- PK: `UNIQUEIDENTIFIER` + `NEWID()` default
- Audit kolonları zorunlu: `CompanyId`, `IsDeleted`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`
- `GETUTCDATE()` (`GETDATE` değil — timezone)
- `NVARCHAR` Türkçe için
- Filtered index: `WHERE IsDeleted = 0` sık sorgu için
- FK constraint adı: `FK_<Cocuk>_<Ata>`

### Şablon 2 — Kolon ekleme (idempotent)

```sql
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = '<YeniKolon>' AND Object_ID = OBJECT_ID('<TabloAdi>'))
BEGIN
    ALTER TABLE <TabloAdi> ADD <YeniKolon> NVARCHAR(100) NULL;
    PRINT '<TabloAdi>.<YeniKolon> kolonu eklendi.';
END
GO

-- Backfill (gerekirse)
UPDATE <TabloAdi>
SET <YeniKolon> = 'default'
WHERE <YeniKolon> IS NULL;
GO
```

### Şablon 3 — Data UPDATE (WHERE zorunlu)

```sql
-- ÖNCE: SELECT ile mevcut state
SELECT TOP 10 Id, Kolon FROM <Tablo> WHERE <kosul>;

-- UPDATE: WHERE clause olmadan ASLA
UPDATE <Tablo>
SET Kolon = 'yeni'
WHERE <kosul>
  AND Kolon <> 'yeni';  -- idempotency

-- DOĞRULA
SELECT @@ROWCOUNT AS Etkilenen;
```

### Şablon 4 — Stored Procedure (Operax standart)

```sql
CREATE OR ALTER PROCEDURE dbo.sp_<Modul><Action>
    @CompanyId UNIQUEIDENTIFIER,
    @<Param1>  UNIQUEIDENTIFIER,
    @UserId    UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Guard clause: kayıt var mı?
        DECLARE @Status NVARCHAR(20);
        SELECT @Status = Status FROM <Tablo>
        WHERE Id = @<Param1> AND CompanyId = @CompanyId AND IsDeleted = 0;

        IF @Status IS NULL
            THROW 50001, N'<Belge> bulunamadı.', 1;

        IF @Status <> 'DRAFT'
            THROW 50002, N'Sadece DRAFT durumdaki belgeler işlenebilir.', 1;

        -- İş mantığı
        UPDATE <Tablo>
        SET Status      = 'POSTED',
            UpdatedAt   = GETUTCDATE(),
            UpdatedBy   = @UserId
        WHERE Id = @<Param1>;

        -- İlgili StockMovement / FinancialTransaction eklemeleri buraya

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
```

**Kritik:**
- `CREATE OR ALTER` (CREATE değil — idempotent)
- `SET NOCOUNT ON` + `SET XACT_ABORT ON` zorunlu
- `BEGIN TRY` / `BEGIN CATCH` ile rollback garanti
- `THROW 5xxxx, N'Türkçe mesaj', 1` iş kuralı hatalarında
- `@CompanyId`, `@UserId` zorunlu parametre
- C# tarafı `SqlException.Number 50000-59999` aralığını "iş kuralı" sayar, user'a gösterilebilir

### Şablon 5 — View (single source)

```sql
CREATE OR ALTER VIEW dbo.v_<ViewAdi> AS
SELECT
    p.Id, p.CompanyId,
    p.Name,
    -- agregat / computed
    ISNULL(SUM(...), 0) AS TotalAmount
FROM <Tablo> p
LEFT JOIN <Diger> d ON d.Id = p....
WHERE p.IsDeleted = 0
GROUP BY p.Id, p.CompanyId, p.Name;
GO
```

### Şablon 6 — Inline TVF

```sql
CREATE OR ALTER FUNCTION dbo.tvf_<Adi>
(
    @CompanyId UNIQUEIDENTIFIER,
    @<Param> ...
)
RETURNS TABLE
AS
RETURN
(
    SELECT ... FROM ... WHERE CompanyId = @CompanyId
);
GO
```

## Test edilecekler

1. **Çoklu çalıştırma:** Aynı migration 2-3 kez → hata yok, state aynı
2. **Migrate komutu:** `dotnet run --project src/Operax.Cli -- migrate` 0 hata
3. **SP smoke:** `operax-cli query "EXEC sp_X @Param=..."`
4. **Plan referansı:** Commit mesajında `(plan: NN)` (Tier 3 için)

## Yaygın hatalar (KAÇIN)

- ❌ `WHERE` olmadan UPDATE
- ❌ `DROP TABLE` öncesi backup yok
- ❌ Constraint adı sistem üretsin (`DF__abc123`)
- ❌ `GETDATE()` (UTC değil)
- ❌ `VARCHAR` Türkçe kolon (`NVARCHAR` zorunlu)
- ❌ Mevcut migration'ı güncelleme (idempotent IF NOT EXISTS bloğu ekle)
- ❌ SP'de `CREATE PROCEDURE` (idempotent değil — `CREATE OR ALTER`)
- ❌ Audit kolonları eksik (`CompanyId`, `IsDeleted`, `CreatedAt`)

## İlişkili

- `.Codex/rules/sql-conventions.md` — Tablo/kolon naming, parametreli sorgu
- `.Codex/rules/architecture.md` — SQL-first iş mantığı, atomic POST
- `.Codex/rules/document-immutability.md` — POSTED kilitleme SP guard
- `docs/MASTER_ROADMAP.md` — modül numaraları
