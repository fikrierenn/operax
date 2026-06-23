# C# Konvansiyonları (Operax)

## Dosya Boyutu Disiplini

**Kural:** Yeni yazılan/düzenlenen C# dosyaları **300 satırın altında** kalmalı. **500 satır kırmızı çizgi** — bir sonraki commit'te split zorunlu.

**Neden:** Tek dosyada iç içe 7 feature merge conflict + test zorluğu + onboarding yükü üretir. Solo dev için bile context switch maliyeti yüksek.

**Uygulama:**
- PageModel: SP çağrıları çoksa Service Layer'a böl (örn. `PurchaseOrderService`)
- DTO çoksa ayrı dosya (record'lar `Lib/Dtos.cs`'te)
- 5+ public helper varsa scope bazlı ayır
- **Legacy dosyalar:** TODO + plan, touch ettikçe azalt

## Razor PageModel Action'ları

- **Async:** `public async Task<IActionResult> OnPostAsync(...)`
- **Authorize:** Sayfa-level `[Authorize]` (varsayılan), Admin'de `[Authorize(Roles = "Administrator")]`
- **POST handler:** AntiForgeryToken view'da, handler async `Task<IActionResult>` döner
- **Dönüş:** DTO/record (Entity yerine — mass assignment riski)

## Dapper (EF değil)

Operax EF kullanmaz. Tüm veri erişimi Dapper üzerindendir.

```csharp
using var conn = db.Open();
var rows = await conn.QueryAsync<OrderDto>(
    "SELECT Id, OrderNo FROM PurchaseOrderHeader WHERE CompanyId = @CompanyId",
    new { CompanyId = company.Id });
```

- `using var conn = db.Open();` — bağlantı dispose garanti; **`db.Open()` AÇIK bağlantı döner** (eager `conn.Open()`). 
- **Transaction → `conn.Open()` şart:** Dapper `QueryAsync`/`ExecuteAsync` kapalı bağlantıyı OTOMATİK açar ama `conn.BeginTransaction()` AÇMAZ → kapalı bağlantıda `InvalidOperationException: Bağlantı kapalı`. (2026-06-24 bug: `Db.Open()` connection'ı açmıyordu, sorgular çalışıyordu ama 5 transaction handler'ı sessizce kırıktı.) Yeni bir connection-üreten yol eklersen transaction'dan ÖNCE açıldığından emin ol.
- Parametreli sorgular (string concat YASAK — `.claude/rules/sql-conventions.md`)
- `QueryAsync`, `ExecuteAsync`, `QuerySingleAsync`, `QueryMultipleAsync` — sync yok
- Stored Procedure çağrısı:

```csharp
await conn.ExecuteAsync("sp_ReceivingPost",
    new { HeaderId = id, UserId = userId },
    commandType: CommandType.StoredProcedure);
```

## Exception Handling

- **Spesifik exception önce, generic en son:**
  ```csharp
  catch (SqlException sqlEx)
  {
      _logger.LogError(sqlEx, "DB hatası: {Op}", "ReceivingPost");
      return BadRequest("Veritabanı hatası oluştu.");
  }
  catch (Exception ex)
  {
      _logger.LogError(ex, "Beklenmeyen: {Op}", "ReceivingPost");
      return StatusCode(500, "Beklenmeyen bir hata.");
  }
  ```
- **User'a `ex.Message` GÖSTERME** — stack/connection string sızar
- **Sessiz `catch {}` yasak** — minimum `_logger.LogWarning`
- Detay: `.claude/rules/error-handling.md`

## Nullability

- `<Nullable>enable</Nullable>` aktif
- `?` ve `!` doğru kullan
- `string?` vs `string` tutarlı — default `string.Empty` tercih

## Async / await

- PageModel handler → `async Task<IActionResult>`
- **`async void` yasak** (event handler hariç)
- `ConfigureAwait(false)` library code'da, ASP.NET Core'da gerek yok

## DI + Primary Constructor (C# 12+)

```csharp
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> log) : PageModel
{
    public async Task OnGetAsync()
    {
        log.LogInformation("Dashboard yüklendi: {CompanyId}", company.Id);
    }
}
```

- Property injection **yasak**
- `IHttpClientFactory` — `new HttpClient()` asla
- `DateTime.UtcNow` → `DateTime.Now` asla (timezone)

## Audit Logging

Kritik aksiyonlarda `AuditLog` tablosuna yaz:
```csharp
await conn.ExecuteAsync(@"
    INSERT INTO AuditLog (Id, CompanyId, UserId, Action, EntityType, EntityId, Timestamp)
    VALUES (NEWID(), @CompanyId, @UserId, 'PO_POSTED', 'PurchaseOrder', @Id, GETUTCDATE())",
    new { CompanyId = company.Id, UserId = user.Id, Id = poId });
```

## Naming

- **PascalCase:** class, method, property, public field
- **camelCase:** local variable, parameter
- **_underscorePrefix:** private readonly field (legacy — primary ctor'da kaldırılır)
- **Interface:** `I` prefix (`ICurrentCompany`)
- **Async method:** `Async` suffix
- **Yanlış anlam çıkaran kısaltma YASAK:** Değişken/kısaltma adı argo veya farklı bir İngilizce kelimeye dönüşmemeli. `SqlException` → **`sqlEx`** (asla `sex`). Net olmayan kısaltma yerine açık ad: `ex`, `sqlEx`, `httpEx`.

## Modern C# 13 / .NET 10

Tercih edilen modern pattern'ler:

| Eski | Yeni | Ne zaman |
|---|---|---|
| Constructor + `_x = x` boilerplate | **Primary constructor** | Her DI'lı class |
| `new List<string>() { "a", "b" }` | **Collection expression** `["a", "b"]` | List/array init |
| DTO için class | **`record`** | Flat data taşıma |
| Küçük value object | **`readonly record struct`** | ≤16 byte, immutable |

### Karar matrisi
- **DTO / ViewModel** → `record`
- **DI'lı service** → primary constructor
- **Default:** `sealed` ekle
- **Immutability:** `required`, `init` ile invalid state imkânsız

### Anti-pattern
- Manuel backing field (`field` kw varken)
- `var` belirsizken (`var x = GetThing()` — `Thing x` tercih)
- Deeply nested pattern match (>2 seviye) — ayrı method'a çıkar
- `record` yerine tuple ile domain type taşıma
- `new HttpClient()` — `IHttpClientFactory` zorunlu

## İlişkili

- `.claude/rules/coding-discipline.md` — Türkçe yorum, 80-satır metot
- `.claude/rules/sql-conventions.md` — Parametreli sorgu
- `.claude/rules/error-handling.md` — Result pattern
- `.claude/rules/architecture.md` — Dapper, single-tenant
