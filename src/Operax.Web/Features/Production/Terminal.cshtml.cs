using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Production;

public class TerminalModel(Db db, ICurrentUser user) : PageModel
{
    public IEnumerable<ActiveTaskDto> ActiveTasks { get; set; } = [];
    public ActiveActivityDto? CurrentActivity { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        
        // Operatörün şu an üzerinde çalıştığı iş
        CurrentActivity = await conn.QueryFirstOrDefaultAsync<ActiveActivityDto>(@"
            SELECT a.Id, a.StartTime, po.DocNo, rs.OperationName, wc.Name as WorkCenterName
            FROM ProductionActivity a
            JOIN ProductionOrder po ON po.Id = a.ProductionOrderId
            JOIN ProductRouteStep rs ON rs.Id = CAST(a.Notes as UNIQUEIDENTIFIER)
            JOIN WorkCenter wc ON wc.Id = a.WorkCenterId
            WHERE a.UserId = @UserId AND a.EndTime IS NULL", new { UserId = user.Id });

        // Başlatabileceği (Hazır) görevler: Önceki aşaması bitmiş olanlar
        ActiveTasks = await conn.QueryAsync<ActiveTaskDto>(@"
            SELECT po.Id as OrderId, po.DocNo, i.Name as ItemName, rs.Id as RouteStepId, rs.OperationName, wc.Name as WorkCenterName
            FROM ProductionOrder po
            JOIN Item i ON i.Id = po.ItemId
            JOIN ProductRouteStep rs ON rs.Id = po.CurrentRouteStepId
            JOIN WorkCenter wc ON wc.Id = rs.WorkCenterId
            WHERE po.Status IN ('RELEASED', 'IN_PROGRESS')
            AND NOT EXISTS (SELECT 1 FROM ProductionActivity WHERE ProductionOrderId = po.Id AND EndTime IS NULL)");
    }

    public async Task<IActionResult> OnPostStartAsync(Guid orderId, Guid stepId)
    {
        // Business logic will call ProductionActivityService.StartActivityAsync
        // For simplicity in this demo, we'll assume it's implemented and available
        return RedirectToPage();
    }

    public record ActiveTaskDto { public Guid OrderId { get; set; } public string DocNo { get; set; } = ""; public string ItemName { get; set; } = ""; public Guid RouteStepId { get; set; } public string OperationName { get; set; } = ""; public string WorkCenterName { get; set; } = ""; }
    public record ActiveActivityDto { public Guid Id { get; set; } public DateTime StartTime { get; set; } public string DocNo { get; set; } = ""; public string OperationName { get; set; } = ""; public string WorkCenterName { get; set; } = ""; }
}
