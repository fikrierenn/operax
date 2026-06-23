using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Dictionary;

/// <summary>
/// Sözlük değer yönetimi (Plan 46 M0). ADR-02: kod-çıpalı tipler (SP/ledger CODE'a göre dallanır)
/// yalnız LABEL düzenlenir (Plan 42 vaadi) — değer EKLE/SİL YASAK (DOC_STATUS "POSTED" silinemez).
/// Dinamik tipler (AllowValueCrud=1: UOM/TAX_RATE/CURRENCY...) tam CRUD. CODE asla düzenlenmez (identity).
/// Her değişiklikte DictionaryLabels cache (Plan 42) temizlenir → etiket anında yansır.
/// </summary>
[Authorize(Roles = Operax.Web.Lib.Roles.Administrator)]
public class ValuesModel(Db db, ICurrentCompany company, ICurrentUser user, IMemoryCache cache) : PageModel
{
    private static readonly Guid SystemCompany = Guid.Empty; // global sözlük (CompanyId=0)
    private const string LabelCacheKey = "dict-labels-v1";   // Plan 42 DictionaryLabels cache anahtarı

    public Guid TypeId { get; set; }
    public DictionaryTypeDto? Type { get; set; }
    public IEnumerable<DictionaryValueDto> Values { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid typeId, CancellationToken ct = default)
    {
        TypeId = typeId;
        using var conn = db.Open();
        Type = await LoadTypeAsync(conn, typeId, ct);
        if (Type == null) return NotFound();
        Values = await LoadValuesAsync(conn, typeId, ct);
        return Page();
    }

