using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Inventory.Balance;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    // Filtre parametreleri — GET sorgu dizesinden bağlanır
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? WarehouseId { get; set; }

    public IEnumerable<BalanceDto> BalanceLines { get; set; } = [];
    public IEnumerable<WarehouseOptionDto> Warehouses { get; set; } = [];

    public decimal TotalStockQty { get; set; } = 0;
    public int CriticalItemCount { get; set; } = 0;
    public int ActiveBinCount { get; set; } = 0;

    public async Task OnGetAsync(CancellationToken ct)
    {
        using var conn = db.Open();

        // Depo listesi — filtre dropdown'ı için
        Warehouses = await conn.QueryAsync<WarehouseOptionDto>(
            new CommandDefinition(
                "SELECT Id, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Name",
                new { CompanyId = company.Id },
                cancellationToken: ct));

        // tvf_InventoryBalance: CompanyId yalıtılmış, sıfır bakiyeler zaten hariç
        // TOP 1000 + filtre: büyük tablolarda performans koruması; WHERE SARGable
        const string sql = @"
            SELECT TOP 1000
                   i.Code as ItemCode, i.Name as ItemName, wh.Name as WarehouseName,
                   b.Code as BinCode, inv.QtyBalance as QtyOnHand
            FROM tvf_InventoryBalance(@CompanyId) inv
            JOIN Item i ON i.Id = inv.ItemId
            JOIN Warehouse wh ON wh.Id = inv.WarehouseId
            LEFT JOIN Bin b ON b.Id = inv.BinId
            WHERE (@WarehouseId IS NULL OR inv.WarehouseId = @WarehouseId)
              AND (@Q IS NULL
                   OR i.Code LIKE '%' + @Q + '%'
                   OR i.Name LIKE '%' + @Q + '%')
            ORDER BY i.Code, wh.Name, b.Code";

        BalanceLines = await conn.QueryAsync<BalanceDto>(
            new CommandDefinition(sql,
                new { CompanyId = company.Id, WarehouseId, Q },
                cancellationToken: ct));

        // Total Stock Quantity
        TotalStockQty = BalanceLines.Sum(x => x.QtyOnHand);

        // Active Storage Bins Count
        ActiveBinCount = await conn.QueryFirstOrDefaultAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(DISTINCT BinId) FROM tvf_InventoryBalance(@CompanyId) WHERE BinId IS NOT NULL",
                new { CompanyId = company.Id },
                cancellationToken: ct));

        // Calculate Critical Items (Violating Emniyet/Min Stok Seviyesi)
        var items = await conn.QueryAsync<ItemMinQtyDto>(
            new CommandDefinition(
                "SELECT Code, Description FROM Item WHERE CompanyId = @CompanyId AND IsDeleted = 0",
                new { CompanyId = company.Id },
                cancellationToken: ct));

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
    public record WarehouseOptionDto(Guid Id, string Name);
    private record ItemMinQtyDto(string Code, string? Description);

    private class UdfDataDto
    {
        public decimal? MinQty { get; set; }
    }
}
