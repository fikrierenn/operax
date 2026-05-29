-- =============================================================================
-- M11 Cari Hesap Defteri — AccountMovement (Plan 09)
-- StockMovement muadili: tek immutable cari hareket defteri.
-- Bakiye = SUM(Borc - Alacak). Silme yok → cancel için REVERSAL ters kayıt.
-- İşaret: NetBakiye = SUM(Borc) - SUM(Alacak); + = cari bize borçlu, - = biz borçluyuz.
--   Satış faturası → Borç · Tahsilat → Alacak · Alış faturası → Alacak · Ödeme → Borç
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
        Borc          DECIMAL(18,2)    NOT NULL DEFAULT 0,   -- cari borçlanır (alacağımız artar)
        Alacak        DECIMAL(18,2)    NOT NULL DEFAULT 0,   -- cari alacaklanır (borcumuz artar)
        Currency      NVARCHAR(3)      NOT NULL DEFAULT 'TRY',
        -- Kaynak belge: SALES_INVOICE / PURCHASE_INVOICE / PAYMENT / COLLECTION /
        --               CHEQUE_IN / CHEQUE_OUT / OPENING / VARIANCE / REVERSAL
        SourceDocType NVARCHAR(30)     NOT NULL,
        SourceDocId   UNIQUEIDENTIFIER NULL,
        SourceDocNo   NVARCHAR(50)     NULL,
        Description   NVARCHAR(500)    NULL,
        -- Zorunlu standart kolonlar
        IsDeleted     BIT              NOT NULL DEFAULT 0,
        CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy     NVARCHAR(450)    NULL,
        UpdatedAt     DATETIME2        NULL,
        UpdatedBy     NVARCHAR(450)    NULL,
        CONSTRAINT FK_AccountMovement_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id)
    );
    PRINT 'AccountMovement tablosu oluşturuldu.';
END
GO

-- Bakiye/ekstre sorguları için kapsayan index (CompanyId, PartnerId, tarih)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccountMovement_Partner_Date')
    CREATE INDEX IX_AccountMovement_Partner_Date
        ON AccountMovement (CompanyId, PartnerId, MovementDate)
        INCLUDE (Borc, Alacak) WHERE IsDeleted = 0;
GO

-- Idempotent backfill + çift-post koruması: bir kaynak belge tek hareket üretir.
-- (REVERSAL farklı SourceDocType olduğu için aynı SourceDocId'yi ikinci kez yazabilir.)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_AccountMovement_Source')
    CREATE UNIQUE INDEX UX_AccountMovement_Source
        ON AccountMovement (SourceDocType, SourceDocId)
        WHERE SourceDocId IS NOT NULL AND IsDeleted = 0;
GO
