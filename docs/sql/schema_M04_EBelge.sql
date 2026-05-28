-- =============================================================================
-- M04 — e-Belge altyapısı (e-Fatura / e-Arşiv / e-İrsaliye / e-Müstahsil)
-- Operax çekirdek omurgasında. GİB'e gönderim entegratör (Logo eLogo / Foriba /
-- Mikro / Nilvera / SignArchive vb.) üzerinden yapılır.
--
-- Defter (Yevmiye/Kebir) ve Beyanname (KDV/Stopaj/Muhtasar/BA-BS) Operax dışı.
-- =============================================================================
SET NOCOUNT ON;
GO

-- ─── EBelgeProvider: GİB entegratörü tanımı ────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EBelgeProvider')
BEGIN
    CREATE TABLE EBelgeProvider (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId     UNIQUEIDENTIFIER NOT NULL,
        Code          NVARCHAR(50)  NOT NULL,        -- LOGO_ELOGO, FORIBA, MIKRO, NILVERA, SIGNARCHIVE, ISIS_BTRANS
        Name          NVARCHAR(200) NOT NULL,
        Environment   NVARCHAR(20)  DEFAULT 'TEST',   -- TEST, PROD
        ApiBaseUrl    NVARCHAR(500) NULL,
        Username      NVARCHAR(200) NULL,
        ApiSecretEnc  NVARCHAR(MAX) NULL,             -- şifreli (Data Protection API)
        VknTckn       NVARCHAR(20)  NULL,
        IsActive      BIT DEFAULT 1,
        IsDeleted     BIT DEFAULT 0,
        CreatedAt     DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy     UNIQUEIDENTIFIER,
        UpdatedAt     DATETIME2 NULL,
        UpdatedBy     UNIQUEIDENTIFIER,
        CONSTRAINT FK_EProvider_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    CREATE UNIQUE INDEX UX_EProvider_Code ON EBelgeProvider(CompanyId, Code) WHERE IsDeleted = 0;
    PRINT 'EBelgeProvider tablosu olusturuldu.';
END
GO

-- ─── InvoiceEnvelope: gönderilen UBL 2.1 paketi ────────────────────
-- Bir SalesInvoice için bir veya daha çok gönderim denemesi olabilir
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InvoiceEnvelope')
BEGIN
    CREATE TABLE InvoiceEnvelope (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId     UNIQUEIDENTIFIER NOT NULL,
        InvoiceId     UNIQUEIDENTIFIER NOT NULL,        -- SalesInvoice (veya gelecekte ExpenseInvoice de olabilir)
        DocumentType  NVARCHAR(30) NOT NULL,            -- E_FATURA, E_ARSIV, E_IRSALIYE, E_MUSTAHSIL
        ProviderId    UNIQUEIDENTIFIER NOT NULL,
        Uuid          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),  -- GİB UUID
        EttN          NVARCHAR(50)  NULL,               -- e-Fatura için ETTN
        EnvelopeNo    NVARCHAR(50)  NULL,               -- zarf numarası
        DirectionType NVARCHAR(20)  NOT NULL,           -- OUTBOUND (giden), INBOUND (gelen)
        Recipient     NVARCHAR(200) NULL,               -- alıcı unvan
        RecipientVkn  NVARCHAR(20)  NULL,
        UblXml        NVARCHAR(MAX) NULL,               -- UBL 2.1 XML payload
        UblPdf        VARBINARY(MAX) NULL,              -- görüntü PDF
        Status        NVARCHAR(30)  DEFAULT 'DRAFT',
            -- DRAFT, READY, SENT, ACCEPTED, REJECTED, CANCELLED, RECEIVED, FAILED, RETURNING
        ResponseCode  NVARCHAR(50)  NULL,               -- GİB döndüğü kod
        ResponseText  NVARCHAR(MAX) NULL,
        SentAt        DATETIME2 NULL,
        AcceptedAt    DATETIME2 NULL,
        RejectedAt    DATETIME2 NULL,
        RetryCount    INT DEFAULT 0,
        LastTryAt     DATETIME2 NULL,
        NextRetryAt   DATETIME2 NULL,
        IsDeleted     BIT DEFAULT 0,
        CreatedAt     DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy     UNIQUEIDENTIFIER,
        UpdatedAt     DATETIME2 NULL,
        UpdatedBy     UNIQUEIDENTIFIER,
        CONSTRAINT FK_Env_Company  FOREIGN KEY (CompanyId)  REFERENCES Company(Id),
        CONSTRAINT FK_Env_Provider FOREIGN KEY (ProviderId) REFERENCES EBelgeProvider(Id)
    );
    CREATE INDEX IX_Env_Status ON InvoiceEnvelope(CompanyId, Status, CreatedAt) WHERE IsDeleted = 0;
    CREATE INDEX IX_Env_Invoice ON InvoiceEnvelope(InvoiceId);
    CREATE INDEX IX_Env_Direction ON InvoiceEnvelope(CompanyId, DirectionType, Status);
    PRINT 'InvoiceEnvelope tablosu olusturuldu.';
