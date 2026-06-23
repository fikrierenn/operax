-- =============================================================================
-- db_objects_reconciliation.sql — Plan 18: Açık-Kalem Kapama SP + TVF'leri
-- =============================================================================
-- sp_ReconcileMovements   — manuel/oto eşleştirme (Debit↔Credit+tutar) + kümülatif aşım guard
-- sp_UnreconcileMovements — eşleştirme geri al (IsReversal ters satır)
-- tvf_OpenItems           — açık AM hareketleri (Debit - SUM eşleşen)
-- tvf_OpenItemAging        — açık kalem yaşlandırma (0-30/31-60/61-90/>90)
-- THROW aralığı: 51500-51599.
-- =============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- -----------------------------------------------------------------------------
-- sp_ReconcileMovements — bir Debit AM hareketini bir Credit AM hareketine eşleştir
-- NE YAPAR: kısmi/tam kapama kaydı yazar; her iki tarafta kümülatif aşım kontrolü.
-- Açık kalem = hareket tutarı - SUM(eşleşen Amount); aşım THROW.
-- PARAMETRELERİ:
--   @CompanyId, @PartnerId, @DebitMovementId, @CreditMovementId, @Amount, @UserId
-- SIDE EFFECTS: AccountReconciliation (INSERT)
-- THROW: 51500-51509
-- BAĞIMLILIK: sp_GuardPeriodOpen
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ReconcileMovements
    @CompanyId        UNIQUEIDENTIFIER,
    @PartnerId        UNIQUEIDENTIFIER,
    @DebitMovementId  UNIQUEIDENTIFIER,
    @CreditMovementId UNIQUEIDENTIFIER,
    @Amount           DECIMAL(18,2),
    @UserId           NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi
        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        IF @Amount <= 0
            THROW 51500, N'Eşleştirme tutarı pozitif olmalıdır.', 1;

        -- İki hareketi al ve doğrula (aynı firma + partner)
        DECLARE @debitVal DECIMAL(18,2), @creditVal DECIMAL(18,2);

        SELECT @debitVal = Debit
        FROM AccountMovement WITH (UPDLOCK)
        WHERE Id = @DebitMovementId AND CompanyId = @CompanyId AND PartnerId = @PartnerId;

        SELECT @creditVal = Credit
        FROM AccountMovement WITH (UPDLOCK)
        WHERE Id = @CreditMovementId AND CompanyId = @CompanyId AND PartnerId = @PartnerId;

        IF @debitVal IS NULL OR @creditVal IS NULL
            THROW 51501, N'Eşleştirilecek hareket bulunamadı (firma/cari uyumsuz).', 1;
        IF @debitVal = 0
            THROW 51502, N'Borç hareketi seçilmeli (Debit > 0).', 1;
        IF @creditVal = 0
            THROW 51503, N'Alacak hareketi seçilmeli (Credit > 0).', 1;

        -- Kümülatif aşım: borç tarafı (Mikro iki-yön index bunun için)
        DECLARE @debitUsed DECIMAL(18,2) = ISNULL((
            SELECT SUM(Amount) FROM AccountReconciliation
            WHERE DebitMovementId = @DebitMovementId AND IsReversal = 0), 0);
        IF @debitUsed + @Amount > @debitVal + 0.001
            THROW 51504, N'Borç hareketinin açık tutarı aşılıyor.', 1;

        -- Kümülatif aşım: alacak tarafı
        DECLARE @creditUsed DECIMAL(18,2) = ISNULL((
            SELECT SUM(Amount) FROM AccountReconciliation
            WHERE CreditMovementId = @CreditMovementId AND IsReversal = 0), 0);
        IF @creditUsed + @Amount > @creditVal + 0.001
            THROW 51505, N'Alacak hareketinin açık tutarı aşılıyor.', 1;

        -- Eşleştirme kaydı
        INSERT INTO dbo.AccountReconciliation
            (Id, CompanyId, PartnerId, DebitMovementId, CreditMovementId,
             Amount, MovementDate, IsReversal, CreatedBy)
        VALUES
            (NEWID(), @CompanyId, @PartnerId, @DebitMovementId, @CreditMovementId,
             @Amount, @now, 0, @UserId);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- -----------------------------------------------------------------------------
-- sp_UnreconcileMovements — eşleştirmeyi geri al (append-only: IsReversal ters satır)
-- NE YAPAR: orijinal eşleştirme satırını ters çevirir; açık kalem yeniden açılır.
-- PARAMETRELERİ: @ReconciliationId, @CompanyId, @UserId
-- SIDE EFFECTS: AccountReconciliation (IsReversal=1 ters INSERT)
-- THROW: 51510-51519
-- BAĞIMLILIK: sp_GuardPeriodOpen
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_UnreconcileMovements
    @ReconciliationId UNIQUEIDENTIFIER,
    @CompanyId        UNIQUEIDENTIFIER,
    @UserId           NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        DECLARE @PartnerId UNIQUEIDENTIFIER, @DebitId UNIQUEIDENTIFIER,
                @CreditId UNIQUEIDENTIFIER, @Amount DECIMAL(18,2), @IsRev BIT;

        SELECT @PartnerId = PartnerId, @DebitId = DebitMovementId,
               @CreditId = CreditMovementId, @Amount = Amount, @IsRev = IsReversal
        FROM AccountReconciliation WITH (UPDLOCK)
        WHERE Id = @ReconciliationId AND CompanyId = @CompanyId;

        IF @PartnerId IS NULL
            THROW 51510, N'Eşleştirme kaydı bulunamadı.', 1;
        IF @IsRev = 1
            THROW 51511, N'Ters kayıt geri alınamaz.', 1;

        -- Zaten geri alınmış mı (çift unreconcile koruması)
        IF EXISTS (SELECT 1 FROM AccountReconciliation
                   WHERE ReversalOfId = @ReconciliationId AND CompanyId = @CompanyId)
            THROW 51512, N'Bu eşleştirme zaten geri alınmış.', 1;

        -- Append-only ters satır (negatif değil, IsReversal=1 + ReversalOfId)
        INSERT INTO dbo.AccountReconciliation
            (Id, CompanyId, PartnerId, DebitMovementId, CreditMovementId,
             Amount, MovementDate, IsReversal, ReversalOfId, CreatedBy)
        VALUES
            (NEWID(), @CompanyId, @PartnerId, @DebitId, @CreditId,
             @Amount, @now, 1, @ReconciliationId, @UserId);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- -----------------------------------------------------------------------------
