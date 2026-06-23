using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.UdfFields;

// Dinamik kullanıcı tanımlı alan (UDF) tanım yönetimi — yalnız yönetici (Plan 34)
[Authorize(Roles = Operax.Web.Lib.Roles.Administrator)]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    public List<UdfDefRow> Definitions { get; set; } = [];

    // Aktif entity'ler (Faz 1: Item · Faz 2: Partner)
    public string[] AllowedEntities { get; } = ["Item", "Partner"];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    // UDF tanımlarını listeler (şirket-kapsamlı)
    public async Task OnGetAsync(CancellationToken ct = default)
    {
        try
        {
            using var conn = db.Open();
            var page = Page < 1 ? 1 : Page;
            const string sql = @"
                SELECT Id, EntityName, FieldName, LabelText, FieldType,
                       DataSourceType, DataSourceKey, IsRequired, IsActive, OrderNo
                FROM UserFieldDefinition
                WHERE CompanyId = @CompanyId AND IsDeleted = 0
                ORDER BY EntityName, OrderNo, LabelText
                OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

                SELECT COUNT(1) FROM UserFieldDefinition WHERE CompanyId = @CompanyId AND IsDeleted = 0;";
            using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
            Definitions = (await grid.ReadAsync<UdfDefRow>()).ToList();
            FilteredCount = await grid.ReadSingleAsync<int>();
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "UDF tanım listesi yükleme hatası");
            TempData["Error"] = "Veriler yüklenirken bir hata oluştu.";
        }
    }

    // Yeni UDF tanımı ekler
    public async Task<IActionResult> OnPostCreateAsync(string entityName, string fieldName, string labelText,
        string fieldType, string? dataSourceType, string? dataSourceKey, int orderNo, bool isRequired, CancellationToken ct = default)
    {
        // Guard: zorunlu alanlar + entity beyaz listesi
        if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(labelText))
        {
            TempData["Error"] = "Alan adı ve etiket zorunludur.";
            return RedirectToPage();
        }
        if (!AllowedEntities.Contains(entityName))
        {
            TempData["Error"] = "Geçersiz varlık (entity).";
            return RedirectToPage();
        }
        if (fieldType is not ("TEXT" or "NUMBER" or "SELECT" or "DATE" or "BOOLEAN"))
        {
            TempData["Error"] = "Geçersiz alan tipi. (TEXT / NUMBER / SELECT / DATE / BOOLEAN)";
            return RedirectToPage();
        }
        // SELECT veri kaynağı: STATIC (virgüllü) | DICTIONARY (sözlük tipi) | TABLE (beyaz-liste lookup)
        var srcType = fieldType == UdfFieldType.Select
            ? (dataSourceType is UdfDataSourceType.Dictionary or UdfDataSourceType.Table ? dataSourceType : UdfDataSourceType.Static)
            : null;
        if (fieldType == UdfFieldType.Select && string.IsNullOrWhiteSpace(dataSourceKey))
        {
            TempData["Error"] = srcType switch
            {
                UdfDataSourceType.Dictionary => "Sözlük kaynağı için sözlük tipi kodu zorunludur (örn. BRAND).",
                UdfDataSourceType.Table      => "Tablo kaynağı için anahtar zorunludur (Partner / Warehouse / Item).",
                _            => "Sabit liste için virgülle ayrılmış değerler zorunludur."
            };
            return RedirectToPage();
        }
        // TABLE anahtarı yalnız beyaz-listede olabilir (SQL injection koruması)
        if (srcType == UdfDataSourceType.Table && UdfWhitelist.GetQuery(dataSourceKey) == null)
        {
            TempData["Error"] = $"Geçersiz tablo kaynağı. İzinli: {string.Join(", ", UdfWhitelist.Keys)}.";
            return RedirectToPage();
        }

        try
        {
            using var conn = db.Open();
            // İş kuralı: aynı şirket+entity+alan adı tekil
            var exists = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
                SELECT COUNT(1) FROM UserFieldDefinition
                WHERE CompanyId = @CompanyId AND EntityName = @EntityName
                  AND FieldName = @FieldName AND IsDeleted = 0",
                new { CompanyId = company.Id, EntityName = entityName, FieldName = fieldName }, cancellationToken: ct));
            if (exists > 0)
            {
                TempData["Error"] = $"'{fieldName}' alanı bu varlık için zaten tanımlı.";
                return RedirectToPage();
            }

            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO UserFieldDefinition
                    (Id, CompanyId, EntityName, FieldName, LabelText, FieldType,
                     DataSourceType, DataSourceKey, OrderNo, IsRequired, CreatedBy)
                VALUES
                    (NEWID(), @CompanyId, @EntityName, @FieldName, @LabelText, @FieldType,
                     @DataSourceType, @DataSourceKey, @OrderNo, @IsRequired, @UserId)",
                new
                {
                    CompanyId = company.Id, EntityName = entityName, FieldName = fieldName,
                    LabelText = labelText, FieldType = fieldType,
                    DataSourceType = srcType,
                    DataSourceKey = fieldType == UdfFieldType.Select ? dataSourceKey : null,
                    OrderNo = orderNo, IsRequired = isRequired, UserId = User.Identity?.Name
                }, cancellationToken: ct));
            TempData["Success"] = $"'{labelText}' özel alanı oluşturuldu.";
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "UDF tanım oluşturma hatası: {Field}", fieldName);
            TempData["Error"] = "Özel alan oluşturulurken bir hata oluştu.";
        }
        return RedirectToPage();
    }

    // Etiket / sıra / zorunluluk / aktiflik + SELECT seçenek listesi (DataSourceKey) günceller.
    // FieldName ve FieldType DEĞİŞTİRİLEMEZ (JSON anahtarı + tip uyumu — veri bütünlüğü).
    public async Task<IActionResult> OnPostSaveAsync(Guid id, string labelText, int orderNo, bool isRequired, bool isActive, string? dataSourceKey, CancellationToken ct = default)
    {
        try
        {
            using var conn = db.Open();
            // İş kuralı: boş dataSourceKey mevcut seçenekleri SİLMEZ (SELECT alanın listesi korunur) —
            // yalnız dolu değer geldiğinde güncellenir. Non-SELECT alanlarda zaten NULL kalır.
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE UserFieldDefinition
                SET LabelText = @LabelText, OrderNo = @OrderNo, IsRequired = @IsRequired,
                    IsActive = @IsActive,
                    DataSourceKey = CASE WHEN NULLIF(@DataSourceKey, N'') IS NULL THEN DataSourceKey ELSE @DataSourceKey END,
                    UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
                WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, LabelText = labelText, OrderNo = orderNo, IsRequired = isRequired,
                      IsActive = isActive, DataSourceKey = dataSourceKey, CompanyId = company.Id, UserId = User.Identity?.Name }, cancellationToken: ct));
            TempData["Success"] = "Özel alan güncellendi.";
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "UDF tanım güncelleme hatası: {Id}", id);
            TempData["Error"] = "Özel alan güncellenirken bir hata oluştu.";
        }
        return RedirectToPage();
    }

    // UDF tanımını siler (soft delete)
    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            using var conn = db.Open();
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE UserFieldDefinition SET IsDeleted = 1, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
                WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id, UserId = User.Identity?.Name }, cancellationToken: ct));
            TempData["Success"] = "Özel alan silindi.";
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "UDF tanım silme hatası: {Id}", id);
            TempData["Error"] = "Özel alan silinirken bir hata oluştu.";
        }
        return RedirectToPage();
    }

    public record UdfDefRow(Guid Id, string EntityName, string FieldName, string LabelText, string FieldType,
        string? DataSourceType, string? DataSourceKey, bool IsRequired, bool IsActive, int OrderNo);
}
