using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Items;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    public IEnumerable<ItemDto> Items { get; set; } = [];

    public int TotalSkuCount { get; set; }
    public int CriticalSkuCount { get; set; }
    public int NoCategoryCount { get; set; }

    // Arama ve filtre parametreleri — GET query string'den bind edilir
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? CategoryFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }

    // Sayfalama (PF-1) — GET'ten bind, varsayılan 1; sabit sayfa boyutu
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;   // negatif/0 sayfa koruması

        // Ürün listesi — arama (Q), kategori, durum filtreleri + sayfalama (OFFSET/FETCH).
        // Tek round-trip: sayfa satırları + aynı filtrenin toplam sayısı (QueryMultiple).
        const string sql = @"
            SELECT i.Id, i.Code, i.Name, i.Barcode, i.TaxRate,
                   dv.NameTr as BaseUom, c.Name as CategoryName,
                   i.IsLotTracked, i.IsSerialTracked, i.IsActive,
                   si.SupplierItemCode AS SupplierItemCode
            FROM Item i
            JOIN DictionaryValue dv ON dv.Id = i.BaseUomId
            LEFT JOIN Category c ON c.Id = i.CategoryId
            -- P32-7: tercih edilen tedarikçinin ürün kodu (varsa) listede gösterilir
            LEFT JOIN SupplierItem si ON si.ItemId = i.Id AND si.IsPreferred = 1 AND si.IsDeleted = 0 AND si.CompanyId = i.CompanyId
            WHERE i.CompanyId = @CompanyId AND i.IsDeleted = 0
              AND (@Q IS NULL OR @Q = '' OR i.Code LIKE '%' + @Q + '%' OR i.Name LIKE '%' + @Q + '%')
              AND (@CategoryFilter IS NULL OR @CategoryFilter = '' OR c.Name = @CategoryFilter)
              AND (@StatusFilter IS NULL OR @StatusFilter = ''
                   OR (@StatusFilter = 'active'  AND i.IsActive = 1)
                   OR (@StatusFilter = 'passive' AND i.IsActive = 0))
            ORDER BY i.Code
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1)
            FROM Item i
            LEFT JOIN Category c ON c.Id = i.CategoryId
            WHERE i.CompanyId = @CompanyId AND i.IsDeleted = 0
              AND (@Q IS NULL OR @Q = '' OR i.Code LIKE '%' + @Q + '%' OR i.Name LIKE '%' + @Q + '%')
              AND (@CategoryFilter IS NULL OR @CategoryFilter = '' OR c.Name = @CategoryFilter)
              AND (@StatusFilter IS NULL OR @StatusFilter = ''
                   OR (@StatusFilter = 'active'  AND i.IsActive = 1)
                   OR (@StatusFilter = 'passive' AND i.IsActive = 0));";

        using (var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            CompanyId = company.Id, Q, CategoryFilter, StatusFilter, Page = page, PageSize
        }, cancellationToken: ct)))
        {
            Items = (await grid.ReadAsync<ItemDto>()).ToList();
            FilteredCount = await grid.ReadSingleAsync<int>();
        }

        TotalSkuCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Item WHERE CompanyId = @CompanyId AND IsDeleted = 0",
            new { CompanyId = company.Id }, cancellationToken: ct));

        NoCategoryCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Item WHERE CompanyId = @CompanyId AND CategoryId IS NULL AND IsDeleted = 0",
            new { CompanyId = company.Id }, cancellationToken: ct));

        // Emniyet altı kritik SKU: ürün-seviyesi MinStockLevel altına düşmüş kalemler (Plan 34)
        // Eski C# JSON parse döngüsü yerine tek SARGable SQL COUNT.
        CriticalSkuCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(1)
            FROM Item i
            WHERE i.CompanyId = @CompanyId AND i.IsDeleted = 0 AND i.MinStockLevel IS NOT NULL
              AND (SELECT ISNULL(SUM(b.QtyBalance), 0)
                   FROM tvf_InventoryBalance(@CompanyId) b
                   WHERE b.ItemId = i.Id) < i.MinStockLevel",
            new { CompanyId = company.Id }, cancellationToken: ct));
    }

    // Ürünü soft-delete eder (IsDeleted=1) — stok hareketi görmüşse engellenir (Pasif Yap önerilir)
    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            using var conn = db.Open();

            // İş kuralı: ürün stok hareketi görmüşse silinemez (tarihçe korunur)
            var mvCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM StockMovement WHERE ItemId = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct));
            if (mvCount > 0)
            {
                TempData["Error"] = "Ürün silinemez: stok hareketi mevcut. Bunun yerine 'Pasif Yap' kullanın.";
                return RedirectToPage();
            }

            // Soft-delete — yalnızca bu firmanın silinmemiş ürünü
            var affected = await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE Item SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct));
            TempData[affected > 0 ? "Success" : "Error"] = affected > 0 ? "Ürün silindi." : "Ürün bulunamadı.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Sistem hatası — ham mesaj gösterilmez, detay log'a
            logger.LogError(sqlEx, "Ürün silme DB hatası. Ürün {ItemId}", id);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage();
    }

    public record ItemDto(Guid Id, string Code, string Name, string? Barcode, decimal TaxRate, string BaseUom, string? CategoryName, bool IsLotTracked, bool IsSerialTracked, bool IsActive, string? SupplierItemCode);
}
