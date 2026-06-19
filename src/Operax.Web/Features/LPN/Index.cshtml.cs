using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.LPN;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<LpnListDto> Lpns { get; set; } = [];

    public async Task OnGetAsync()
    {
        // Şirkete ait tüm palet/kapları konum ve içerik özeti ile listeler
        using var conn = db.Open();
        Lpns = await conn.QueryAsync<LpnListDto>(@"
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
            ORDER BY l.CreatedAt DESC",
            new { CompanyId = company.Id });
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
