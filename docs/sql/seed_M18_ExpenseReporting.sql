-- Baseline: Gider merkezi (CostCenter) + gider tipi (ExpenseType) taban ağacı.
-- Jenerik departman/gider kalemleri (Genel Müdürlük/Üretim/Satış · Elektrik/Su/Kira) —
-- her şirkette bulunan standart yapı, kuruluma özel sonradan düzenlenir.
-- Idempotent: IF NOT EXISTS korumalı (tekrar koşumda no-op).

DECLARE @CompanyId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Company WHERE IsDeleted = 0 ORDER BY CreatedAt);

-- Gider Merkezleri (CostCenter ağaç)
IF NOT EXISTS (SELECT 1 FROM CostCenter WHERE CompanyId = @CompanyId AND Code = 'GM')
BEGIN
    DECLARE @GmId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO CostCenter (Id, CompanyId, Code, Name, Type, IsActive)
    VALUES (@GmId, @CompanyId, 'GM', 'Genel Müdürlük', 'ADMIN', 1);

    INSERT INTO CostCenter (Id, CompanyId, Code, Name, Type, ParentId, IsActive)
    VALUES
        (NEWID(), @CompanyId, 'GM-IT',  'Bilgi İşlem', 'ADMIN', @GmId, 1),
        (NEWID(), @CompanyId, 'GM-MUH', 'Muhasebe',    'ADMIN', @GmId, 1);
END

IF NOT EXISTS (SELECT 1 FROM CostCenter WHERE CompanyId = @CompanyId AND Code = 'URT')
    INSERT INTO CostCenter (Id, CompanyId, Code, Name, Type, IsActive)
    VALUES (NEWID(), @CompanyId, 'URT', 'Üretim', 'PRODUCTION', 1);

IF NOT EXISTS (SELECT 1 FROM CostCenter WHERE CompanyId = @CompanyId AND Code = 'SAT')
    INSERT INTO CostCenter (Id, CompanyId, Code, Name, Type, IsActive)
    VALUES (NEWID(), @CompanyId, 'SAT', 'Satış & Pazarlama', 'SALES', 1);

-- Gider Tipleri (ExpenseType ağaç)
IF NOT EXISTS (SELECT 1 FROM ExpenseType WHERE CompanyId = @CompanyId AND Code = 'UTIL')
BEGIN
    DECLARE @UtilId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO ExpenseType (Id, CompanyId, Code, Name, UnitOfMeasure)
    VALUES (@UtilId, @CompanyId, 'UTIL', 'Genel Giderler', 'Ay');

    INSERT INTO ExpenseType (Id, CompanyId, Code, Name, UnitOfMeasure, ParentId)
    VALUES
        (NEWID(), @CompanyId, 'ELK', 'Elektrik', 'kWh', @UtilId),
        (NEWID(), @CompanyId, 'SU',  'Su',       'm3',  @UtilId),
        (NEWID(), @CompanyId, 'KIRA','Kira',      'Ay',  @UtilId);
END
