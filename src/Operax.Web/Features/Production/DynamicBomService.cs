using System.Data;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Production;

public class DynamicBomService(Db db)
{
    public async Task CalculateAndCreateOrderLinesAsync(Guid productionOrderId, Guid productModelId, Guid productRouteId, Dictionary<string, string> parameterValues)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        decimal totalPlannedMaterialCost = 0;
        try
        {
            // 1. Model Reçetesini Getir
            var bomLines = await conn.QueryAsync<BomLineDto>(@"
                SELECT * FROM ProductModelBOM WHERE ProductModelId = @ModelId", 
                new { ModelId = productModelId }, trans);

            foreach (var bom in bomLines)
            {
                // 2. Formülü Hesapla (Basit string replace ve eval simülasyonu)
                decimal netQty = EvaluateFormula(bom.QtyFormula, parameterValues);
                
                // 3. Fireyi Ekle (Waste)
                decimal grossQty = netQty * (1 + (bom.WastePercentage / 100m));
                
                // Koşul kontrolü
                if (!string.IsNullOrEmpty(bom.ConditionFormula))
                {
                    if (!EvaluateCondition(bom.ConditionFormula, parameterValues)) continue;
                }

                if (grossQty > 0)
                {
                    // Hammadde fiyatını çek (Basit üretim maliyeti hesabı)
                    decimal itemCost = await conn.ExecuteScalarAsync<decimal>(
                        "SELECT ISNULL(LastPurchasePrice, 10.0) FROM Item WHERE Id = @ItemId", 
                        new { ItemId = bom.ComponentItemId }, trans);

                    totalPlannedMaterialCost += (grossQty * itemCost);

                    await conn.ExecuteAsync(@"
                        INSERT INTO ProductionOrderLine (ProductionOrderId, ItemId, QtyRequired)
                        VALUES (@OrderId, @ItemId, @Qty)",
                        new { OrderId = productionOrderId, ItemId = bom.ComponentItemId, Qty = grossQty }, trans);
                }
            }

            // 3. Rota Standart Maliyetlerini Hesapla
            decimal totalPlannedResourceCost = await conn.ExecuteScalarAsync<decimal>(@"
                SELECT ISNULL(SUM(StandardLaborCost + StandardMachineCost), 0) 
                FROM ProductRouteStep 
                WHERE ProductRouteId = @RouteId", 
                new { RouteId = productRouteId }, trans);

            // 4. Tahmini Maliyetleri Kaydet
            await conn.ExecuteAsync(@"
                UPDATE ProductionOrder 
                SET PlannedMaterialCost = @MatCost, 
                    PlannedResourceCost = @ResCost 
                WHERE Id = @OrderId",
                new { OrderId = productionOrderId, MatCost = totalPlannedMaterialCost, ResCost = totalPlannedResourceCost }, trans);

            trans.Commit();
        }
        catch { trans.Rollback(); throw; }
    }

    private decimal EvaluateFormula(string formula, Dictionary<string, string> values)
    {
        string processed = formula;
        foreach (var v in values) processed = processed.Replace($"[{v.Key}]", v.Value);
        
        // Basit matematiksel eval (Gerçek projede NCalc vb. kütüphane kullanılır)
        try { return Convert.ToDecimal(new DataTable().Compute(processed, null)); }
        catch { return 0; }
    }

    private bool EvaluateCondition(string condition, Dictionary<string, string> values)
    {
        // Basit koşul eval simülasyonu
        // Örn: [GLASS_TYPE] == 'FROSTED'
        string processed = condition;
        foreach (var v in values) processed = processed.Replace($"[{v.Key}]", $"'{v.Value}'");
        
        try { return (bool)new DataTable().Compute(processed, null); }
        catch { return false; }
    }

    public record BomLineDto { 
        public Guid ComponentItemId { get; set; } 
        public string QtyFormula { get; set; } = ""; 
        public string? ConditionFormula { get; set; } 
        public decimal WastePercentage { get; set; }
    }
}
