-- OPERAX 360 Derece "Canlı Sistem" Seed Data Scripti
-- Amacı: Sistem ayağa kalktığında tüm ekranlarda zengin veri görmektir.

-- 0. Değişkenler ve Temel ID'ler
DECLARE @CId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Company);
DECLARE @WH_ANA UNIQUEIDENTIFIER = NEWID();
DECLARE @WH_MAMUL UNIQUEIDENTIFIER = NEWID();
DECLARE @WH_WIP UNIQUEIDENTIFIER = NEWID();

-- 1. DEPOLAR ve RAFLAR
INSERT INTO Warehouse (Id, CompanyId, Code, Name) VALUES 
(@WH_ANA, @CId, 'ANA-DEP', 'Ana Hammadde Deposu'),
(@WH_MAMUL, @CId, 'MAM-DEP', 'Mamül Ambarı'),
(@WH_WIP, @CId, 'WIP-AREA', 'Üretim Sahası');

INSERT INTO Bin (Id, WarehouseId, Code, IsPickingArea) VALUES
(NEWID(), @WH_ANA, 'A-01-01', 1), (NEWID(), @WH_ANA, 'A-01-02', 1),
(NEWID(), @WH_MAMUL, 'M-01-01', 1), (NEWID(), @WH_MAMUL, 'M-01-02', 1),
(NEWID(), @WH_WIP, 'WIP-01', 0);

-- 2. PARTNERLER (Cari Kartlar)
DECLARE @SUP_CAM UNIQUEIDENTIFIER = NEWID();
DECLARE @CUST_OTEL UNIQUEIDENTIFIER = NEWID();

INSERT INTO Partner (Id, CompanyId, Code, Name, Type) VALUES
(@SUP_CAM, @CId, 'CAMSAN', 'Camsan Cam Sanayi A.Ş.', 'VENDOR'),
(@CUST_OTEL, @CId, 'HILTON', 'Hilton Hotels & Resorts', 'CUSTOMER'),
(NEWID(), @CId, 'TEKNIK-PROF', 'Teknik Profil LTD.', 'VENDOR');

-- 3. ÜRÜNLER (Item) & FIYATLAR
DECLARE @IT_DK UNIQUEIDENTIFIER = NEWID();
DECLARE @IT_CAM UNIQUEIDENTIFIER = NEWID();
DECLARE @IT_PROF UNIQUEIDENTIFIER = NEWID();

INSERT INTO Item (Id, CompanyId, Code, Name, BaseUomId, LastPurchasePrice, IsLotTracked) VALUES
(@IT_DK, @CId, 'DK-8080', 'Duşakabin 80x80 Kare', NEWID(), 1100, 1),
(@IT_CAM, @CId, 'CAM-80', 'Temperli Cam 80cm', NEWID(), 400, 0),
(@IT_PROF, @CId, 'PROF-ALU', 'Alüminyum Yan Profil', NEWID(), 150, 0);

-- 4. SATINALMA (PO) ve MAL KABUL (RCV)
DECLARE @PO_ID UNIQUEIDENTIFIER = NEWID();
INSERT INTO PurchaseOrder (Id, CompanyId, DocNo, SupplierId, WarehouseId, Status)
VALUES (@PO_ID, @CId, 'PO-2026-001', @SUP_CAM, @WH_ANA, 'APPROVED');

INSERT INTO PurchaseOrderLine (Id, PurchaseOrderId, ItemId, RequestedQty)
VALUES (NEWID(), @PO_ID, @IT_CAM, 100), (NEWID(), @PO_ID, @IT_PROF, 200);

-- Mal Kabul Girişi ve Stok Artışı
INSERT INTO StockMovement (ItemId, MovementType, Qty, WarehouseId, ReferenceId) VALUES
(@IT_CAM, 'RECEIPT', 100, @WH_ANA, @PO_ID),
(@IT_PROF, 'RECEIPT', 200, @WH_ANA, @PO_ID);

