-- =============================================================================
-- OPERAX BUSINESS HISTORY SEED
-- Sirket: Operax Demo LTD (d1e1b1a5-0000-0000-0000-000000000001)
-- 1 Ocak 2026 - bugun arasi gercekci gunluk operasyon datasi.
-- Hafta ici 5 gun islem: 2 PO, 1 Receiving, 2 SO, 1 Shipment, haftalik 1 Transfer
-- Tum DocNo prefix'leri BIZ-* (idempotent re-run garantisi)
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CompanyId UNIQUEIDENTIFIER = 'd1e1b1a5-0000-0000-0000-000000000001';
DECLARE @WH001     UNIQUEIDENTIFIER = 'BEF08E7E-8869-4980-A084-5FB8D21A0F6C';
DECLARE @WH002     UNIQUEIDENTIFIER = '93BF43EB-C1E6-4D4A-BCDD-77CBE259FF2A';
DECLARE @GRS01     UNIQUEIDENTIFIER = '32E9FBDE-68DE-4299-AD3D-5C2E7C27C404';
DECLARE @RA01      UNIQUEIDENTIFIER = 'EF5E5804-8FAA-4A64-B277-6A624C1EE83F';
DECLARE @CIK01     UNIQUEIDENTIFIER = 'B14C27C1-BD48-4E07-8498-E2C452D084D7';

-- Items
DECLARE @HAM001    UNIQUEIDENTIFIER = '2D089444-516A-482B-967C-D7547F62AE02';
DECLARE @HAM002    UNIQUEIDENTIFIER = 'B21905F7-3652-42F2-ACAD-ED3AE51CE609';
DECLARE @PRD001    UNIQUEIDENTIFIER = 'F780133C-F8F5-4B12-A3D4-749500E50125';
DECLARE @PRD002    UNIQUEIDENTIFIER = '9D732E3B-FEB0-49D1-9686-CC8B1C7496F9';
DECLARE @PRD003    UNIQUEIDENTIFIER = 'AD7E22C3-77A6-41B1-9082-8228364976E7';

-- Partners
DECLARE @SUP001    UNIQUEIDENTIFIER = '68EC1270-3DEF-4D6C-A1CF-ADDF492EBC27';
DECLARE @SUP002    UNIQUEIDENTIFIER = 'F3FEA396-222A-4679-AE9C-9AC21FD62A1A';
DECLARE @CUS001    UNIQUEIDENTIFIER = '3FC8974C-E2B0-443D-AB56-9775ACBC9E29';
DECLARE @CUS002    UNIQUEIDENTIFIER = '3D2EC5A8-4EB1-4C7A-BDDC-17AFD4DD49D6';

-- UOMs
DECLARE @UOM_ADET  UNIQUEIDENTIFIER = 'C29E2FC2-E8D7-486A-90CE-B45E6039EE42';
DECLARE @UOM_KG    UNIQUEIDENTIFIER = 'A935A202-E6B8-46B8-B4C0-6EE8AA9D009F';

DECLARE @StartDate  DATE = '2026-01-01';
DECLARE @EndDate    DATE = CAST(GETUTCDATE() AS DATE);
DECLARE @CurDate    DATE = @StartDate;
DECLARE @DayIdx     INT = 0;

DECLARE @poId           UNIQUEIDENTIFIER;
DECLARE @rcvId          UNIQUEIDENTIFIER;
DECLARE @soId           UNIQUEIDENTIFIER;
DECLARE @soLine1Id      UNIQUEIDENTIFIER;
DECLARE @soLine2Id      UNIQUEIDENTIFIER;
DECLARE @shpId          UNIQUEIDENTIFIER;
DECLARE @docNo          NVARCHAR(100);
DECLARE @partnerId      UNIQUEIDENTIFIER;
DECLARE @itemA          UNIQUEIDENTIFIER;
DECLARE @itemB          UNIQUEIDENTIFIER;
DECLARE @qtyA           DECIMAL(18,6);
DECLARE @qtyB           DECIMAL(18,6);
DECLARE @priceA         DECIMAL(18,4);
DECLARE @priceB         DECIMAL(18,4);
DECLARE @status         NVARCHAR(50);
DECLARE @dow            INT;       -- haftanin gunu (1=Pazartesi)

PRINT '=== BUSINESS HISTORY SEED BASLADI ===';
PRINT '  Tarih araligi: ' + CONVERT(VARCHAR, @StartDate, 23) + ' -> ' + CONVERT(VARCHAR, @EndDate, 23);

