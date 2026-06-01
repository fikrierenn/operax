-- =============================================================================
-- OPERAX DASHBOARD SEED DATA
-- Sirket: Operax Demo LTD (d1e1b1a5-0000-0000-0000-000000000001)
-- Anasayfa KPI / liste / aylik grafik icin son 6 ayi kapsayan zengin veri.
-- Tum sorgular idempotent (IF NOT EXISTS / MERGE / WHERE NOT EXISTS).
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CompanyId   UNIQUEIDENTIFIER = 'd1e1b1a5-0000-0000-0000-000000000001';

-- Mevcut seed_demo.sql'den gelen sabitler (PO, Receiving, vb. icin temel)
DECLARE @WH001       UNIQUEIDENTIFIER = 'BEF08E7E-8869-4980-A084-5FB8D21A0F6C';

-- Items (PR-0103, PR-0612, PR-0840, PR-0023 + temel hammaddeler)
DECLARE @HAM001      UNIQUEIDENTIFIER = '2D089444-516A-482B-967C-D7547F62AE02';
DECLARE @HAM002      UNIQUEIDENTIFIER = 'B21905F7-3652-42F2-ACAD-ED3AE51CE609';
DECLARE @PRD001      UNIQUEIDENTIFIER = 'F780133C-F8F5-4B12-A3D4-749500E50125';
DECLARE @PRD002      UNIQUEIDENTIFIER = '9D732E3B-FEB0-49D1-9686-CC8B1C7496F9';

-- Partners (seed_demo'dan)
DECLARE @SUP001      UNIQUEIDENTIFIER = '68EC1270-3DEF-4D6C-A1CF-ADDF492EBC27';
DECLARE @SUP002      UNIQUEIDENTIFIER = 'F3FEA396-222A-4679-AE9C-9AC21FD62A1A';

-- UOMs
DECLARE @UOM_ADET    UNIQUEIDENTIFIER = 'C29E2FC2-E8D7-486A-90CE-B45E6039EE42';
DECLARE @UOM_KG      UNIQUEIDENTIFIER = 'A935A202-E6B8-46B8-B4C0-6EE8AA9D009F';

-- Bin
DECLARE @GRS01       UNIQUEIDENTIFIER = '32E9FBDE-68DE-4299-AD3D-5C2E7C27C404';

PRINT '=== DASHBOARD SEED BASLADI ===';

-- =============================================================================
-- 1. CITIES (Sehir lookup tablosu)
-- =============================================================================
IF OBJECT_ID('dbo.City') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM City WHERE Name = N'İstanbul')
        INSERT INTO City (Id, Name, Code) VALUES (NEWID(), N'İstanbul', '34');
    IF NOT EXISTS (SELECT 1 FROM City WHERE Name = N'Ankara')
        INSERT INTO City (Id, Name, Code) VALUES (NEWID(), N'Ankara', '06');
    IF NOT EXISTS (SELECT 1 FROM City WHERE Name = N'İzmir')
        INSERT INTO City (Id, Name, Code) VALUES (NEWID(), N'İzmir', '35');
    IF NOT EXISTS (SELECT 1 FROM City WHERE Name = N'Bursa')
        INSERT INTO City (Id, Name, Code) VALUES (NEWID(), N'Bursa', '16');
    IF NOT EXISTS (SELECT 1 FROM City WHERE Name = N'Kocaeli')
        INSERT INTO City (Id, Name, Code) VALUES (NEWID(), N'Kocaeli', '41');
    PRINT '  [OK] Cities seed';

    -- Mevcut partnerlere CityId atayalim (CityId NULL olanlara)
    DECLARE @IstanbulId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM City WHERE Name = N'İstanbul');
    DECLARE @BursaId    UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM City WHERE Name = N'Bursa');
    DECLARE @KocaeliId  UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM City WHERE Name = N'Kocaeli');
    DECLARE @IzmirId    UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM City WHERE Name = N'İzmir');

    UPDATE Partner SET CityId = @IstanbulId WHERE CityId IS NULL AND Code LIKE 'SUP-001%';
    UPDATE Partner SET CityId = @BursaId    WHERE CityId IS NULL AND Code LIKE 'SUP-002%';
    UPDATE Partner SET CityId = @KocaeliId  WHERE CityId IS NULL AND Code LIKE 'CUS-001%';
    UPDATE Partner SET CityId = @IzmirId    WHERE CityId IS NULL AND Code LIKE 'CUS-002%';
    -- Geri kalan tum Partner'lere Istanbul ata (fallback degil — zorunlu sehir verisi)
    UPDATE Partner SET CityId = @IstanbulId WHERE CityId IS NULL AND CompanyId = @CompanyId;
    PRINT '  [OK] Partner CityId atamasi';
