-- =============================================================================
-- M11 FINANS — STARTER demo verisi (COHERENT — Plan 09)
-- Şirket: Operax Demo LTD (d1e1b1a5-0000-0000-0000-000000000001)
-- Her cari için fatura → vade planı → ödeme/tahsilat zinciri TUTARLI kurulur;
-- AccountMovement defteri = PaymentPlan açık bakiyesi (reconcile temiz).
-- Idempotent: demo şirket finans verisi silinip yeniden kurulur.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CompanyId UNIQUEIDENTIFIER = 'd1e1b1a5-0000-0000-0000-000000000001';

-- Hesaplar
DECLARE @AccKasa    UNIQUEIDENTIFIER = 'F1A00001-0000-0000-0000-000000000001';
DECLARE @AccGaranti UNIQUEIDENTIFIER = 'F1A00002-0000-0000-0000-000000000002';
DECLARE @AccIsBank  UNIQUEIDENTIFIER = 'F1A00003-0000-0000-0000-000000000003';
DECLARE @AccCard1   UNIQUEIDENTIFIER = 'F1A00004-0000-0000-0000-000000000004';
DECLARE @AccLoan1   UNIQUEIDENTIFIER = 'F1A00005-0000-0000-0000-000000000005';
DECLARE @Loan1      UNIQUEIDENTIFIER = 'F1B00001-0000-0000-0000-000000000001';
DECLARE @Card1      UNIQUEIDENTIFIER = 'F1C00001-0000-0000-0000-000000000001';

