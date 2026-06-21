using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    // Sarf fişlerini depo + sayaçlarla yükler
    public async Task OnGetAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;
        try
        {
            // Sayfa satırları + durum sayaçları (rows yerine SQL) tek round-trip (PF-1)
            const string sql = @"
                SELECT h.Id, h.DocNo, h.IssueDate, h.Status,
                       w.Name AS WarehouseName, cc.Name AS CostCenterName,
                       (SELECT COUNT(1) FROM MaterialIssueLine WHERE HeaderId = h.Id) AS LineCount
                FROM MaterialIssueHeader h
                JOIN Warehouse w ON w.Id = h.WarehouseId
                LEFT JOIN CostCenter cc ON cc.Id = h.CostCenterId
                WHERE h.CompanyId = @CompanyId
                ORDER BY h.CreatedAt DESC
                OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

                SELECT
                    COUNT(*) AS Total,
                    SUM(CASE WHEN Status = @StDraft  THEN 1 ELSE 0 END) AS Draft,
                    SUM(CASE WHEN Status = @StPosted THEN 1 ELSE 0 END) AS Posted
                FROM MaterialIssueHeader WHERE CompanyId = @CompanyId;";

            using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new
            {
                CompanyId = company.Id, Page = page, PageSize,
                StDraft = DocStatus.Draft, StPosted = DocStatus.Posted
            }, cancellationToken: ct));
            Rows = (await grid.ReadAsync<RowDto>()).ToList();
            var c = await grid.ReadSingleAsync<CountRow>();
            FilteredCount = c.Total;
            DraftCount    = c.Draft;
            PostedCount   = c.Posted;
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

    private record CountRow(int Total, int Draft, int Posted);
}
