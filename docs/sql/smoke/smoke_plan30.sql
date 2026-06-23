-- Plan 30 smoke: zincir iskonto 10+5+3 (base 100) → 82,935 doğrulama. ROLLBACK ile kalıntı yok.
SET NOCOUNT ON;
BEGIN TRAN;

DECLARE @Cid UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Company);
DECLARE @Iid UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Item WHERE CompanyId = @Cid);
DECLARE @PlId UNIQUEIDENTIFIER = NEWID();
DECLARE @LineId UNIQUEIDENTIFIER = NEWID();

INSERT INTO PriceList (Id, CompanyId, Code, Name, Direction, IsActive, Priority)
VALUES (@PlId, @Cid, 'SMOKE30', 'Smoke Plan30', 'SALES', 1, 0);

INSERT INTO PriceListLine (Id, PriceListId, ItemId, UnitPrice, MinQty, LineType)
VALUES (@LineId, @PlId, @Iid, 100.0000, 0, 'FIXED');

INSERT INTO PriceListLineDiscount (Id, LineId, Seq, Pct) VALUES (NEWID(), @LineId, 1, 10);
INSERT INTO PriceListLineDiscount (Id, LineId, Seq, Pct) VALUES (NEWID(), @LineId, 2, 5);
INSERT INTO PriceListLineDiscount (Id, LineId, Seq, Pct) VALUES (NEWID(), @LineId, 3, 3);

-- Beklenti: BasePrice=100, EffectivePrice=82.9350, TotalDiscountAmount=17.0650, NetMultiplier=0.829350
SELECT BasePrice, NetMultiplier, EffectivePrice, TotalDiscountAmount, LineType, Direction, Priority
FROM dbo.tvf_PriceListEffective(@Cid)
WHERE LineId = @LineId;

-- İkinci satır: iskontosuz (base 250) → Effective=250, indirim 0
DECLARE @LineId2 UNIQUEIDENTIFIER = NEWID();
INSERT INTO PriceListLine (Id, PriceListId, ItemId, UnitPrice, MinQty, LineType)
VALUES (@LineId2, @PlId, @Iid, 250.0000, 0, 'FIXED');
SELECT BasePrice, NetMultiplier, EffectivePrice, TotalDiscountAmount
FROM dbo.tvf_PriceListEffective(@Cid)
WHERE LineId = @LineId2;

ROLLBACK;
