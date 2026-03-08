using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Production;

[Authorize]
public class TerminalModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    public IEnumerable<ActiveTaskDto> ActiveTasks   { get; set; } = [];
    public ActiveActivityDto?         CurrentActivity { get; set; }

    public async Task OnGetAsync()
    {
        // Operatörün mevcut aktivitesini ve başlatabileceği görevleri yükler
        using var conn = db.Open();

        // Operatörün açık aktivitesi (EndTime NULL = devam ediyor)
        CurrentActivity = await conn.QueryFirstOrDefaultAsync<ActiveActivityDto>(@"
            SELECT a.Id, a.StartTime, po.DocNo, rs.OperationName, wc.Name as WorkCenterName
            FROM ProductionActivity a
            JOIN ProductionOrder po ON po.Id = a.ProductionOrderId
            JOIN ProductRouteStep rs ON rs.Id = a.RouteStepId
            JOIN WorkCenter wc ON wc.Id = a.WorkCenterId
            WHERE a.UserId = @UserId AND a.EndTime IS NULL AND po.CompanyId = @CompanyId",
            new { UserId = user.Id, CompanyId = company.Id });

        // Başlatabileceği görevler: RELEASED veya IN_PROGRESS, aktif aktivitesi olmayan emirler
        ActiveTasks = await conn.QueryAsync<ActiveTaskDto>(@"
            SELECT po.Id as OrderId, po.DocNo, i.Name as ItemName,
                   rs.Id as RouteStepId, rs.OperationName, wc.Name as WorkCenterName
            FROM ProductionOrder po
            JOIN Item i ON i.Id = po.ItemId
            JOIN ProductRouteStep rs ON rs.Id = po.CurrentRouteStepId
            JOIN WorkCenter wc ON wc.Id = rs.WorkCenterId
            WHERE po.CompanyId = @CompanyId
              AND po.Status IN ('RELEASED', 'IN_PROGRESS')
              AND NOT EXISTS (
                  SELECT 1 FROM ProductionActivity
                  WHERE ProductionOrderId = po.Id AND EndTime IS NULL
              )",
            new { CompanyId = company.Id });
    }

    public async Task<IActionResult> OnPostStartAsync(Guid orderId, Guid stepId)
    {
        // Mevcut açık aktiviteyi kapatır ve yeni aktivite başlatır
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            // Varsa operatörün mevcut açık aktivitesini kapat
            await conn.ExecuteAsync(
                "UPDATE ProductionActivity SET EndTime = GETUTCDATE() WHERE UserId = @UserId AND EndTime IS NULL",
                new { UserId = user.Id }, trans);

            // Rota adımından iş merkezini al
            var workCenterId = await conn.ExecuteScalarAsync<Guid?>(
                "SELECT WorkCenterId FROM ProductRouteStep WHERE Id = @StepId",
                new { StepId = stepId }, trans);

            // Yeni aktivite kaydı oluştur
            await conn.ExecuteAsync(@"
                INSERT INTO ProductionActivity
                    (Id, ProductionOrderId, UserId, WorkCenterId, RouteStepId, StartTime)
                VALUES
                    (NEWID(), @OrderId, @UserId, @WorkCenterId, @StepId, GETUTCDATE())",
                new { OrderId = orderId, UserId = user.Id, WorkCenterId = workCenterId, StepId = stepId }, trans);

            // İş kuralı: üretim emri IN_PROGRESS değilse güncelle
            await conn.ExecuteAsync(
                "UPDATE ProductionOrder SET Status = 'IN_PROGRESS', CurrentRouteStepId = @StepId WHERE Id = @OrderId AND Status <> 'COMPLETED'",
                new { StepId = stepId, OrderId = orderId }, trans);

            trans.Commit();
        }
        catch { trans.Rollback(); throw; }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStopAsync(Guid activityId)
    {
        // Aktif aktiviteyi durdurur (EndTime set eder)
        using var conn = db.Open();
        await conn.ExecuteAsync(
            "UPDATE ProductionActivity SET EndTime = GETUTCDATE() WHERE Id = @Id AND UserId = @UserId",
            new { Id = activityId, UserId = user.Id });
        return RedirectToPage();
    }

    public record ActiveTaskDto
    {
        public Guid   OrderId        { get; set; }
        public string DocNo          { get; set; } = "";
        public string ItemName       { get; set; } = "";
        public Guid   RouteStepId    { get; set; }
        public string OperationName  { get; set; } = "";
        public string WorkCenterName { get; set; } = "";
    }

    public record ActiveActivityDto
    {
        public Guid     Id             { get; set; }
        public DateTime StartTime      { get; set; }
        public string   DocNo          { get; set; } = "";
        public string   OperationName  { get; set; } = "";
        public string   WorkCenterName { get; set; } = "";
    }
}
