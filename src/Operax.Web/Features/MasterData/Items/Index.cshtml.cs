using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Items;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ItemDto> Items { get; set; } = [];

    public int TotalSkuCount { get; set; }
    public int CriticalSkuCount { get; set; }
    public int NoCategoryCount { get; set; }

    // Arama ve filtre parametreleri — GET query string'den bind edilir
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? CategoryFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        // Ürün listesi — arama (Q), kategori ve durum filtreleriyle
        const string sql = @"
            SELECT i.Id, i.Code, i.Name, i.Barcode, i.TaxRate,
                   dv.NameTr as BaseUom, c.Name as CategoryName,
                   i.IsLotTracked, i.IsSerialTracked, i.IsActive
            FROM Item i
            JOIN DictionaryValue dv ON dv.Id = i.BaseUomId
            LEFT JOIN Category c ON c.Id = i.CategoryId
            WHERE i.CompanyId = @CompanyId AND i.IsDeleted = 0
              AND (@Q IS NULL OR @Q = '' OR i.Code LIKE '%' + @Q + '%' OR i.Name LIKE '%' + @Q + '%')
              AND (@CategoryFilter IS NULL OR @CategoryFilter = '' OR c.Name = @CategoryFilter)
              AND (@StatusFilter IS NULL OR @StatusFilter = ''
                   OR (@StatusFilter = 'active'  AND i.IsActive = 1)
                   OR (@StatusFilter = 'passive' AND i.IsActive = 0))
            ORDER BY i.Code";

        Items = await conn.QueryAsync<ItemDto>(sql, new
        {
            CompanyId = company.Id,
            Q,
            CategoryFilter,
            StatusFilter
        });

        TotalSkuCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Item WHERE CompanyId = @CompanyId AND IsDeleted = 0",
            new { CompanyId = company.Id });

        NoCategoryCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Item WHERE CompanyId = @CompanyId AND CategoryId IS NULL AND IsDeleted = 0",
            new { CompanyId = company.Id });

        // Emniyet altı kritik SKU: ürün-seviyesi MinStockLevel altına düşmüş kalemler (Plan 34)
        // Eski C# JSON parse döngüsü yerine tek SARGable SQL COUNT.
        CriticalSkuCount = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM Item i
            WHERE i.CompanyId = @CompanyId AND i.IsDeleted = 0 AND i.MinStockLevel IS NOT NULL
              AND (SELECT ISNULL(SUM(b.QtyBalance), 0)
                   FROM tvf_InventoryBalance(@CompanyId) b
                   WHERE b.ItemId = i.Id) < i.MinStockLevel",
            new { CompanyId = company.Id });
    }

    public record ItemDto(Guid Id, string Code, string Name, string? Barcode, decimal TaxRate, string BaseUom, string? CategoryName, bool IsLotTracked, bool IsSerialTracked, bool IsActive);
}