-- 5. SATIŞ SİPARİŞİ (SO) ve SEVKİYAT (SHP)
DECLARE @SO_ID UNIQUEIDENTIFIER = NEWID();
INSERT INTO SalesOrder (Id, CompanyId, DocNo, CustomerId, WarehouseId, Status)
VALUES (@SO_ID, @CId, 'SO-2026-001', @CUST_OTEL, @WH_MAMUL, 'POSTED');

INSERT INTO SalesOrderLine (Id, SalesOrderId, ItemId, RequestedQty)
VALUES (NEWID(), @SO_ID, @IT_DK, 5);

-- 6. ÜRETİM (M10) - Rota, İş Emri, Aktivite
DECLARE @ROUTE_ID UNIQUEIDENTIFIER = NEWID();
INSERT INTO ProductRoute (Id, CompanyId, ItemId, Name) VALUES (@ROUTE_ID, @CId, @IT_DK, 'Standart 80x80 Rotası');

DECLARE @STEP_KESSIN UNIQUEIDENTIFIER = NEWID();
DECLARE @STEP_MONT UNIQUEIDENTIFIER = NEWID();

INSERT INTO ProductRouteStep (Id, ProductRouteId, StepOrder, OperationName, StandardLaborCost, StandardMachineCost) VALUES
(@STEP_KESSIN, @ROUTE_ID, 1, 'Cam Kesim', 20, 50),
(@STEP_MONT, @ROUTE_ID, 2, 'Montaj', 60, 10);

-- Tamamlanmış Üretim (PRD-001)
DECLARE @PRD_001 UNIQUEIDENTIFIER = NEWID();
DECLARE @UID UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM AspNetUsers);

INSERT INTO ProductionOrder (Id, CompanyId, DocNo, ItemId, QtyTarget, QtyProduced, Status, PlannedMaterialCost, PlannedResourceCost, ActualMaterialCost, ActualResourceCost)
VALUES (@PRD_001, @CId, 'PRD-2026-001', @IT_DK, 10, 10, 'COMPLETED', 7000, 1500, 7200, 1650);

-- Üretim Aktivitesi (Operatör saniye kasmış)
INSERT INTO ProductionActivity (Id, ProductionOrderId, WorkCenterId, UserId, StartTime, EndTime, QtyProduced, Notes)
VALUES (NEWID(), @PRD_001, NEWID(), @UID, DATEADD(MINUTE, -120, GETDATE()), DATEADD(MINUTE, -10, GETDATE()), 10, CAST(@STEP_MONT as NVARCHAR(50)));

-- 9. FIYAT LISTELERI
DECLARE @PL_GENEL UNIQUEIDENTIFIER = NEWID();
DECLARE @PL_VIP UNIQUEIDENTIFIER = NEWID();

INSERT INTO PriceList (Id, CompanyId, Code, Name, Currency) VALUES
(@PL_GENEL, @CId, 'GENEL-2026', '2026 Genel Satış Listesi', 'TRY'),
(@PL_VIP, @CId, 'VIP-HOTEL', 'Otel Grubu Özel Fiyatları', 'USD');

INSERT INTO PriceListLine (PriceListId, ItemId, UnitPrice) VALUES
(@PL_GENEL, @IT_DK, 2400.00),
(@PL_VIP, @IT_DK, 650.00);

-- 10. LOT & SERI NO KAYITLARI
DECLARE @LOT_ID UNIQUEIDENTIFIER = NEWID();
INSERT INTO ItemLot (Id, ItemId, LotNo, StockQty, ExpiryDate)
VALUES (@LOT_ID, @IT_DK, 'LOT-MAR-001', 50, DATEADD(YEAR, 2, GETDATE()));

INSERT INTO ItemSerial (ItemId, SerialNo, Status)
VALUES (@IT_DK, 'SN-DK-00001', 'IN_STOCK'), (@IT_DK, 'SN-DK-00002', 'IN_STOCK');

