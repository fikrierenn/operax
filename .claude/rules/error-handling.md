# Hata Yönetimi (Result Pattern + Exception Disiplini)

## Temel Ayrım

**Beklenen sonuç (business outcome)** ≠ **gerçek exception (system failure)**.

| Durum | Tip | Mekanizma |
|---|---|---|
| "Belge bulunamadı", "yetki yok", "stok yetersiz" | Beklenen | **Result pattern** (`OpResult<T>` / null + audit) |
| DB connection loss, network timeout, JSON parse, file I/O fail | Gerçek exception | `try/catch` + log + generic kullanıcı mesajı |
| Bug / impossible state (null check fail, off-by-one) | Programming error | Fırlat, üst handler yakalasın |

**Anti-pattern:** Beklenen iş sonucu için `throw new Exception("Bulunamadı")` → exception flow control = expensive + okunabilirlik düşer.

## Result Pattern (Operax)

Operax'ta tercih: `Lib/OpResult.cs` (yazılacak — şu an PageModel'lerde inline `BadRequest`/`NotFound`):

```csharp
public record OpResult<T>(bool IsSuccess, T? Value, string? Error)
{
    public static OpResult<T> Ok(T value)        => new(true, value, null);
    public static OpResult<T> Fail(string error) => new(false, default, error);
}
```

### Kullanım — service / handler

```csharp
public async Task<OpResult<Guid>> CreatePoAsync(PoCreateDto dto, ICurrentUser user)
{
    if (string.IsNullOrWhiteSpace(dto.SupplierName))
        return OpResult<Guid>.Fail("Tedarikçi seçimi zorunlu.");

    using var conn = db.Open();
    var exists = await conn.ExecuteScalarAsync<bool>(
        "SELECT 1 FROM PurchaseOrderHeader WHERE OrderNo = @no AND CompanyId = @cid",
        new { no = dto.OrderNo, cid = company.Id });
    if (exists)
        return OpResult<Guid>.Fail("Bu evrak no zaten kullanılmış.");

    var id = Guid.NewGuid();
    await conn.ExecuteAsync("...");
    return OpResult<Guid>.Ok(id);
}
```

### PageModel — Result tüketme

```csharp
public async Task<IActionResult> OnPostAsync(PoCreateDto dto)
{
    var r = await _service.CreatePoAsync(dto, CurrentUser);
    if (!r.IsSuccess)
    {
        TempData["Error"] = r.Error;
        return Page();
    }
    TempData["Success"] = "Sipariş oluşturuldu.";
    return RedirectToPage("Details", new { id = r.Value });
}
```

## Exception Handling (gerçek arızalar)

```csharp
catch (SqlException sex)
{
    _logger.LogError(sex, "DB error: {Op}", "PoCreate");
    return BadRequest("Veritabanı işleminde hata oluştu.");
}
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
    throw; // propagate, clean shutdown
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected: {Op}", "PoCreate");
    return StatusCode(500, "Beklenmedik bir hata. Sistem yöneticisine bildirin.");
}
```

### Mutlak Kurallar

1. **Boş catch yasak.** `catch {}` veya `catch { _ = ex; }` → silent failure. Minimum `_logger.LogWarning(ex, ...)`.
2. **`ex.Message` kullanıcıya gösterme.** SqlException → connection string sızar. Generic Türkçe mesaj. Detay log'a.
3. **`Exception` yakalamadan önce spesifik.** `SqlException`, `JsonException`, `HttpRequestException` ayrı catch.
4. **`OperationCanceledException` rethrow** when `ct.IsCancellationRequested`.
5. **Async + CancellationToken.** Tüm `async Task` method'lar `CancellationToken ct = default` alır + downstream'e geçir.

## SP'lerden Gelen THROW

Operax SP'leri Türkçe hata mesajı fırlatır (`THROW 50001, N'Belge bulunamadı.', 1`). PageModel bunu yakalar ve kullanıcıya gösterir:

```csharp
try
{
    await conn.ExecuteAsync("sp_ReceivingPost", new { HeaderId = id, UserId = userId },
        commandType: CommandType.StoredProcedure);
}
catch (SqlException sex) when (sex.Number >= 50000 && sex.Number < 60000)
{
    // İş kuralı hatası — kullanıcıya gösterilebilir (SP Türkçe yazdı)
    TempData["Error"] = sex.Message;
    return RedirectToPage();
}
catch (SqlException sex)
{
    // Sistem hatası — log'a, generic mesaj
    _logger.LogError(sex, "ReceivingPost SQL error");
    TempData["Error"] = "Veritabanı hatası.";
    return RedirectToPage();
}
```

## Anti-pattern Listesi

| Anti-pattern | Doğrusu |
|---|---|
| `throw new Exception("Not found")` business case | `return OpResult.Fail("Bulunamadı")` |
| `catch (Exception) { return null; }` | Spesifik catch + log + result |
| `catch { /* sessiz */ }` | Minimum `_logger.LogWarning` |
| `TempData["Error"] = ex.Message` | Generic + log detayı |
| Result + Exception karışık | Tek strateji, layer tutarlı |

## İlişkili

- `.claude/rules/csharp-conventions.md` Exception Handling bölümü
- `.claude/rules/security-principles.md` `ex.Message` gizleme
- `.claude/rules/sql-conventions.md` SP THROW pattern
