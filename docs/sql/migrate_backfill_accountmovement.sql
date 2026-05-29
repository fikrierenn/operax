-- =============================================================================
-- Plan 09 Faz 2 — Cari Hesap Defteri Backfill (TEK SEFERLİK)
-- Mevcut SalesInvoice / ExpenseInvoice / FinancialTransaction → AccountMovement.
-- Idempotent: (SourceDocType, SourceDocId) zaten varsa atlar (tekrar çalıştırılabilir).
-- İşaret: Satış faturası→Borç · Alış faturası→Alacak · Tahsilat→Alacak · Ödeme→Borç
-- Çalıştır: operax-cli script docs/sql/migrate_backfill_accountmovement.sql
-- =============================================================================
SET NOCOUNT ON;

-- 1) Satış faturaları → SALES_INVOICE (Borç) ────────────────────────────────
INSERT INTO AccountMovement
    (Id, CompanyId, PartnerId, MovementDate, Borc, Alacak, Currency,
     SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
SELECT NEWID(), si.CompanyId, si.PartnerId, si.InvoiceDate, si.GrandTotal, 0, 'TRY',
       'SALES_INVOICE', si.Id, si.InvoiceNo, N'Satış Faturası', N'BACKFILL'
FROM SalesInvoice si
WHERE si.IsDeleted = 0 AND si.Status <> 'CANCELLED' AND si.PartnerId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM AccountMovement am
                  WHERE am.SourceDocType = 'SALES_INVOICE' AND am.SourceDocId = si.Id);
GO

-- 2) Alış faturaları → PURCHASE_INVOICE (Alacak) ─────────────────────────────
INSERT INTO AccountMovement
    (Id, CompanyId, PartnerId, MovementDate, Borc, Alacak, Currency,
     SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
SELECT NEWID(), ei.CompanyId, ei.PartnerId, ei.InvoiceDate, 0, ei.TotalAmount, 'TRY',
       'PURCHASE_INVOICE', ei.Id, ei.DocNo, N'Alış Faturası', N'BACKFILL'
FROM ExpenseInvoice ei
WHERE ei.Status <> 'CANCELLED' AND ei.PartnerId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM AccountMovement am
                  WHERE am.SourceDocType = 'PURCHASE_INVOICE' AND am.SourceDocId = ei.Id);
GO

-- 3) Finansal hareketler → tahsilat (Alacak) / ödeme (Borç) ──────────────────
-- INCOME = müşteriden tahsilat → COLLECTION (Alacak); EXPENSE = ödeme → PAYMENT (Borç)
-- TRANSFER_IN/OUT (kasalar arası) cari değil → hariç.
INSERT INTO AccountMovement
    (Id, CompanyId, PartnerId, MovementDate, Borc, Alacak, Currency,
     SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
SELECT NEWID(), ft.CompanyId, ft.PartnerId, ft.TransactionDate,
       CASE WHEN ft.TransactionType = 'EXPENSE' THEN ft.AmountTRY ELSE 0 END,
       CASE WHEN ft.TransactionType = 'INCOME'  THEN ft.AmountTRY ELSE 0 END,
       'TRY',
       CASE WHEN ft.TransactionType = 'EXPENSE' THEN 'PAYMENT' ELSE 'COLLECTION' END,
       ft.Id, ft.SourceDocNo,
       ISNULL(ft.Description, CASE WHEN ft.TransactionType = 'EXPENSE' THEN N'Ödeme' ELSE N'Tahsilat' END),
       N'BACKFILL'
FROM FinancialTransaction ft
WHERE ft.IsDeleted = 0 AND ft.PartnerId IS NOT NULL
  AND ft.TransactionType IN ('INCOME', 'EXPENSE')
  AND NOT EXISTS (SELECT 1 FROM AccountMovement am
                  WHERE am.SourceDocType = CASE WHEN ft.TransactionType = 'EXPENSE' THEN 'PAYMENT' ELSE 'COLLECTION' END
                    AND am.SourceDocId = ft.Id);
GO
