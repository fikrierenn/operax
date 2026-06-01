-- Smoke Plan 26: gider satırı ekle → tvf_ExpenseBreakdown kırılım doğru mu
DECLARE @C UNIQUEIDENTIFIER = 'd1e1b1a5-0000-0000-0000-000000000001';
DECLARE @Inv UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM ExpenseInvoice WHERE Status='POSTED' AND CompanyId=@C);
DECLARE @Elk UNIQUEIDENTIFIER = (SELECT Id FROM ExpenseType WHERE Code='ELK' AND CompanyId=@C);
DECLARE @Kira UNIQUEIDENTIFIER = (SELECT Id FROM ExpenseType WHERE Code='KIRA' AND CompanyId=@C);
DECLARE @Gm UNIQUEIDENTIFIER = (SELECT Id FROM CostCenter WHERE Code='GM' AND CompanyId=@C);
DECLARE @Urt UNIQUEIDENTIFIER = (SELECT Id FROM CostCenter WHERE Code='URT' AND CompanyId=@C);

-- 2 farklı merkez + 2 tip satır (kalem bazlı CostCenter)
INSERT INTO ExpenseInvoiceLine (Id, ExpenseInvoiceId, ExpenseTypeId, CostCenterId, Quantity, UnitPrice, Amount, TaxRate)
VALUES
    (NEWID(), @Inv, @Elk,  @Urt, 1000, 2.5, 2500, 20),   -- Üretim elektrik
    (NEWID(), @Inv, @Kira, @Gm,  1,    8000, 8000, 20);  -- GM kira

SELECT 'Kirilim' AS Rapor, CostCenterName, ExpenseTypeName, NetAmount, TaxAmount, TotalAmount, LineCount
FROM tvf_ExpenseBreakdown(@C, '2026-01-01', '2026-12-31')
ORDER BY CostCenterName;
