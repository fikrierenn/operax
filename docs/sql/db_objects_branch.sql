-- Plan 23 Faz F: Branch boyutu yardımcı nesneleri
-- Idempotent: CREATE OR ALTER korumalı
-- NOT: StockMovement BranchId artık her Post SP'sinde Warehouse.BranchId subquery ile set edilir.
--      Trigger YOK — SP içinde açık ve izlenebilir.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Mevcut trigger varsa kaldır (önceki sürümden kalmış olabilir)
IF OBJECT_ID('dbo.tr_StockMovement_BranchId', 'TR') IS NOT NULL
    DROP TRIGGER dbo.tr_StockMovement_BranchId;
GO

-- ============================================================
-- fn_DefaultBranchId
-- Şirketin MERKEZ tipi şubesini döner.
-- Tek şubeli firmada otomatik inject için Post SP'leri kullanır.
-- ============================================================
CREATE OR ALTER FUNCTION dbo.fn_DefaultBranchId(@CompanyId UNIQUEIDENTIFIER)
RETURNS UNIQUEIDENTIFIER
AS
BEGIN
    RETURN (
        SELECT TOP 1 Id
        FROM dbo.Branch
        WHERE CompanyId = @CompanyId
          AND BranchType = 'MERKEZ'
          AND IsDeleted = 0
          AND IsActive = 1
        -- CreatedAt + Id ile deterministik sıralama (aynı ms'de oluşturulan MERKEZ'lere karşı)
        ORDER BY CreatedAt, Id
    );
END
GO