END

-- =============================================================================
-- 2. PURCHASE ORDERS — Son 6 Ay, durum karisik (POSTED/DRAFT/CANCELLED)
-- =============================================================================
-- Yardimci: ay basina 2-3 PO eklenir, OrderDate=GETUTCDATE()-N gun

DECLARE @i INT = 0;
DECLARE @poId UNIQUEIDENTIFIER;
DECLARE @lineId UNIQUEIDENTIFIER;
DECLARE @orderDate DATETIME2;
DECLARE @orderNo NVARCHAR(100);
DECLARE @status NVARCHAR(50);
DECLARE @partnerId UNIQUEIDENTIFIER;
DECLARE @qty DECIMAL(18,6);
DECLARE @price DECIMAL(18,4);
DECLARE @itemId UNIQUEIDENTIFIER;

-- 15 PO, son 180 gun icine yayilmis. PO-DEMO-001..015 (idempotent prefix)
WHILE @i < 15
BEGIN
    SET @orderNo = 'PO-DEMO-' + RIGHT('000' + CAST(@i + 1 AS VARCHAR), 3);

    IF NOT EXISTS (SELECT 1 FROM PurchaseOrderHeader WHERE OrderNo = @orderNo)
    BEGIN
        SET @poId = NEWID();
        -- Tarih: bugunden geriye 12 * @i gun (yaklasik 6 ay yayilim)
        SET @orderDate = DATEADD(DAY, -12 * @i, GETUTCDATE());
        -- Status dagilimi: 0,1,2,3 -> POSTED; 4 -> DRAFT; 5 -> CANCELLED (donem icinde dengeli)
        SET @status = CASE (@i % 6)
                          WHEN 4 THEN 'DRAFT'
                          WHEN 5 THEN 'CANCELLED'
                          ELSE 'POSTED'
                      END;
        SET @partnerId = CASE (@i % 2) WHEN 0 THEN @SUP001 ELSE @SUP002 END;

        INSERT INTO PurchaseOrderHeader
            (Id, CompanyId, WarehouseId, PartnerId, OrderNo, OrderDate, Status, Notes, CreatedAt, IsDeleted)
        VALUES
            (@poId, @CompanyId, @WH001, @partnerId, @orderNo, @orderDate, @status, N'Anasayfa demo verisi', @orderDate, 0);

        -- 2 satir: hammadde + bitmis urun
        SET @qty   = 100 + (@i * 50);
        SET @price = 25.50 + (@i * 3.25);
        INSERT INTO PurchaseOrderLine
            (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReceived, Price, Currency, CreatedAt)
        VALUES
            (NEWID(), @poId, @HAM001, @UOM_KG,   @qty,        0, @price,         'TRY', @orderDate),
            (NEWID(), @poId, @PRD001, @UOM_ADET, @qty * 0.5,  0, @price * 1.8,   'TRY', @orderDate);
    END

    SET @i = @i + 1;
END
PRINT '  [OK] 15 PurchaseOrderHeader (son 6 ay)';

