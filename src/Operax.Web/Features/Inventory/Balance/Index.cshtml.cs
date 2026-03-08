using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Inventory.Balance;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<BalanceDto> BalanceLines { get; set; } = [];

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
    }

    public record BalanceDto(string ItemCode, string ItemName, string WarehouseName, string? BinCode, decimal QtyOnHand);
}
