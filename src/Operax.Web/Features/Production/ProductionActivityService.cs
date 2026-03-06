using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Production;

public class ProductionActivityService(Db db)
{
    public async Task ConsumeMaterialAsync(Guid productionOrderId, Guid itemId, decimal qty, string? lotNo = null, string? serialNo = null)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            // 0. İş Emrini ve Üretim Alanını Çek
            var order = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT SourceBinId FROM ProductionOrder WHERE Id = @Id", new { Id = productionOrderId }, trans);
            Guid? sourceBinId = order?.SourceBinId;

            // 1. Maliyeti Çek...
            var item = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT LastPurchasePrice FROM Item WHERE Id = @Id", new { Id = itemId }, trans);
            decimal unitPrice = item?.LastPurchasePrice ?? 0;

            // 2. Sarfiyat Kaydı... (ProductionConsumption tablosu)
            await conn.ExecuteAsync(@"
                INSERT INTO ProductionConsumption (ProductionOrderId, ItemId, QtyConsumed, UnitPrice, LotNo, SerialNo, RouteStepId)
                VALUES (@OrderId, @ItemId, @Qty, @Price, @Lot, @Serial, @BinId)",
                new { OrderId = productionOrderId, ItemId = itemId, Qty = qty, Price = unitPrice, Lot = lotNo, Serial = serialNo, BinId = sourceBinId }, trans);

            // 3. Stoktan Düş (StockMovement)
            await conn.ExecuteAsync(@"
                INSERT INTO StockMovement (ItemId, MovementType, Qty, ReferenceId, LotNo, SerialNo, BinId)
                VALUES (@ItemId, 'CONSUMPTION', @Qty * -1, @OrderId, @Lot, @Serial, @BinId)",
                new { ItemId = itemId, Qty = qty, OrderId = productionOrderId, Lot = lotNo, Serial = serialNo, BinId = sourceBinId }, trans);

            // 4. Fiili Maliyeti Güncelle
            await conn.ExecuteAsync(@"
                UPDATE ProductionOrder 
                SET ActualMaterialCost = ActualMaterialCost + (@Qty * @Price)
                WHERE Id = @OrderId",
                new { OrderId = productionOrderId, Qty = qty, Price = unitPrice }, trans);

            trans.Commit();
        }
        catch { trans.Rollback(); throw; }
    }
    public async Task StartActivityAsync(Guid productionOrderId, Guid workCenterId, Guid routeStepId)
    {
        using var conn = db.Open();
        
        // 0. Ardışık Operasyon Kontrolü (Predecessor Check)
        var currentStep = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT StepOrder, ProductRouteId FROM ProductRouteStep WHERE Id = @Id", new { Id = routeStepId });
            
        if (currentStep != null)
        {
            var prevStep = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT Id FROM ProductRouteStep 
                WHERE ProductRouteId = @RouteId AND StepOrder < @Order 
                ORDER BY StepOrder DESC", new { RouteId = currentStep.ProductRouteId, Order = currentStep.StepOrder });
                
            if (prevStep != null)
            {
                var finishedInPrev = await conn.ExecuteScalarAsync<decimal>(@"
                    SELECT ISNULL(SUM(QtyProduced), 0) FROM ProductionActivity 
                    WHERE ProductionOrderId = @OrderId AND Notes = @PrevStepId",
                    new { OrderId = productionOrderId, PrevStepId = prevStep.Id.ToString() });
                
                if (finishedInPrev <= 0) 
                    throw new Exception("Önceki istasyon henüz ürün bitirmediği için burada işe başlanamaz.");
            }
        }

        // 1. Aktif aktivite kontrolü
        var active = await conn.QueryFirstOrDefaultAsync("SELECT Id FROM ProductionActivity WHERE ProductionOrderId = @OrderId AND EndTime IS NULL", new { OrderId = productionOrderId });
        if (active != null) throw new Exception("Bu iş emri için halihazırda devam eden bir aktivite var.");

        // 2. Başlat
        await conn.ExecuteAsync(@"
            INSERT INTO ProductionActivity (Id, ProductionOrderId, WorkCenterId, StartTime, Notes)
            VALUES (NEWID(), @OrderId, @WcId, @Now, @StepId)",
            new { OrderId = productionOrderId, WcId = workCenterId, Now = DateTime.UtcNow, StepId = routeStepId.ToString() });
    }

    public async Task StopActivityAsync(Guid productionOrderId, decimal qty = 1)
    {
        using var conn = db.Open();
        
        var activity = await conn.QueryFirstOrDefaultAsync<ActivityDto>(@"
            SELECT a.*, w.LaborRatePerSec, w.ElectricityRatePerSec, w.MachineRatePerSec 
            FROM ProductionActivity a
            JOIN WorkCenter w ON w.Id = a.WorkCenterId
            WHERE a.ProductionOrderId = @OrderId AND a.EndTime IS NULL",
            new { OrderId = productionOrderId });

        if (activity == null) throw new Exception("Durdurulacak aktif bir aktivite bulunamadı.");

        var endTime = DateTime.UtcNow;
        var durationSec = (decimal)(endTime - activity.StartTime).TotalSeconds;

        var laborCost = durationSec * activity.LaborRatePerSec;
        var energyCost = durationSec * activity.ElectricityRatePerSec;

        await conn.ExecuteAsync(@"
            UPDATE ProductionActivity 
            SET EndTime = @EndTime,
                QtyProduced = @Qty,
                CalculatedLaborCost = @LaborCost,
                CalculatedEnergyCost = @EnergyCost
            WHERE Id = @Id",
            new { Id = activity.Id, EndTime = endTime, Qty = qty, LaborCost = laborCost, EnergyCost = energyCost });

        // 3. İş Emrine Kümülatif Maliyetleri Ekle
        await conn.ExecuteAsync(@"
            UPDATE ProductionOrder 
            SET CumulativeLaborCost = CumulativeLaborCost + @LaborCost,
                CumulativeEnergyCost = CumulativeEnergyCost + @EnergyCost,
                ActualResourceCost = ActualResourceCost + @LaborCost + @EnergyCost
            WHERE Id = @OrderId",
            new { OrderId = productionOrderId, LaborCost = laborCost, EnergyCost = energyCost });

        // 4. İzlenebilirlik Bildirimi (Opsiyonel Etiket veya Dijital Kuyruk)
        // Kullanıcı etiketten emin değilse; bu adım sadece bir 'Dijital Refakat Formu' güncellemesi olarak çalışabilir.
        if (qty > 0)
        {
            // Senaryo A: Fiziksel bir Refakat Formu (Travel Card) varsa, üzerine barkod basılmaz, sadece sistem güncellenir.
            // Senaryo B: İstasyonlar uzaksa, sadece bu adımın bittiğine dair dijital bildirim (WIP Queue) oluşturulur.
            await conn.ExecuteAsync(@"
                INSERT INTO ProductionMessageLog (ProductionOrderId, Message, Type) 
                VALUES (@OrderId, @Msg, 'WIP_FLOW')", 
                new { 
                    OrderId = productionOrderId, 
                    Msg = $"{qty} adet ürün {activity.Id} adımından başarıyla çıktı. Bir sonraki istasyon için hazır." 
                });
        }
    }

    private record ActivityDto { 
        public Guid Id { get; set; } 
        public DateTime StartTime { get; set; } 
        public decimal LaborRatePerSec { get; set; } 
        public decimal ElectricityRatePerSec { get; set; } 
        public decimal MachineRatePerSec { get; set; }
    }
}