-- =============================================================================
-- 3. RECEIVING — 4 adet DRAFT (yaklasan sevkiyat icin)
-- =============================================================================
DECLARE @rcvId UNIQUEIDENTIFIER;
DECLARE @rcvNo NVARCHAR(100);
DECLARE @j INT = 0;
DECLARE @rcvDate DATETIME2;
DECLARE @rcvItem UNIQUEIDENTIFIER;
DECLARE @rcvPartner UNIQUEIDENTIFIER;

WHILE @j < 4
BEGIN
    SET @rcvNo = 'LPN-DEMO-' + RIGHT('000' + CAST(@j + 1 AS VARCHAR), 3);

    IF NOT EXISTS (SELECT 1 FROM ReceivingHeader WHERE DocNo = @rcvNo)
    BEGIN
        SET @rcvId = NEWID();
        SET @rcvDate = DATEADD(DAY, @j + 1, GETUTCDATE());  -- gelecek 1-4 gun
        SET @rcvItem = CASE (@j % 2) WHEN 0 THEN @HAM001 ELSE @PRD001 END;
        SET @rcvPartner = CASE (@j % 2) WHEN 0 THEN @SUP001 ELSE @SUP002 END;

        INSERT INTO ReceivingHeader
            (Id, CompanyId, WarehouseId, PartnerId, DocNo, DocDate, Status, Notes, CreatedAt, UpdatedAt, IsDeleted)
        VALUES
            (@rcvId, @CompanyId, @WH001, @rcvPartner, @rcvNo, @rcvDate, 'DRAFT', N'Bekleyen sevkiyat', GETUTCDATE(), @rcvDate, 0);

        INSERT INTO ReceivingLine
            (Id, HeaderId, ItemId, UomId, QtyOriginal, QtyBase, BinId, CreatedAt)
        VALUES
            (NEWID(), @rcvId, @rcvItem, @UOM_KG, 250 + (@j * 100), 250 + (@j * 100), @GRS01, GETUTCDATE());
    END

    SET @j = @j + 1;
END
PRINT '  [OK] 4 ReceivingHeader DRAFT (yaklasan sevkiyat)';

-- =============================================================================
-- 4. STOCK MOVEMENTS — Son aktivite icin son 7 gunde 6 hareket
-- =============================================================================
DECLARE @smId UNIQUEIDENTIFIER;
DECLARE @smType NVARCHAR(50);
DECLARE @k INT = 0;
DECLARE @smDate DATETIME2;
DECLARE @smItem UNIQUEIDENTIFIER;
DECLARE @smQty DECIMAL(18,6);

WHILE @k < 6
BEGIN
    SET @smType = CASE @k
                      WHEN 0 THEN 'RECEIPT'
                      WHEN 1 THEN 'ISSUE'
                      WHEN 2 THEN 'TRANSFER'
                      WHEN 3 THEN 'RECEIPT'
                      WHEN 4 THEN 'COUNT_ADJ'
                      ELSE 'ISSUE'
                  END;
    SET @smDate = DATEADD(HOUR, -3 * (@k + 1), GETUTCDATE());
    SET @smItem = CASE (@k % 2) WHEN 0 THEN @HAM001 ELSE @PRD001 END;
    SET @smQty  = 50 + (@k * 25);

    IF NOT EXISTS (SELECT 1 FROM StockMovement
                   WHERE CompanyId = @CompanyId AND ItemId = @smItem AND MovementType = @smType
                     AND ABS(DATEDIFF(MINUTE, CreatedAt, @smDate)) < 5)
    BEGIN
        INSERT INTO StockMovement
            (Id, CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal,
             SourceDocType, SourceDocId, SourceDocNo, CreatedAt)
        VALUES
            (NEWID(), @CompanyId, @WH001, @GRS01, @smItem, @smType, @smQty, @UOM_KG, @smQty,
             'DEMO', NULL, 'DEMO-MV-' + CAST(@k AS VARCHAR), @smDate);
    END

    SET @k = @k + 1;
END
PRINT '  [OK] 6 StockMovement (son aktivite)';

PRINT '=== DASHBOARD SEED TAMAMLANDI ===';
