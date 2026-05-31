-- =============================================================================
-- db_objects_partner_reconciliation.sql — Plan 19: Cari Mutabakat SP'leri
-- =============================================================================
-- sp_CreateReconciliationStatement — bakiye snapshot + SENT mutabakat kaydı
-- sp_RespondReconciliation         — CONFIRMED / DISPUTED işle
-- sp_GuardPartnerReconciled        — mutabakat kilidi (plan 14 3. kilit ailesi)
-- THROW aralığı: 51600-51699.
-- =============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- -----------------------------------------------------------------------------
-- sp_CreateReconciliationStatement — mutabakat turu başlat
-- NE YAPAR: StatementDate itibarıyla net bakiye snapshot alır, SENT kaydı oluşturur,
--   1 ay sessiz onay penceresi (DeadlineAt) hesaplar.
-- PARAMETRELERİ: @CompanyId, @PartnerId, @StatementDate, @SentChannel, @UserId, @NewId OUTPUT
-- SIDE EFFECTS: PartnerReconciliationLog (INSERT, Status=SENT)
-- THROW: 51600-51609
-- BAĞIMLILIK: fn_PartnerBalanceAsOf, sp_GuardPeriodOpen
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_CreateReconciliationStatement
    @CompanyId     UNIQUEIDENTIFIER,
    @PartnerId     UNIQUEIDENTIFIER,
    @StatementDate DATETIME2,
    @SentChannel   NVARCHAR(20),
    @UserId        NVARCHAR(450),
    @NewId         UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi: mutabakat tarihinin dönemi açık/yetkili olmalı
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @StatementDate, @UserId;

        IF NOT EXISTS (SELECT 1 FROM Partner WHERE Id = @PartnerId AND CompanyId = @CompanyId)
            THROW 51600, N'Cari bulunamadı.', 1;
        IF @SentChannel NOT IN ('KEP','NOTER','EMAIL','POST')
            THROW 51601, N'Geçersiz gönderim kanalı.', 1;

        -- Açık (yanıt bekleyen) SENT mutabakat varsa yenisini engelle
        IF EXISTS (
            SELECT 1 FROM PartnerReconciliationLog
            WHERE CompanyId = @CompanyId AND PartnerId = @PartnerId AND Status = 'SENT'
        )
            THROW 51602, N'Bu cari için yanıt bekleyen mutabakat var; önce sonuçlandırın.', 1;

        -- Bakiye snapshot: StatementDate itibarıyla (o ana kadarki net bakiye)
        DECLARE @bal DECIMAL(18,2) =
            dbo.fn_PartnerBalanceAsOf(@CompanyId, @PartnerId, DATEADD(DAY, 1, @StatementDate));

        DECLARE @now DATETIME2 = GETUTCDATE();
        SET @NewId = NEWID();

        INSERT INTO dbo.PartnerReconciliationLog
            (Id, CompanyId, PartnerId, StatementDate, BalanceSnapshot, Status,
             SentAt, SentChannel, DeadlineAt, CreatedBy)
        VALUES
            (@NewId, @CompanyId, @PartnerId, @StatementDate, @bal, 'SENT',
             @now, @SentChannel, DATEADD(MONTH, 1, @now), @UserId);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- -----------------------------------------------------------------------------
-- sp_RespondReconciliation — mutabakata yanıt (onay / itiraz)
-- NE YAPAR: SENT mutabakatı CONFIRMED veya DISPUTED yapar; yanıt notu + tarih.
-- PARAMETRELERİ: @ReconciliationId, @CompanyId, @Confirmed BIT, @ResponseNote, @UserId
-- SIDE EFFECTS: PartnerReconciliationLog (UPDATE: Status, ResponseAt, ResponseNote)
-- THROW: 51610-51619
-- NOT: Append-only ilke — yalnızca yanıt alanları doldurulur, snapshot/tarih değişmez.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_RespondReconciliation
    @ReconciliationId UNIQUEIDENTIFIER,
    @CompanyId        UNIQUEIDENTIFIER,
    @Confirmed        BIT,
    @ResponseNote     NVARCHAR(1000) = NULL,
    @UserId           NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20);
        SELECT @Status = Status
        FROM PartnerReconciliationLog WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @ReconciliationId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51610, N'Mutabakat kaydı bulunamadı.', 1;
        IF @Status <> 'SENT'
            THROW 51611, N'Yalnızca yanıt bekleyen (SENT) mutabakat sonuçlandırılabilir.', 1;
        -- İtirazda gerekçe zorunlu (denetlenebilirlik)
        IF @Confirmed = 0 AND (@ResponseNote IS NULL OR LEN(LTRIM(RTRIM(@ResponseNote))) < 3)
            THROW 51612, N'İtiraz için gerekçe zorunludur.', 1;

        UPDATE PartnerReconciliationLog
        SET Status       = CASE WHEN @Confirmed = 1 THEN 'CONFIRMED' ELSE 'DISPUTED' END,
            ResponseAt   = GETUTCDATE(),
            ResponseNote = @ResponseNote
        WHERE Id = @ReconciliationId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- -----------------------------------------------------------------------------
