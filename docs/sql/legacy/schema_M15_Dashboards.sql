-- ======================================================
-- CATEGORY: MANUFACTURING DASHBOARD
-- ======================================================
CREATE VIEW View_ProductionEfficiency AS
SELECT 
    po.DocNo,
    i.Name as ItemName,
    po.QtyTarget,
    po.QtyProduced,
    po.PlannedResourceCost,
    po.ActualResourceCost,
    (po.ActualResourceCost - po.PlannedResourceCost) as CostVariance,
    CASE WHEN po.PlannedResourceCost > 0 
         THEN (po.ActualResourceCost / po.PlannedResourceCost) * 100 
         ELSE 0 END as EfficiencyIndex
FROM ProductionOrder po
JOIN Item i ON i.Id = po.ItemId
WHERE po.Status = 'COMPLETED';
GO

-- 2. Üretim Fire ve Hurda Analizi (Scrap Rate)
CREATE VIEW View_ManufacturingScrapRate AS
SELECT 
    i.Name as ItemName,
    SUM(QtyProduced) as GoodQty,
    ISNULL((SELECT SUM(QtyFailed) FROM ProductionInspection WHERE ProductionOrderId = po.Id), 0) as ScrapQty
FROM ProductionOrder po
JOIN Item i ON i.Id = po.ItemId
GROUP BY i.Name, po.Id;
GO

-- ======================================================
-- CATEGORY: WAREHOUSE DASHBOARD
-- ======================================================

-- 3. Hammadde Stok Risk Analizi (Inventory Health)
CREATE VIEW View_InventoryHealth AS
SELECT 
    i.Code as SKU,
    i.Name,
    SUM(ISNULL(sm.Qty, 0)) as OnHand,
    (SELECT ISNULL(SUM(QtyRequired), 0) FROM ProductionOrderLine pol 
     JOIN ProductionOrder po ON po.Id = pol.ProductionOrderId 
     WHERE pol.ItemId = i.Id AND po.Status IN ('DRAFT', 'RELEASED')) as FutureRequirement
FROM Item i
LEFT JOIN StockMovement sm ON sm.ItemId = i.Id
GROUP BY i.Code, i.Name;
GO

-- 4. Depo Doluluk Oranı (Warehouse Occupation)
CREATE VIEW View_WarehouseOccupation AS
SELECT 
    w.Name as WarehouseName,
    COUNT(b.Id) as TotalBins,
    (SELECT COUNT(DISTINCT BinId) FROM StockMovement sm WHERE sm.WarehouseId = w.Id AND sm.Qty > 0) as OccupiedBins,
    CAST((SELECT COUNT(DISTINCT BinId) FROM StockMovement sm WHERE sm.WarehouseId = w.Id AND sm.Qty > 0) AS FLOAT) / 
    NULLIF(COUNT(b.Id), 0) * 100 as OccupationRate
FROM Warehouse w
LEFT JOIN Bin b ON b.WarehouseId = w.Id
GROUP BY w.Name, w.Id;
GO

-- 5. Operatör Verimlilik Analizi (Operator Performance)
CREATE VIEW View_OperatorPerformance AS
SELECT 
    u.FullName,
    SUM(DATEDIFF(SECOND, a.StartTime, a.EndTime)) as TotalWorkSeconds,
    COUNT(a.Id) as TotalActivities,
    ISNULL(SUM(a.QtyProduced), 0) as TotalUnitsProduced
FROM AspNetUsers u
JOIN ProductionActivity a ON a.UserId = u.Id
WHERE a.EndTime IS NOT NULL
GROUP BY u.FullName;
GO

-- ======================================================
-- CATEGORY: FINANCE DASHBOARD
-- ======================================================

-- 6. Satış vs Üretim (Sipariş Teslim Performansı)
CREATE VIEW View_SalesVsProduction AS
SELECT 
    i.Name as ItemName,
    SUM(sol.RequestedQty) as OrderedQty,
    ISNULL((SELECT SUM(po.QtyProduced) FROM ProductionOrder po WHERE po.ItemId = i.Id AND po.Status = 'COMPLETED'), 0) as ProducedQty
FROM SalesOrderLine sol
JOIN Item i ON i.Id = sol.ItemId
GROUP BY i.Name, i.Id;
GO
