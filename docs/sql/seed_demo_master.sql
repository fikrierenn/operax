-- =============================================================================
-- OPERAX DEMO MASTER SEED — seed_demo.sql'in bağımlı olduğu master katman
-- Şirket: Operax Demo LTD (d1e1b1a5-0000-0000-0000-000000000001)
-- seed_demo/seed_dashboard/seed_business_history/seed_finance_starter SABİT GUID'lerle
-- Warehouse/Bin/Item/Partner/UOM/Category referans eder; bu dosya o satırları oluşturur.
-- seed-demo komutunda seed_demo'dan ÖNCE çalışır. Idempotent: tümü IF NOT EXISTS (Id bazlı).
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CompanyId UNIQUEIDENTIFIER = 'd1e1b1a5-0000-0000-0000-000000000001';

-- İş kuralı: demo şirketi yoksa (seed_core çalışmamış) master yazma — sessiz çık
IF NOT EXISTS (SELECT 1 FROM Company WHERE Id = @CompanyId)
BEGIN
    PRINT 'Demo şirketi yok — demo master atlandı (önce seed_core).';
    RETURN;
END

-- ---- UOM (DictionaryValue) — demo sabit GUID'leri (ADET/KG/MT) ----
-- UOM DictionaryType demo şirketinde olmalı (seed_reference oluşturur; standalone seed-demo için garanti)
DECLARE @UomTypeId UNIQUEIDENTIFIER =
    (SELECT TOP 1 Id FROM DictionaryType WHERE CompanyId = @CompanyId AND Code = 'UOM' AND IsDeleted = 0);
IF @UomTypeId IS NULL
BEGIN
    SET @UomTypeId = NEWID();
    INSERT INTO DictionaryType (Id, CompanyId, Code, NameTr, NameEn, IsSystem)
    VALUES (@UomTypeId, @CompanyId, 'UOM', N'Birim', N'Unit', 1);
END

IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE Id = 'C29E2FC2-E8D7-486A-90CE-B45E6039EE42')
    INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive, UnEceCode, IsWholeNumber)
    VALUES ('C29E2FC2-E8D7-486A-90CE-B45E6039EE42', @CompanyId, @UomTypeId, 'ADET', N'Adet', N'Each', 1, 1, 'C62', 1);
IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE Id = 'A935A202-E6B8-46B8-B4C0-6EE8AA9D009F')
    INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive, UnEceCode, IsWholeNumber)
    VALUES ('A935A202-E6B8-46B8-B4C0-6EE8AA9D009F', @CompanyId, @UomTypeId, 'KG', N'Kilogram', N'Kilogram', 2, 1, 'KGM', 0);
IF NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE Id = '86C09707-AC97-4B4C-AD57-36F11BE9A77A')
    INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive, UnEceCode, IsWholeNumber)
    VALUES ('86C09707-AC97-4B4C-AD57-36F11BE9A77A', @CompanyId, @UomTypeId, 'MT', N'Metre', N'Metre', 3, 1, 'MTR', 0);

-- ---- Category — demo sabit GUID'leri (Elektronik / Hammadde) ----
IF NOT EXISTS (SELECT 1 FROM Category WHERE Id = '7D83A9EB-955C-4613-8512-C4A9CEC75F7D')
    INSERT INTO Category (Id, CompanyId, Code, Name, IsActive, IsDeleted)
    VALUES ('7D83A9EB-955C-4613-8512-C4A9CEC75F7D', @CompanyId, 'ELEK', N'Elektronik', 1, 0);
IF NOT EXISTS (SELECT 1 FROM Category WHERE Id = '04ED30CB-733E-4C89-B817-80443A70C2CD')
    INSERT INTO Category (Id, CompanyId, Code, Name, IsActive, IsDeleted)
    VALUES ('04ED30CB-733E-4C89-B817-80443A70C2CD', @CompanyId, 'HAM', N'Hammadde', 1, 0);

-- ---- Warehouse — demo sabit GUID'leri (WH-001 / WH-002) ----
IF NOT EXISTS (SELECT 1 FROM Warehouse WHERE Id = 'BEF08E7E-8869-4980-A084-5FB8D21A0F6C')
    INSERT INTO Warehouse (Id, CompanyId, Code, Name, IsActive, IsDeleted)
    VALUES ('BEF08E7E-8869-4980-A084-5FB8D21A0F6C', @CompanyId, 'WH-001', N'Ana Depo', 1, 0);
IF NOT EXISTS (SELECT 1 FROM Warehouse WHERE Id = '93BF43EB-C1E6-4D4A-BCDD-77CBE259FF2A')
    INSERT INTO Warehouse (Id, CompanyId, Code, Name, IsActive, IsDeleted)
    VALUES ('93BF43EB-C1E6-4D4A-BCDD-77CBE259FF2A', @CompanyId, 'WH-002', N'Üretim Deposu', 1, 0);

