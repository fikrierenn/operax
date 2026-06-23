-- M02 — Inventory Balance & Movements View Update
-- Addressing 'Stok Defteri' naming and ensuring consistency

-- Update existing StockMovement to ensure SourceDocType naming alignment
-- No schema change needed, but documenting logic:
-- SourceDocType values: RECEIVING, SHIPPING, TRANSFER, ADJUSTMENT...

/*
-- Optional: Helper view for Inventory Balance with better naming
CREATE OR ALTER VIEW vw_EnvanterDurumu AS
SELECT 
    i.Code as ÜrünKodu,
    i.Name as ÜrünAdı,
    wh.Name as Depo,
    b.Code as Hücre,
    bal.QtyBase as Miktar,
    dv.NameTr as Birim
FROM InventoryBalance bal
JOIN Item i ON i.Id = bal.ItemId
JOIN Warehouse wh ON wh.Id = bal.WarehouseId
JOIN Bin b ON b.Id = bal.BinId
JOIN DictionaryValue dv ON dv.Id = i.BaseUomId;
*/
