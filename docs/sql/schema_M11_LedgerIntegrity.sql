-- =============================================================================
-- schema_M11_LedgerIntegrity.sql — Plan 14: Ledger Immutability + Dönem Kontrolü
-- =============================================================================
-- İçerik:
--   1. AccountingPeriod        — firma+dönem bazlı OPEN/CLOSED/LOCKED statü (ZAMAN bazlı kilit)
--   2. PeriodOverrideLog       — kontrollü istisna iz tablosu (SİLİNMEZ, ADR-01 BIGINT clustered)
--   3. sp_GuardPeriodOpen      — her ledger hareket/onay SP'sinin ilk satırı (tek geçiş noktası)
--   4. sp_GuardStockFrozen     — K5 sayım freeze kancası (bugün NO-OP; gerçek gövde M08/S7)
-- NOT: Mekanizma kurulur, SÜREÇ/UI kurulmaz (statü değişimi bugün admin elle).
-- =============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- -----------------------------------------------------------------------------
-- 1. AccountingPeriod — firma başına dönem statüsü (5 firma ayrı kapanır)
--    ZAMAN bazlıdır; sayım freeze (SATIR bazlı, M08) ile KARIŞTIRILMAZ.
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccountingPeriod')
BEGIN
    CREATE TABLE dbo.AccountingPeriod (
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        PeriodYear  SMALLINT         NOT NULL,
        PeriodMonth TINYINT          NOT NULL,          -- 1-12
        -- OPEN: serbest · CLOSED: yetki+gerekçeyle aşılır · LOCKED: mutlak (e-Defter berat), istisna yok
        Status      NVARCHAR(10)     NOT NULL DEFAULT 'OPEN',
        ClosedAt    DATETIME2        NULL,
        ClosedBy    NVARCHAR(450)    NULL,
        LockedAt    DATETIME2        NULL,
        LockedBy    NVARCHAR(450)    NULL,
        CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT PK_AccountingPeriod PRIMARY KEY (CompanyId, PeriodYear, PeriodMonth),
        CONSTRAINT CK_AccountingPeriod_Status CHECK (Status IN ('OPEN','CLOSED','LOCKED')),
        CONSTRAINT CK_AccountingPeriod_Month CHECK (PeriodMonth BETWEEN 1 AND 12),
        CONSTRAINT FK_AccountingPeriod_Company FOREIGN KEY (CompanyId) REFERENCES dbo.Company(Id)
    );
    PRINT 'AccountingPeriod tablosu olusturuldu.';
END
GO

