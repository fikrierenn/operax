-- Seed Dictionary Types & Values

-- Note: In a real migration, we'd need a CompanyId. 
-- Using a fixed GUID for the SEED / System Company or placeholder.
DECLARE @SystemCompanyId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- Types
INSERT INTO DictionaryType (Id, CompanyId, Code, NameTr, NameEn, IsSystem)
VALUES 
(NEWID(), @SystemCompanyId, 'STATUS', 'Belge Durumu', 'Document Status', 1),
(NEWID(), @SystemCompanyId, 'UOM', 'Ölçü Birimi', 'Unit of Measure', 1),
(NEWID(), @SystemCompanyId, 'MOV_TYPE', 'Hareket Tipi', 'Movement Type', 1);

-- Values for STATUS
DECLARE @StatusTypeId UNIQUEIDENTIFIER = (SELECT Id FROM DictionaryType WHERE Code = 'STATUS');
INSERT INTO DictionaryValue (CompanyId, TypeId, Code, NameTr, NameEn, OrderNo)
VALUES 
(@SystemCompanyId, @StatusTypeId, 'DRAFT', 'Taslak', 'Draft', 10),
(@SystemCompanyId, @StatusTypeId, 'POSTED', 'Onaylı', 'Posted', 20),
(@SystemCompanyId, @StatusTypeId, 'CANCELLED', 'İptal', 'Cancelled', 30);

-- Modules (v2.2-TR Standart)
INSERT INTO Module (Code, NameTr, NameEn)
VALUES 
('M00', 'Platform Çekirdek', 'Platform Core'),
('M01', 'Ana Veri Yönetimi', 'Master Data'),
('M02', 'Envanter Hareketleri', 'Inventory Ledger'),
('M03', 'Satınalma & Mal Kabul', 'Procurement & Receiving'),
('M04', 'Satış Siparişi', 'Sales Order'),
('M05', 'Sevkiyat', 'Shipping'),
('M06', 'Toplama (Picking)', 'Picking'),
('M07', 'Transfer', 'Transfer'),
('M08', 'Sayım (Cycle Count)', 'Cycle Count'),
('M09', 'İzlenebilirlik', 'Traceability'),
('M10', 'Üretim Yönetimi', 'Manufacturing'),
('M11', 'B2B Portal', 'B2B Portal'),
('M12', 'Servis Yönetimi', 'Service'),
('M13', 'Proje Yönetimi', 'Project'),
('M14', 'Prim & Teşvik', 'Incentives'),
('M15', 'Dashboard & Analiz', 'Dashboards'),
('M16', 'Entegrasyon Köprüsü', 'Integration Bridge'),
('M17', 'Paketleme & Lisans', 'Packaging & Licensing');
()