using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.MaterialIssue;

/// <summary>
/// Sarf fişi listesi: depodan iç tüketime çıkan stok belgeleri.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    public List<RowDto> Rows { get; set; } = [];
    public int DraftCount { get; set; }
    public int PostedCount { get; set; }

    // Sarf fişlerini depo + sayaçlarla yükler
    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        try
        {
            Rows = (await conn.QueryAsync<RowDto>(@"
                SELECT h.Id, h.DocNo, h.IssueDate, h.Status,
                       w.Name AS WarehouseName, cc.Name AS CostCenterName,
                       (SELECT COUNT(1) FROM MaterialIssueLine WHERE HeaderId = h.Id) AS LineCount
                FROM MaterialIssueHeader h
                JOIN Warehouse w ON w.Id = h.WarehouseId
                LEFT JOIN CostCenter cc ON cc.Id = h.CostCenterId
                WHERE h.CompanyId = @CompanyId
                ORDER BY h.CreatedAt DESC",
                new { CompanyId = company.Id })).ToList();

            DraftCount  = Rows.Count(r => r.Status == DocStatus.Draft);
            PostedCount = Rows.Count(r => r.Status == DocStatus.Posted);
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Sarf fişi listesi yükleme hatası");
            TempData["Error"] = "Liste yüklenirken veritabanı hatası oluştu.";
        }
    }

    public record RowDto(
        Guid Id, string DocNo, DateTime IssueDate, string Status,
        string WarehouseName, string? CostCenterName, int LineCount);
}
