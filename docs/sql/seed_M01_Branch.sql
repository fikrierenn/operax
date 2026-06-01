-- Plan 23 Faz E: Demo şube + depo bağlama seed
-- Idempotent: IF NOT EXISTS korumalı

DECLARE @CompanyId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Company WHERE IsDeleted = 0 ORDER BY CreatedAt);
DECLARE @W01Id     UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code);

-- Merkez şubesi
IF NOT EXISTS (SELECT 1 FROM Branch WHERE CompanyId = @CompanyId AND Code = 'MRK')
BEGIN
    DECLARE @MerkezId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO Branch (Id, CompanyId, Code, Name, City, BranchType, IsActive)
    VALUES (@MerkezId, @CompanyId, 'MRK', 'Merkez', 'İstanbul', 'MERKEZ', 1);

    -- Ana depoyu merkeze bağla
    UPDATE Warehouse SET BranchId = @MerkezId WHERE Id = @W01Id;
END

-- İstanbul satış şubesi
IF NOT EXISTS (SELECT 1 FROM Branch WHERE CompanyId = @CompanyId AND Code = 'IST-01')
BEGIN
    DECLARE @IstId UNIQUEIDENTIFIER = NEWID();
    DECLARE @IstWhId UNIQUEIDENTIFIER = NEWID();

    INSERT INTO Branch (Id, CompanyId, Code, Name, City, BranchType, IsActive)
    VALUES (@IstId, @CompanyId, 'IST-01', 'İstanbul Satış Şubesi', 'İstanbul', 'SUBE', 1);

    -- Şubeye ait depo
    INSERT INTO Warehouse (Id, CompanyId, Code, Name, BranchId, IsActive)
    VALUES (@IstWhId, @CompanyId, 'W-IST', 'İstanbul Depo', @IstId, 1);

    -- İade deposu olarak kendini ata
    UPDATE Branch SET ReturnWarehouseId = @IstWhId WHERE Id = @IstId;
END
