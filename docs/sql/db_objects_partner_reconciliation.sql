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

    -- En son onaylanmış mutabakatın kesim tarihi
    DECLARE @lockedThru DATETIME2;
    SELECT @lockedThru = MAX(StatementDate)
    FROM PartnerReconciliationLog
    WHERE CompanyId = @CompanyId AND PartnerId = @PartnerId
      AND Status IN ('CONFIRMED','EXPIRED_CONFIRMED');

    IF @lockedThru IS NULL
        RETURN;  -- Onaylı mutabakat yok: serbest

    -- İş kuralı: mutabakat kesim tarihinden öncesine geriye kayıt girilemez
    IF @MovementDate <= @lockedThru
        THROW 51620, N'Cari mutabakatı yapılmış döneme kayıt girilemez; sonraki açık tarihe işleyin.', 1;
END
GO