    // Label düzenleme — TÜM tiplerde izinli (kod-çıpalı dahil; CODE değişmez, yalnız görünen ad/sıra).
    public async Task<IActionResult> OnPostEditAsync(Guid typeId, Guid valueId, string nameTr, string? nameEn, int orderNo, CancellationToken ct = default)
    {
        // İş kuralı: NameTr zorunlu (görünen etiket boş olamaz)
        if (string.IsNullOrWhiteSpace(nameTr)) { TempData["Error"] = "Türkçe ad zorunlu."; return RedirectToPage(new { typeId }); }

        using var conn = db.Open();
        // Yazma kapsamı okuma ile simetrik: yalnız global (CompanyId=0) veya bu şirketin değeri (security-principles §8)
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE DictionaryValue SET NameTr = @NameTr, NameEn = @NameEn, OrderNo = @OrderNo, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
            WHERE Id = @ValueId AND TypeId = @TypeId AND IsDeleted = 0 AND (CompanyId = @CompanyId OR CompanyId = @Global)",
            new { ValueId = valueId, TypeId = typeId, NameTr = nameTr.Trim(), NameEn = nameEn?.Trim(), OrderNo = orderNo, UserId = user.Id, CompanyId = company.Id, Global = SystemCompany }, cancellationToken: ct));
        InvalidateLabelCache();
        TempData["Success"] = "Değer güncellendi.";
        return RedirectToPage(new { typeId });
    }

    // Aktif/Pasif — TÜM tiplerde izinli (görünürlük; kod silinmez).
    public async Task<IActionResult> OnPostToggleActiveAsync(Guid typeId, Guid valueId, CancellationToken ct = default)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE DictionaryValue SET IsActive = CASE WHEN ISNULL(IsActive,1)=1 THEN 0 ELSE 1 END, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
            WHERE Id = @ValueId AND TypeId = @TypeId AND IsDeleted = 0 AND (CompanyId = @CompanyId OR CompanyId = @Global)",
            new { ValueId = valueId, TypeId = typeId, UserId = user.Id, CompanyId = company.Id, Global = SystemCompany }, cancellationToken: ct));
        InvalidateLabelCache();
        return RedirectToPage(new { typeId });
    }

    // Yeni değer — YALNIZ dinamik tiplerde (AllowValueCrud=1). Kod-çıpalı tipe yeni kod eklenemez.
    public async Task<IActionResult> OnPostAddAsync(Guid typeId, string code, string nameTr, string? nameEn, int orderNo, CancellationToken ct = default)
    {
        using var conn = db.Open();
        if (!await IsCrudAllowedAsync(conn, typeId, ct)) { TempData["Error"] = "Bu sistem tipine yeni değer eklenemez (yalnız etiket düzenlenir)."; return RedirectToPage(new { typeId }); }
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(nameTr)) { TempData["Error"] = "Kod ve Türkçe ad zorunlu."; return RedirectToPage(new { typeId }); }

        // İş kuralı: aynı tip+kod (şirket kapsamında) tekrar eklenemez
        var dup = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(*) FROM DictionaryValue WHERE TypeId=@TypeId AND Code=@Code AND IsDeleted=0
              AND (CompanyId=@CompanyId OR CompanyId=@Global)",
            new { TypeId = typeId, Code = code.Trim().ToUpperInvariant(), CompanyId = company.Id, Global = SystemCompany }, cancellationToken: ct));
        if (dup > 0) { TempData["Error"] = "Bu kod zaten tanımlı."; return RedirectToPage(new { typeId }); }

        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive, CreatedAt, IsDeleted)
            VALUES (NEWID(), @CompanyId, @TypeId, @Code, @NameTr, @NameEn, @OrderNo, 1, GETUTCDATE(), 0)",
            new { CompanyId = company.Id, TypeId = typeId, Code = code.Trim().ToUpperInvariant(), NameTr = nameTr.Trim(), NameEn = nameEn?.Trim(), OrderNo = orderNo }, cancellationToken: ct));
        InvalidateLabelCache();
        TempData["Success"] = "Değer eklendi.";
        return RedirectToPage(new { typeId });
    }

    // Soft delete — YALNIZ dinamik tiplerde. Kod-çıpalı değer silinemez (ledger/SP kırılır).
    public async Task<IActionResult> OnPostDeleteAsync(Guid typeId, Guid valueId, CancellationToken ct = default)
    {
        using var conn = db.Open();
        if (!await IsCrudAllowedAsync(conn, typeId, ct)) { TempData["Error"] = "Bu sistem tipinden değer silinemez (yalnız pasife alınabilir)."; return RedirectToPage(new { typeId }); }

        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE DictionaryValue SET IsDeleted = 1, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
            WHERE Id = @ValueId AND TypeId = @TypeId AND IsDeleted = 0 AND (CompanyId = @CompanyId OR CompanyId = @Global)",
            new { ValueId = valueId, TypeId = typeId, UserId = user.Id, CompanyId = company.Id, Global = SystemCompany }, cancellationToken: ct));
        InvalidateLabelCache();
        TempData["Success"] = "Değer silindi.";
        return RedirectToPage(new { typeId });
    }

    // --- yardımcılar ---
    private async Task<bool> IsCrudAllowedAsync(System.Data.IDbConnection conn, Guid typeId, CancellationToken ct)
        => await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT AllowValueCrud FROM DictionaryType WHERE Id = @Id", new { Id = typeId }, cancellationToken: ct));

    private void InvalidateLabelCache() => cache.Remove(LabelCacheKey);

    private async Task<DictionaryTypeDto?> LoadTypeAsync(System.Data.IDbConnection conn, Guid typeId, CancellationToken ct)
        => await conn.QueryFirstOrDefaultAsync<DictionaryTypeDto>(new CommandDefinition(@"
            -- isolation-guard:ignore: DictionaryType global sistem verisi (IsSystem); yalnız Administrator
            SELECT Id, Code, NameTr, NameEn, AllowValueCrud FROM DictionaryType WHERE Id = @Id",
            new { Id = typeId }, cancellationToken: ct));

    private async Task<IEnumerable<DictionaryValueDto>> LoadValuesAsync(System.Data.IDbConnection conn, Guid typeId, CancellationToken ct)
        => await conn.QueryAsync<DictionaryValueDto>(new CommandDefinition(@"
            -- Global (CompanyId=0) + şirket-özel değerler birlikte (kod-çıpalı tipler global'de yaşar)
            -- isolation-guard:ignore: global sistem sözlüğü + şirket; yalnız Administrator
            SELECT Id, Code, NameTr, NameEn, OrderNo, ISNULL(IsActive,1) AS IsActive,
                   CAST(CASE WHEN CompanyId = @Global THEN 1 ELSE 0 END AS BIT) AS IsGlobal
            FROM DictionaryValue
            WHERE TypeId = @TypeId AND IsDeleted = 0 AND (CompanyId = @CompanyId OR CompanyId = @Global)
            ORDER BY OrderNo, NameTr",
            new { TypeId = typeId, CompanyId = company.Id, Global = SystemCompany }, cancellationToken: ct));

    public record DictionaryTypeDto(Guid Id, string Code, string NameTr, string NameEn, bool AllowValueCrud);
    public record DictionaryValueDto(Guid Id, string Code, string NameTr, string NameEn, int OrderNo, bool IsActive, bool IsGlobal);
}
