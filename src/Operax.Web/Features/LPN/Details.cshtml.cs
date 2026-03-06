using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.LPN;

public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    public LpnDto Lpn { get; set; } = new();
    public IEnumerable<LpnContentDto> Contents { get; set; } = [];

    public async Task OnGetAsync(string code)
    {
        using var conn = db.Open();
        
        Lpn = await conn.QueryFirstOrDefaultAsync<LpnDto>(@"
            SELECT l.*, w.Code as WhCode, b.Code as BinCode
            FROM LPN l
            LEFT JOIN Warehouse w ON w.Id = l.CurrentWarehouseId
            LEFT JOIN Bin b ON b.Id = l.CurrentBinId
            WHERE l.Code = @Code AND l.CompanyId = @CompanyId", 
            new { Code = code, CompanyId = company.Id }) ?? new();

        if (Lpn.Id != Guid.Empty)
        {
            Contents = await conn.QueryAsync<LpnContentDto>(@"
                SELECT ItemId, LotNo, SUM(QtyBase) as Qty, i.Code as ItemCode, i.Name as ItemName
                FROM StockMovement sm
                JOIN Item i ON i.Id = sm.ItemId
                WHERE sm.LpnId = @LpnId AND sm.IsCancelled = 0
                GROUP BY ItemId, LotNo, i.Code, i.Name
                HAVING SUM(QtyBase) <> 0", new { LpnId = Lpn.Id });
        }
    }

    public record LpnDto { 
        public Guid Id { get; set; } 
        public string Code { get; set; } = ""; 
        public string Status { get; set; } = "";
        public string WhCode { get; set; } = "";
        public string BinCode { get; set; } = "";
    }
    public record LpnContentDto { 
        public string ItemCode { get; set; } = ""; 
        public string ItemName { get; set; } = ""; 
        public string? LotNo { get; set; }
        public decimal Qty { get; set; } 
    }
}
