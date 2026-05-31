using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Parameters;

[Authorize(Roles = "Administrator")]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    public List<ParameterDto> Parameters { get; set; } = [];

    // Parametre listesini veritabanından yükler
    public async Task OnGetAsync()
    {
        try
        {
            using var conn = db.Open();
            Parameters = (await conn.QueryAsync<ParameterDto>(@"
                SELECT Id, ModuleCode, Code, Value, Description
                FROM Parameter
                WHERE CompanyId = @CompanyId AND IsDeleted = 0
                ORDER BY ModuleCode, Code",
                new { CompanyId = company.Id })).ToList();
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Parametre listesi veri yükleme hatası");
            TempData["Error"] = "Veriler yüklenirken bir hata oluştu.";
        }
    }

    // Mevcut parametre değeri güncelle
    public async Task<IActionResult> OnPostSaveAsync(Guid id, string value, string description)
    {
        try
        {
            using var conn = db.Open();
            await conn.ExecuteAsync(@"
                UPDATE Parameter SET Value = @Value, Description = @Description, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, Value = value, Description = description, CompanyId = company.Id });
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
    public async Task<IActionResult> OnPostCreateAsync(string moduleCode, string code, string value, string description)
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
            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Parameter WHERE CompanyId = @CompanyId AND Code = @Code AND IsDeleted = 0",
                new { CompanyId = company.Id, Code = code });

            if (exists > 0)
            {
                TempData["Error"] = $"'{code}' kodu zaten tanımlı.";
                return RedirectToPage();
            }

            await conn.ExecuteAsync(@"
                INSERT INTO Parameter (Id, CompanyId, ModuleCode, Code, Value, Description)
                VALUES (NEWID(), @CompanyId, @ModuleCode, @Code, @Value, @Description)",
                new { CompanyId = company.Id, ModuleCode = moduleCode?.ToUpper() ?? "SYS", Code = code.ToUpper(), Value = value, Description = description });

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
    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        try
        {
            using var conn = db.Open();
            await conn.ExecuteAsync(@"
                UPDATE Parameter SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id });
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
