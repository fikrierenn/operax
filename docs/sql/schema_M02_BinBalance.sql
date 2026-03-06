-- M02 — Inventory: Stock Balance with Bin Support
-- Bu view, stokları hem Depo hem de Raf (Bin) bazında kırılımlı gösterir.

CREATE OR ALTER VIEW vw_InventoryBalanceByBin
AS
SELECT 
    m.CompanyId,
    m.WarehouseId,
    w.Name as WarehouseName,
    m.BinId,
    b.Code as BinCode,
    m.ItemId,
    i.Code as ItemCode,
    i.Name as ItemName,
    SUM(m.QtyBase) as OnHandQty,
    dv.Code as BaseUomCode
FROM StockMovement m
JOIN Item i ON i.Id = m.ItemId
JOIN Warehouse w ON w.Id = m.WarehouseId
LEFT JOIN Bin b ON b.Id = m.BinId
JOIN DictionaryValue dv ON dv.Id = i.BaseUomId
GROUP BY 
    m.CompanyId, m.WarehouseId, w.Name, m.BinId, b.Code, m.ItemId, i.Code, i.Name, dv.Code;
GO
