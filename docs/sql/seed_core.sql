-- Seed Dictionary Types & Values

-- Note: In a real migration, we'd need a CompanyId. 
-- Using a fixed GUID for the SEED / System Company or placeholder.
DECLARE @SystemCompanyId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- Types (Idempotent)
IF NOT EXISTS (SELECT 1 FROM DictionaryType WHERE Code = 'STATUS' AND CompanyId = @SystemCompanyId)
    INSERT INTO DictionaryType (Id, CompanyId, Code, NameTr, NameEn, IsSystem)
    VALUES (NEWID(), @SystemCompanyId, 'STATUS', 'Belge Durumu', 'Document Status', 1);

IF NOT EXISTS (SELECT 1 FROM DictionaryType WHERE Code = 'UOM' AND CompanyId = @SystemCompanyId)
    INSERT INTO DictionaryType (Id, CompanyId, Code, NameTr, NameEn, IsSystem)
    VALUES (NEWID(), @SystemCompanyId, 'UOM', 'Ölçü Birimi', 'Unit of Measure', 1);

IF NOT EXISTS (SELECT 1 FROM DictionaryType WHERE Code = 'MOV_TYPE' AND CompanyId = @SystemCompanyId)
    INSERT INTO DictionaryType (Id, CompanyId, Code, NameTr, NameEn, IsSystem)
    VALUES (NEWID(), @SystemCompanyId, 'MOV_TYPE', 'Hareket Tipi', 'Movement Type', 1);

-- Values for STATUS (Idempotent)
DECLARE @StatusTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM DictionaryType WHERE Code = 'STATUS' AND CompanyId = @SystemCompanyId);

IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE TypeId = @StatusTypeId AND Code = 'DRAFT' AND CompanyId = @SystemCompanyId)
    INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
    VALUES (NEWID(), @SystemCompanyId, @StatusTypeId, 'DRAFT', 'Taslak', 'Draft', 10);

IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE TypeId = @StatusTypeId AND Code = 'POSTED' AND CompanyId = @SystemCompanyId)
    INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
    VALUES (NEWID(), @SystemCompanyId, @StatusTypeId, 'POSTED', 'Onaylı', 'Posted', 20);

IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE TypeId = @StatusTypeId AND Code = 'CANCELLED' AND CompanyId = @SystemCompanyId)
    INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
    VALUES (NEWID(), @SystemCompanyId, @StatusTypeId, 'CANCELLED', 'İptal', 'Cancelled', 30);

-- Modules (v2.2-TR Standart - Idempotent because Code has a UNIQUE constraint)
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M00') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M00', 'Platform Çekirdek', 'Platform Core');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M01') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M01', 'Ana Veri Yönetimi', 'Master Data');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M02') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M02', 'Envanter Hareketleri', 'Inventory Ledger');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M03') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M03', 'Satınalma & Mal Kabul', 'Procurement & Receiving');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M04') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M04', 'Satış Siparişi', 'Sales Order');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M05') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M05', 'Sevkiyat', 'Shipping');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M06') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M06', 'Toplama (Picking)', 'Picking');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M07') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M07', 'Transfer', 'Transfer');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M08') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M08', 'Sayım (Cycle Count)', 'Cycle Count');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M09') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M09', 'İzlenebilirlik', 'Traceability');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M10') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M10', 'Üretim Yönetimi', 'Manufacturing');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M11') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M11', 'B2B Portal', 'B2B Portal');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M12') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M12', 'Servis Yönetimi', 'Service');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M13') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M13', 'Proje Yönetimi', 'Project');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M14') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M14', 'Prim & Teşvik', 'Incentives');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M15') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M15', 'Dashboard & Analiz', 'Dashboards');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M16') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M16', 'Entegrasyon Köprüsü', 'Integration Bridge');
IF NOT EXISTS (SELECT 1 FROM Module WHERE Code = 'M17') INSERT INTO Module (Id, Code, NameTr, NameEn) VALUES (NEWID(), 'M17', 'Paketleme & Lisans', 'Packaging & Licensing');

-- Seed Status Transitions
-- Hem System Şirketi hem de Demo Şirketi için standart geçişler tanımlanır.
-- RECEIVING, SHIPMENT, TRANSFER için DRAFT -> POSTED
-- CYCLE_COUNT için DRAFT -> COMPLETED
-- PRODUCTION için DRAFT -> COMPLETED

DECLARE @DemoCompanyId UNIQUEIDENTIFIER = 'd1e1b1a5-0000-0000-0000-000000000001';

