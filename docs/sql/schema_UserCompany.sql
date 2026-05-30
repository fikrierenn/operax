-- =============================================================================
-- schema_UserCompany.sql — Plan 13: UserCompany tablosu
-- =============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserCompany')
BEGIN
    CREATE TABLE dbo.UserCompany (
        UserId      NVARCHAR(450) NOT NULL,
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        Role        NVARCHAR(256) NOT NULL DEFAULT 'User',
        CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT PK_UserCompany PRIMARY KEY (UserId, CompanyId),
        CONSTRAINT FK_UserCompany_User FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserCompany_Company FOREIGN KEY (CompanyId) REFERENCES dbo.Company(Id) ON DELETE CASCADE
    );
    PRINT 'UserCompany tablosu olusturuldu.';
END
GO

-- Mevcut company claim'lerini UserCompany tablosuna tasima (Migration / Backfill)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserCompany')
BEGIN
    INSERT INTO dbo.UserCompany (UserId, CompanyId, Role)
    SELECT 
        c.UserId, 
        TRY_CAST(c.ClaimValue AS UNIQUEIDENTIFIER) AS CompanyId, 
        'Admin' AS Role
    FROM dbo.AspNetUserClaims c
    WHERE c.ClaimType = 'company'
      AND TRY_CAST(c.ClaimValue AS UNIQUEIDENTIFIER) IS NOT NULL
      AND NOT EXISTS (
          SELECT 1 FROM dbo.UserCompany uc 
          WHERE uc.UserId = c.UserId 
            AND uc.CompanyId = TRY_CAST(c.ClaimValue AS UNIQUEIDENTIFIER)
      );
END
GO
