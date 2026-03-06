using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Inventory.Movements;

public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<MovementDto> Movements { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        const string sql = @"
            SELECT sm.CreatedAt, sm.MovementType, i.Name as ItemName, i.Code as ItemCode, 
                   wh.Code as WarehouseCode, b.Code as BinCode, sm.QtyBase, 
                   sm.SourceDocType, sm.SourceDocNo, u.UserName as OperatorName
            FROM StockMovement sm
            JOIN Item i ON i.Id = sm.ItemId
            JOIN Warehouse wh ON wh.Id = sm.WarehouseId
            JOIN Bin b ON b.Id = sm.BinId
            LEFT JOIN AspNetUsers u ON u.Id = CAST(sm.CreatedBy AS NVARCHAR(450))
            WHERE sm.CompanyId = @CompanyId
            ORDER BY sm.CreatedAt DESC";

        Movements = await conn.QueryAsync<MovementDto>(sql, new { CompanyId = company.Id });
    }

    public record MovementDto(DateTime CreatedAt, string MovementType, string ItemName, string ItemCode, string WarehouseCode, string BinCode, decimal QtyBase, string SourceDocType, string SourceDocNo, string? OperatorName);
}
