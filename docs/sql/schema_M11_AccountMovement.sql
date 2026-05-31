-- =============================================================================
-- M11 Cari Hesap Defteri — AccountMovement (Plan 09)
-- StockMovement muadili: tek immutable cari hareket defteri.
-- Bakiye = SUM(Debit - Credit). Silme yok → cancel için REVERSAL ters kayıt.
-- İşaret: NetBakiye = SUM(Debit) - SUM(Credit); + = cari bize borçlu, - = biz borçluyuz.
--   Satış faturası → Debit · Tahsilat → Credit · Alış faturası → Credit · Ödeme → Debit
-- Idempotent (IF NOT EXISTS koruması).
-- =============================================================================
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccountMovement')
BEGIN
    CREATE TABLE AccountMovement
    (
        Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AccountMovement PRIMARY KEY DEFAULT NEWID(),
        CompanyId     UNIQUEIDENTIFIER NOT NULL,
        PartnerId     UNIQUEIDENTIFIER NOT NULL,
        MovementDate  DATETIME2        NOT NULL,
        Debit         DECIMAL(18,2)    NOT NULL DEFAULT 0,   -- cari borçlanır (alacağımız artar)
        Credit        DECIMAL(18,2)    NOT NULL DEFAULT 0,   -- cari alacaklanır (borcumuz azalır)
        Currency      NVARCHAR(3)      NOT NULL DEFAULT 'TRY',
        -- Kaynak belge: SALES_INVOICE / PURCHASE_INVOICE / PAYMENT / COLLECTION /
        --               CHEQUE_IN / CHEQUE_OUT / OPENING / VARIANCE / REVERSAL
        SourceDocType NVARCHAR(30)     NOT NULL,
        SourceDocId   UNIQUEIDENTIFIER NULL,
        SourceDocNo   NVARCHAR(50)     NULL,
        Description   NVARCHAR(500)    NULL,
        -- Append-only ledger: IsDeleted yok (silme yerine REVERSAL ters kayıt — Plan 14)
        CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy     NVARCHAR(450)    NULL,
        CONSTRAINT FK_AccountMovement_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id)
    );
    PRINT 'AccountMovement tablosu oluşturuldu.';
END
GO

-- =============================================================================
-- PLAN 16 MIGRATION: AccountMovement şema genişleme
-- DueDate, TaxAmount, ExchangeRate, AmountForeign — nullable, backward compat.
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns c JOIN sys.tables t ON c.object_id = t.object_id
               WHERE t.name = 'AccountMovement' AND c.name = 'DueDate')
    ALTER TABLE dbo.AccountMovement ADD DueDate DATETIME2 NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns c JOIN sys.tables t ON c.object_id = t.object_id
               WHERE t.name = 'AccountMovement' AND c.name = 'TaxAmount')
    ALTER TABLE dbo.AccountMovement ADD TaxAmount DECIMAL(18,2) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns c JOIN sys.tables t ON c.object_id = t.object_id
               WHERE t.name = 'AccountMovement' AND c.name = 'ExchangeRate')
    ALTER TABLE dbo.AccountMovement ADD ExchangeRate DECIMAL(18,6) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns c JOIN sys.tables t ON c.object_id = t.object_id
               WHERE t.name = 'AccountMovement' AND c.name = 'AmountForeign')
    ALTER TABLE dbo.AccountMovement ADD AmountForeign DECIMAL(18,2) NULL;
GO

-- =============================================================================
-- PLAN 14 MIGRATION: IsDeleted kaldır + Borc/Alacak → Debit/Credit rename
-- Idempotent: IF EXISTS / IF NOT EXISTS korumalarıyla güvenle tekrar çalıştırılabilir.
-- =============================================================================

-- Filtreli/eski index'leri DROP et
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_AccountMovement_Partner_Date'
      AND object_id = OBJECT_ID('dbo.AccountMovement')
)
    DROP INDEX IX_AccountMovement_Partner_Date ON dbo.AccountMovement;
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_AccountMovement_Source'
      AND object_id = OBJECT_ID('dbo.AccountMovement')
)
    DROP INDEX UX_AccountMovement_Source ON dbo.AccountMovement;
GO

-- IsDeleted kolonu kaldır: önce default constraint DROP et, sonra kolonu kaldır
DECLARE @dfName NVARCHAR(200);
SELECT @dfName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
JOIN sys.tables t ON c.object_id = t.object_id
WHERE t.name = 'AccountMovement' AND c.name = 'IsDeleted';

IF @dfName IS NOT NULL
    EXEC('ALTER TABLE dbo.AccountMovement DROP CONSTRAINT ' + @dfName);
GO

IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.tables t ON c.object_id = t.object_id
    WHERE t.name = 'AccountMovement' AND c.name = 'IsDeleted'
)
    ALTER TABLE dbo.AccountMovement DROP COLUMN IsDeleted;
GO

-- Borc → Debit, Alacak → Credit rename
IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.tables t ON c.object_id = t.object_id
    WHERE t.name = 'AccountMovement' AND c.name = 'Borc'
)
    EXEC sp_rename 'dbo.AccountMovement.Borc', 'Debit', 'COLUMN';
GO

IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.tables t ON c.object_id = t.object_id
    WHERE t.name = 'AccountMovement' AND c.name = 'Alacak'
)
    EXEC sp_rename 'dbo.AccountMovement.Alacak', 'Credit', 'COLUMN';
GO

-- Index'leri filtre olmadan yeniden oluştur
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_AccountMovement_Partner_Date'
      AND object_id = OBJECT_ID('dbo.AccountMovement')
)
    CREATE NONCLUSTERED INDEX IX_AccountMovement_Partner_Date
        ON dbo.AccountMovement (CompanyId, PartnerId, MovementDate)
        INCLUDE (Debit, Credit);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_AccountMovement_Source'
      AND object_id = OBJECT_ID('dbo.AccountMovement')
)
    CREATE UNIQUE NONCLUSTERED INDEX UX_AccountMovement_Source
        ON dbo.AccountMovement (SourceDocType, SourceDocId)
        WHERE SourceDocId IS NOT NULL;
GO
