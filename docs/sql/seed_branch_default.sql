-- Baseline: varsayılan Merkez şubesi (her kurulum için — fn_DefaultBranchId taban şubesi ister).
-- Jenerik: yalnız MRK/Merkez + ana depoyu ona bağlar. Örnek/demo şube YOK (o seed-demo'da).
-- Idempotent: IF NOT EXISTS korumalı (tekrar koşumda no-op).

DECLARE @CompanyId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Company WHERE IsDeleted = 0 ORDER BY CreatedAt);
DECLARE @MainWhId  UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code);

-- Varsayılan merkez şubesi (şehir varsayımı yok — kuruluma özel sonradan düzenlenir)
IF @CompanyId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Branch WHERE CompanyId = @CompanyId AND Code = 'MRK')
BEGIN
    DECLARE @MerkezId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO Branch (Id, CompanyId, Code, Name, BranchType, IsActive)
    VALUES (@MerkezId, @CompanyId, 'MRK', N'Merkez', 'MERKEZ', 1);

    -- Ana depoyu merkez şubeye bağla (varsa)
    IF @MainWhId IS NOT NULL
        UPDATE Warehouse SET BranchId = @MerkezId WHERE Id = @MainWhId AND BranchId IS NULL;
END
