using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Identity;

namespace Operax.Web.Features.Picking;

public class DetailsModel(Db db, ICurrentCompany company, UserManager<IdentityUser> userManager) : PageModel
{
    public PickTaskDto Task { get; set; } = new();
    public IEnumerable<PickLineDto> Lines { get; set; } = [];
    public IEnumerable<IdentityUser> WarehouseStaff { get; set; } = [];

    public async Task OnGetAsync(Guid id)
    {
        using var conn = db.Open();
        
        Task = await conn.QueryFirstOrDefaultAsync<PickTaskDto>(@"
            SELECT t.*, u.UserName as AssignedUserName 
            FROM PickTask t 
            LEFT JOIN AspNetUsers u ON u.Id = t.AssignedUserId 
            WHERE t.Id = @Id", new { Id = id }) ?? new();

        Lines = await conn.QueryAsync<PickLineDto>(@"
            SELECT l.*, i.Code as ItemCode, i.Name as ItemName, 
                   tb.Code as TargetBinCode, pb.Code as PickedBinCode,
                   u.UserName as PickedByUserName
            FROM PickTaskLine l
            JOIN Item i ON i.Id = l.ItemId
            JOIN Bin tb ON tb.Id = l.TargetBinId
            LEFT JOIN Bin pb ON pb.Id = l.PickedBinId
            LEFT JOIN AspNetUsers u ON u.Id = l.PickedByUserId
            WHERE l.PickTaskId = @Id", new { Id = id });

        WarehouseStaff = await userManager.GetUsersInRoleAsync("Warehouse");
        if (!WarehouseStaff.Any()) WarehouseStaff = userManager.Users.Take(10).ToList();
    }

    public async Task<IActionResult> OnPostAssignAsync(Guid id, string userId)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("UPDATE PickTask SET AssignedUserId = @UserId, Status = 'ASSIGNED' WHERE Id = @Id", new { Id = id, UserId = userId });
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPickLineAsync(Guid id, Guid lineId, decimal qty)
    {
        var userId = userManager.GetUserId(User);
        using var conn = db.Open();
        
        // 1. Satırı güncelle (Kim, Ne Zaman, Ne Kadar)
        const string updateLineSql = @"
            UPDATE PickTaskLine 
            SET QtyPicked = @Qty, 
                QtyPickedBase = @Qty, 
                PickedByUserId = @UserId, 
                PickedAt = GETUTCDATE(),
                PickedBinId = TargetBinId 
            WHERE Id = @LineId";
        
        await conn.ExecuteAsync(updateLineSql, new { Qty = qty, UserId = userId, LineId = lineId });
        
        // 2. STOK HAREKETİ (Hammadde Sarfiyatı / Issue)
        var line = await conn.QueryFirstOrDefaultAsync("SELECT * FROM PickTaskLine WHERE Id = @Id", new { Id = lineId });
        var task = await conn.QueryFirstOrDefaultAsync("SELECT * FROM PickTask WHERE Id = @Id", new { Id = id });
        // Satır veya görev bulunamadıysa yönlendir
        if (line is null || task is null) return RedirectToPage(new { id });

        const string moveSql = @"
            INSERT INTO StockMovement (CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
            VALUES (@CompanyId, @WarehouseId, @BinId, @ItemId, 'ISSUE', -@Qty, @UomId, @Qty, 'PICKING', @PickTaskId, @DocNo, @UserId)";
        
        await conn.ExecuteAsync(moveSql, new { 
            CompanyId = company.Id, WarehouseId = line.TargetWarehouseId, BinId = line.TargetBinId, 
            ItemId = line.ItemId, Qty = qty, UomId = line.UomId, PickTaskId = id, DocNo = task.DocNo, UserId = userId 
        });

        // 3. ÜRETİM GÜNCELLEME: Eğer bu bir hammadde toplamasıysa
        if (task.DocNo.StartsWith("PRD-PCK-"))
        {
            await conn.ExecuteAsync(@"
                UPDATE ProductionOrderLine 
                SET QtyIssued = QtyIssued + @Qty 
                WHERE ProductionOrderId = @PrdOrderId AND ItemId = @ItemId",
                new { Qty = qty, PrdOrderId = task.SourceDocId, ItemId = line.ItemId });
        }

        // 4. Görev durumunu güncelle
        await conn.ExecuteAsync(@"
            UPDATE PickTask SET Status = 'IN_PROGRESS', StartedAt = ISNULL(StartedAt, GETUTCDATE()) WHERE Id = @Id;
            IF NOT EXISTS (SELECT 1 FROM PickTaskLine WHERE PickTaskId = @Id AND QtyPicked = 0)
                UPDATE PickTask SET Status = 'COMPLETED', CompletedAt = GETUTCDATE() WHERE Id = @Id;
        ", new { Id = id });

        return RedirectToPage(new { id });
    }

    public record PickTaskDto { 
        public Guid Id { get; set; } 
        public string DocNo { get; set; } = ""; 
        public string Status { get; set; } = ""; 
        public string? AssignedUserId { get; set; }
        public string? AssignedUserName { get; set; }
    }

    public record PickLineDto { 
        public Guid Id { get; set; } 
        public string ItemCode { get; set; } = ""; 
        public string ItemName { get; set; } = ""; 
        public string TargetBinCode { get; set; } = ""; 
        public string? PickedBinCode { get; set; }
        public decimal QtyRequested { get; set; } 
        public decimal QtyPicked { get; set; } 
        public string? PickedByUserName { get; set; }
        public DateTime? PickedAt { get; set; }
    }
}
