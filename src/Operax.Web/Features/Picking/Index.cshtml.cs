using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Picking;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<PickTaskDto> Tasks { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        const string sql = @"
            SELECT t.*, u.UserName as AssignedUserName, 
                   (SELECT COUNT(*) FROM PickTaskLine WHERE PickTaskId = t.Id) as LineCount,
                   (SELECT COUNT(*) FROM PickTaskLine WHERE PickTaskId = t.Id AND QtyPickedBase > 0) as PickedLineCount
            FROM PickTask t
            LEFT JOIN AspNetUsers u ON u.Id = t.AssignedUserId
            WHERE t.CompanyId = @CompanyId
            ORDER BY t.CreatedAt DESC";

        Tasks = await conn.QueryAsync<PickTaskDto>(sql, new { CompanyId = company.Id });
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
