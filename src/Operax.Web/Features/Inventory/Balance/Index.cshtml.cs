using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Inventory.Balance;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<BalanceDto> BalanceLines { get; set; } = [];

    public decimal TotalStockQty { get; set; } = 0;
    public int CriticalItemCount { get; set; } = 0;
    public int ActiveBinCount { get; set; } = 0;

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        // tvf_InventoryBalance: CompanyId yalıtılmış, sıfır bakiyeler zaten hariç
        const string sql = @"
            SELECT i.Code as ItemCode, i.Name as ItemName, wh.Name as WarehouseName,
                   b.Code as BinCode, inv.QtyBalance as QtyOnHand
            FROM tvf_InventoryBalance(@CompanyId) inv
            JOIN Item i ON i.Id = inv.ItemId
            JOIN Warehouse wh ON wh.Id = inv.WarehouseId
            LEFT JOIN Bin b ON b.Id = inv.BinId
            ORDER BY i.Code, wh.Name, b.Code";

        BalanceLines = await conn.QueryAsync<BalanceDto>(sql, new { CompanyId = company.Id });

        // Total Stock Quantity
        TotalStockQty = BalanceLines.Sum(x => x.QtyOnHand);

        // Active Storage Bins Count
        ActiveBinCount = await conn.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(DISTINCT BinId) FROM tvf_InventoryBalance(@CompanyId) WHERE BinId IS NOT NULL",
            new { CompanyId = company.Id });

        // Calculate Critical Items (Violating Emniyet/Min Stok Seviyesi)
        var items = await conn.QueryAsync<ItemMinQtyDto>(
            "SELECT Code, Description FROM Item WHERE CompanyId = @CompanyId AND IsDeleted = 0",
            new { CompanyId = company.Id });

        var stockGroup = BalanceLines.GroupBy(b => b.ItemCode).ToDictionary(g => g.Key, g => g.Sum(x => x.QtyOnHand));

        CriticalItemCount = 0;
        foreach (var item in items)
        {
            if (!string.IsNullOrEmpty(item.Description) && item.Description.TrimStart().StartsWith("{"))
            {
                try
                {
                    var udf = System.Text.Json.JsonSerializer.Deserialize<UdfDataDto>(item.Description);
                    if (udf != null && udf.MinQty.HasValue && udf.MinQty.Value > 0)
                    {
                        stockGroup.TryGetValue(item.Code, out var currentQty);
                        if (currentQty < udf.MinQty.Value)
                        {
                            CriticalItemCount++;
                        }
                    }
                }
                catch
                {
                    // Ignore JSON parsing errors
                }
            }
        }
    }

    public record BalanceDto(string ItemCode, string ItemName, string WarehouseName, string? BinCode, decimal QtyOnHand);
    
    private record ItemMinQtyDto(string Code, string? Description);

    private class UdfDataDto
    {
        public decimal? MinQty { get; set; }
    }
}