-- sp_GuardPartnerReconciled — MUTABAKAT KİLİDİ (plan 14 3. kilit ailesi — K9)
-- NE YAPAR: bir partner+tarih için onaylı (CONFIRMED/EXPIRED_CONFIRMED) mutabakat varsa
--   ve hareket tarihi o mutabakatın StatementDate'inden ESKİyse → THROW.
--   AM yazan onay SP'lerinde sp_GuardPeriodOpen yanında çağrılır.
-- PARAMETRELERİ: @CompanyId, @PartnerId, @MovementDate
-- THROW: 51620 (yetkili override → PeriodOverrideLog LockType='PARTNER_RECONCILED' — ileride)
-- BAĞIMLILIK: PartnerReconciliationLog
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GuardPartnerReconciled
    @CompanyId    UNIQUEIDENTIFIER,
    @PartnerId    UNIQUEIDENTIFIER,
    @MovementDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    -- En son AÇIK ONAYLI (imzalı/KEP CONFIRMED) mutabakatın kesim tarihi.
    -- SERT kilit yalnızca CONFIRMED'e bağlı (deep-research/TTK md.94): sessiz onay
    -- (EXPIRED_CONFIRMED) yazılı cari hesap sözleşmesi olmadan hukuken zayıf → sert kilit yapmaz.
    -- DISPUTED/SENT → bakiye kesinleşmemiş, serbest.
    DECLARE @lockedThru DATETIME2;
    SELECT @lockedThru = MAX(StatementDate)
    FROM PartnerReconciliationLog
    WHERE CompanyId = @CompanyId AND PartnerId = @PartnerId
      AND Status = 'CONFIRMED';

    IF @lockedThru IS NULL
        RETURN;  -- Onaylı mutabakat yok: serbest

    -- İş kuralı: mutabakat kesim günü DAHİL kapalı (snapshot SUM(<=StatementDate) ile tutarlı).
    -- SARGable: fonksiyon WHERE'de değil; kesim gününün ertesini sınır al.
    IF @MovementDate < DATEADD(DAY, 1, @lockedThru)
        THROW 51620, N'Cari mutabakatı yapılmış döneme kayıt girilemez; sonraki açık tarihe işleyin.', 1;
END
GO

-- -----------------------------------------------------------------------------
-- tvf_ReconciliationPrep — MUTABAKAT HAZIRLIK: bir tarihe kadar cari özet
-- NE YAPAR: muhasebe @AsOf tarihi girince o cariyle mutabakat için hazırlık verisi:
--   net bakiye (snapshot) + dönem içi hareket sayısı + açık kalem sayısı/tutarı.
--   Mutabakat = imzalı uzlaşma; bu view iki tarafın karşılaştıracağı veriyi üretir.
-- -----------------------------------------------------------------------------
CREATE OR ALTER FUNCTION dbo.tvf_ReconciliationPrep
(
    @CompanyId UNIQUEIDENTIFIER,
    @PartnerId UNIQUEIDENTIFIER,
    @AsOf      DATETIME2
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        @PartnerId AS PartnerId,
        -- AsOf günü DAHİL net bakiye (snapshot ile aynı sınır: < ertesi gün)
        dbo.fn_PartnerBalanceAsOf(@CompanyId, @PartnerId, DATEADD(DAY, 1, @AsOf)) AS NetBalance,
        -- Dönem içi (AsOf'a kadar) toplam hareket sayısı
        (SELECT COUNT(*) FROM AccountMovement am
         WHERE am.CompanyId = @CompanyId AND am.PartnerId = @PartnerId
           AND am.MovementDate < DATEADD(DAY, 1, @AsOf)) AS MovementCount,
        -- Açık kalem (kapatılmamış) sayısı + tutarı — anlaşmazlık adayları
        ISNULL((SELECT COUNT(*) FROM dbo.tvf_OpenItems(@CompanyId, @PartnerId)
                WHERE MovementDate < DATEADD(DAY, 1, @AsOf)), 0) AS OpenItemCount,
        ISNULL((SELECT SUM(OpenAmount) FROM dbo.tvf_OpenItems(@CompanyId, @PartnerId)
                WHERE MovementDate < DATEADD(DAY, 1, @AsOf)), 0) AS OpenItemTotal
);
GO

-- -----------------------------------------------------------------------------
-- sp_ExpireReconciliations — sessiz onay job'ı (TTK md.94, 1 ay deadline)
-- NE YAPAR: DeadlineAt geçmiş + SENT + İSPATLI kanal (KEP/NOTER) mutabakatları
--   EXPIRED_CONFIRMED yapar. deep-research: EMAIL/POST ispatı zayıf → otomatik onay YAPMAZ
--   (manuel CONFIRMED bekler). Hangfire NotificationDispatcher günlük çağırır.
-- SIDE EFFECTS: PartnerReconciliationLog (UPDATE Status=EXPIRED_CONFIRMED, ResponseAt)
-- THROW: yok (toplu job, hata yutmaz — XACT_ABORT + CATCH THROW)
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ExpireReconciliations
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Sessiz onay: süresi dolmuş, ispatlanabilir kanaldan gönderilmiş, yanıtsız
        UPDATE PartnerReconciliationLog
        SET Status     = 'EXPIRED_CONFIRMED',
            ResponseAt = GETUTCDATE(),
            ResponseNote = N'Süre doldu — 1 ay içinde itiraz edilmedi (TTK md.94 zımni kabul).'
        WHERE Status = 'SENT'
          AND DeadlineAt IS NOT NULL
          AND DeadlineAt < GETUTCDATE()
          AND SentChannel IN ('KEP','NOTER');  -- ispatlı kanal şartı (deep-research)

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
