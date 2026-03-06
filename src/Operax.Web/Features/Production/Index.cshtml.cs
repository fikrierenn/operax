using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Production;

public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ProductionOrderDto> Orders { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        const string sql = @"
            SELECT p.*, i.Code as ItemCode, i.Name as ItemName,
                   (SELECT COUNT(*) FROM ProductionOrderLine WHERE ProductionOrderId = p.Id) as LineCount
            FROM ProductionOrder p
            JOIN Item i ON i.Id = p.ItemId
            WHERE p.CompanyId = @CompanyId
            ORDER BY p.CreatedAt DESC";

        Orders = await conn.QueryAsync<ProductionOrderDto>(sql, new { CompanyId = company.Id });
    }

    public record ProductionOrderDto { 
        public Guid Id { get; set; } 
        public string DocNo { get; set; } = ""; 
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public decimal QtyTarget { get; set; } 
        public decimal QtyProduced { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int LineCount { get; set; }
    }
}
