using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.CycleCount;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<CycleCountDto> Counts { get; set; } = [];

    public int TotalSessions { get; set; } = 0;
    public int ActiveCountingWarehouses { get; set; } = 0;
    public int TotalCountedLines { get; set; } = 0;

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

        TotalSessions = Counts.Count();
        ActiveCountingWarehouses = Counts.Where(x => x.Status == "COUNTING").Select(x => x.WarehouseCode).Distinct().Count();
        TotalCountedLines = Counts.Sum(x => x.LineCount);
    }

    public record CycleCountDto { public Guid Id { get; set; } public string DocNo { get; set; } = ""; public string Status { get; set; } = ""; public string WarehouseCode { get; set; } = ""; public int LineCount { get; set; } public DateTime CreatedAt { get; set; } }
}