END
GO

-- ─── InvoiceSubmissionLog: her gönderim/sorgu adımı için log ───────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InvoiceSubmissionLog')
BEGIN
    CREATE TABLE InvoiceSubmissionLog (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        EnvelopeId    UNIQUEIDENTIFIER NOT NULL,
        Step          NVARCHAR(50) NOT NULL,           -- BUILD_XML, SEND, POLL_STATUS, ACK, ERROR
        HttpStatus    INT NULL,
        RequestBody   NVARCHAR(MAX) NULL,
        ResponseBody  NVARCHAR(MAX) NULL,
        ErrorMessage  NVARCHAR(MAX) NULL,
        DurationMs    INT NULL,
        CreatedAt     DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_SubLog_Env FOREIGN KEY (EnvelopeId) REFERENCES InvoiceEnvelope(Id)
    );
    CREATE INDEX IX_SubLog_Env ON InvoiceSubmissionLog(EnvelopeId, CreatedAt DESC);
    PRINT 'InvoiceSubmissionLog tablosu olusturuldu.';
END
GO

-- ─── SalesInvoice e-Belge alanları ─────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'EBelgeType' AND Object_ID = OBJECT_ID('SalesInvoice'))
BEGIN
    ALTER TABLE SalesInvoice ADD EBelgeType NVARCHAR(30) NULL;
    -- NULL = sadece dahili; E_FATURA, E_ARSIV, E_IRSALIYE
    PRINT 'SalesInvoice.EBelgeType kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'EBelgeStatus' AND Object_ID = OBJECT_ID('SalesInvoice'))
BEGIN
    ALTER TABLE SalesInvoice ADD EBelgeStatus NVARCHAR(30) NULL;
    -- DRAFT, READY, SENT, ACCEPTED, REJECTED, CANCELLED
    PRINT 'SalesInvoice.EBelgeStatus kolonu eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'EBelgeUuid' AND Object_ID = OBJECT_ID('SalesInvoice'))
BEGIN
    ALTER TABLE SalesInvoice ADD EBelgeUuid UNIQUEIDENTIFIER NULL;
    PRINT 'SalesInvoice.EBelgeUuid kolonu eklendi.';
END
GO

-- ─── EBelgeMukellef: alıcı mükellef bilgi cache'i (cari e-fatura mukellefi mi?)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EBelgeMukellef')
BEGIN
    CREATE TABLE EBelgeMukellef (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        VknTckn       NVARCHAR(20) NOT NULL,
        Title         NVARCHAR(300) NULL,
        IsEFatura     BIT DEFAULT 0,
        IsEIrsaliye   BIT DEFAULT 0,
        IsEArsiv      BIT DEFAULT 0,
        Alias         NVARCHAR(200) NULL,             -- e-fatura etiketi
        LastCheckedAt DATETIME2 NULL,
        CreatedAt     DATETIME2 DEFAULT GETUTCDATE()
    );
    CREATE UNIQUE INDEX UX_Mukellef_Vkn ON EBelgeMukellef(VknTckn);
    PRINT 'EBelgeMukellef tablosu olusturuldu.';
END
GO

-- ─── EBelgeQueue: e-fatura background job kuyruğu (Hangfire alternatif) ──
-- Genelde Hangfire kullanılır; bu tablo Hangfire dışına çıkılırsa fallback olur
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EBelgeQueue')
BEGIN
    CREATE TABLE EBelgeQueue (
        Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        EnvelopeId   UNIQUEIDENTIFIER NOT NULL,
        Operation    NVARCHAR(30) NOT NULL,           -- SEND, POLL, CANCEL
        Status       NVARCHAR(20) DEFAULT 'PENDING',  -- PENDING, PROCESSING, DONE, FAILED
        ScheduledAt  DATETIME2 DEFAULT GETUTCDATE(),
        StartedAt    DATETIME2 NULL,
        FinishedAt   DATETIME2 NULL,
        ErrorMessage NVARCHAR(MAX) NULL,
        RetryCount   INT DEFAULT 0,
        CONSTRAINT FK_EQueue_Env FOREIGN KEY (EnvelopeId) REFERENCES InvoiceEnvelope(Id)
    );
    CREATE INDEX IX_EQueue_Status ON EBelgeQueue(Status, ScheduledAt);
    PRINT 'EBelgeQueue tablosu olusturuldu.';
END
GO
