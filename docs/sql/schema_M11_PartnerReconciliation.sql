-- =============================================================================
-- schema_M11_PartnerReconciliation.sql — Plan 19: Cari Mutabakat Turu (TTK md.94)
-- =============================================================================
-- Mutabakat turu = partner'a bakiye cetveli gönder, onay/itiraz al.
-- AYRI domain: plan 18 AccountReconciliation = iç defter (ödeme↔fatura kapama).
-- TTK md.94: yıl sonu kapatma + 1 ay sessiz onay. md.82: 10 yıl saklama (append-only).
-- =============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PartnerReconciliationLog')
BEGIN
    CREATE TABLE dbo.PartnerReconciliationLog (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        CompanyId       UNIQUEIDENTIFIER NOT NULL,
        PartnerId       UNIQUEIDENTIFIER NOT NULL,
        -- Bakiye cetveli kesim tarihi (mutabakat dönemi sonu)
        StatementDate   DATETIME2        NOT NULL,
        -- O tarihteki net bakiye snapshot (SUM(Debit-Credit) <= StatementDate)
        BalanceSnapshot DECIMAL(18,2)    NOT NULL,
        -- TTK md.94 statü makinesi
        Status          NVARCHAR(20)     NOT NULL DEFAULT 'SENT',
        -- Gönderim
        SentAt          DATETIME2        NULL,
        SentChannel     NVARCHAR(20)     NULL,   -- KEP / NOTER / EMAIL / POST
        -- 1 ay sessiz onay penceresi (TTK md.94)
        DeadlineAt      DATETIME2        NULL,
        -- Yanıt (onay/itiraz)
        ResponseAt      DATETIME2        NULL,
        ResponseNote    NVARCHAR(1000)   NULL,
        -- Append-only (TTK md.82): silme/UpdatedBy yok
        CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy       NVARCHAR(450)    NULL,
        CONSTRAINT PK_PartnerReconciliationLog PRIMARY KEY (Id),
        CONSTRAINT CK_PRL_Status CHECK (Status IN ('SENT','CONFIRMED','DISPUTED','EXPIRED_CONFIRMED')),
        CONSTRAINT CK_PRL_Channel CHECK (SentChannel IS NULL OR SentChannel IN ('KEP','NOTER','EMAIL','POST')),
        CONSTRAINT FK_PRL_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_PRL_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id)
    );
    -- "Bu partner için en son onaylı mutabakat tarihi" sorgusu (mutabakat kilidi + kart)
    CREATE NONCLUSTERED INDEX IX_PRL_Partner_Date
        ON dbo.PartnerReconciliationLog (CompanyId, PartnerId, StatementDate DESC)
        INCLUDE (Status, BalanceSnapshot);
    PRINT 'PartnerReconciliationLog tablosu olusturuldu.';
END
GO
