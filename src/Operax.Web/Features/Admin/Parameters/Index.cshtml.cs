using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Parameters;

[Authorize(Roles = Operax.Web.Lib.Roles.Administrator)]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    public List<ParameterDto> Parameters { get; set; } = [];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    // Parametre listesini veritabanından yükler
    public async Task OnGetAsync(CancellationToken ct = default)
    {
        try
        {
            using var conn = db.Open();
            var page = Page < 1 ? 1 : Page;
            const string sql = @"
                SELECT Id, ModuleCode, Code, Value, Description
                FROM Parameter
                WHERE CompanyId = @CompanyId AND IsDeleted = 0
                ORDER BY ModuleCode, Code
                OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

                SELECT COUNT(1) FROM Parameter WHERE CompanyId = @CompanyId AND IsDeleted = 0;";
            using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
            Parameters = (await grid.ReadAsync<ParameterDto>()).ToList();
            FilteredCount = await grid.ReadSingleAsync<int>();
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Parametre listesi veri yükleme hatası");
            TempData["Error"] = "Veriler yüklenirken bir hata oluştu.";
        }
    }

    // Mevcut parametre değeri güncelle
    public async Task<IActionResult> OnPostSaveAsync(Guid id, string value, string description, CancellationToken ct = default)
    {
        try
        {
            using var conn = db.Open();
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE Parameter SET Value = @Value, Description = @Description, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, Value = value, Description = description, CompanyId = company.Id }, cancellationToken: ct));
            TempData["Success"] = "Parametre güncellendi.";
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Parametre güncelleme hatası: {Id}", id);
            TempData["Error"] = "Parametre güncellenirken bir hata oluştu.";
        }
        return RedirectToPage();
    }

    // Yeni parametre ekle
    public async Task<IActionResult> OnPostCreateAsync(string moduleCode, string code, string value, string description, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(value))
        {
            TempData["Error"] = "Kod ve Değer zorunludur.";
            return RedirectToPage();
        }

        try
        {
            using var conn = db.Open();

            // Aynı CompanyId + Code kombinasyonu var mı kontrol
            var exists = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM Parameter WHERE CompanyId = @CompanyId AND Code = @Code AND IsDeleted = 0",
                new { CompanyId = company.Id, Code = code }, cancellationToken: ct));

            if (exists > 0)
            {
                TempData["Error"] = $"'{code}' kodu zaten tanımlı.";
                return RedirectToPage();
            }

            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO Parameter (Id, CompanyId, ModuleCode, Code, Value, Description)
                VALUES (NEWID(), @CompanyId, @ModuleCode, @Code, @Value, @Description)",
                new { CompanyId = company.Id, ModuleCode = moduleCode?.ToUpper() ?? "SYS", Code = code.ToUpper(), Value = value, Description = description }, cancellationToken: ct));

            TempData["Success"] = $"'{code}' parametresi oluşturuldu.";
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Parametre oluşturma hatası: {Code}", code);
            TempData["Error"] = "Parametre oluşturulurken bir hata oluştu.";
        }
        return RedirectToPage();
    }

    // Parametre sil (soft delete)
    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            using var conn = db.Open();
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE Parameter SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct));
            TempData["Success"] = "Parametre silindi.";
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Parametre silme hatası: {Id}", id);
            TempData["Error"] = "Parametre silinirken bir hata oluştu.";
        }
        return RedirectToPage();
    }

    public record ParameterDto(Guid Id, string ModuleCode, string Code, string Value, string? Description);
}
