-- GEÇİCİ test — sp_UpdateItemCostMovingAvg moving-avg + ledger-türev doğrulama.
-- BEGIN TRAN ... ROLLBACK → DB temiz kalır (immutability trigger DELETE'i değil, rollback INSERT'i geri alır).
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @co UNIQUEIDENTIFIER = (SELECT TOP 1 CompanyId FROM ItemCost WHERE WarehouseId IS NULL);
DECLARE @item UNIQUEIDENTIFIER = 'f780133c-f8f5-4b12-a3d4-749500e50125';
DECLARE @wh UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Warehouse WHERE CompanyId=@co AND IsDeleted=0);
DECLARE @bin UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Bin WHERE WarehouseId=@wh AND IsActive=1 AND IsDeleted=0);
DECLARE @uom UNIQUEIDENTIFIER = (SELECT TOP 1 UomId FROM StockMovement WHERE ItemId=@item AND IsCancelled=0);

DECLARE @before DECIMAL(18,6) = (SELECT OnHandQty FROM ItemCost WHERE CompanyId=@co AND ItemId=@item AND WarehouseId IS NULL);

-- RECEIPT 100 @ 50 (StockMovement ÖNCE yazılır, SP ledger'dan okur)
INSERT INTO StockMovement (CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, UnitCost, CreatedBy, BranchId)
VALUES (@co, @wh, @bin, @item, 'RECEIPT', 100, @uom, 100, 'RECEIVING', 50, NULL, dbo.fn_DefaultBranchId(@co));

EXEC dbo.sp_UpdateItemCostMovingAvg @CompanyId=@co, @ItemId=@item, @WarehouseId=NULL, @Qty=100, @UnitCost=50, @MovementType='RECEIPT';

SELECT @before AS OnHandQty_Before, AvgCost AS AvgCost_After, OnHandQty AS OnHandQty_After,
       CAST(100.0*50 / (@before+100) AS DECIMAL(18,4)) AS Expected_AvgCost,
       @before+100 AS Expected_OnHandQty
FROM ItemCost WHERE CompanyId=@co AND ItemId=@item AND WarehouseId IS NULL;

ROLLBACK;