-- tvf_OpenItems — açık (kapatılmamış) AM hareketleri
-- Açık tutar = hareket tutarı - SUM(aktif eşleşen Amount). Snapshot tutulmaz (Odoo residual).
-- IsReversal=0 eşleşmeler düşülür, IsReversal=1 (geri alınmış) sayılmaz → net açık.
-- Direction: RECEIVABLE (Debit açık = bize borçlu) / PAYABLE (Credit açık = biz borçluyuz)
-- -----------------------------------------------------------------------------
CREATE OR ALTER FUNCTION dbo.tvf_OpenItems
(
    @CompanyId UNIQUEIDENTIFIER,
    @PartnerId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    -- Borç hareketleri (fatura): açık = Debit - eşleşen
    SELECT
        am.Id              AS MovementId,
        am.PartnerId,
        'RECEIVABLE'       AS Direction,
        am.SourceDocType,
        am.SourceDocNo,
        am.MovementDate,
        am.DueDate,
        am.Debit           AS DocAmount,
        ISNULL(r.Matched, 0) AS MatchedAmount,
        am.Debit - ISNULL(r.Matched, 0) AS OpenAmount
    FROM AccountMovement am
    OUTER APPLY (
        SELECT SUM(Amount) AS Matched FROM AccountReconciliation
        WHERE DebitMovementId = am.Id AND IsReversal = 0
    ) r
    WHERE am.CompanyId = @CompanyId AND (@PartnerId IS NULL OR am.PartnerId = @PartnerId)
      AND am.Debit > 0
      AND am.Debit - ISNULL(r.Matched, 0) > 0.001

    UNION ALL

    -- Alacak hareketleri (tahsilat): açık = Credit - eşleşen
    SELECT
        am.Id              AS MovementId,
        am.PartnerId,
        'PAYABLE'          AS Direction,
        am.SourceDocType,
        am.SourceDocNo,
        am.MovementDate,
        am.DueDate,
        am.Credit          AS DocAmount,
        ISNULL(r.Matched, 0) AS MatchedAmount,
        am.Credit - ISNULL(r.Matched, 0) AS OpenAmount
    FROM AccountMovement am
    OUTER APPLY (
        SELECT SUM(Amount) AS Matched FROM AccountReconciliation
        WHERE CreditMovementId = am.Id AND IsReversal = 0
    ) r
    WHERE am.CompanyId = @CompanyId AND (@PartnerId IS NULL OR am.PartnerId = @PartnerId)
      AND am.Credit > 0
      AND am.Credit - ISNULL(r.Matched, 0) > 0.001
);
GO

-- -----------------------------------------------------------------------------
-- tvf_OpenItemAging — açık kalem yaşlandırma (AM bazlı; tvf_PaymentPlanAging'in AM karşılığı)
-- Vade = AM.DueDate (yoksa MovementDate). Kova: NotDue / 1-30 / 31-60 / 61-90 / >90.
-- -----------------------------------------------------------------------------
CREATE OR ALTER FUNCTION dbo.tvf_OpenItemAging
(
    @CompanyId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        oi.PartnerId,
        p.Name AS PartnerName,
        oi.Direction,
        SUM(CASE WHEN DATEDIFF(DAY, ISNULL(oi.DueDate, oi.MovementDate), GETUTCDATE()) <= 0
                 THEN oi.OpenAmount ELSE 0 END) AS NotDue,
        SUM(CASE WHEN DATEDIFF(DAY, ISNULL(oi.DueDate, oi.MovementDate), GETUTCDATE()) BETWEEN 1 AND 30
                 THEN oi.OpenAmount ELSE 0 END) AS Days1_30,
        SUM(CASE WHEN DATEDIFF(DAY, ISNULL(oi.DueDate, oi.MovementDate), GETUTCDATE()) BETWEEN 31 AND 60
                 THEN oi.OpenAmount ELSE 0 END) AS Days31_60,
        SUM(CASE WHEN DATEDIFF(DAY, ISNULL(oi.DueDate, oi.MovementDate), GETUTCDATE()) BETWEEN 61 AND 90
                 THEN oi.OpenAmount ELSE 0 END) AS Days61_90,
        SUM(CASE WHEN DATEDIFF(DAY, ISNULL(oi.DueDate, oi.MovementDate), GETUTCDATE()) > 90
                 THEN oi.OpenAmount ELSE 0 END) AS Over90,
        SUM(oi.OpenAmount) AS TotalOpen
    FROM dbo.tvf_OpenItems(@CompanyId, NULL) oi
    JOIN Partner p ON p.Id = oi.PartnerId
    GROUP BY oi.PartnerId, p.Name, oi.Direction
);
GO

-- NOT (Plan 48): v_ExpenseDistribution buradan ÇIKARILDI — canonical tanım
-- db_objects_expensereport.sql'de (Plan 26, genişletilmiş sürüm). Çift-tanım
-- migrate sırasına bağlı sessiz view-regresyonu üretiyordu; tek kaynak bırakıldı.
