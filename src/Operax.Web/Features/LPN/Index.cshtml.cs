using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.LPN;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<LpnListDto> Lpns { get; set; } = [];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Şirkete ait tüm palet/kapları konum ve içerik özeti ile listeler + sayfalama
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        const string sql = @"
            SELECT
                l.Id, l.Code, l.Status, l.LpnType,
                w.Code AS WhCode, b.Code AS BinCode,
                (SELECT COUNT(DISTINCT sm.ItemId)
                 FROM StockMovement sm
                 WHERE sm.LpnId = l.Id AND sm.CompanyId = @CompanyId AND sm.IsCancelled = 0
                 GROUP BY sm.LpnId
                 HAVING SUM(sm.QtyBase) <> 0) AS ItemCount,
                (SELECT ISNULL(SUM(sm.QtyBase), 0)
                 FROM StockMovement sm
                 WHERE sm.LpnId = l.Id AND sm.CompanyId = @CompanyId AND sm.IsCancelled = 0) AS TotalQty,
                l.CreatedAt
            FROM LPN l
            LEFT JOIN Warehouse w ON w.Id = l.CurrentWarehouseId
            LEFT JOIN Bin b ON b.Id = l.CurrentBinId
            WHERE l.CompanyId = @CompanyId
            ORDER BY l.CreatedAt DESC
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM LPN WHERE CompanyId = @CompanyId;";

        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
        Lpns = (await grid.ReadAsync<LpnListDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
    }

    public record LpnListDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Status { get; set; } = "";
        public string LpnType { get; set; } = "";
        public string? WhCode { get; set; }
        public string? BinCode { get; set; }
        public int? ItemCount { get; set; }
        public decimal TotalQty { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
