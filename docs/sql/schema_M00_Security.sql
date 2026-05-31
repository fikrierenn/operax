-- =============================================================================
-- schema_M00_Security.sql — F0.2 DB-Driven RBAC
-- Rol ↔ modül (üst Feature klasörü) yetki eşleştirmesi.
-- Roller AspNetRoles'ta dinamik; bu tablo hangi rolün hangi modüle eriştiğini tutar.
-- Identity-bitişik tablo: AspNetRoles gibi CompanyId taşımaz (single-tenant → roller global).
-- Idempotent: tekrar çalıştırılabilir.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RoleModuleAccess')
BEGIN
    CREATE TABLE RoleModuleAccess (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        RoleId      NVARCHAR(450)    NOT NULL,            -- AspNetRoles.Id
        ModuleKey   NVARCHAR(64)     NOT NULL,            -- üst Feature klasörü: 'Receiving','Finance',...
        AccessLevel TINYINT          NOT NULL DEFAULT 1,  -- 1=VIEW (sadece GET), 2=EDIT (GET+POST)
        CreatedAt   DATETIME2        DEFAULT GETUTCDATE(),
        CONSTRAINT FK_RMA_Role FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_RMA_RoleModule UNIQUE (RoleId, ModuleKey)
    );

    -- Yetki sorgusu rol bazlı çalışır → RoleId üzerinde index
    CREATE INDEX IX_RoleModuleAccess_RoleId ON RoleModuleAccess(RoleId);
END
GO
