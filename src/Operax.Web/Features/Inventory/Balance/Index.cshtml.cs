using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Inventory.Balance;

public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<BalanceDto> BalanceLines { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        const string sql = @"
            SELECT i.Code as ItemCode, i.Name as ItemName, wh.Name as WarehouseName, b.Code as BinCode, SUM(sm.QtyBase) as QtyOnHand
            FROM StockMovement sm
            JOIN Item i ON i.Id = sm.ItemId
            JOIN Warehouse wh ON wh.Id = sm.WarehouseId
            LEFT JOIN Bin b ON b.Id = sm.BinId
            WHERE sm.CompanyId = @CompanyId AND sm.IsCancelled = 0
            GROUP BY i.Code, i.Name, wh.Name, b.Code
            HAVING SUM(sm.QtyBase) <> 0
            ORDER BY i.Code, wh.Name, b.Code";

        BalanceLines = await conn.QueryAsync<BalanceDto>(sql, new { CompanyId = company.Id });
    }

    public record BalanceDto(string ItemCode, string ItemName, string WarehouseName, string? BinCode, decimal QtyOnHand);
}
