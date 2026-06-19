using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Lot;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<LotListDto> Lots { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        Lots = await conn.QueryAsync<LotListDto>(@"
            SELECT l.*, i.Code as ItemCode, i.Name as ItemName,
                   (SELECT ISNULL(SUM(QtyBase), 0) FROM StockMovement
                    WHERE CompanyId = @CompanyId AND ItemId = l.ItemId AND LotNo = l.LotNo AND IsCancelled = 0) as QtyOnHand
            FROM ItemLot l
            JOIN Item i ON i.Id = l.ItemId
            WHERE l.CompanyId = @CompanyId
            ORDER BY l.ExpiryDate ASC", new { CompanyId = company.Id });
    }

    public record LotListDto { 
        public Guid Id { get; set; } 
        public string LotNo { get; set; } = ""; 
        public string ItemCode { get; set; } = ""; 
        public string ItemName { get; set; } = ""; 
        public DateTime? ExpiryDate { get; set; } 
        public decimal QtyOnHand { get; set; } 
        public string Status { get; set; } = "";
    }
}
