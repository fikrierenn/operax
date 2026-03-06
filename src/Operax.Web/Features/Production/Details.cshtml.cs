using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Production;

public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    public ProductionOrderDto Order { get; set; } = new();
    public IEnumerable<ProductionLineDto> Lines { get; set; } = [];
    public Guid? ActivePickTaskId { get; set; }

    public async Task OnGetAsync(Guid id)
    {
        using var conn = db.Open();

        Order = await conn.QueryFirstOrDefaultAsync<ProductionOrderDto>(@"
            SELECT p.*, i.Code as ItemCode, i.Name as ItemName
            FROM ProductionOrder p
            JOIN Item i ON i.Id = p.ItemId
            WHERE p.Id = @Id", new { Id = id }) ?? new();

        Lines = await conn.QueryAsync<ProductionLineDto>(@"
            SELECT l.*, i.Code as ItemCode, i.Name as ItemName, dv.Code as UomCode
            FROM ProductionOrderLine l
            JOIN Item i ON i.Id = l.ItemId
            JOIN DictionaryValue dv ON dv.Id = i.BaseUomId
            WHERE l.ProductionOrderId = @Id", new { Id = id });

        ActivePickTaskId = await conn.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT Id FROM PickTask WHERE SourceDocId = @Id AND DocNo LIKE 'PRD-PCK%'", 
            new { Id = id });
    }

    public async Task<IActionResult> OnPostLoadBOMAsync(Guid id)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();

        try 
        {
            var order = await conn.QueryFirstOrDefaultAsync<ProductionOrderDto>("SELECT * FROM ProductionOrder WHERE Id = @Id", new { Id = id }, trans);
            // Üretim emri bulunamadıysa iptal et
            if (order is null) { trans.Rollback(); return RedirectToPage(new { id }); }

            // Reçeteden (BOM) hammadde ihtiyaçlarını çek
            var bomItems = await conn.QueryAsync(@"
                SELECT ChildItemId, QtyRequired * @TargetQty as TotalRequired
                FROM ItemBOM WHERE ParentItemId = @ItemId AND IsActive = 1", 
                new { ItemId = order.ItemId, TargetQty = order.QtyTarget }, trans);

            foreach (var bom in bomItems)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO ProductionOrderLine (Id, ProductionOrderId, ItemId, QtyRequired)
                    VALUES (NEWID(), @OrderId, @ItemId, @Qty)",
                    new { OrderId = id, ItemId = bom.ChildItemId, Qty = bom.TotalRequired }, trans);
            }

            trans.Commit();
        }
        catch { trans.Rollback(); throw; }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreatePickTaskAsync(Guid id)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();

        try 
        {
            var order = await conn.QueryFirstOrDefaultAsync<ProductionOrderDto>("SELECT * FROM ProductionOrder WHERE Id = @Id", new { Id = id }, trans);
            // Üretim emri bulunamadıysa iptal et
            if (order is null) { trans.Rollback(); return RedirectToPage(new { id }); }
            var lines = await conn.QueryAsync<ProductionLineDto>("SELECT * FROM ProductionOrderLine WHERE ProductionOrderId = @Id", new { Id = id }, trans);

            var taskId = Guid.NewGuid();
            var docNo = $"PRD-PCK-{order.DocNo}";

            // 1. Pick Task Header
            await conn.ExecuteAsync(@"
                INSERT INTO PickTask (Id, CompanyId, DocNo, SourceDocId, Status, Priority, CreatedBy)
                VALUES (@Id, @CompanyId, @DocNo, @SourceDocId, 'DRAFT', 20, @UserId)",
                new { Id = taskId, CompanyId = company.Id, DocNo = docNo, SourceDocId = id, UserId = user.Id }, trans);

            // 2. Pick Task Lines (Finding raw material bins)
            foreach (var line in lines)
            {
                var targetBinId = await conn.QueryFirstOrDefaultAsync<Guid?>(
                    "SELECT TOP 1 Id FROM Bin WHERE WarehouseId = @WhId AND IsPickingArea = 1", 
                    new { WhId = order.TargetWarehouseId }, trans);

                await conn.ExecuteAsync(@"
                    INSERT INTO PickTaskLine (PickTaskId, ItemId, UomId, TargetWarehouseId, TargetBinId, QtyRequested, QtyRequestedBase)
                    SELECT @TaskId, @ItemId, BaseUomId, @WhId, @BinId, @Qty, @Qty
                    FROM Item WHERE Id = @ItemId",
                    new { TaskId = taskId, line.ItemId, WhId = order.TargetWarehouseId, BinId = targetBinId, Qty = line.QtyRequired }, trans);
            }

            trans.Commit();
            return RedirectToPage("/Picking/Details", new { id = taskId });
        }
        catch { trans.Rollback(); throw; }
    }

    public async Task<IActionResult> OnPostFinishAsync(Guid id, decimal qty)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();

        try 
        {
            var order = await conn.QueryFirstOrDefaultAsync<ProductionOrderDto>("SELECT * FROM ProductionOrder WHERE Id = @Id", new { Id = id }, trans);
            // Üretim emri bulunamadıysa iptal et
            if (order is null) { trans.Rollback(); return RedirectToPage("./Index"); }

            // 1. Stock Movement (PRODUCTION RECEIPT)
            const string moveSql = @"
                INSERT INTO StockMovement (CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
                SELECT @CompanyId, @WhId, @BinId, @ItemId, 'PRODUCTION', @Qty, i.BaseUomId, @Qty, 'PRODUCTION', @OrderId, @DocNo, @UserId
                FROM Item i WHERE i.Id = @ItemId";
            
            await conn.ExecuteAsync(moveSql, new { 
                CompanyId = company.Id, WhId = order.TargetWarehouseId, BinId = order.TargetBinId ?? Guid.Empty,
                ItemId = order.ItemId, Qty = qty, OrderId = id, DocNo = order.DocNo, UserId = user.Id
            }, trans);

            // 2. Update Order
            await conn.ExecuteAsync("UPDATE ProductionOrder SET Status = 'COMPLETED', QtyProduced = @Qty, CompletedAt = GETUTCDATE() WHERE Id = @Id", new { Id = id, Qty = qty }, trans);

            trans.Commit();
        }
        catch { trans.Rollback(); throw; }

        return RedirectToPage("./Index");
    }

    public record ProductionOrderDto { 
        public Guid Id { get; set; } 
        public string DocNo { get; set; } = ""; 
        public Guid ItemId { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public decimal QtyTarget { get; set; } 
        public decimal QtyProduced { get; set; }
        public string Status { get; set; } = "";
        public Guid? TargetWarehouseId { get; set; }
        public Guid? TargetBinId { get; set; }
    }
    public record ProductionLineDto { 
        public Guid Id { get; set; } 
        public Guid ItemId { get; set; }
        public string ItemCode { get; set; } = ""; 
        public string ItemName { get; set; } = ""; 
        public string UomCode { get; set; } = "";
        public Guid UomId { get; set; }
        public decimal QtyRequired { get; set; } 
        public decimal QtyIssued { get; set; } 
    }
}
