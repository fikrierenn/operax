using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Manufacturing.WorkOrders;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    public RouteDto Route { get; set; } = new();
    public IEnumerable<RouteStepDto> Steps { get; set; } = [];
    public IEnumerable<DdlDto> WorkCenters { get; set; } = [];

    public async Task OnGetAsync(Guid id)
    {
        // Rota detayını ve adımlarını yükler
        using var conn = db.Open();

        Route = await conn.QueryFirstOrDefaultAsync<RouteDto>(@"
            SELECT Id, Code, Name, IsActive
            FROM ProductRoute
            WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }) ?? new();

        if (Route.Id == Guid.Empty) return;

        Steps = await conn.QueryAsync<RouteStepDto>(@"
            /* isolation-guard:ignore: parent ProductRoute CompanyId ile dogrulandi (satir 21) */
            SELECT s.Id, s.StepOrder, s.OperationCode, s.OperationName,
                   s.WorkCenterId, wc.Name AS WorkCenterName,
                   s.StandardDurationSec / 60 AS StandardDurationMin,
                   s.StandardLaborCost, s.StandardMachineCost
            FROM ProductRouteStep s
            JOIN WorkCenter wc ON wc.Id = s.WorkCenterId
            WHERE s.ProductRouteId = @RouteId
            ORDER BY s.StepOrder",
            new { RouteId = id });

        WorkCenters = await conn.QueryAsync<DdlDto>(@"
            SELECT Id, Code + ' — ' + Name AS Name
            FROM WorkCenter
            WHERE CompanyId = @CompanyId AND IsActive = 1
            ORDER BY Code",
            new { CompanyId = company.Id });
    }

    public async Task<IActionResult> OnPostAddStepAsync(
        Guid id, string operationCode, string operationName,
        Guid workCenterId, int standardDurationMin,
        decimal standardLaborCost, decimal standardMachineCost)
    {
        // Rotaya yeni adım ekler — rotanın şirkete ait olduğunu doğrula
        using var conn = db.Open();

        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM ProductRoute WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id });
        if (exists == 0) return RedirectToPage("./Index");

        // WorkCenter aynı şirkete ait mi?
        var wcExists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM WorkCenter WHERE Id = @WcId AND CompanyId = @CompanyId",
            new { WcId = workCenterId, CompanyId = company.Id });
        if (wcExists == 0) return RedirectToPage(new { id });

        // Sonraki sıra numarasını hesapla
        var nextOrder = await conn.ExecuteScalarAsync<int>(
            "SELECT ISNULL(MAX(StepOrder), 0) + 10 FROM ProductRouteStep WHERE ProductRouteId = @RouteId",
            new { RouteId = id });

        await conn.ExecuteAsync(@"
            /* isolation-guard:ignore: parent ProductRoute CompanyId ile dogrulandi (satir 56); WorkCenter de CompanyId ile dogrulandi (satir 62) */
            INSERT INTO ProductRouteStep
                (Id, ProductRouteId, StepOrder, OperationCode, OperationName,
                 WorkCenterId, StandardDurationSec, StandardLaborCost, StandardMachineCost)
            VALUES
                (NEWID(), @RouteId, @StepOrder, @OpCode, @OpName,
                 @WorkCenterId, @DurationSec, @LaborCost, @MachineCost)",
            new
            {
                RouteId = id,
                StepOrder = nextOrder,
                OpCode = operationCode.ToUpper(),
                OpName = operationName,
                WorkCenterId = workCenterId,
                DurationSec = standardDurationMin * 60,
                LaborCost = standardLaborCost,
                MachineCost = standardMachineCost
            });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteStepAsync(Guid id, Guid stepId)
    {
        // Adımı siler — JOIN ile şirket sahipliği doğrulanır
        using var conn = db.Open();
        await conn.ExecuteAsync(@"
            DELETE s FROM ProductRouteStep s
            JOIN ProductRoute r ON r.Id = s.ProductRouteId
            WHERE s.Id = @StepId AND r.CompanyId = @CompanyId",
            new { StepId = stepId, CompanyId = company.Id });
        return RedirectToPage(new { id });
    }

    public record RouteDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public record RouteStepDto
    {
        public Guid Id { get; set; }
        public int StepOrder { get; set; }
        public string OperationCode { get; set; } = "";
        public string OperationName { get; set; } = "";
        public Guid WorkCenterId { get; set; }
        public string WorkCenterName { get; set; } = "";
        public int StandardDurationMin { get; set; }
        public decimal StandardLaborCost { get; set; }
        public decimal StandardMachineCost { get; set; }
    }
}
