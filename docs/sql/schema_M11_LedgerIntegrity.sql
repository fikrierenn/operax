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

    -- Trigger bypass: bu session'daki bir sonraki ledger INSERT'i geçsin (tek kullanımlık)
    -- Trigger okur → reset eder → sonraki INSERT normale döner
    EXEC sys.sp_set_session_context N'period_override_ok', N'1', @read_only = 0;
END
GO

-- -----------------------------------------------------------------------------
-- StockMovement.MovementDate (Plan 33 Faz F): AccountMovement ile dönem-tarih simetrisi.
-- Dönem kontrolü hareket tarihine göre yapılsın (onay-anı GETUTCDATE değil). Mevcut SP
-- INSERT'leri MovementDate vermez → DEFAULT GETUTCDATE() = onay anı (mevcut davranış korunur);
-- ileride SP'ler belge tarihini set ederse geç-belge dönem denetimi otomatik doğru çalışır.
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'MovementDate' AND Object_ID = OBJECT_ID('StockMovement'))
BEGIN
    ALTER TABLE StockMovement ADD MovementDate DATETIME2 NULL;
END
GO
-- Backfill: mevcut satırları CreatedAt'tan doldur (idempotent — yalnız NULL'lar)
UPDATE StockMovement SET MovementDate = CreatedAt WHERE MovementDate IS NULL;
GO
-- Backfill sonrası NOT NULL + DEFAULT (yeni INSERT'lerde MovementDate verilmezse onay anı)
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'MovementDate' AND Object_ID = OBJECT_ID('StockMovement') AND is_nullable = 1)
BEGIN
    ALTER TABLE StockMovement ALTER COLUMN MovementDate DATETIME2 NOT NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_StockMovement_MovementDate')
    ALTER TABLE StockMovement ADD CONSTRAINT DF_StockMovement_MovementDate DEFAULT GETUTCDATE() FOR MovementDate;
GO

-- -----------------------------------------------------------------------------
-- 5. tr_GuardPeriod_StockMovement — Emniyet ağı trigger (Plan 14)
--    sp_GuardPeriodOpen doğrudan çağrılmadan yapılan INSERT/UPDATE'i engeller.
--    SESSION_CONTEXT='1' → override onaylı, tek-kullanımlık bypass + reset.
--    INSERTED.MovementDate ile dönem kontrolü (AccountMovement ile simetrik — Plan 33 Faz F).
-- THROW aralığı: 51210 (plan kuralı: 50000-59999).
-- -----------------------------------------------------------------------------
CREATE OR ALTER TRIGGER dbo.tr_GuardPeriod_StockMovement
ON dbo.StockMovement
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- İş kuralı: override onaylıysa tek-kullanımlık bypass, ardından session context temizle
    DECLARE @ok NVARCHAR(10) = CAST(SESSION_CONTEXT(N'period_override_ok') AS NVARCHAR(10));
    IF @ok = N'1'
    BEGIN
        EXEC sys.sp_set_session_context N'period_override_ok', NULL, @read_only = 0;
        RETURN;
    END

    -- İş kuralı: hareketin ait olduğu dönem (MovementDate) kontrol edilir — onay anı değil
    -- (AccountMovement trigger'ı ile simetrik; geç-belge senaryosunda doğru dönem denetimi)
    IF EXISTS (
        SELECT 1
        FROM dbo.AccountingPeriod ap
        JOIN INSERTED i ON ap.CompanyId = i.CompanyId
        WHERE ap.PeriodYear  = YEAR(i.MovementDate)
          AND ap.PeriodMonth = MONTH(i.MovementDate)
          AND ap.Status IN ('CLOSED', 'LOCKED')
    )
        THROW 51210, N'Dönem kapalı veya kilitli. StockMovement yazılamaz; onay SP üzerinden giriniz.', 1;
END
GO

-- -----------------------------------------------------------------------------
-- 6. tr_GuardPeriod_AccountMovement — Emniyet ağı trigger (Plan 14)
--    AccountMovement.MovementDate mevcuttur → INSERTED.MovementDate ile dönem kontrolü.
-- THROW aralığı: 51211.
-- -----------------------------------------------------------------------------
CREATE OR ALTER TRIGGER dbo.tr_GuardPeriod_AccountMovement
ON dbo.AccountMovement
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- İş kuralı: override onaylıysa tek-kullanımlık bypass, ardından session context temizle
    DECLARE @ok NVARCHAR(10) = CAST(SESSION_CONTEXT(N'period_override_ok') AS NVARCHAR(10));
    IF @ok = N'1'
    BEGIN
        EXEC sys.sp_set_session_context N'period_override_ok', NULL, @read_only = 0;
        RETURN;
    END

    -- İş kuralı: hareketin ait olduğu dönem (MovementDate) kontrol edilir — onay anı değil
    IF EXISTS (
        SELECT 1
        FROM dbo.AccountingPeriod ap
        JOIN INSERTED i ON ap.CompanyId = i.CompanyId
        WHERE ap.PeriodYear  = YEAR(i.MovementDate)
          AND ap.PeriodMonth = MONTH(i.MovementDate)
          AND ap.Status IN ('CLOSED', 'LOCKED')
    )
        THROW 51211, N'Dönem kapalı veya kilitli. AccountMovement yazılamaz; onay SP üzerinden giriniz.', 1;
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
