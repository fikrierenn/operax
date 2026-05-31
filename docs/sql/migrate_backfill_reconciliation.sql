-- =============================================================================
-- migrate_backfill_reconciliation.sql — Plan 18 Faz 5 (TEK SEFERLİK, idempotent)
-- Mevcut PAID/PARTIAL PaymentPlan'lardan AccountReconciliation seed.
-- Fatura AM (SourceDocId=PaymentPlan.SourceDocId) ↔ ödeme AM (SourceDocId=FinancialTransactionId).
-- Çift sayım koruması: aynı (Debit,Credit) eşleşmesi zaten varsa atlar.
-- Çalıştır: operax-cli script docs/sql/migrate_backfill_reconciliation.sql
-- =============================================================================
SET NOCOUNT ON;

INSERT INTO dbo.AccountReconciliation
    (Id, CompanyId, PartnerId, DebitMovementId, CreditMovementId,
     Amount, MovementDate, IsReversal, CreatedBy)
SELECT
    NEWID(), pp.CompanyId, pp.PartnerId,
    -- DebitMovementId = Debit>0 hareket / CreditMovementId = Credit>0 hareket
    CASE WHEN pp.Direction = 'RECEIVABLE' THEN invAm.Id ELSE payAm.Id END,
    CASE WHEN pp.Direction = 'RECEIVABLE' THEN payAm.Id ELSE invAm.Id END,
    pp.PaidAmount, GETUTCDATE(), 0, N'BACKFILL'
FROM PaymentPlan pp
-- Fatura AM hareketi (başlık-bazlı; SourceDocId = PaymentPlan kaynak belgesi)
JOIN AccountMovement invAm
      ON invAm.CompanyId = pp.CompanyId
     AND invAm.SourceDocType = pp.SourceDocType
     AND invAm.SourceDocId = pp.SourceDocId
-- Ödeme/tahsilat AM hareketi (SourceDocId = ilişkili FinancialTransaction)
JOIN AccountMovement payAm
      ON payAm.CompanyId = pp.CompanyId
     AND payAm.SourceDocId = pp.FinancialTransactionId
     AND payAm.SourceDocType IN ('COLLECTION','PAYMENT')
WHERE pp.Status IN ('PAID','PARTIAL')
  AND pp.FinancialTransactionId IS NOT NULL
  AND pp.SourceDocType <> 'PREPAYMENT'
  AND pp.PaidAmount > 0
  -- Çift sayım koruması: bu eşleşme zaten varsa atla
  AND NOT EXISTS (
      SELECT 1 FROM AccountReconciliation r
      WHERE r.DebitMovementId = CASE WHEN pp.Direction = 'RECEIVABLE' THEN invAm.Id ELSE payAm.Id END
        AND r.CreditMovementId = CASE WHEN pp.Direction = 'RECEIVABLE' THEN payAm.Id ELSE invAm.Id END
        AND r.IsReversal = 0
  );

PRINT 'Reconciliation backfill tamamlandi: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' kayit.';
GO
