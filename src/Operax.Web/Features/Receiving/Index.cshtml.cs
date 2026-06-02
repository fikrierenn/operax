using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Receiving;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public IEnumerable<ReceivingDto> Documents { get; set; } = [];

    public int DraftCount { get; set; } = 0;
    public int PostedCount { get; set; } = 0;
    public int CancelledCount { get; set; } = 0;

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        // KPI sayaçları: filtre uygulanmadan tüm belgelerin durum dağılımı
        const string countSql = @"
            SELECT r.Status, COUNT(*) AS Cnt
            FROM ReceivingHeader r
            WHERE r.CompanyId = @CompanyId AND r.IsDeleted = 0
            GROUP BY r.Status";

        var statusCounts = await conn.QueryAsync<(string Status, int Cnt)>(countSql, new { CompanyId = company.Id });
        foreach (var (status, cnt) in statusCounts)
        {
            if (status == DocStatus.Draft)     DraftCount     = cnt;
            if (status == DocStatus.Posted)    PostedCount    = cnt;
            if (status == DocStatus.Cancelled) CancelledCount = cnt;
        }

        // Arama + durum filtresini parametreli WHERE koşuluyla uygula
        const string sql = @"
            SELECT r.Id, r.DocNo, r.DocDate, r.Status, p.Name as PartnerName, wh.Code as WarehouseCode
            FROM ReceivingHeader r
            JOIN Partner p  ON p.Id  = r.PartnerId
            JOIN Warehouse wh ON wh.Id = r.WarehouseId
            WHERE r.CompanyId = @CompanyId
              AND r.IsDeleted = 0
              AND (@Q      IS NULL OR @Q      = '' OR r.DocNo LIKE '%' + @Q + '%' OR p.Name LIKE '%' + @Q + '%')
              AND (@Status IS NULL OR @Status = '' OR r.Status = @Status)
            ORDER BY r.DocDate DESC, r.DocNo DESC";

        Documents = await conn.QueryAsync<ReceivingDto>(sql, new
        {
            CompanyId = company.Id,
            Q         = Q,
            Status    = Status
        });
    }

    public record ReceivingDto(Guid Id, string DocNo, DateTime DocDate, string Status, string PartnerName, string WarehouseCode);
}
