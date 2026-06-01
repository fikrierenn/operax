-- Plan 23 Faz A: Branch (Şube) tablosu + Warehouse lokasyon kolonları
-- Idempotent (tolerant migrate ile çalışır)

-- Şube tablosu
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Branch')
BEGIN
    CREATE TABLE Branch (
        Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId        UNIQUEIDENTIFIER NOT NULL,
        Code             NVARCHAR(50)     NOT NULL,
        Name             NVARCHAR(200)    NOT NULL,
        City             NVARCHAR(100)    NULL,
        Address          NVARCHAR(500)    NULL,
        Phone            NVARCHAR(50)     NULL,
        -- Şube tipi: SUBE (satış şubesi) | FABRIKA | MAGAZA | OFIS | MERKEZ
        BranchType       NVARCHAR(20)     NOT NULL DEFAULT 'SUBE',
        -- İade deposu: NULL ise şubenin ilk deposu fallback olarak kullanılır
        ReturnWarehouseId UNIQUEIDENTIFIER NULL,
        IsActive         BIT              NOT NULL DEFAULT 1,
        IsDeleted        BIT              NOT NULL DEFAULT 0,
        CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy        NVARCHAR(450)    NULL,
        UpdatedAt        DATETIME2        NULL,
        UpdatedBy        NVARCHAR(450)    NULL,
        CONSTRAINT FK_Branch_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    CREATE INDEX IX_Branch_Company ON Branch(CompanyId, IsDeleted);
END

-- Warehouse: BranchId, City, Address kolonları ekle
IF COL_LENGTH('Warehouse', 'BranchId') IS NULL
    ALTER TABLE Warehouse ADD BranchId UNIQUEIDENTIFIER NULL;

IF COL_LENGTH('Warehouse', 'City') IS NULL
    ALTER TABLE Warehouse ADD City NVARCHAR(100) NULL;

IF COL_LENGTH('Warehouse', 'Address') IS NULL
    ALTER TABLE Warehouse ADD Address NVARCHAR(500) NULL;

-- BranchType kolonu (sonradan eklenen — idempotent)
IF COL_LENGTH('Branch', 'BranchType') IS NULL
    ALTER TABLE Branch ADD BranchType NVARCHAR(20) NOT NULL DEFAULT 'SUBE';

-- FK: Warehouse → Branch (Branch tablosu oluşunca ekle)
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Warehouse_Branch'
)
    ALTER TABLE Warehouse
    ADD CONSTRAINT FK_Warehouse_Branch
    FOREIGN KEY (BranchId) REFERENCES Branch(Id);

-- FK: Branch → Warehouse (ReturnWarehouseId — Warehouse oluşunca ekle)
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Branch_ReturnWarehouse'
)
    ALTER TABLE Branch
    ADD CONSTRAINT FK_Branch_ReturnWarehouse
    FOREIGN KEY (ReturnWarehouseId) REFERENCES Warehouse(Id);
