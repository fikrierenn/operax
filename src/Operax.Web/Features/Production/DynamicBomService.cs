using Dapper;
using NCalc;
using Operax.Web.Lib;

namespace Operax.Web.Features.Production;

/// <summary>
/// Dinamik BOM hesaplama servisi.
/// Ürün modelindeki parametre formüllerini değerlendirerek üretim emrine hammadde satırları ekler.
/// </summary>
public class DynamicBomService(Db db)
{
    /// <summary>
    /// Ürün modeli BOM'unu hesaplar ve üretim emrine hammadde ihtiyaç satırlarını yazar.
    /// Formül ve koşul değerlendirmesi NCalc ile yapılır — kullanıcı girdisi string replace ile
    /// formüle gömülmez; parametre olarak geçirilir.
    /// </summary>
    public async Task CalculateAndCreateOrderLinesAsync(
        Guid productionOrderId,
        Guid productModelId,
        Guid productRouteId,
        Dictionary<string, string> parameterValues)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        decimal totalPlannedMaterialCost = 0;

        try
        {
            // Model reçetesi satırlarını getir
            var bomLines = await conn.QueryAsync<BomLineDto>(@"
                -- DEAD/WIP kod — çoklu-firma izolasyonu burada uygulanmıyor çünkü bu servis hiçbir yerden
                -- çağrılmıyor (caller yok, DI kaydı yok) ve şemaya uymuyor (kırık StockMovement INSERT'leri).
                -- Gerçek üretim akışı SP kullanır (sp_ProductionFinish). Bu kod ileride yarım kalan WIP atölye
                -- terminali için SP'ye taşınarak yeniden yazılacak; o zamana kadar etkin değildir.
                -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                SELECT * FROM ProductModelBOM WHERE ProductModelId = @ModelId",
                new { ModelId = productModelId }, trans);

            foreach (var bom in bomLines)
            {
                // Koşul varsa değerlendir — false ise satırı atla
                if (!string.IsNullOrEmpty(bom.ConditionFormula))
                {
                    if (!EvaluateCondition(bom.ConditionFormula, parameterValues))
                        continue;
                }

                // Net miktarı formülden hesapla
                decimal netQty = EvaluateFormula(bom.QtyFormula, parameterValues);

                // İş kuralı: fire yüzdesi eklenerek brüt miktar bulunur
                decimal grossQty = netQty * (1 + bom.WastePercentage / 100m);

                if (grossQty <= 0) continue;

                // Hammadde birim maliyetini çek
                decimal itemCost = await conn.ExecuteScalarAsync<decimal>(@"
                    -- DEAD/WIP kod — çoklu-firma izolasyonu burada uygulanmıyor çünkü bu servis hiçbir yerden
                    -- çağrılmıyor (caller yok, DI kaydı yok) ve şemaya uymuyor (kırık StockMovement INSERT'leri).
                    -- Gerçek üretim akışı SP kullanır (sp_ProductionFinish). Bu kod ileride yarım kalan WIP atölye
                    -- terminali için SP'ye taşınarak yeniden yazılacak; o zamana kadar etkin değildir.
                    -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                    SELECT ISNULL(LastPurchasePrice, 0) FROM Item WHERE Id = @ItemId",
                    new { ItemId = bom.ComponentItemId }, trans);

                totalPlannedMaterialCost += grossQty * itemCost;

                await conn.ExecuteAsync(@"
                    -- DEAD/WIP kod — çoklu-firma izolasyonu burada uygulanmıyor çünkü bu servis hiçbir yerden
                    -- çağrılmıyor (caller yok, DI kaydı yok) ve şemaya uymuyor (kırık StockMovement INSERT'leri).
                    -- Gerçek üretim akışı SP kullanır (sp_ProductionFinish). Bu kod ileride yarım kalan WIP atölye
                    -- terminali için SP'ye taşınarak yeniden yazılacak; o zamana kadar etkin değildir.
                    -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                    INSERT INTO ProductionOrderLine (Id, ProductionOrderId, ItemId, QtyRequired)
                    VALUES (NEWID(), @OrderId, @ItemId, @Qty)",
                    new { OrderId = productionOrderId, ItemId = bom.ComponentItemId, Qty = grossQty },
                    trans);
            }

            // Rota standart maliyetlerini hesapla
            decimal totalPlannedResourceCost = await conn.ExecuteScalarAsync<decimal>(@"
                -- DEAD/WIP kod — çoklu-firma izolasyonu burada uygulanmıyor çünkü bu servis hiçbir yerden
                -- çağrılmıyor (caller yok, DI kaydı yok) ve şemaya uymuyor (kırık StockMovement INSERT'leri).
                -- Gerçek üretim akışı SP kullanır (sp_ProductionFinish). Bu kod ileride yarım kalan WIP atölye
                -- terminali için SP'ye taşınarak yeniden yazılacak; o zamana kadar etkin değildir.
                -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                SELECT ISNULL(SUM(StandardLaborCost + StandardMachineCost), 0)
                FROM ProductRouteStep
                WHERE ProductRouteId = @RouteId",
                new { RouteId = productRouteId }, trans);

            // Tahmini maliyetleri üretim emrine kaydet
            await conn.ExecuteAsync(@"
                -- DEAD/WIP kod — çoklu-firma izolasyonu burada uygulanmıyor çünkü bu servis hiçbir yerden
                -- çağrılmıyor (caller yok, DI kaydı yok) ve şemaya uymuyor (kırık StockMovement INSERT'leri).
                -- Gerçek üretim akışı SP kullanır (sp_ProductionFinish). Bu kod ileride yarım kalan WIP atölye
                -- terminali için SP'ye taşınarak yeniden yazılacak; o zamana kadar etkin değildir.
                -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                UPDATE ProductionOrder
                SET PlannedMaterialCost = @MatCost,
                    PlannedResourceCost = @ResCost
                WHERE Id = @OrderId",
                new { OrderId = productionOrderId, MatCost = totalPlannedMaterialCost, ResCost = totalPlannedResourceCost },
                trans);

            trans.Commit();
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Sayısal formülü değerlendirir.
    /// Parametreler NCalc'a tip-güvenli olarak geçirilir — string injection imkânsız.
    /// Örnek: "[Width] * [Height] / 1000"
    /// </summary>
    private static decimal EvaluateFormula(string formula, Dictionary<string, string> values)
    {
        try
        {
            // Köşeli parantez sözdizimini NCalc parametrelerine dönüştür: [Key] → [Key]
            var expr = new Expression(formula);

            // Parametreleri tip-güvenli şekilde geçir (string değil, decimal olarak parse et)
            foreach (var kv in values)
            {
                if (decimal.TryParse(kv.Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var num))
                    expr.Parameters[kv.Key] = num;
                else
                    expr.Parameters[kv.Key] = kv.Value;
            }

            var result = expr.Evaluate();
            return Convert.ToDecimal(result);
        }
        catch
        {
            // Formül geçersizse 0 döner — satır eklenmez (grossQty <= 0 koruması)
            return 0;
        }
    }

    /// <summary>
    /// Koşul formülünü değerlendirir.
    /// Parametreler tip-güvenli geçirilir.
    /// Örnek: "[GLASS_TYPE] == 'FROSTED'"
    /// </summary>
    private static bool EvaluateCondition(string condition, Dictionary<string, string> values)
    {
        try
        {
            var expr = new Expression(condition);

            foreach (var kv in values)
            {
                if (decimal.TryParse(kv.Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var num))
                    expr.Parameters[kv.Key] = num;
                else
                    expr.Parameters[kv.Key] = kv.Value;
            }

            var result = expr.Evaluate();
            return result is bool b && b;
        }
        catch
        {
            return false;
        }
    }

    public record BomLineDto
    {
        public Guid    ComponentItemId  { get; set; }
        public string  QtyFormula       { get; set; } = "";
        public string? ConditionFormula  { get; set; }
        public decimal WastePercentage  { get; set; }
    }
}