-- System Company Transitions (Idempotent)
IF NOT EXISTS (SELECT 1 FROM StatusTransition WHERE CompanyId = @SystemCompanyId AND DocumentType = 'RECEIVING' AND FromStatusCode = 'DRAFT' AND ToStatusCode = 'POSTED')
    INSERT INTO StatusTransition (Id, CompanyId, DocumentType, FromStatusCode, ToStatusCode, ActionNameTr, ActionNameEn, OrderNo)
    VALUES (NEWID(), @SystemCompanyId, 'RECEIVING', 'DRAFT', 'POSTED', 'Mal Kabulü Onayla', 'Approve Receiving', 10);

IF NOT EXISTS (SELECT 1 FROM StatusTransition WHERE CompanyId = @SystemCompanyId AND DocumentType = 'SHIPMENT' AND FromStatusCode = 'DRAFT' AND ToStatusCode = 'POSTED')
    INSERT INTO StatusTransition (Id, CompanyId, DocumentType, FromStatusCode, ToStatusCode, ActionNameTr, ActionNameEn, OrderNo)
    VALUES (NEWID(), @SystemCompanyId, 'SHIPMENT', 'DRAFT', 'POSTED', 'Sevkiyatı Onayla', 'Approve Shipment', 10);

IF NOT EXISTS (SELECT 1 FROM StatusTransition WHERE CompanyId = @SystemCompanyId AND DocumentType = 'TRANSFER' AND FromStatusCode = 'DRAFT' AND ToStatusCode = 'POSTED')
    INSERT INTO StatusTransition (Id, CompanyId, DocumentType, FromStatusCode, ToStatusCode, ActionNameTr, ActionNameEn, OrderNo)
    VALUES (NEWID(), @SystemCompanyId, 'TRANSFER', 'DRAFT', 'POSTED', 'Transferi Onayla', 'Approve Transfer', 10);

IF NOT EXISTS (SELECT 1 FROM StatusTransition WHERE CompanyId = @SystemCompanyId AND DocumentType = 'CYCLE_COUNT' AND FromStatusCode = 'DRAFT' AND ToStatusCode = 'COMPLETED')
    INSERT INTO StatusTransition (Id, CompanyId, DocumentType, FromStatusCode, ToStatusCode, ActionNameTr, ActionNameEn, OrderNo)
    VALUES (NEWID(), @SystemCompanyId, 'CYCLE_COUNT', 'DRAFT', 'COMPLETED', 'Sayımı Tamamla', 'Complete Count', 10);

IF NOT EXISTS (SELECT 1 FROM StatusTransition WHERE CompanyId = @SystemCompanyId AND DocumentType = 'PRODUCTION' AND FromStatusCode = 'DRAFT' AND ToStatusCode = 'COMPLETED')
    INSERT INTO StatusTransition (Id, CompanyId, DocumentType, FromStatusCode, ToStatusCode, ActionNameTr, ActionNameEn, OrderNo)
    VALUES (NEWID(), @SystemCompanyId, 'PRODUCTION', 'DRAFT', 'COMPLETED', 'Üretimi Tamamla', 'Complete Production', 10);


-- Demo Company Transitions (Idempotent)
IF NOT EXISTS (SELECT 1 FROM StatusTransition WHERE CompanyId = @DemoCompanyId AND DocumentType = 'RECEIVING' AND FromStatusCode = 'DRAFT' AND ToStatusCode = 'POSTED')
    INSERT INTO StatusTransition (Id, CompanyId, DocumentType, FromStatusCode, ToStatusCode, ActionNameTr, ActionNameEn, OrderNo)
    VALUES (NEWID(), @DemoCompanyId, 'RECEIVING', 'DRAFT', 'POSTED', 'Mal Kabulü Onayla', 'Approve Receiving', 10);

IF NOT EXISTS (SELECT 1 FROM StatusTransition WHERE CompanyId = @DemoCompanyId AND DocumentType = 'SHIPMENT' AND FromStatusCode = 'DRAFT' AND ToStatusCode = 'POSTED')
    INSERT INTO StatusTransition (Id, CompanyId, DocumentType, FromStatusCode, ToStatusCode, ActionNameTr, ActionNameEn, OrderNo)
    VALUES (NEWID(), @DemoCompanyId, 'SHIPMENT', 'DRAFT', 'POSTED', 'Sevkiyatı Onayla', 'Approve Shipment', 10);