WHILE @CurDate <= @EndDate
BEGIN
    SET @dow = ((DATEPART(WEEKDAY, @CurDate) + @@DATEFIRST - 1) % 7) + 1;

    -- Hafta sonu islem yok (Cumartesi=6, Pazar=7)
    IF @dow < 6
    BEGIN
        ----------------------------------------------------------------------
        -- A) GUNLUK 2 SATINALMA SIPARISI (PO)
        ----------------------------------------------------------------------
        -- 1. PO (sabah)
        SET @docNo = 'BIZ-PO-' + RIGHT('0000' + CAST(@DayIdx * 2 + 1 AS VARCHAR), 5);
        IF NOT EXISTS (SELECT 1 FROM PurchaseOrderHeader WHERE OrderNo = @docNo)
        BEGIN
            SET @poId = NEWID();
            SET @partnerId = CASE @DayIdx % 2 WHEN 0 THEN @SUP001 ELSE @SUP002 END;
            SET @itemA = CASE @DayIdx % 3 WHEN 0 THEN @HAM001 WHEN 1 THEN @HAM002 ELSE @PRD001 END;
            SET @qtyA = 100 + (@DayIdx % 20) * 10;
            SET @priceA = 24.50 + (@DayIdx % 15);
            -- Eskimis evraklarin %80'i POSTED, son hafta DRAFT/POSTED karisik
            SET @status = CASE
                WHEN DATEDIFF(DAY, @CurDate, @EndDate) > 7 THEN
                    CASE @DayIdx % 10 WHEN 9 THEN 'CANCELLED' ELSE 'POSTED' END
                ELSE CASE @DayIdx % 3 WHEN 0 THEN 'DRAFT' ELSE 'POSTED' END
            END;

            INSERT INTO PurchaseOrderHeader (Id, CompanyId, WarehouseId, PartnerId, OrderNo, OrderDate, Status, Notes, CreatedAt, IsDeleted)
            VALUES (@poId, @CompanyId, @WH001, @partnerId, @docNo,
                    DATEADD(HOUR, 9, CAST(@CurDate AS DATETIME2)), @status, N'Gunluk satinalma', CAST(@CurDate AS DATETIME2), 0);

            INSERT INTO PurchaseOrderLine (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReceived, Price, Currency, CreatedAt)
            VALUES (NEWID(), @poId, @itemA, @UOM_KG, @qtyA,
                    CASE WHEN @status = 'POSTED' THEN @qtyA ELSE 0 END, @priceA, 'TRY', CAST(@CurDate AS DATETIME2)),
                   (NEWID(), @poId, CASE @DayIdx % 2 WHEN 0 THEN @PRD002 ELSE @PRD003 END,
                    @UOM_ADET, @qtyA * 0.3, 0, @priceA * 2.1, 'TRY', CAST(@CurDate AS DATETIME2));

            -- POSTED ise StockMovement RECEIPT + ReceivingHeader (otomatik mal kabul)
            IF @status = 'POSTED'
            BEGIN
                SET @rcvId = NEWID();
                SET @docNo = 'BIZ-RCV-' + RIGHT('0000' + CAST(@DayIdx * 2 + 1 AS VARCHAR), 5);
                IF NOT EXISTS (SELECT 1 FROM ReceivingHeader WHERE DocNo = @docNo)
                BEGIN
                    INSERT INTO ReceivingHeader (Id, CompanyId, WarehouseId, PartnerId, PurchaseOrderId, DocNo, DocDate, Status, Notes, CreatedAt, IsDeleted)
                    VALUES (@rcvId, @CompanyId, @WH001, @partnerId, @poId, @docNo,
                            DATEADD(HOUR, 14, CAST(@CurDate AS DATETIME2)), 'POSTED', N'Otomatik mal kabul',
                            DATEADD(HOUR, 14, CAST(@CurDate AS DATETIME2)), 0);

                    INSERT INTO ReceivingLine (Id, HeaderId, ItemId, UomId, QtyOriginal, QtyBase, BinId, CreatedAt)
                    VALUES (NEWID(), @rcvId, @itemA, @UOM_KG, @qtyA, @qtyA, @GRS01,
                            DATEADD(HOUR, 14, CAST(@CurDate AS DATETIME2)));

                    INSERT INTO StockMovement (Id, CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal,
                                                SourceDocType, SourceDocId, SourceDocNo, CreatedAt)
                    VALUES (NEWID(), @CompanyId, @WH001, @GRS01, @itemA, 'RECEIPT', @qtyA, @UOM_KG, @qtyA,
                            'RECEIVING', @rcvId, @docNo, DATEADD(HOUR, 14, CAST(@CurDate AS DATETIME2)));
                END
            END
        END

        -- 2. PO (oglen) — sadece tek satir hammadde
        SET @docNo = 'BIZ-PO-' + RIGHT('0000' + CAST(@DayIdx * 2 + 2 AS VARCHAR), 5);
        IF NOT EXISTS (SELECT 1 FROM PurchaseOrderHeader WHERE OrderNo = @docNo)
        BEGIN
            SET @poId = NEWID();
            SET @partnerId = CASE @DayIdx % 2 WHEN 1 THEN @SUP001 ELSE @SUP002 END;
            SET @itemA = CASE @DayIdx % 2 WHEN 0 THEN @HAM002 ELSE @HAM001 END;
            SET @qtyA = 50 + (@DayIdx % 30) * 5;
            SET @priceA = 18.75 + (@DayIdx % 10);
            SET @status = CASE
                WHEN DATEDIFF(DAY, @CurDate, @EndDate) > 14 THEN 'POSTED'
                ELSE CASE @DayIdx % 4 WHEN 0 THEN 'DRAFT' WHEN 1 THEN 'CANCELLED' ELSE 'POSTED' END
            END;

            INSERT INTO PurchaseOrderHeader (Id, CompanyId, WarehouseId, PartnerId, OrderNo, OrderDate, Status, Notes, CreatedAt, IsDeleted)
            VALUES (@poId, @CompanyId, @WH001, @partnerId, @docNo,
                    DATEADD(HOUR, 13, CAST(@CurDate AS DATETIME2)), @status, N'Ek siparis', CAST(@CurDate AS DATETIME2), 0);

            INSERT INTO PurchaseOrderLine (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReceived, Price, Currency, CreatedAt)
            VALUES (NEWID(), @poId, @itemA, @UOM_KG, @qtyA,
                    CASE WHEN @status = 'POSTED' THEN @qtyA ELSE 0 END, @priceA, 'TRY', CAST(@CurDate AS DATETIME2));
        END

        ----------------------------------------------------------------------
        -- B) GUNLUK 2 SATIS SIPARISI (SO)
        ----------------------------------------------------------------------
        SET @docNo = 'BIZ-SO-' + RIGHT('0000' + CAST(@DayIdx * 2 + 1 AS VARCHAR), 5);
        IF NOT EXISTS (SELECT 1 FROM SalesOrderHeader WHERE OrderNo = @docNo)
        BEGIN
            SET @soId = NEWID();
            SET @partnerId = CASE @DayIdx % 2 WHEN 0 THEN @CUS001 ELSE @CUS002 END;
            SET @itemA = CASE @DayIdx % 3 WHEN 0 THEN @PRD001 WHEN 1 THEN @PRD002 ELSE @PRD003 END;
            SET @itemB = CASE @DayIdx % 3 WHEN 0 THEN @PRD002 WHEN 1 THEN @PRD003 ELSE @PRD001 END;
            SET @qtyA = 20 + (@DayIdx % 15) * 3;
            SET @qtyB = 10 + (@DayIdx % 8) * 2;
            SET @priceA = 95.00 + (@DayIdx % 25) * 2.5;
            SET @priceB = 142.00 + (@DayIdx % 20) * 3;
            SET @status = CASE
                WHEN DATEDIFF(DAY, @CurDate, @EndDate) > 10 THEN
                    CASE @DayIdx % 8 WHEN 7 THEN 'CANCELLED' ELSE 'APPROVED' END
                ELSE CASE @DayIdx % 3 WHEN 0 THEN 'DRAFT' ELSE 'APPROVED' END
            END;

            INSERT INTO SalesOrderHeader (Id, CompanyId, WarehouseId, PartnerId, OrderNo, OrderDate, RequestedDeliveryDate, Status, Notes, CreatedAt, IsDeleted)
            VALUES (@soId, @CompanyId, @WH001, @partnerId, @docNo,
                    DATEADD(HOUR, 10, CAST(@CurDate AS DATETIME2)),
                    DATEADD(DAY, 7, CAST(@CurDate AS DATETIME2)),
                    @status, N'Musteri siparisi', CAST(@CurDate AS DATETIME2), 0);

            SET @soLine1Id = NEWID();
            SET @soLine2Id = NEWID();
            INSERT INTO SalesOrderLine (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReserved, QtyShipped, Price, Currency, CreatedAt)
            VALUES (@soLine1Id, @soId, @itemA, @UOM_ADET, @qtyA, 0, 0, @priceA, 'TRY', CAST(@CurDate AS DATETIME2)),
                   (@soLine2Id, @soId, @itemB, @UOM_ADET, @qtyB, 0, 0, @priceB, 'TRY', CAST(@CurDate AS DATETIME2));

            -- APPROVED ise haftada 1 kez Shipment olustur (her 5 SO'da 1)
            IF @status = 'APPROVED' AND @DayIdx % 5 = 0
            BEGIN
                SET @docNo = 'BIZ-SHP-' + RIGHT('0000' + CAST(@DayIdx + 1 AS VARCHAR), 5);
                IF NOT EXISTS (SELECT 1 FROM ShippingHeader WHERE DocNo = @docNo)
                BEGIN
                    SET @shpId = NEWID();
                    INSERT INTO ShippingHeader (Id, CompanyId, WarehouseId, DocNo, DocDate, Status, CarrierName, VehiclePlate, Notes, CreatedAt, IsDeleted)
                    VALUES (@shpId, @CompanyId, @WH001, @docNo,
                            DATEADD(HOUR, 16, CAST(@CurDate AS DATETIME2)),
                            'POSTED', N'Aras Kargo',
                            '34 ABC ' + CAST(@DayIdx % 999 AS VARCHAR), N'Sevkiyat',
                            DATEADD(HOUR, 16, CAST(@CurDate AS DATETIME2)), 0);

                    INSERT INTO ShippingLine (Id, HeaderId, SalesOrderLineId, ItemId, UomId, QtyOriginal, QtyBase, BinId, CreatedAt)
                    VALUES (NEWID(), @shpId, @soLine1Id, @itemA, @UOM_ADET, @qtyA, @qtyA, @CIK01,
                            DATEADD(HOUR, 16, CAST(@CurDate AS DATETIME2)));

                    -- Stok cikis hareketi
                    INSERT INTO StockMovement (Id, CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal,
                                                SourceDocType, SourceDocId, SourceDocNo, CreatedAt)
                    VALUES (NEWID(), @CompanyId, @WH001, @CIK01, @itemA, 'ISSUE', -@qtyA, @UOM_ADET, @qtyA,
                            'SHIPPING', @shpId, @docNo, DATEADD(HOUR, 16, CAST(@CurDate AS DATETIME2)));

                    UPDATE SalesOrderLine SET QtyShipped = @qtyA WHERE Id = @soLine1Id;
                END
            END
        END

        -- 2. SO (oglen sonrasi) — kucuk siparis
        SET @docNo = 'BIZ-SO-' + RIGHT('0000' + CAST(@DayIdx * 2 + 2 AS VARCHAR), 5);
        IF NOT EXISTS (SELECT 1 FROM SalesOrderHeader WHERE OrderNo = @docNo)
        BEGIN
            SET @soId = NEWID();
            SET @partnerId = CASE @DayIdx % 2 WHEN 1 THEN @CUS001 ELSE @CUS002 END;
            SET @itemA = CASE @DayIdx % 2 WHEN 0 THEN @PRD002 ELSE @PRD003 END;
            SET @qtyA = 10 + (@DayIdx % 10);
            SET @priceA = 175.00 + (@DayIdx % 15) * 4;
            SET @status = CASE @DayIdx % 5 WHEN 0 THEN 'DRAFT' WHEN 1 THEN 'CANCELLED' ELSE 'APPROVED' END;

            INSERT INTO SalesOrderHeader (Id, CompanyId, WarehouseId, PartnerId, OrderNo, OrderDate, RequestedDeliveryDate, Status, Notes, CreatedAt, IsDeleted)
            VALUES (@soId, @CompanyId, @WH001, @partnerId, @docNo,
                    DATEADD(HOUR, 15, CAST(@CurDate AS DATETIME2)),
                    DATEADD(DAY, 5, CAST(@CurDate AS DATETIME2)),
                    @status, N'Kucuk siparis', CAST(@CurDate AS DATETIME2), 0);

            INSERT INTO SalesOrderLine (Id, HeaderId, ItemId, UomId, QtyOrdered, QtyReserved, QtyShipped, Price, Currency, CreatedAt)
            VALUES (NEWID(), @soId, @itemA, @UOM_ADET, @qtyA, 0, 0, @priceA, 'TRY', CAST(@CurDate AS DATETIME2));
        END

        ----------------------------------------------------------------------
        -- C) HAFTADA 1 TRANSFER (Pazartesi)
        ----------------------------------------------------------------------
        IF @dow = 1
        BEGIN
            SET @docNo = 'BIZ-TRF-' + RIGHT('000' + CAST(@DayIdx / 7 + 1 AS VARCHAR), 4);
            -- StockTransfer schema'si varsa ekle (idempotent)
            IF OBJECT_ID('dbo.StockTransfer') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM StockTransfer WHERE DocNo = @docNo)
            BEGIN
                DECLARE @trfId UNIQUEIDENTIFIER = NEWID();
                INSERT INTO StockTransfer (Id, CompanyId, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes, CreatedAt, PostedAt)
                VALUES (@trfId, @CompanyId, @docNo, 'POSTED', 'BIN_TO_BIN', @WH001, @WH001, N'Haftalik dahili transfer',
                        DATEADD(HOUR, 11, CAST(@CurDate AS DATETIME2)),
                        DATEADD(HOUR, 12, CAST(@CurDate AS DATETIME2)));

                INSERT INTO StockTransferLine (Id, TransferId, ItemId, UomId, FromBinId, ToBinId, Qty, QtyBase)
                VALUES (NEWID(), @trfId, @HAM001, @UOM_KG, @GRS01, @RA01, 100 + @DayIdx, 100 + @DayIdx);

                INSERT INTO StockMovement (Id, CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal,
                                            SourceDocType, SourceDocId, SourceDocNo, CreatedAt)
                VALUES (NEWID(), @CompanyId, @WH001, @GRS01, @HAM001, 'TRANSFER', -(100 + @DayIdx), @UOM_KG, 100 + @DayIdx,
                        'TRANSFER', @trfId, @docNo, DATEADD(HOUR, 12, CAST(@CurDate AS DATETIME2))),
                       (NEWID(), @CompanyId, @WH001, @RA01, @HAM001, 'TRANSFER', 100 + @DayIdx, @UOM_KG, 100 + @DayIdx,
                        'TRANSFER', @trfId, @docNo, DATEADD(HOUR, 12, CAST(@CurDate AS DATETIME2)));
            END
        END

    END  -- hafta ici

    SET @CurDate = DATEADD(DAY, 1, @CurDate);
    SET @DayIdx = @DayIdx + 1;

    -- Her 30 gunde durum mesaji
    IF @DayIdx % 30 = 0
        PRINT '  ... ' + CAST(@DayIdx AS VARCHAR) + ' gun islendi (' + CONVERT(VARCHAR, @CurDate, 23) + ')';
