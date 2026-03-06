using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.CycleCount;

public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<CycleCountDto> Counts { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        Counts = await conn.QueryAsync<CycleCountDto>(@"
            SELECT c.*, w.Code as WarehouseCode,
                   (SELECT COUNT(*) FROM CycleCountLine WHERE CycleCountId = c.Id) as LineCount
            FROM CycleCount c
            JOIN Warehouse w ON w.Id = c.WarehouseId
            WHERE c.CompanyId = @CompanyId
            ORDER BY c.CreatedAt DESC", new { CompanyId = company.Id });
    }

    public record CycleCountDto { public Guid Id { get; set; } public string DocNo { get; set; } = ""; public string Status { get; set; } = ""; public string WarehouseCode { get; set; } = ""; public int LineCount { get; set; } public DateTime CreatedAt { get; set; } }
}
