using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Picking;

[Authorize]
public class TerminalModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<TerminalModel> logger) : PageModel
{
    public PickTaskTermDto? ActiveTask { get; set; }
    public IEnumerable<PendingTaskDto> PendingTasks { get; set; } = [];
    public CurrentLineDto? CurrentLine { get; set; }

    public async Task OnGetAsync(Guid? taskId, CancellationToken ct)
    {
        // El terminali: toplama görevleri listelenir, barkod onayıyla satırlar tamamlanır
        using var conn = db.Open();

        PendingTasks = await conn.QueryAsync<PendingTaskDto>(
            new CommandDefinition(@"
            SELECT t.Id, t.DocNo, t.Status,
                   (SELECT COUNT(*) FROM PickTaskLine WHERE PickTaskId = t.Id) AS TotalLines,
                   (SELECT COUNT(*) FROM PickTaskLine WHERE PickTaskId = t.Id AND QtyPickedBase >= QtyRequestedBase) AS DoneLines
            FROM PickTask t
            WHERE t.CompanyId = @CompanyId AND t.Status IN (@StDraft, @StReleased, @StAssigned, @StInProgress)
            ORDER BY t.CreatedAt ASC",
            new { CompanyId = company.Id, StDraft = DocStatus.Draft, StReleased = PickTaskStatus.Released,
                  StAssigned = DocStatus.Assigned, StInProgress = DocStatus.InProgress },
            cancellationToken: ct));

        if (!taskId.HasValue) return;

        ActiveTask = await conn.QueryFirstOrDefaultAsync<PickTaskTermDto>(
            new CommandDefinition(@"
            SELECT t.Id, t.DocNo, t.Status
            FROM PickTask t
            WHERE t.Id = @TaskId AND t.CompanyId = @CompanyId",
            new { TaskId = taskId, CompanyId = company.Id }, cancellationToken: ct));

        if (ActiveTask == null) return;

        // Tamamlanmamış ilk satır — RAF ROTASI (serpentine) sırası: operatör depoda zikzak değil
        // koğuş+raf sırasıyla yürür (Plan 56). Bin.Zone+SortNo seed'li (R-A-01→1 ... R-B-02→5).
        // PickSeq dondurulunca (Faz B) ORDER BY PickSeq olacak; şimdilik canlı Zone/SortNo.
        CurrentLine = await conn.QueryFirstOrDefaultAsync<CurrentLineDto>(
            new CommandDefinition(@"
            SELECT TOP 1 l.Id, l.QtyRequestedBase AS QtyToPickBase, l.QtyPickedBase,
                   i.Code AS ItemCode, i.Name AS ItemName,
                   b.Code AS BinCode, w.Code AS WhCode
            FROM PickTaskLine l
            JOIN PickTask pt ON pt.Id = l.PickTaskId
            JOIN Item i ON i.Id = l.ItemId
            LEFT JOIN Bin b ON b.Id = l.TargetBinId
            LEFT JOIN Warehouse w ON w.Id = l.TargetWarehouseId
            WHERE l.PickTaskId = @TaskId AND l.QtyPickedBase < l.QtyRequestedBase
              AND pt.CompanyId = @CompanyId
            ORDER BY l.PickSeq, b.Zone, b.SortNo, l.Id",
            new { TaskId = taskId, CompanyId = company.Id }, cancellationToken: ct));

        // Aktif ekran ilerleme sayacı ("Kalem 3/10") — toplanan + toplam
        if (ActiveTask != null)
        {
            var prog = await conn.QueryFirstOrDefaultAsync<(int Done, int Total)>(
                new CommandDefinition(@"
                SELECT
                    SUM(CASE WHEN QtyPickedBase >= QtyRequestedBase THEN 1 ELSE 0 END) AS Done,
                    COUNT(*) AS Total
                FROM PickTaskLine WHERE PickTaskId = @TaskId",
                new { TaskId = taskId }, cancellationToken: ct));
            ActiveTask.DoneLines = prog.Done;
            ActiveTask.TotalLines = prog.Total;
        }
    }

    public async Task<IActionResult> OnPostConfirmAsync(Guid taskId, Guid lineId, string barcode, CancellationToken ct)
    {
        // Barkod onayı + satır tamamlama + durum geçişi — iş mantığı SP'de (SQL-First).
        // sp_PickConfirm: barkod doğrula, satırı tamamla, DRAFT→IN_PROGRESS, bitince COMPLETED.
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(
                new CommandDefinition("sp_PickConfirm",
                    new { TaskId = taskId, LineId = lineId, Barcode = barcode, CompanyId = company.Id, UserId = user.Id },
                    commandType: CommandType.StoredProcedure, cancellationToken: ct));
            TempData["Success"] = "Toplama onaylandı!";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (SqlException sqlEx) when (sqlEx.Number is >= 50000 and < 60000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı (barkod eşleşmedi / satır yok)
            TempData["Error"] = sqlEx.Message;
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Pick onaylama SQL hatası");
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { taskId });
    }

    public record PickTaskTermDto { public Guid Id { get; set; } public string DocNo { get; set; } = ""; public string Status { get; set; } = ""; public int DoneLines { get; set; } public int TotalLines { get; set; } }
    public record PendingTaskDto { public Guid Id { get; set; } public string DocNo { get; set; } = ""; public string Status { get; set; } = ""; public int TotalLines { get; set; } public int DoneLines { get; set; } }
    public record CurrentLineDto
    {
        public Guid Id { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public decimal QtyToPickBase { get; set; }
        public decimal QtyPickedBase { get; set; }
        public string? BinCode { get; set; }
        public string? WhCode { get; set; }
        public decimal Remaining => QtyToPickBase - QtyPickedBase;
    }
}