-- -----------------------------------------------------------------------------
-- 2. PeriodOverrideLog — kontrollü istisna izi (SİLİNMEZ append-only)
--    ADR-01: Seq BIGINT IDENTITY clustered PK + Id GUID nonclustered unique.
--    İŞLEM ANI (CreatedAt) ile HAREKET TARİHİ (MovementDate) AYRI tutulur (geç-belge denetimi).
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PeriodOverrideLog')
BEGIN
    CREATE TABLE dbo.PeriodOverrideLog (
        Seq             BIGINT           IDENTITY(1,1) NOT NULL,
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        CompanyId       UNIQUEIDENTIFIER NOT NULL,
        -- Hangi kilit ailesi aşıldı (üçü tek log'da iz; ama her kilit AYRI guard/tablo)
        LockType        NVARCHAR(20)     NOT NULL,   -- PERIOD_CLOSED / PARTNER_RECONCILED / STOCK_FROZEN
        -- Kaynak belge
        SourceDocType   NVARCHAR(30)     NULL,
        SourceDocId     UNIQUEIDENTIFIER NULL,
        SourceDocNo     NVARCHAR(100)    NULL,
        TargetTable     NVARCHAR(128)    NULL,
        -- Hareketin AİT OLDUĞU tarih (giriş anından ayrı)
        MovementDate    DATETIME2        NOT NULL,
        -- Kim aştı + (opsiyonel, çift-onay) kim onayladı — GÖREVLER AYRILIĞI: ikisi aynı olamaz
        OverriddenBy    NVARCHAR(450)    NOT NULL,
        ApprovedBy      NVARCHAR(450)    NULL,
        -- Neden (ZORUNLU): kategori raporu denetlenebilir yapar; serbest metin boş geçilemez
        ReasonCategory  NVARCHAR(20)     NOT NULL,   -- LATE_DOCUMENT / CORRECTION / SYSTEM_ERROR / OTHER
        ReasonText      NVARCHAR(1000)   NOT NULL,
        -- İşlem anı (giriş tarihi)
        CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT PK_PeriodOverrideLog PRIMARY KEY CLUSTERED (Seq),
        CONSTRAINT UX_PeriodOverrideLog_Id UNIQUE NONCLUSTERED (Id),
        CONSTRAINT CK_PeriodOverrideLog_Lock CHECK (LockType IN ('PERIOD_CLOSED','PARTNER_RECONCILED','STOCK_FROZEN')),
        CONSTRAINT CK_PeriodOverrideLog_Reason CHECK (ReasonCategory IN ('LATE_DOCUMENT','CORRECTION','SYSTEM_ERROR','OTHER')),
        -- Gerekçe metni boş/çok kısa geçilemez (denetlenebilirlik)
        CONSTRAINT CK_PeriodOverrideLog_ReasonText CHECK (LEN(LTRIM(RTRIM(ReasonText))) >= 5),
        -- Görevler ayrılığı: aşan kişi kendi override'ını kendi onaylayamaz
        CONSTRAINT CK_PeriodOverrideLog_SelfApprove CHECK (ApprovedBy IS NULL OR ApprovedBy <> OverriddenBy),
        CONSTRAINT FK_PeriodOverrideLog_Company FOREIGN KEY (CompanyId) REFERENCES dbo.Company(Id)
    );
    -- Dönem İstisna Raporu (sonradan tek view ile): firma + tarih + kullanıcı + kilit tipi filtresi
    CREATE NONCLUSTERED INDEX IX_PeriodOverrideLog_Report
        ON dbo.PeriodOverrideLog (CompanyId, MovementDate, LockType)
        INCLUDE (OverriddenBy, ReasonCategory);
    PRINT 'PeriodOverrideLog tablosu olusturuldu.';
END
GO

-- -----------------------------------------------------------------------------
-- 3. sp_GuardPeriodOpen — ZAMAN kilidi tek geçiş noktası
--    Her ledger hareket/onay SP'sinin İLK satırında çağrılır.
--    OPEN/dönem yok → serbest, iz yok.
--    CLOSED → yetkili (UserCompany.Role IN Administrator/Finance) + zorunlu gerekçe → atomik log; aksi THROW.
--    LOCKED → KOŞULSUZ THROW (istisna yok; tek çözüm sonraki açık döneme düzeltme kaydı).
-- THROW kod aralığı: 51200-51299 (plan: 50000-59999).
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GuardPeriodOpen
    @CompanyId      UNIQUEIDENTIFIER,
    @MovementDate   DATETIME2,
    @UserId         NVARCHAR(450),
    @ReasonCategory NVARCHAR(20)     = NULL,
    @ReasonText     NVARCHAR(1000)   = NULL,
    @LockType       NVARCHAR(20)     = 'PERIOD_CLOSED',
    @SourceDocType  NVARCHAR(30)     = NULL,
    @SourceDocId    UNIQUEIDENTIFIER = NULL,
    @SourceDocNo    NVARCHAR(100)    = NULL,
    @TargetTable    NVARCHAR(128)    = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @y SMALLINT = YEAR(@MovementDate);
    DECLARE @m TINYINT  = MONTH(@MovementDate);
    DECLARE @status NVARCHAR(10);

    -- İş kuralı: ilgili firma+dönemin statüsünü çek; kayıt yoksa dönem henüz kapatılmamış = OPEN
    SELECT @status = Status
    FROM dbo.AccountingPeriod
    WHERE CompanyId = @CompanyId AND PeriodYear = @y AND PeriodMonth = @m;

    IF @status IS NULL OR @status = 'OPEN'
        RETURN;  -- Dönem açık: serbest geçiş, iz tutulmaz

    -- İş kuralı: LOCKED (e-Defter berat) mutlaktır — hiçbir gerekçeyle aşılamaz
    IF @status = 'LOCKED'
        THROW 51201, N'Dönem kesin kilitli (LOCKED). Bu tarihe kayıt girilemez; düzeltmeyi sonraki açık döneme işleyin.', 1;

    -- Buradan sonrası: @status = 'CLOSED' → kontrollü istisna
    -- İş kuralı: gerekçe kategorisi + metni zorunlu (boş geçilemez)
    IF @ReasonCategory IS NULL OR @ReasonText IS NULL OR LEN(LTRIM(RTRIM(@ReasonText))) < 5
        THROW 51202, N'Dönem kapalı. Kayıt için yetkili gerekçe (kategori + açıklama) zorunludur.', 1;

    -- İş kuralı: yalnızca dar yetki (aktif firmada Administrator veya Finance rolü) kapalı dönemi aşabilir
    IF NOT EXISTS (
        SELECT 1 FROM dbo.UserCompany
        WHERE UserId = @UserId AND CompanyId = @CompanyId AND IsActive = 1
          AND Role IN ('Administrator','Finance'))
        THROW 51203, N'Dönem kapalı. Bu işlemi yapma yetkiniz yok (yalnızca muhasebe sorumlusu/yönetici).', 1;

    -- İstisna onaylandı → izi ATOMİK yaz (çağıran transaction'ın içinde çalışır)
    INSERT INTO dbo.PeriodOverrideLog
        (CompanyId, LockType, SourceDocType, SourceDocId, SourceDocNo, TargetTable,
         MovementDate, OverriddenBy, ReasonCategory, ReasonText)
    VALUES
        (@CompanyId, @LockType, @SourceDocType, @SourceDocId, @SourceDocNo, @TargetTable,
         @MovementDate, @UserId, @ReasonCategory, @ReasonText);
END
GO

-- -----------------------------------------------------------------------------
-- 4. sp_GuardStockFrozen — K5 sayım freeze kancası (BUGÜN NO-OP)
--    Gerçek gövde M08/S7'de (sayım freeze SATIR bazlı; bu plan ZAMAN bazlı — ayrı).
--    Guard zinciri buradan geçecek şekilde bugünden imza sabitlenir.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GuardStockFrozen
    @CompanyId   UNIQUEIDENTIFIER,
    @WarehouseId UNIQUEIDENTIFIER = NULL,
    @BinId       UNIQUEIDENTIFIER = NULL,
    @ItemId      UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    -- NO-OP: sayım freeze mekanizması M08/S7'de gelir (docs/MODULE_SPECS/M08_CycleCount_Freeze.md).
    -- İmza bugünden sabit ki onay SP'leri guard zincirine bugün bağlanabilsin.
    RETURN;
END
GO
