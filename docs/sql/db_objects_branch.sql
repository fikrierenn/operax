-- Plan 23 Faz F: Branch boyutu SP/trigger nesneleri
-- Idempotent: CREATE OR ALTER / IF NOT EXISTS korumalı
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ============================================================
-- tr_StockMovement_BranchId
-- StockMovement INSERT anında BranchId boşsa Warehouse.BranchId'den türetir.
-- Tüm mevcut Post SP'lerine dokunmadan BranchId'yi otomatik doldurur.
-- ============================================================
CREATE OR ALTER TRIGGER dbo.tr_StockMovement_BranchId
ON dbo.StockMovement
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    -- BranchId NULL olan yeni satırları Warehouse.BranchId ile güncelle
    UPDATE sm
    SET sm.BranchId = w.BranchId
    FROM dbo.StockMovement sm
    JOIN inserted i ON i.Id = sm.Id
    JOIN dbo.Warehouse w ON w.Id = sm.WarehouseId
    WHERE sm.BranchId IS NULL
      AND w.BranchId IS NOT NULL;
END
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

-- tr_StockMovement_BranchId'yi son sıraya al (guard trigger önce çalışsın)
EXEC sp_settriggerorder
    @triggername = 'tr_StockMovement_BranchId',
    @order       = 'last',
    @stmttype    = 'INSERT',
    @namespace   = 'DATABASE';
GO