IF NOT EXISTS (SELECT 1 FROM StatusTransition WHERE CompanyId = @DemoCompanyId AND DocumentType = 'TRANSFER' AND FromStatusCode = 'DRAFT' AND ToStatusCode = 'POSTED')
    INSERT INTO StatusTransition (Id, CompanyId, DocumentType, FromStatusCode, ToStatusCode, ActionNameTr, ActionNameEn, OrderNo)
    VALUES (NEWID(), @DemoCompanyId, 'TRANSFER', 'DRAFT', 'POSTED', 'Transferi Onayla', 'Approve Transfer', 10);

IF NOT EXISTS (SELECT 1 FROM StatusTransition WHERE CompanyId = @DemoCompanyId AND DocumentType = 'CYCLE_COUNT' AND FromStatusCode = 'DRAFT' AND ToStatusCode = 'COMPLETED')
    INSERT INTO StatusTransition (Id, CompanyId, DocumentType, FromStatusCode, ToStatusCode, ActionNameTr, ActionNameEn, OrderNo)
    VALUES (NEWID(), @DemoCompanyId, 'CYCLE_COUNT', 'DRAFT', 'COMPLETED', 'Sayımı Tamamla', 'Complete Count', 10);

IF NOT EXISTS (SELECT 1 FROM StatusTransition WHERE CompanyId = @DemoCompanyId AND DocumentType = 'PRODUCTION' AND FromStatusCode = 'DRAFT' AND ToStatusCode = 'COMPLETED')
    INSERT INTO StatusTransition (Id, CompanyId, DocumentType, FromStatusCode, ToStatusCode, ActionNameTr, ActionNameEn, OrderNo)
    VALUES (NEWID(), @DemoCompanyId, 'PRODUCTION', 'DRAFT', 'COMPLETED', 'Üretimi Tamamla', 'Complete Production', 10);

-- ── EKSİKSİZ SİSTEM-GENELİ GEÇİŞ SETİ (Plan 54 Faz 3) ──────────────────────────
-- sp_ValidateStatusTransition Guid.Empty (system) fallback yapar → her şirket bu seti
-- miras alır (fresh kurulum per-company seed'e/zamanlamaya bağlı kalmaz). Set-based +
-- idempotent (NOT EXISTS). Eksik olanları tamamlar: POSTED→CANCELLED (mevcut postable'lar)
-- + PURCHASE_ORDER tam yaşam döngüsü (POSTED→CLOSED tam kabul, POSTED→CLOSED_PARTIAL kalan iptal).
INSERT INTO StatusTransition (Id, CompanyId, DocumentType, FromStatusCode, ToStatusCode, ActionNameTr, ActionNameEn, OrderNo)
SELECT NEWID(), @SystemCompanyId, t.DocumentType, t.FromStatusCode, t.ToStatusCode, t.ActionNameTr, t.ActionNameEn, t.OrderNo
FROM (VALUES
    ('RECEIVING',      'DRAFT',  'POSTED',         N'Onayla',          'Approve',       10),
    ('RECEIVING',      'POSTED', 'CANCELLED',      N'İptal Et',        'Cancel',        20),
    ('SHIPMENT',       'DRAFT',  'POSTED',         N'Onayla',          'Approve',       10),
    ('SHIPMENT',       'POSTED', 'CANCELLED',      N'İptal Et',        'Cancel',        20),
    ('TRANSFER',       'DRAFT',  'POSTED',         N'Onayla',          'Approve',       10),
    ('TRANSFER',       'POSTED', 'CANCELLED',      N'İptal Et',        'Cancel',        20),
    ('CYCLE_COUNT',    'DRAFT',  'COMPLETED',      N'Tamamla',         'Complete',      10),
    ('PRODUCTION',     'DRAFT',  'COMPLETED',      N'Tamamla',         'Complete',      10),
    ('PURCHASE_ORDER', 'DRAFT',  'POSTED',         N'Onayla',          'Approve',       10),
    ('PURCHASE_ORDER', 'POSTED', 'CANCELLED',      N'İptal Et',        'Cancel',        20),
    ('PURCHASE_ORDER', 'POSTED', 'CLOSED',         N'Kapat',           'Close',         30),
    ('PURCHASE_ORDER', 'POSTED', 'CLOSED_PARTIAL', N'Kalanı Kapat',    'Close Partial', 40)
) AS t(DocumentType, FromStatusCode, ToStatusCode, ActionNameTr, ActionNameEn, OrderNo)
WHERE NOT EXISTS (
    SELECT 1 FROM StatusTransition st
    WHERE st.CompanyId = @SystemCompanyId
      AND st.DocumentType = t.DocumentType
      AND st.FromStatusCode = t.FromStatusCode
      AND st.ToStatusCode = t.ToStatusCode
);