using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Picking;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<PickTaskDto> Tasks { get; set; } = [];

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
            SELECT t.*, u.UserName as AssignedUserName,
                   (SELECT COUNT(*) FROM PickTaskLine WHERE PickTaskId = t.Id) as LineCount,
                   (SELECT COUNT(*) FROM PickTaskLine WHERE PickTaskId = t.Id AND QtyPickedBase > 0) as PickedLineCount
            FROM PickTask t
            LEFT JOIN AspNetUsers u ON u.Id = t.AssignedUserId
            WHERE t.CompanyId = @CompanyId
            ORDER BY t.CreatedAt DESC
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM PickTask WHERE CompanyId = @CompanyId;";

        using var grid = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
        Tasks = (await grid.ReadAsync<PickTaskDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
    }

    public record PickTaskDto { 
        public Guid Id { get; set; } 
        public string DocNo { get; set; } = ""; 
        public string Status { get; set; } = ""; 
        public string? AssignedUserName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int LineCount { get; set; }
        public int PickedLineCount { get; set; }
        public decimal Progress => LineCount == 0 ? 0 : (decimal)PickedLineCount / LineCount * 100;
    }
}