-- ---- Bin — WH-001 (6) + WH-002 (3) demo sabit GUID'leri ----
INSERT INTO Bin (Id, WarehouseId, Code, Zone, SortNo, IsPickingArea, IsReceivingArea, IsActive, IsDeleted)
SELECT v.Id, v.WhId, v.Code, v.Zone, v.SortNo, v.Pick, v.Recv, 1, 0
FROM (VALUES
    ('32E9FBDE-68DE-4299-AD3D-5C2E7C27C404', 'BEF08E7E-8869-4980-A084-5FB8D21A0F6C', 'GRS-01', 'GRS', 1, 0, 1),
    ('B14C27C1-BD48-4E07-8498-E2C452D084D7', 'BEF08E7E-8869-4980-A084-5FB8D21A0F6C', 'CIK-01', 'CIK', 2, 0, 0),
    ('EF5E5804-8FAA-4A64-B277-6A624C1EE83F', 'BEF08E7E-8869-4980-A084-5FB8D21A0F6C', 'R-A-01', 'A',   3, 1, 0),
    ('81F5B334-A865-4929-9C35-E233806BCD8D', 'BEF08E7E-8869-4980-A084-5FB8D21A0F6C', 'R-A-02', 'A',   4, 1, 0),
    ('EA8A2D07-D233-489E-953D-06C04B4C1AA5', 'BEF08E7E-8869-4980-A084-5FB8D21A0F6C', 'R-A-03', 'A',   5, 1, 0),
    ('7B6D19E9-A38C-4991-99C8-E8A92F0FA18A', 'BEF08E7E-8869-4980-A084-5FB8D21A0F6C', 'R-B-01', 'B',   6, 1, 0),
    ('92853AB4-2DA6-491D-BBB5-D72913CD795B', '93BF43EB-C1E6-4D4A-BCDD-77CBE259FF2A', 'URE-GRS','GRS', 1, 0, 1),
    ('BEF3B305-EFEF-41EA-BBA0-5010C326BFB7', '93BF43EB-C1E6-4D4A-BCDD-77CBE259FF2A', 'URE-A-01','URE',2, 1, 0),
    ('C8FB1DC4-BA93-454A-B807-2E9B62899CBF', '93BF43EB-C1E6-4D4A-BCDD-77CBE259FF2A', 'URE-A-02','URE',3, 1, 0)
) AS v(Id, WhId, Code, Zone, SortNo, Pick, Recv)
WHERE NOT EXISTS (SELECT 1 FROM Bin b WHERE b.Id = v.Id);

-- ---- Item — demo sabit GUID'leri (3 mamul + 2 hammadde) ----
INSERT INTO Item (Id, CompanyId, Code, Name, CategoryId, BaseUomId, IsActive, IsLotTracked, IsSerialTracked, CreatedAt, IsDeleted, TaxRate)
SELECT v.Id, @CompanyId, v.Code, v.Name, v.CatId, v.UomId, 1, v.Lot, 0, GETUTCDATE(), 0, v.Tax
FROM (VALUES
    ('F780133C-F8F5-4B12-A3D4-749500E50125', 'PRD-001', N'Laptop Dell XPS 15',         '7D83A9EB-955C-4613-8512-C4A9CEC75F7D', 'C29E2FC2-E8D7-486A-90CE-B45E6039EE42', 0, CAST(20.00 AS DECIMAL(5,2))),
    ('9D732E3B-FEB0-49D1-9686-CC8B1C7496F9', 'PRD-002', N'Kablosuz Mouse',             '7D83A9EB-955C-4613-8512-C4A9CEC75F7D', 'C29E2FC2-E8D7-486A-90CE-B45E6039EE42', 0, CAST(20.00 AS DECIMAL(5,2))),
    ('AD7E22C3-77A6-41B1-9082-8228364976E7', 'PRD-003', N'USB-C Hub 7 Port',           '7D83A9EB-955C-4613-8512-C4A9CEC75F7D', 'C29E2FC2-E8D7-486A-90CE-B45E6039EE42', 0, CAST(20.00 AS DECIMAL(5,2))),
    ('2D089444-516A-482B-967C-D7547F62AE02', 'HAM-001', N'Alüminyum Profil 6063-T5',   '04ED30CB-733E-4C89-B817-80443A70C2CD', '86C09707-AC97-4B4C-AD57-36F11BE9A77A', 1, CAST(10.00 AS DECIMAL(5,2))),
    ('B21905F7-3652-42F2-ACAD-ED3AE51CE609', 'HAM-002', N'Çelik Levha 3mm',            '04ED30CB-733E-4C89-B817-80443A70C2CD', 'A935A202-E6B8-46B8-B4C0-6EE8AA9D009F', 1, CAST(10.00 AS DECIMAL(5,2)))
) AS v(Id, Code, Name, CatId, UomId, Lot, Tax)
WHERE NOT EXISTS (SELECT 1 FROM Item i WHERE i.Id = v.Id);

-- ---- Partner — demo sabit GUID'leri (2 tedarikçi + 2 müşteri) ----
INSERT INTO Partner (Id, CompanyId, Code, Name, Type, IsActive, IsDeleted)
SELECT v.Id, @CompanyId, v.Code, v.Name, v.Type, 1, 0
FROM (VALUES
    ('68EC1270-3DEF-4D6C-A1CF-ADDF492EBC27', 'SUP-001', N'Teknoloji Dağıtım A.Ş.',  'VENDOR'),
    ('F3FEA396-222A-4679-AE9C-9AC21FD62A1A', 'SUP-002', N'Global Hammadde Ltd.',     'VENDOR'),
    ('3FC8974C-E2B0-443D-AB56-9775ACBC9E29', 'CUS-001', N'ABC Bilişim Sistemleri',   'CUSTOMER'),
    ('3D2EC5A8-4EB1-4C7A-BDDC-17AFD4DD49D6', 'CUS-002', N'XYZ Mühendislik Sanayi',   'CUSTOMER')
) AS v(Id, Code, Name, Type)
WHERE NOT EXISTS (SELECT 1 FROM Partner p WHERE p.Id = v.Id);

PRINT 'Demo master hazır: 2 depo, 9 raf, 5 ürün, 4 cari, 3 birim, 2 kategori (sabit GUID).';
