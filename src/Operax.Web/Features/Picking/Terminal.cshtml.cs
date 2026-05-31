using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Picking;

[Authorize]
public class TerminalModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<TerminalModel> logger) : PageModel
{
    public PickTaskTermDto? ActiveTask { get; set; }
    public IEnumerable<PendingTaskDto> PendingTasks { get; set; } = [];
    public CurrentLineDto? CurrentLine { get; set; }

    public async Task OnGetAsync(Guid? taskId)
    {
        // El terminali: toplama görevleri listelenir, barkod onayıyla satırlar tamamlanır
        using var conn = db.Open();

        PendingTasks = await conn.QueryAsync<PendingTaskDto>(@"
            SELECT t.Id, t.DocNo, t.Status,
                   (SELECT COUNT(*) FROM PickTaskLine WHERE PickTaskId = t.Id) AS TotalLines,
                   (SELECT COUNT(*) FROM PickTaskLine WHERE PickTaskId = t.Id AND QtyPickedBase >= QtyRequestedBase) AS DoneLines
            FROM PickTask t
            WHERE t.CompanyId = @CompanyId AND t.Status IN (@StDraft, @StAssigned, @StInProgress)
            ORDER BY t.CreatedAt ASC",
            new { CompanyId = company.Id, StDraft = DocStatus.Draft, StAssigned = DocStatus.Assigned, StInProgress = DocStatus.InProgress });

        if (!taskId.HasValue) return;

        ActiveTask = await conn.QueryFirstOrDefaultAsync<PickTaskTermDto>(@"
            SELECT t.Id, t.DocNo, t.Status
            FROM PickTask t
            WHERE t.Id = @TaskId AND t.CompanyId = @CompanyId",
            new { TaskId = taskId, CompanyId = company.Id });

        if (ActiveTask == null) return;

        // Tamamlanmamış ilk satır — FIFO sırası
        CurrentLine = await conn.QueryFirstOrDefaultAsync<CurrentLineDto>(@"
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
            ORDER BY l.Id",
            new { TaskId = taskId, CompanyId = company.Id });
    }

    public async Task<IActionResult> OnPostConfirmAsync(Guid taskId, Guid lineId, string barcode)
    {
        // Barkod doğrulama: ürün kodu eşleşiyorsa satırı tamamla
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            var line = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT l.ItemId, l.QtyRequestedBase AS QtyToPickBase, l.QtyPickedBase, i.Code AS ItemCode
                FROM PickTaskLine l
                JOIN PickTask pt ON pt.Id = l.PickTaskId
                JOIN Item i ON i.Id = l.ItemId
                WHERE l.Id = @LineId AND l.PickTaskId = @TaskId AND pt.CompanyId = @CompanyId",
                new { LineId = lineId, TaskId = taskId, CompanyId = company.Id }, trans);

            if (line == null) { TempData["Error"] = "Satır bulunamadı."; return RedirectToPage(new { taskId }); }

            // İş kuralı: barkod ürün koduyla veya barkod listesiyle eşleşmeli
            var barcodeMatch = await conn.ExecuteScalarAsync<int>(@"
                /* isolation-guard:ignore: ItemId, parent PickTaskLine uzerinden CompanyId ile dogrulandi (satir 64) */
                SELECT COUNT(1) FROM ItemBarcode WHERE ItemId = @ItemId AND Barcode = @Barcode",
                new { ItemId = (Guid)line.ItemId, Barcode = barcode }, trans);

            bool codeMatch = string.Equals((string)line.ItemCode, barcode, StringComparison.OrdinalIgnoreCase);

            if (barcodeMatch == 0 && !codeMatch)
            {
                TempData["Error"] = $"Barkod eşleşmedi! Beklenen: {line.ItemCode}";
                return RedirectToPage(new { taskId });
            }

            // Satırı tamamla
            await conn.ExecuteAsync(
                "UPDATE PickTaskLine SET QtyPickedBase = QtyRequestedBase WHERE Id = @LineId",
                new { LineId = lineId }, trans);

            // İş kuralı: DRAFT toplama görevi ilk barkodla IN_PROGRESS'e geçer
            await conn.ExecuteAsync(
                "UPDATE PickTask SET Status = @StInProgress, AssignedUserId = @UserId WHERE Id = @TaskId AND Status = @StDraft",
                new { UserId = user.Id, TaskId = taskId, StDraft = DocStatus.Draft, StInProgress = DocStatus.InProgress }, trans);

            // Tüm satırlar tamamlandıysa COMPLETED yap
            var remaining = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM PickTaskLine WHERE PickTaskId = @TaskId AND QtyPickedBase < QtyRequestedBase",
                new { TaskId = taskId }, trans);
            if (remaining == 0)
                await conn.ExecuteAsync(
                    "UPDATE PickTask SET Status = @StCompleted WHERE Id = @TaskId",
                    new { TaskId = taskId, StCompleted = DocStatus.Completed }, trans);

            trans.Commit();
            TempData["Success"] = "Toplama onaylandı!";
        }
        catch (Exception ex) { logger.LogWarning(ex, "Pick satırı onaylama hatası"); trans.Rollback(); TempData["Error"] = "Hata oluştu."; }
        return RedirectToPage(new { taskId });
    }

    public record PickTaskTermDto { public Guid Id { get; set; } public string DocNo { get; set; } = ""; public string Status { get; set; } = ""; }
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
