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

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    // Sayım oturumu listesini ve KPI toplamlarını tek round-trip ile yükler (PF-1).
    public async Task OnGetAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        // Sayfa satırları + KPI'lar (rows yerine SQL aggregate) tek round-trip (PF-1)
        const string sql = @"
            SELECT c.*, w.Code as WarehouseCode,
                   (SELECT COUNT(*) FROM CycleCountLine WHERE CycleCountId = c.Id AND IsDeleted = 0) as LineCount
            FROM CycleCount c
            JOIN Warehouse w ON w.Id = c.WarehouseId
            WHERE c.CompanyId = @CompanyId AND c.IsDeleted = 0
            ORDER BY c.CreatedAt DESC
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT
                COUNT(*) AS TotalSessions,
                COUNT(DISTINCT CASE WHEN c.Status = 'COUNTING' THEN c.WarehouseId END) AS ActiveCountingWarehouses,
                -- Toplam sayılan satır: korelasyonsuz skaler alt-sorgu (aggregate içinde aggregate yasağını önler)
                (SELECT COUNT(*) FROM CycleCountLine ccl
                   JOIN CycleCount c2 ON c2.Id = ccl.CycleCountId
                  WHERE c2.CompanyId = @CompanyId AND c2.IsDeleted = 0 AND ccl.IsDeleted = 0) AS TotalCountedLines
            FROM CycleCount c
            WHERE c.CompanyId = @CompanyId AND c.IsDeleted = 0;";

        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
        Counts = (await grid.ReadAsync<CycleCountDto>()).ToList();
        var agg = await grid.ReadSingleAsync<AggRow>();
        FilteredCount            = agg.TotalSessions;
        TotalSessions            = agg.TotalSessions;
        ActiveCountingWarehouses = agg.ActiveCountingWarehouses;
        TotalCountedLines        = agg.TotalCountedLines;
    }

    public record CycleCountDto { public Guid Id { get; set; } public string DocNo { get; set; } = ""; public string Status { get; set; } = ""; public string WarehouseCode { get; set; } = ""; public int LineCount { get; set; } public DateTime CreatedAt { get; set; } }
    private record AggRow(int TotalSessions, int ActiveCountingWarehouses, int TotalCountedLines);
}