END

DECLARE @poCount    INT = (SELECT COUNT(*) FROM PurchaseOrderHeader WHERE OrderNo LIKE 'BIZ-PO-%');
DECLARE @rcvCount   INT = (SELECT COUNT(*) FROM ReceivingHeader WHERE DocNo LIKE 'BIZ-RCV-%');
DECLARE @soCount    INT = (SELECT COUNT(*) FROM SalesOrderHeader WHERE OrderNo LIKE 'BIZ-SO-%');
DECLARE @shpCount   INT = (SELECT COUNT(*) FROM ShippingHeader WHERE DocNo LIKE 'BIZ-SHP-%');
DECLARE @movCount   INT = (SELECT COUNT(*) FROM StockMovement WHERE SourceDocNo LIKE 'BIZ-%');

PRINT '=== BUSINESS HISTORY SEED TAMAMLANDI ===';
PRINT '  Toplam ' + CAST(@DayIdx AS VARCHAR) + ' gun islendi (' + CONVERT(VARCHAR, @StartDate, 23) + ' -> ' + CONVERT(VARCHAR, @EndDate, 23) + ')';
PRINT '  PurchaseOrder: '  + CAST(@poCount AS VARCHAR);
PRINT '  Receiving:     '  + CAST(@rcvCount AS VARCHAR);
PRINT '  SalesOrder:    '  + CAST(@soCount AS VARCHAR);
PRINT '  Shipping:      '  + CAST(@shpCount AS VARCHAR);
PRINT '  StockMovement: '  + CAST(@movCount AS VARCHAR);
