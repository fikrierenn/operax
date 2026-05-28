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

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        const string sql = @"
            SELECT i.Id, i.Code, i.Name, i.Barcode, i.TaxRate,
                   dv.NameTr as BaseUom, c.Name as CategoryName,
                   i.IsLotTracked, i.IsSerialTracked, i.IsActive
            FROM Item i
            JOIN DictionaryValue dv ON dv.Id = i.BaseUomId
            LEFT JOIN Category c ON c.Id = i.CategoryId
            WHERE i.CompanyId = @CompanyId AND i.IsDeleted = 0
            ORDER BY i.Code";

        Items = await conn.QueryAsync<ItemDto>(sql, new { CompanyId = company.Id });

        TotalSkuCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Item WHERE CompanyId = @CompanyId AND IsDeleted = 0",
            new { CompanyId = company.Id });

        NoCategoryCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Item WHERE CompanyId = @CompanyId AND CategoryId IS NULL AND IsDeleted = 0",
            new { CompanyId = company.Id });

        // Emniyet altı kritik SKU hesaplama
        var balances = (await conn.QueryAsync<(Guid ItemId, decimal Bal)>(
            "SELECT ItemId, SUM(QtyBalance) as Bal FROM tvf_InventoryBalance(@CompanyId) GROUP BY ItemId",
            new { CompanyId = company.Id })).ToDictionary(x => x.ItemId, x => x.Bal);

        var itemDescs = await conn.QueryAsync<(Guid Id, string Description)>(
            "SELECT Id, Description FROM Item WHERE CompanyId = @CompanyId AND IsDeleted = 0",
            new { CompanyId = company.Id });

        int criticalCount = 0;
        foreach (var item in itemDescs)
        {
            if (!string.IsNullOrEmpty(item.Description) && item.Description.TrimStart().StartsWith("{"))
            {
                try
                {
                    var udf = System.Text.Json.JsonSerializer.Deserialize<DetailsModel.UdfDataDto>(item.Description);
                    if (udf != null && udf.MinQty.HasValue)
                    {
                        balances.TryGetValue(item.Id, out decimal bal);
                        if (bal < udf.MinQty.Value)
                        {
                            criticalCount++;
                        }
                    }
                }
                catch { }
            }
        }
        CriticalSkuCount = criticalCount;
    }

    public record ItemDto(Guid Id, string Code, string Name, string? Barcode, decimal TaxRate, string BaseUom, string? CategoryName, bool IsLotTracked, bool IsSerialTracked, bool IsActive);
}
