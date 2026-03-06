using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Production;

public class ProductionReceiptService(Db db)
{
    public async Task RecordInspectionAsync(Guid productionOrderId, decimal qty, string result, string? failAction = null, Guid? defectId = null)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            // 1. Teftiş Kaydı
            await conn.ExecuteAsync(@"
                INSERT INTO ProductionInspection (ProductionOrderId, CompanyId, InspectorUserId, QtyInspected, QtyPassed, Result, FailAction, DefectCodeId)
                SELECT @OrderId, CompanyId, CreatedBy, @Qty, (CASE WHEN @Result='PASS' THEN @Qty ELSE 0 END), @Result, @Action, @Defect
                FROM ProductionOrder WHERE Id = @OrderId",
                new { OrderId = productionOrderId, Qty = qty, Result = result, Action = failAction, Defect = defectId }, trans);

            if (result == "FAIL")
            {
                if (failAction == "SCRAP")
                {
                    // Hurda: Üretim hedefini arttır ki yeniden üretim tetiklensin (Opsiyonel)
                    // Veya sadece üretilen adedi arttırma.
                }
                else if (failAction == "REWORK")
                {
                    // 1. Rework Emri Oluştur (Esnek Akış)
                    var reworkId = Guid.NewGuid();
                    await conn.ExecuteAsync(@"
                        INSERT INTO ProductionRework (Id, CompanyId, ProductionOrderId, InspectionId, Status, ReworkStepId)
                        SELECT @Id, CompanyId, @OrderId, @InspId, 'OPEN', @ReworkStep
                        FROM ProductionOrder WHERE Id = @OrderId",
                        new { Id = reworkId, OrderId = productionOrderId, InspId = Guid.NewGuid(), ReworkStep = defectId }, trans); // DefectId placeholder olarak routeStepId gelebilir

                    // 2. İş Emrini Rework Adımına Çek
                    await conn.ExecuteAsync(@"
                        UPDATE ProductionOrder SET Status = 'REWORK', 
                        CurrentRouteStepId = @StepId WHERE Id = @OrderId",
                        new { OrderId = productionOrderId, StepId = defectId }, trans);
                }
                else if (failAction == "CANCEL_ORDER")
                {
                    await conn.ExecuteAsync("UPDATE ProductionOrder SET Status = 'CANCELLED' WHERE Id = @OrderId", new { OrderId = productionOrderId }, trans);
                    // TODO: Sales Order Notify Logic
                }
            }
            else // PASS
            {
                // Stok girişini yap
                await CompleteProductionInternalAsync(productionOrderId, qty, null, null, conn, trans);
            }

            trans.Commit();
        }
        catch { trans.Rollback(); throw; }
    }

    private async Task CompleteProductionInternalAsync(Guid productionOrderId, decimal qty, string? lotNo, string? serialNo, System.Data.IDbConnection conn, System.Data.IDbTransaction trans)
    {
        // ... Mevcut CompleteProductionAsync mantığı buraya taşınacak (Internal hale gelecek)
    }

    public async Task CompleteProductionAsync(Guid productionOrderId, decimal qty, string? lotNo = null, string? serialNo = null)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            // 1. İş Emri Bilgilerini Çek
            var order = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT ItemId, TargetWarehouseId, TargetBinId, ActualMaterialCost, ActualResourceCost, QtyTarget, QtyProduced
                FROM ProductionOrder WHERE Id = @Id", new { Id = productionOrderId }, trans);

            if (order == null) throw new Exception("İş emri bulunamadı.");

            // 2. Mamül Stok Girişi (PRODUCTION)
            await conn.ExecuteAsync(@"
                INSERT INTO StockMovement (ItemId, MovementType, Qty, ReferenceId, WarehouseId, BinId, LotNo, SerialNo)
                VALUES (@ItemId, 'PRODUCTION', @Qty, @OrderId, @WhId, @BinId, @Lot, @Serial)",
                new { 
                    ItemId = (Guid)order.ItemId, 
                    Qty = qty, 
                    OrderId = productionOrderId, 
                    WhId = (Guid?)order.TargetWarehouseId,
                    BinId = (Guid?)order.TargetBinId,
                    Lot = lotNo,
                    Serial = serialNo 
                }, trans);

            // 3. İş Emrini Güncelle
            var newQtyProduced = (decimal)order.QtyProduced + qty;
            var status = newQtyProduced >= (decimal)order.QtyTarget ? "COMPLETED" : "IN_PROGRESS";
            var completedAt = status == "COMPLETED" ? (DateTime?)DateTime.UtcNow : null;

            await conn.ExecuteAsync(@"
                UPDATE ProductionOrder 
                SET QtyProduced = @Qty, 
                    Status = @Status,
                    CompletedAt = @CompletedAt
                WHERE Id = @Id",
                new { Id = productionOrderId, Qty = newQtyProduced, Status = status, CompletedAt = completedAt }, trans);

            trans.Commit();
        }
        catch { trans.Rollback(); throw; }
    }
}
