---
name: code-quality-checklist
description: Operax kod yazarken/düzenlerken otomatik danış. Yaygın hataları yazım sırasında önle (handoff taraması son doğrulama olsun). "checklist", "kontrol", "kalite kontrol" komutlarıyla çağrılır.
allowed-tools: Read, Grep, Glob
user-invocable: true
model: inherit
---

# Operax Code Quality Checklist

**Amaç:** Taramalarda tekrar tekrar çıkan hataları yazım sırasında önle. İlk savunma hattı bu skill, agent'lar son doğrulama.

---

## 1. Exception Handling (EN KRİTİK)

### YASAK Pattern'ler

```csharp
// YASAK 1: Bare catch
catch { return defaultValue; }
catch { /* yorum */ }

// YASAK 2: _ = ex discard, logger yok
catch (Exception ex) { _ = ex; TempData["Error"] = "Hata"; }

// YASAK 3: ex.Message kullanıcıya sızdırma
catch (Exception ex) { return $"Hata: {ex.Message}"; }
TempData["Error"] = $"İşlem başarısız: {ex.Message}";

// YASAK 4: ExecuteAsync/Dapper try/catch'siz
await conn.ExecuteAsync(sql, p); // exception sızar caller'a
```

### DOĞRU Pattern'ler

```csharp
// DOĞRU 1: Spesifik catch + logger + generic mesaj
catch (SqlException sex)
{
    _logger.LogError(sex, "ReceivingPost SQL: {Id}", id);
    return BadRequest("Veritabanı işleminde hata.");
}
catch (Exception ex)
{
    _logger.LogError(ex, "ReceivingPost beklenmeyen: {Id}", id);
    return StatusCode(500, "Beklenmedik bir hata.");
}

// DOĞRU 2: SP-level THROW yakalama (50000-59999 = iş kuralı)
catch (SqlException sex) when (sex.Number >= 50000 && sex.Number < 60000)
{
    TempData["Error"] = sex.Message; // SP Türkçe yazdı, user görebilir
    return RedirectToPage();
}
```

### ILogger Inject (Primary Constructor)

```csharp
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    public async Task OnGetAsync()
    {
        logger.LogInformation("Sayfa yüklendi: {CompanyId}", company.Id);
    }
}
```

---

## 2. Güvenlik

### SQL Injection (kritik — Dapper Parametre)

```csharp
// YASAK
var sql = "SELECT * FROM PO WHERE Id = '" + id + "'";
await conn.QueryAsync(sql);

// DOĞRU
await conn.QueryAsync("SELECT * FROM PO WHERE Id = @Id", new { Id = id });

// DOĞRU - dinamik WHERE
var sql = new StringBuilder("WHERE CompanyId = @CompanyId");
var parms = new DynamicParameters();
parms.Add("CompanyId", company.Id);
if (Tab != "all")
{
    sql.Append(" AND Status = @Status");
    parms.Add("Status", Tab);
}
```

### Mass Assignment

```csharp
// YASAK: Entity direkt bind
[BindProperty] public PurchaseOrderHeader Order { get; set; }

// DOĞRU: DTO bind + manuel atama
[BindProperty] public PoCreateDto Form { get; set; }

public async Task<IActionResult> OnPostAsync()
{
    await conn.ExecuteAsync(@"
        INSERT INTO PurchaseOrderHeader (Id, CompanyId, OrderNo, OrderDate, ...)
        VALUES (NEWID(), @CompanyId, @OrderNo, @OrderDate, ...)",
        new { CompanyId = company.Id, Form.OrderNo, Form.OrderDate, ... });
}
```

### CSRF

```cshtml
<!-- Razor Pages otomatik AntiForgery, yine de açık yaz: -->
<form method="post">
    @Html.AntiForgeryToken()
    ...
</form>
```

### Authorize

```csharp
[Authorize]                                  // tüm authenticated user
[Authorize(Roles = "Administrator")]         // sadece admin
```

### Open Redirect

```csharp
if (!string.IsNullOrEmpty(returnUrl)
    && Url.IsLocalUrl(returnUrl)
    && returnUrl.StartsWith("/")
    && !returnUrl.StartsWith("//")
    && !returnUrl.StartsWith("/\\"))
{
    return Redirect(returnUrl);
}
```

---

## 3. CompanyId Disiplini

**Tüm SELECT/UPDATE/DELETE'te `WHERE CompanyId = @CompanyId` zorunlu** — single-tenant olsa bile multi-tenant test ortamı için.

```csharp
// YASAK
await conn.QueryAsync<PoDto>("SELECT * FROM PurchaseOrderHeader WHERE Id = @Id", new { Id = id });

// DOĞRU
await conn.QueryAsync<PoDto>(
    "SELECT * FROM PurchaseOrderHeader WHERE Id = @Id AND CompanyId = @CompanyId",
    new { Id = id, CompanyId = company.Id });
```

---

## 4. Evrak Bütünlüğü (Document Lock)

POSTED bir belge child kayıt varsa **edit engellenir**. SP-level + UI-level guard:

```csharp
public async Task<IActionResult> OnPostEditLineAsync(Guid lineId, ...)
{
    using var conn = db.Open();

    // Guard: bağlı Receiving varsa kilitle
    var hasReceiving = await conn.ExecuteScalarAsync<bool>(
        "SELECT 1 FROM ReceivingHeader WHERE PurchaseOrderId = @poId AND IsDeleted = 0",
        new { poId });
    if (hasReceiving)
        return BadRequest("Belge kilitli: bu siparişe mal kabul yapılmış.");

    // ...
}
```