-- Cariler (gerçek seed_demo GUID'leri)
DECLARE @CusABC     UNIQUEIDENTIFIER = '3FC8974C-E2B0-443D-AB56-9775ACBC9E29';  -- CUS-001 ABC Bilişim
DECLARE @CusXYZ     UNIQUEIDENTIFIER = '3D2EC5A8-4EB1-4C7A-BDDC-17AFD4DD49D6';  -- CUS-002 XYZ Mühendislik
DECLARE @SupTekno   UNIQUEIDENTIFIER = '68EC1270-3DEF-4D6C-A1CF-ADDF492EBC27';  -- SUP-001 Teknoloji Dağıtım
DECLARE @SupGlobal  UNIQUEIDENTIFIER = 'F3FEA396-222A-4679-AE9C-9AC21FD62A1A';  -- SUP-002 Global Hammadde

-- Fatura sabit GUID'leri
DECLARE @SiABC1 UNIQUEIDENTIFIER='F1F00001-0000-0000-0000-000000000001';  -- 28.500 (çekle tahsil)
DECLARE @SiABC2 UNIQUEIDENTIFIER='F1F00002-0000-0000-0000-000000000002';  -- 50.000 (EFT tahsil)
DECLARE @SiABC3 UNIQUEIDENTIFIER='F1F00003-0000-0000-0000-000000000003';  -- 75.000 (havale tahsil)
DECLARE @SiABC4 UNIQUEIDENTIFIER='F1F00004-0000-0000-0000-000000000004';  -- 124.500 (açık)
DECLARE @SiABC5 UNIQUEIDENTIFIER='F1F00005-0000-0000-0000-000000000005';  -- 18.500 (gecikmiş)
DECLARE @SiXYZ1 UNIQUEIDENTIFIER='F1F00006-0000-0000-0000-000000000006';  -- 60.000 (kısmi)
DECLARE @EiTek1 UNIQUEIDENTIFIER='F1F10001-0000-0000-0000-000000000001';  -- 35.000 (ödendi)
DECLARE @EiTek2 UNIQUEIDENTIFIER='F1F10002-0000-0000-0000-000000000002';  -- 78.500 (açık)
DECLARE @EiGlo1 UNIQUEIDENTIFIER='F1F10003-0000-0000-0000-000000000003';  -- 102.000 (gecikmiş)

PRINT '=== M11 FINANS SEED (COHERENT) BASLIYOR ===';

-- ─── 0. Temizlik: demo şirket finans verisi (FK-güvenli sıra) ──────────────
DELETE FROM AccountMovement       WHERE CompanyId = @CompanyId;
DELETE FROM CreditCardTransaction WHERE CardId IN (SELECT Id FROM CreditCard WHERE CompanyId = @CompanyId);
DELETE FROM CreditCardStatement   WHERE CardId IN (SELECT Id FROM CreditCard WHERE CompanyId = @CompanyId);
DELETE FROM CreditCard            WHERE CompanyId = @CompanyId;
DELETE FROM LoanPayment           WHERE LoanId IN (SELECT Id FROM Loan WHERE CompanyId = @CompanyId);
DELETE FROM Loan                  WHERE CompanyId = @CompanyId;
DELETE FROM PaymentPlan           WHERE CompanyId = @CompanyId;
DELETE FROM Cheque                WHERE CompanyId = @CompanyId;
DELETE FROM PromissoryNote        WHERE CompanyId = @CompanyId;
DELETE FROM FinancialTransaction  WHERE CompanyId = @CompanyId;
DELETE FROM SalesInvoiceLine      WHERE InvoiceId IN (SELECT Id FROM SalesInvoice WHERE CompanyId = @CompanyId);
DELETE FROM SalesInvoice          WHERE CompanyId = @CompanyId;
DELETE FROM ExpenseInvoice        WHERE CompanyId = @CompanyId;
DELETE FROM FinancialAccount      WHERE CompanyId = @CompanyId;
PRINT '  Demo finans verisi temizlendi.';

-- ─── 1. Hesaplar ───────────────────────────────────────────────────
INSERT INTO FinancialAccount (Id, CompanyId, Code, Name, AccountType, Currency, OpeningBalance, Notes)
VALUES (@AccKasa, @CompanyId, 'KASA-01', N'Merkez Kasa (TRY)', 'CASH', 'TRY', 25000, N'Şirket merkez kasası');
INSERT INTO FinancialAccount (Id, CompanyId, Code, Name, AccountType, Currency, BankName, BranchName, AccountNumber, IBAN, OpeningBalance)
VALUES (@AccGaranti, @CompanyId, 'BNK-GAR01', N'Garanti BBVA · Cari', 'BANK', 'TRY', N'Garanti BBVA', N'Levent', '6298321', 'TR55 0006 2000 4290 0006 2983 21', 250000);
INSERT INTO FinancialAccount (Id, CompanyId, Code, Name, AccountType, Currency, BankName, BranchName, AccountNumber, IBAN, OpeningBalance)
VALUES (@AccIsBank, @CompanyId, 'BNK-IS01', N'İş Bankası · USD Mevduat', 'BANK', 'USD', N'Türkiye İş Bankası', N'Maslak', '1234567', 'TR12 0006 4000 0021 0123 4567 89', 12000);
INSERT INTO FinancialAccount (Id, CompanyId, Code, Name, AccountType, Currency, BankName, CreditLimit, OpeningBalance)
VALUES (@AccCard1, @CompanyId, 'CARD-AKBANK', N'Akbank Wings Business', 'CREDIT_CARD', 'TRY', N'Akbank', 75000, 0);
INSERT INTO FinancialAccount (Id, CompanyId, Code, Name, AccountType, Currency, BankName, CreditLimit, InterestRate, OpeningBalance)
VALUES (@AccLoan1, @CompanyId, 'LOAN-GAR1', N'Garanti BBVA · Yatırım Kredisi', 'LOAN', 'TRY', N'Garanti BBVA', 500000, 47.5, 0);
PRINT '  5 hesap yuklendi.';

-- ─── 2. Kredi (Loan) + taksit takvimi (cari değil — PartnerId yok) ──
DECLARE @MR  DECIMAL(18,8) = 47.5 / 100.0 / 12.0;
DECLARE @MP  DECIMAL(18,2) = 500000 * (@MR * POWER(1 + @MR, 24)) / (POWER(1 + @MR, 24) - 1);
INSERT INTO Loan (Id, CompanyId, LoanNo, BankName, AccountId, Principal, InterestRate, TermMonths, StartDate, EndDate, MonthlyPayment, OutstandingBalance, Status)
VALUES (@Loan1, @CompanyId, 'LN-2026-001', N'Garanti BBVA', @AccGaranti, 500000, 47.5, 24, '2026-01-15', '2028-01-15', @MP, 500000, 'ACTIVE');
DECLARE @i INT = 1, @Bal DECIMAL(18,2) = 500000, @IntM DECIMAL(18,2), @PrnM DECIMAL(18,2);
WHILE @i <= 24
BEGIN
    SET @IntM = @Bal * @MR; SET @PrnM = @MP - @IntM;
    INSERT INTO LoanPayment (Id, LoanId, InstallmentNo, DueDate, PrincipalAmount, InterestAmount, TotalAmount, IsPaid, PaidAmount)
    VALUES (NEWID(), @Loan1, @i, DATEADD(MONTH, @i, '2026-01-15'), @PrnM, @IntM, @MP,
            CASE WHEN @i <= 4 THEN 1 ELSE 0 END, CASE WHEN @i <= 4 THEN @MP ELSE 0 END);
    SET @Bal -= @PrnM; SET @i += 1;
END
INSERT INTO FinancialTransaction (Id, CompanyId, AccountId, TransactionDate, TransactionType, Amount, Currency, AmountTRY, Description, InstrumentType, InstrumentId)
VALUES (NEWID(), @CompanyId, @AccGaranti, '2026-01-15', 'INCOME', 500000, 'TRY', 500000, N'Kredi açılışı: LN-2026-001', 'LOAN', @Loan1);
INSERT INTO FinancialTransaction (Id, CompanyId, AccountId, TransactionDate, TransactionType, Amount, Currency, AmountTRY, Description, InstrumentType, InstrumentId, SourceDocType, SourceDocNo)
SELECT NEWID(), @CompanyId, @AccGaranti, lp.DueDate, 'EXPENSE', lp.TotalAmount, 'TRY', lp.TotalAmount,
       N'Kredi taksiti: LN-2026-001 / ' + CAST(lp.InstallmentNo AS NVARCHAR(10)), 'LOAN', @Loan1,
       'LOAN_INSTALLMENT', 'LN-2026-001/' + CAST(lp.InstallmentNo AS NVARCHAR(10))
FROM LoanPayment lp WHERE lp.LoanId = @Loan1 AND lp.IsPaid = 1;
UPDATE Loan SET OutstandingBalance = 500000 - (SELECT SUM(PrincipalAmount) FROM LoanPayment WHERE LoanId = @Loan1 AND IsPaid = 1) WHERE Id = @Loan1;
PRINT '  Kredi LN-2026-001 (500K, 4 taksit ödenmiş).';

-- ─── 3. Kredi Kartı + ekstre (cari değil) ──────────────────────────
INSERT INTO CreditCard (Id, CompanyId, AccountId, CardNoMasked, HolderName, BankName, CreditLimit, AvailableLimit, StatementDay, DueDay, InterestRate, ExpiresAt)
VALUES (@Card1, @CompanyId, @AccCard1, '****-8745', N'OPERAX TICARI A.S.', N'Akbank', 75000, 38500, 15, 25, 4.85, '2028-12-31');
DECLARE @Stmt2 UNIQUEIDENTIFIER = NEWID();
INSERT INTO CreditCardStatement (Id, CardId, PeriodStart, PeriodEnd, StatementDate, DueDate, OpeningBalance, TotalDebit, TotalCredit, ClosingBalance, MinPayment, PaidAmount, IsClosed)
VALUES (@Stmt2, @Card1, '2026-05-15', '2026-06-15', '2026-06-15', '2026-06-25', 24250, 12250, 0, 36500, 2190, 0, 0);
INSERT INTO CreditCardTransaction (Id, CardId, StatementId, TransactionDate, MerchantName, Category, Amount, InstallmentCount, InstallmentNo)
VALUES (NEWID(), @Card1, @Stmt2, '2026-05-18', N'BIM Birleşik Mağazalar', N'Mutfak/Ofis', 3450, 1, 1),
       (NEWID(), @Card1, @Stmt2, '2026-05-22', N'Opet Akaryakıt', N'Yakıt', 2800, 1, 1),
       (NEWID(), @Card1, @Stmt2, '2026-05-25', N'Türk Telekom Faturası', N'İletişim', 1200, 1, 1),
       (NEWID(), @Card1, @Stmt2, '2026-05-28', N'IKEA Mobilya (3 taksit)', N'Demirbaş', 4800, 3, 1);
PRINT '  Kredi karti + ekstre + slipler.';

-- ─── 4. Satış faturaları (ABC Bilişim + XYZ Mühendislik) ───────────
INSERT INTO SalesInvoice (Id, CompanyId, InvoiceNo, InvoiceDate, DueDate, PartnerId, Subtotal, TaxAmount, GrandTotal, PaidAmount, Currency, Status)
VALUES
 (@SiABC1, @CompanyId, 'SI-2026-001', '2026-01-05', '2026-02-04', @CusABC,  28500, 0,  28500,  28500, 'TRY', 'POSTED'),
 (@SiABC2, @CompanyId, 'SI-2026-002', '2026-03-10', '2026-04-09', @CusABC,  50000, 0,  50000,  50000, 'TRY', 'POSTED'),
 (@SiABC3, @CompanyId, 'SI-2026-003', '2026-05-05', '2026-06-04', @CusABC,  75000, 0,  75000,  75000, 'TRY', 'POSTED'),
 (@SiABC4, @CompanyId, 'SI-2026-004', '2026-05-25', DATEADD(DAY,18,GETUTCDATE()), @CusABC, 124500, 0, 124500, 0, 'TRY', 'POSTED'),
 (@SiABC5, @CompanyId, 'SI-2026-005', '2026-02-20', DATEADD(DAY,-45,GETUTCDATE()), @CusABC, 18500, 0, 18500, 0, 'TRY', 'POSTED'),
 (@SiXYZ1, @CompanyId, 'SI-2026-010', '2026-04-01', '2026-05-01', @CusXYZ,  60000, 0,  60000,  20000, 'TRY', 'POSTED');

-- Satış faturası kalemleri (her faturaya 1 satır = GrandTotal; KDV 0 demo)
DECLARE @ItmPRD1 UNIQUEIDENTIFIER = 'F780133C-F8F5-4B12-A3D4-749500E50125';  -- PRD-001
DECLARE @UomAdet UNIQUEIDENTIFIER = 'C29E2FC2-E8D7-486A-90CE-B45E6039EE42';
INSERT INTO SalesInvoiceLine (Id, InvoiceId, ItemId, UomId, Description, Qty, UnitPrice, DiscountPercent, DiscountAmount, LineSubtotal, TaxRatePercent, TaxAmount, LineTotal, UnitCost)
SELECT NEWID(), si.Id, @ItmPRD1, @UomAdet, N'Hizmet / ürün bedeli', 1, si.GrandTotal, 0, 0, si.GrandTotal, 0, 0, si.GrandTotal, 0
FROM SalesInvoice si WHERE si.CompanyId = @CompanyId AND si.IsDeleted = 0;
PRINT '  Satış faturası kalemleri eklendi.';

-- ─── 5. Alış faturaları (Teknoloji Dağıtım + Global Hammadde) ──────
INSERT INTO ExpenseInvoice (Id, CompanyId, PartnerId, DocNo, InvoiceDate, DueDate, TotalAmount, Currency, Status)
VALUES
 (@EiTek1, @CompanyId, @SupTekno,  'ALN-2026-018', '2026-05-01', '2026-05-31', 35000,  'TRY', 'POSTED'),
 (@EiTek2, @CompanyId, @SupTekno,  'ALN-2026-020', '2026-05-15', DATEADD(DAY,8,GETUTCDATE()), 78500, 'TRY', 'POSTED'),
 (@EiGlo1, @CompanyId, @SupGlobal, 'ALN-2026-030', '2026-03-05', DATEADD(DAY,-30,GETUTCDATE()), 102000, 'TRY', 'POSTED');
PRINT '  6 satış + 3 alış faturası.';

-- ─── 6. Vade planları (açık bakiyeler — faturalara bağlı) ──────────
INSERT INTO PaymentPlan (Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo, PartnerId, Direction, InstallmentNo, TotalInstallments, DueDate, Amount, PaidAmount, Status)
VALUES
 (NEWID(), @CompanyId, 'SALES_INVOICE', @SiABC4, 'SI-2026-004', @CusABC, 'RECEIVABLE', 1, 1, DATEADD(DAY,18,GETUTCDATE()), 124500, 0, 'OPEN'),
 (NEWID(), @CompanyId, 'SALES_INVOICE', @SiABC5, 'SI-2026-005', @CusABC, 'RECEIVABLE', 1, 1, DATEADD(DAY,-45,GETUTCDATE()), 18500, 0, 'OVERDUE'),
 (NEWID(), @CompanyId, 'SALES_INVOICE', @SiXYZ1, 'SI-2026-010', @CusXYZ, 'RECEIVABLE', 1, 1, '2026-05-01', 60000, 20000, 'PARTIAL'),
 (NEWID(), @CompanyId, 'PURCHASE_INVOICE', @EiTek2, 'ALN-2026-020', @SupTekno, 'PAYABLE', 1, 1, DATEADD(DAY,8,GETUTCDATE()), 78500, 0, 'OPEN'),
 (NEWID(), @CompanyId, 'PURCHASE_INVOICE', @EiGlo1, 'ALN-2026-030', @SupGlobal, 'PAYABLE', 1, 1, DATEADD(DAY,-30,GETUTCDATE()), 102000, 0, 'OVERDUE');
PRINT '  5 vade planı (faturalara bağlı).';

-- ─── 7. Tahsilat / ödeme hareketleri (cari — PartnerId dolu) ───────
INSERT INTO FinancialTransaction (Id, CompanyId, AccountId, TransactionDate, TransactionType, Amount, Currency, AmountTRY, PartnerId, Description, InstrumentType, SourceDocType, SourceDocNo, IsReconciled)
VALUES
 (NEWID(), @CompanyId, @AccGaranti, '2026-04-10', 'INCOME', 28500, 'TRY', 28500, @CusABC, N'Çek tahsili: 5544-3322 (SI-2026-001)', 'CHEQUE', 'CHEQUE_COLLECTION', '5544-3322', 1),
 (NEWID(), @CompanyId, @AccGaranti, '2026-04-25', 'INCOME', 50000, 'TRY', 50000, @CusABC, N'EFT — ABC Bilişim tahsilat (SI-2026-002)', 'EFT', NULL, 'SI-2026-002', 1),
 (NEWID(), @CompanyId, @AccGaranti, '2026-05-20', 'INCOME', 75000, 'TRY', 75000, @CusABC, N'Havale — ABC Bilişim tahsilat (SI-2026-003)', 'HAVALE', NULL, 'SI-2026-003', 0),
 (NEWID(), @CompanyId, @AccGaranti, '2026-04-20', 'INCOME', 20000, 'TRY', 20000, @CusXYZ, N'Havale — XYZ Mühendislik kısmi tahsilat (SI-2026-010)', 'HAVALE', NULL, 'SI-2026-010', 1),
 (NEWID(), @CompanyId, @AccGaranti, '2026-05-08', 'EXPENSE', 35000, 'TRY', 35000, @SupTekno, N'EFT — Teknoloji Dağıtım ödeme (ALN-2026-018)', 'EFT', NULL, 'ALN-2026-018', 1);

-- Kasa manuel masraflar (cari değil)
INSERT INTO FinancialTransaction (Id, CompanyId, AccountId, TransactionDate, TransactionType, Amount, Currency, AmountTRY, Description)
VALUES (NEWID(), @CompanyId, @AccKasa, '2026-01-02', 'INCOME', 25000, 'TRY', 25000, N'Açılış kasa girişi — seed'),
       (NEWID(), @CompanyId, @AccKasa, '2026-04-20', 'EXPENSE', 1250, 'TRY', 1250, N'Kırtasiye + toner'),
       (NEWID(), @CompanyId, @AccKasa, '2026-05-15', 'EXPENSE', 2100, 'TRY', 2100, N'Personel yemek');
PRINT '  5 cari hareketi + kasa masrafları.';

-- ─── 8. Çekler / senetler (gerçek cari isimleriyle) ────────────────
INSERT INTO Cheque (Id, CompanyId, Direction, ChequeNo, BankName, DrawerName, Amount, Currency, ChequeDate, DueDate, Status, PartnerId, DepositedToAccountId, DepositedAt, CollectedAt)
VALUES ('F1D00003-0000-0000-0000-000000000003', @CompanyId, 'RECEIVED', '5544-3322', N'Garanti BBVA', N'ABC Bilişim Sistemleri', 28500, 'TRY', '2026-01-10', '2026-04-10', 'COLLECTED', @CusABC, @AccGaranti, '2026-04-01', '2026-04-10');
INSERT INTO Cheque (Id, CompanyId, Direction, ChequeNo, BankName, BranchName, DrawerName, DrawerTaxNo, Amount, Currency, ChequeDate, DueDate, Status, PartnerId)
VALUES ('F1D00001-0000-0000-0000-000000000001', @CompanyId, 'RECEIVED', '0123-4567', N'Yapı Kredi Bankası', N'Mecidiyeköy', N'XYZ Mühendislik Sanayi', '1234567890', 45000, 'TRY', '2026-04-15', '2026-06-15', 'PORTFOLIO', @CusXYZ);
INSERT INTO Cheque (Id, CompanyId, Direction, ChequeNo, BankName, DrawerName, Amount, Currency, ChequeDate, DueDate, Status, PartnerId)
VALUES ('F1D00004-0000-0000-0000-000000000004', @CompanyId, 'ISSUED', '9988-7766', N'Garanti BBVA', N'OPERAX TICARI A.S.', 55000, 'TRY', '2026-05-10', '2026-07-10', 'PORTFOLIO', @SupTekno);
INSERT INTO PromissoryNote (Id, CompanyId, Direction, NoteNo, DrawerName, DrawerTaxNo, Amount, Currency, IssueDate, DueDate, Status, PartnerId)
VALUES ('F1E00001-0000-0000-0000-000000000001', @CompanyId, 'RECEIVED', 'SNT-2026-001', N'XYZ Mühendislik Sanayi', '1234567890', 18500, 'TRY', '2026-04-05', '2026-07-05', 'PORTFOLIO', @CusXYZ);
PRINT '  3 çek + 1 senet.';

-- ─── 9. Cari Hesap Defteri (AccountMovement) — fatura + ödeme ──────
-- Satış faturası → Debit · Alış faturası → Credit · Tahsilat → Credit · Ödeme → Debit
INSERT INTO AccountMovement (Id, CompanyId, PartnerId, MovementDate, Debit, Credit, Currency, SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
SELECT NEWID(), CompanyId, PartnerId, InvoiceDate, GrandTotal, 0, 'TRY', 'SALES_INVOICE', Id, InvoiceNo, N'Satış Faturası', N'SEED'
FROM SalesInvoice WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND Status <> 'CANCELLED';
INSERT INTO AccountMovement (Id, CompanyId, PartnerId, MovementDate, Debit, Credit, Currency, SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
SELECT NEWID(), CompanyId, PartnerId, InvoiceDate, 0, TotalAmount, 'TRY', 'PURCHASE_INVOICE', Id, DocNo, N'Alış Faturası', N'SEED'
FROM ExpenseInvoice WHERE CompanyId = @CompanyId AND Status <> 'CANCELLED';
INSERT INTO AccountMovement (Id, CompanyId, PartnerId, MovementDate, Debit, Credit, Currency, SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
SELECT NEWID(), CompanyId, PartnerId, TransactionDate,
       CASE WHEN TransactionType='EXPENSE' THEN AmountTRY ELSE 0 END,
       CASE WHEN TransactionType='INCOME'  THEN AmountTRY ELSE 0 END,
       'TRY', CASE WHEN TransactionType='EXPENSE' THEN 'PAYMENT' ELSE 'COLLECTION' END,
       Id, SourceDocNo, ISNULL(Description, N'Hareket'), N'SEED'
FROM FinancialTransaction
WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND PartnerId IS NOT NULL AND TransactionType IN ('INCOME','EXPENSE');
PRINT '  AccountMovement defteri dolduruldu.';

PRINT '=== M11 FINANS SEED (COHERENT) TAMAMLANDI ===';
GO