-- 11. SEVKIYAT (SHP) - Gerçekleşen
DECLARE @SHP_ID UNIQUEIDENTIFIER = NEWID();
INSERT INTO Shipment (Id, CompanyId, DocNo, CustomerId, WarehouseId, Status, ShipmentDate)
VALUES (@SHP_ID, @CId, 'SHP-2026-001', @CUST_OTEL, @WH_MAMUL, 'POSTED', GETDATE());

INSERT INTO ShipmentLine (Id, ShipmentId, ItemId, QtyBase)
VALUES (NEWID(), @SHP_ID, @IT_DK, 2);

-- 13. GIDER YÖNETIMI (M18)
DECLARE @CC_PROD UNIQUEIDENTIFIER = NEWID();
DECLARE @CC_ADMIN UNIQUEIDENTIFIER = NEWID();

INSERT INTO CostCenter (Id, CompanyId, Code, Name, Type) VALUES
(@CC_PROD, @CId, 'CC-PROD', 'Üretim Gider Merkezi', 'PRODUCTION'),
(@CC_ADMIN, @CId, 'CC-ADMIN', 'Genel Yönetim', 'ADMIN');

DECLARE @ET_ELEC UNIQUEIDENTIFIER = NEWID();
DECLARE @ET_WATER UNIQUEIDENTIFIER = NEWID();

INSERT INTO ExpenseType (Id, CompanyId, Code, Name, UnitOfMeasure) VALUES
(@ET_ELEC, @CId, 'ELEC', 'Elektrik Enerjisi', 'kWh'),
(@ET_WATER, @CId, 'WATER', 'Şehir Suyu', 'm3');

-- Elektrik Faturası Örneği
DECLARE @EX_INV UNIQUEIDENTIFIER = NEWID();
INSERT INTO ExpenseInvoice (Id, CompanyId, PartnerId, DocNo, InvoiceDate, TotalAmount)
VALUES (@EX_INV, @CId, @SUP_CAM, 'INV-ELEC-MAR', GETDATE(), 15000);

INSERT INTO ExpenseInvoiceLine (ExpenseInvoiceId, ExpenseTypeId, CostCenterId, Quantity, UnitPrice, Amount)
VALUES (@EX_INV, @ET_ELEC, @CC_PROD, 5000, 3.00, 15000);

-- 14. BÜTÇE & NAKIT AKIŞI (M19)
-- Senaryo: Elektrik faturası Mart'ta geliyor (Tahakkuk), ama Ödemesi Nisan'da yapılıyor (Nakit Akışı).
DECLARE @BUD_2026 UNIQUEIDENTIFIER = NEWID();
INSERT INTO Budget (Id, CompanyId, Year, Code, Name, Type)
VALUES (@BUD_2026, @CId, 2026, 'OP-2026', '2026 Operasyonel Bütçe', 'OPERATIONAL');

INSERT INTO BudgetLine (BudgetId, AccrualDate, CashDate, Direction, CostCenterId, ExpenseTypeId, AmountPlanned, AmountActual)
VALUES (@BUD_2026, '2026-03-30', '2026-04-10', 'OUT', @CC_PROD, @ET_ELEC, 20000, 0);

-- Tahmini Tahsilat: Satış Mart'ta yapıldı, Tahsilat Nisan başı.
INSERT INTO BudgetLine (BudgetId, AccrualDate, CashDate, Direction, AmountPlanned)
VALUES (@BUD_2026, '2026-03-25', '2026-04-05', 'IN', 45000);

-- Nakit Akış Forecast
INSERT INTO CashFlowForecast (CompanyId, ForecastDate, Type, SourceType, Amount, Probability)
VALUES (@CId, DATEADD(DAY, 30, GETDATE()), 'IN', 'SALES_ORDER', 45000, 85.0);

PRINT 'OPERAX 360 v3.0: Enerji, Gider ve Bütçe Dünyası kuruldu!';