Detay: `.Codex/rules/document-immutability.md`.

---

## 5. Türkçe UI (UTF-8 Zorunlu)

### YASAK: ASCII sadeleştirme

```
"bulunamadi"  → "bulunamadı"
"Deger"       → "Değer"
"Duzenle"     → "Düzenle"
"olusturuldu" → "oluşturuldu"
"guncellendi" → "güncellendi"
"gecersiz"    → "geçersiz"
"secilmeli"   → "seçilmeli"
```

### Çift Dil

```csharp
// L.cs helper
string msg = L.T("Kaydedildi.", "Saved.");
```

```cshtml
<button>@L.T("Kaydet", "Save")</button>
```

---

## 6. Kod Kalitesi

### Console.WriteLine Yasak

```csharp
// YASAK
Console.WriteLine($"Item: {id}");

// DOĞRU
_logger.LogDebug("Item: {Id}", id);
```

### DateTime.Now Yasak

```csharp
// YASAK
var now = DateTime.Now; // timezone sorunu

// DOĞRU
var now = DateTime.UtcNow;
```

### Transaction Disiplini (Multi-Operation)

```csharp
// YASAK: İki ayrı çağrı, ilki başarılı, ikincisi crash
await conn.ExecuteAsync("INSERT INTO ...");
await conn.ExecuteAsync("INSERT INTO ..."); // crash → partial save

// DOĞRU: SP içinde transaction, veya C# tarafında System.Transactions
// Operax tercih: SP içinde BEGIN TRANSACTION (sp_ReceivingPost pattern'i)
```

---

## 7. CSS / UI

### Inline Style Yasak (renk/font için)

```cshtml
<!-- YASAK -->
<span style="color: red;">Hata</span>

<!-- DOĞRU -->
<span class="text-danger">Hata</span>
```

İzinli: tek-seferlik LAYOUT grid (`style="display:grid;grid-template-columns:..."`).

Detay: `.Codex/rules/inline-style-guard.md`.

### Tailwind Utility Salatası Yasak

```cshtml
<!-- YASAK -->
<div class="flex flex-col gap-2 px-4 py-2 bg-white border rounded shadow-sm">...</div>

<!-- DOĞRU - semantic class -->
<div class="card">...</div>
```

---

## 8. Dosya Yazarken Checklist

### Yeni `.cs` dosyası

- [ ] `ILogger<T>` primary ctor'da var mı?
- [ ] Her catch'te `_logger.LogError/Warning`?
- [ ] Bare `catch {}` veya `catch { return default; }` yok mu?
- [ ] `ex.Message` user'a sızmıyor mu?
- [ ] Dapper sorgusunda her parametre `@name` ve `new { name = ... }`?
- [ ] `CompanyId` filtresi her WHERE'de?
- [ ] POST handler'da AntiForgery (Razor Pages otomatik ama göster)?
- [ ] DTO/record bind, Entity değil?
- [ ] Console.WriteLine YOK?
- [ ] DateTime.UtcNow (`.Now` değil)?
- [ ] Türkçe UTF-8 (ı/İ/ş/ğ/ü/ö/ç)?
- [ ] 80 satır metot limiti aşılmadı?

### Yeni `.cshtml`

- [ ] `@Html.AntiForgeryToken()` her POST form'da?
- [ ] `@Html.Raw` user input'a değil, helper'a?
- [ ] Inline JS'de user data interpolasyonu YOK?
- [ ] TempData null guard: `@if (TempData["Error"] is string err)`?
- [ ] Türkçe UTF-8?
- [ ] `<div class="page">` wrapper + `data-screen-label`?
- [ ] `_PageHeader` partial kullanıldı?
- [ ] Inline style sadece layout için?
- [ ] `L.T("tr", "en")` çift dil?

### Yeni SP

- [ ] `SET XACT_ABORT ON` ve `SET NOCOUNT ON`?
- [ ] BEGIN TRY / BEGIN CATCH bloğu?
- [ ] Parametreler: `@CompanyId`, `@UserId` zorunlu?
- [ ] `THROW 50001, N'Türkçe mesaj.', 1` iş kuralı hatalarında?
- [ ] CREATE OR ALTER PROCEDURE (idempotent)?
- [ ] `BEGIN TRANSACTION` / `COMMIT` / `ROLLBACK` cycle?

---

## 9. Bu Skill Nasıl Kullanılır

1. **Kod yazarken:** §8 checklist'i kontrol et
2. **Yeni servis/PageModel:** §1 ILogger + exception pattern kopyala
3. **View:** §7 inline style + §5 Türkçe + §2 güvenlik
4. **Commit öncesi:** 3 paralel agent (code-reviewer + silent-failure-hunter + security-reviewer) — bu skill yazım sırası, agent son doğrulama
5. **SP yazarken:** §8 SP checklist + `sql-migration-writer` skill

## İlişkili

- `.Codex/rules/csharp-conventions.md`
- `.Codex/rules/error-handling.md`
- `.Codex/rules/security-principles.md`
- `.Codex/rules/sql-conventions.md`
- `.Codex/rules/ui-standard.md`
- `.Codex/rules/document-immutability.md`
- `.Codex/rules/inline-style-guard.md`
