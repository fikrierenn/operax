using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Production;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ProductionOrderDto> Orders { get; set; } = [];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        const string sql = @"
            SELECT p.*, i.Code as ItemCode, i.Name as ItemName,
                   (SELECT COUNT(*) FROM ProductionOrderLine WHERE ProductionOrderId = p.Id) as LineCount
            FROM ProductionOrder p
            JOIN Item i ON i.Id = p.ItemId
            WHERE p.CompanyId = @CompanyId
            ORDER BY p.CreatedAt DESC
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM ProductionOrder WHERE CompanyId = @CompanyId;";

        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
        Orders = (await grid.ReadAsync<ProductionOrderDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
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
