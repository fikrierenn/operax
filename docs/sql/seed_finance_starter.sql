-- =============================================================================
-- M11 FINANS — STARTER demo verisi
-- Şirket: Operax Demo LTD (d1e1b1a5-0000-0000-0000-000000000001)
-- 1 kasa + 2 banka + 1 kredi kartı + 1 kredi + 4 çek + 2 senet + hareketler
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CompanyId UNIQUEIDENTIFIER = 'd1e1b1a5-0000-0000-0000-000000000001';

-- Sabit GUID'ler (idempotent için)
DECLARE @AccKasa    UNIQUEIDENTIFIER = 'F1A00001-0000-0000-0000-000000000001';
DECLARE @AccGaranti UNIQUEIDENTIFIER = 'F1A00002-0000-0000-0000-000000000002';
DECLARE @AccIsBank  UNIQUEIDENTIFIER = 'F1A00003-0000-0000-0000-000000000003';
DECLARE @AccCard1   UNIQUEIDENTIFIER = 'F1A00004-0000-0000-0000-000000000004';
DECLARE @AccLoan1   UNIQUEIDENTIFIER = 'F1A00005-0000-0000-0000-000000000005';

DECLARE @Loan1      UNIQUEIDENTIFIER = 'F1B00001-0000-0000-0000-000000000001';
DECLARE @Card1      UNIQUEIDENTIFIER = 'F1C00001-0000-0000-0000-000000000001';

DECLARE @SupAydin   UNIQUEIDENTIFIER = '68EC1270-3DEF-4D6C-A1CF-ADDF492EBC27';  -- seed_demo SUP-001
DECLARE @CusBeta    UNIQUEIDENTIFIER = '3FC8974C-E2B0-443D-AB56-9775ACBC9E29';  -- seed_demo CUS-001

PRINT '=== M11 FINANS SEED BASLIYOR ===';

-- ─── 1. Hesaplar ───────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM FinancialAccount WHERE Id = @AccKasa)
    INSERT INTO FinancialAccount (Id, CompanyId, Code, Name, AccountType, Currency, OpeningBalance, Notes)
    VALUES (@AccKasa, @CompanyId, 'KASA-01', N'Merkez Kasa (TRY)', 'CASH', 'TRY', 25000, N'Şirket merkez kasası');

IF NOT EXISTS (SELECT 1 FROM FinancialAccount WHERE Id = @AccGaranti)
    INSERT INTO FinancialAccount
        (Id, CompanyId, Code, Name, AccountType, Currency, BankName, BranchName, AccountNumber, IBAN, OpeningBalance)
    VALUES
        (@AccGaranti, @CompanyId, 'BNK-GAR01', N'Garanti BBVA · Cari', 'BANK', 'TRY',
         N'Garanti BBVA', N'Levent', '6298321',
         'TR55 0006 2000 4290 0006 2983 21', 250000);

IF NOT EXISTS (SELECT 1 FROM FinancialAccount WHERE Id = @AccIsBank)
    INSERT INTO FinancialAccount
        (Id, CompanyId, Code, Name, AccountType, Currency, BankName, BranchName, AccountNumber, IBAN, OpeningBalance)
    VALUES
        (@AccIsBank, @CompanyId, 'BNK-IS01', N'İş Bankası · USD Mevduat', 'BANK', 'USD',
         N'Türkiye İş Bankası', N'Maslak', '1234567',
         'TR12 0006 4000 0021 0123 4567 89', 12000);

IF NOT EXISTS (SELECT 1 FROM FinancialAccount WHERE Id = @AccCard1)
    INSERT INTO FinancialAccount
        (Id, CompanyId, Code, Name, AccountType, Currency, BankName, CreditLimit, OpeningBalance)
    VALUES
        (@AccCard1, @CompanyId, 'CARD-AKBANK', N'Akbank Wings Business', 'CREDIT_CARD', 'TRY',
         N'Akbank', 75000, 0);

IF NOT EXISTS (SELECT 1 FROM FinancialAccount WHERE Id = @AccLoan1)
    INSERT INTO FinancialAccount
        (Id, CompanyId, Code, Name, AccountType, Currency, BankName, CreditLimit, InterestRate, OpeningBalance)
    VALUES
        (@AccLoan1, @CompanyId, 'LOAN-GAR1', N'Garanti BBVA · Yatırım Kredisi', 'LOAN', 'TRY',
         N'Garanti BBVA', 500000, 47.5, 0);

PRINT '  5 hesap yuklendi (1 kasa, 2 banka, 1 kart, 1 kredi).';

-- ─── 2. Kredi (Loan) + taksit takvimi ──────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Loan WHERE Id = @Loan1)
BEGIN
    DECLARE @LoanIdOut UNIQUEIDENTIFIER;
    -- sp_CreateLoan ile direkt kayıt yapalım
    -- Ama @Loan1 sabit GUID istediğim için manuel yapacağım:
    DECLARE @MR  DECIMAL(18,8) = 47.5 / 100.0 / 12.0;
    DECLARE @MP  DECIMAL(18,2) = 500000 * (@MR * POWER(1 + @MR, 24)) / (POWER(1 + @MR, 24) - 1);

    INSERT INTO Loan
        (Id, CompanyId, LoanNo, BankName, AccountId, Principal, InterestRate,
         TermMonths, StartDate, EndDate, MonthlyPayment, OutstandingBalance, Status)
    VALUES
        (@Loan1, @CompanyId, 'LN-2026-001', N'Garanti BBVA', @AccGaranti,
         500000, 47.5, 24,
         '2026-01-15', '2028-01-15',
         @MP, 500000, 'ACTIVE');

    -- Taksit takvimi
    DECLARE @i INT = 1, @Bal DECIMAL(18,2) = 500000;
    DECLARE @InterestThisMonth DECIMAL(18,2), @PrincipalThisMonth DECIMAL(18,2);
    WHILE @i <= 24
    BEGIN
        SET @InterestThisMonth  = @Bal * @MR;
        SET @PrincipalThisMonth = @MP - @InterestThisMonth;
        INSERT INTO LoanPayment (Id, LoanId, InstallmentNo, DueDate, PrincipalAmount, InterestAmount, TotalAmount,
                                 IsPaid, PaidAmount)
        VALUES (NEWID(), @Loan1, @i, DATEADD(MONTH, @i, '2026-01-15'),
                @PrincipalThisMonth, @InterestThisMonth, @MP,
                CASE WHEN @i <= 4 THEN 1 ELSE 0 END,   -- ilk 4 taksit ödenmiş
                CASE WHEN @i <= 4 THEN @MP ELSE 0 END);
        SET @Bal -= @PrincipalThisMonth;
        SET @i  += 1;
    END

    -- Kredi açılış banka hesabına gelir hareketi
    INSERT INTO FinancialTransaction
        (Id, CompanyId, AccountId, TransactionDate, TransactionType,
         Amount, Currency, AmountTRY, Description, InstrumentType, InstrumentId)
    VALUES
        (NEWID(), @CompanyId, @AccGaranti, '2026-01-15', 'INCOME',
         500000, 'TRY', 500000, N'Kredi açılışı: LN-2026-001 (Yatırım Kredisi)',
         'LOAN', @Loan1);

    -- İlk 4 taksit ödemesi gider hareketleri
    INSERT INTO FinancialTransaction
        (Id, CompanyId, AccountId, TransactionDate, TransactionType,
         Amount, Currency, AmountTRY, Description, InstrumentType, InstrumentId,
         SourceDocType, SourceDocNo)
    SELECT NEWID(), @CompanyId, @AccGaranti, lp.DueDate, 'EXPENSE',
           lp.TotalAmount, 'TRY', lp.TotalAmount,
           N'Kredi taksiti: LN-2026-001 / ' + CAST(lp.InstallmentNo AS NVARCHAR(10)),
           'LOAN', @Loan1,
           'LOAN_INSTALLMENT', 'LN-2026-001/' + CAST(lp.InstallmentNo AS NVARCHAR(10))
    FROM LoanPayment lp WHERE lp.LoanId = @Loan1 AND lp.IsPaid = 1;

    -- Loan kalan anapara güncelle (ilk 4 taksit ödendiği için)
    DECLARE @PaidPrincipal DECIMAL(18,2) =
        (SELECT SUM(PrincipalAmount) FROM LoanPayment WHERE LoanId = @Loan1 AND IsPaid = 1);
    UPDATE Loan SET OutstandingBalance = 500000 - @PaidPrincipal WHERE Id = @Loan1;

    PRINT '  Kredi LN-2026-001 acildi (500K, 24 ay, 4 taksit odenmis).';
END

-- ─── 3. Kredi Kartı + ekstre ───────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM CreditCard WHERE Id = @Card1)
BEGIN
    INSERT INTO CreditCard
        (Id, CompanyId, AccountId, CardNoMasked, HolderName, BankName,
         CreditLimit, AvailableLimit, StatementDay, DueDay, InterestRate, ExpiresAt)
    VALUES
        (@Card1, @CompanyId, @AccCard1, '****-8745', N'OPERAX TICARI A.S.', N'Akbank',
         75000, 38500, 15, 25, 4.85, '2028-12-31');

    -- Son ekstre (Mart-Nisan)
    DECLARE @Stmt1 UNIQUEIDENTIFIER = NEWID();
    INSERT INTO CreditCardStatement
        (Id, CardId, PeriodStart, PeriodEnd, StatementDate, DueDate,
         OpeningBalance, TotalDebit, TotalCredit, ClosingBalance, MinPayment, PaidAmount, IsClosed)
    VALUES
        (@Stmt1, @Card1, '2026-04-15', '2026-05-15', '2026-05-15', '2026-05-25',
         32000, 14250, 22000, 24250, 1450, 22000, 1);

    -- Aktif dönem (Mayıs-Haziran)
    DECLARE @Stmt2 UNIQUEIDENTIFIER = NEWID();
    INSERT INTO CreditCardStatement
        (Id, CardId, PeriodStart, PeriodEnd, StatementDate, DueDate,
         OpeningBalance, TotalDebit, TotalCredit, ClosingBalance, MinPayment, PaidAmount, IsClosed)
    VALUES
        (@Stmt2, @Card1, '2026-05-15', '2026-06-15', '2026-06-15', '2026-06-25',
         24250, 12250, 0, 36500, 2190, 0, 0);

    -- Slip işlemleri (aktif dönem)
    INSERT INTO CreditCardTransaction (Id, CardId, StatementId, TransactionDate, MerchantName, Category, Amount, InstallmentCount, InstallmentNo)
    VALUES
        (NEWID(), @Card1, @Stmt2, '2026-05-18', N'BIM Birleşik Mağazalar',  N'Mutfak/Ofis', 3450, 1, 1),
        (NEWID(), @Card1, @Stmt2, '2026-05-22', N'Opet Akaryakıt',          N'Yakıt',       2800, 1, 1),
        (NEWID(), @Card1, @Stmt2, '2026-05-25', N'Türk Telekom Faturası',   N'İletişim',    1200, 1, 1),
        (NEWID(), @Card1, @Stmt2, '2026-05-28', N'IKEA Mobilya (3 taksit)', N'Demirbaş',    4800, 3, 1);

    PRINT '  Kredi karti CARD-AKBANK + 2 ekstre + 4 slip yuklendi.';
END

-- ─── 4. Çekler (alınan + verilen) ──────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Cheque WHERE Id = 'F1D00001-0000-0000-0000-000000000001')
BEGIN
    -- Müşteriden alınan, portföyde
    INSERT INTO Cheque
        (Id, CompanyId, Direction, ChequeNo, BankName, BranchName,
         DrawerName, DrawerTaxNo, Amount, Currency, ChequeDate, DueDate, Status, PartnerId)
    VALUES
        ('F1D00001-0000-0000-0000-000000000001', @CompanyId, 'RECEIVED',
         '0123-4567', N'Yapı Kredi Bankası', N'Mecidiyeköy',
         N'Beta Otomotiv San. A.Ş.', '1234567890', 45000, 'TRY',
         '2026-04-15', '2026-06-15', 'PORTFOLIO', @CusBeta);

    -- Müşteriden alınan, bankaya verildi (IN_BANK)
    INSERT INTO Cheque
        (Id, CompanyId, Direction, ChequeNo, BankName, BranchName,
         DrawerName, DrawerTaxNo, Amount, Currency, ChequeDate, DueDate, Status, PartnerId,
         DepositedToAccountId, DepositedAt)
    VALUES
        ('F1D00002-0000-0000-0000-000000000002', @CompanyId, 'RECEIVED',
         '0987-6543', N'Halkbank', N'Levent',
         N'Beta Otomotiv San. A.Ş.', '1234567890', 32000, 'TRY',
         '2026-03-01', '2026-06-01', 'IN_BANK', @CusBeta,
         @AccGaranti, '2026-05-25');

    -- Tahsil edilmiş çek (geçmiş)
    INSERT INTO Cheque
        (Id, CompanyId, Direction, ChequeNo, BankName,
         DrawerName, Amount, Currency, ChequeDate, DueDate, Status, PartnerId,
         DepositedToAccountId, DepositedAt, CollectedAt)
    VALUES
        ('F1D00003-0000-0000-0000-000000000003', @CompanyId, 'RECEIVED',
         '5544-3322', N'Garanti BBVA',
         N'Beta Otomotiv San. A.Ş.', 28500, 'TRY',
         '2026-01-10', '2026-04-10', 'COLLECTED', @CusBeta,
         @AccGaranti, '2026-04-01', '2026-04-10');

    -- Verilen çek (bizim kestiğimiz, tedarikçiye)
    INSERT INTO Cheque
        (Id, CompanyId, Direction, ChequeNo, BankName,
         DrawerName, Amount, Currency, ChequeDate, DueDate, Status, PartnerId)
    VALUES
        ('F1D00004-0000-0000-0000-000000000004', @CompanyId, 'ISSUED',
         '9988-7766', N'Garanti BBVA',
         N'OPERAX TICARI A.S.', 55000, 'TRY',
         '2026-05-10', '2026-07-10', 'PORTFOLIO', @SupAydin);

    -- Tahsil edilen çeğin banka gelir hareketi
    INSERT INTO FinancialTransaction
        (Id, CompanyId, AccountId, TransactionDate, TransactionType,
         Amount, Currency, AmountTRY, PartnerId, Description,
         InstrumentType, InstrumentId, SourceDocType, SourceDocNo)
    VALUES
        (NEWID(), @CompanyId, @AccGaranti, '2026-04-10', 'INCOME',
         28500, 'TRY', 28500, @CusBeta, N'Çek tahsili: 5544-3322',
         'CHEQUE', 'F1D00003-0000-0000-0000-000000000003',
         'CHEQUE_COLLECTION', '5544-3322');

    PRINT '  4 cek yuklendi (1 portfoyde, 1 bankada, 1 tahsil edilmis, 1 verilen).';
END

-- ─── 5. Senetler ───────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM PromissoryNote WHERE Id = 'F1E00001-0000-0000-0000-000000000001')
BEGIN
    INSERT INTO PromissoryNote
        (Id, CompanyId, Direction, NoteNo, DrawerName, DrawerTaxNo,
         Amount, Currency, IssueDate, DueDate, Status, PartnerId)
    VALUES
        ('F1E00001-0000-0000-0000-000000000001', @CompanyId, 'RECEIVED', 'SNT-2026-001',
         N'Beta Otomotiv San. A.Ş.', '1234567890', 18500, 'TRY',
         '2026-04-05', '2026-07-05', 'PORTFOLIO', @CusBeta);

    INSERT INTO PromissoryNote
        (Id, CompanyId, Direction, NoteNo, DrawerName, DrawerTaxNo,
         Amount, Currency, IssueDate, DueDate, Status, PartnerId)
    VALUES
        ('F1E00002-0000-0000-0000-000000000002', @CompanyId, 'ISSUED', 'SNT-2026-002',
         N'OPERAX TICARI A.S.', '9999888877', 22000, 'TRY',
         '2026-05-01', '2026-08-01', 'PORTFOLIO', @SupAydin);

    PRINT '  2 senet yuklendi (1 alinan, 1 verilen).';
END

-- ─── 6. PaymentPlan örnekleri (vadeli alış/satış) ──────────────────
IF NOT EXISTS (SELECT 1 FROM PaymentPlan WHERE SourceDocNo IN ('SEED-PO-001','SEED-SI-001'))
BEGIN
    -- Tedarikçi ödemesi (PAYABLE) — Aydın Endüstri'ye vade
    INSERT INTO PaymentPlan
        (Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
         PartnerId, Direction, InstallmentNo, TotalInstallments, DueDate, Amount, Status)
    VALUES
        (NEWID(), @CompanyId, 'PURCHASE_ORDER', NEWID(), 'SEED-PO-001',
         @SupAydin, 'PAYABLE', 1, 1, DATEADD(DAY, 8, GETUTCDATE()), 78500, 'OPEN');

    -- Müşteri alacağı (RECEIVABLE) — Beta Otomotiv'den vade
    INSERT INTO PaymentPlan
        (Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
         PartnerId, Direction, InstallmentNo, TotalInstallments, DueDate, Amount, Status)
    VALUES
        (NEWID(), @CompanyId, 'SALES_INVOICE', NEWID(), 'SEED-SI-001',
         @CusBeta, 'RECEIVABLE', 1, 1, DATEADD(DAY, 18, GETUTCDATE()), 124500, 'OPEN');

    -- Gecikmiş alacak (yaşlandırma için)
    INSERT INTO PaymentPlan
        (Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
         PartnerId, Direction, InstallmentNo, TotalInstallments, DueDate, Amount, Status)
    VALUES
        (NEWID(), @CompanyId, 'SALES_INVOICE', NEWID(), 'SEED-SI-OLD-001',
         @CusBeta, 'RECEIVABLE', 1, 1, DATEADD(DAY, -45, GETUTCDATE()), 18500, 'OVERDUE');

    PRINT '  3 PaymentPlan kaydi (1 verecek, 2 alacak/1 gecikmis).';
END

-- ─── 7. Kasa ve banka manuel hareketleri (mutabakat senaryosu) ─────
IF NOT EXISTS (
    SELECT 1 FROM FinancialTransaction
    WHERE Description = N'Açılış kasa girişi — seed' AND AccountId = @AccKasa
)
BEGIN
    -- Kasa açılış
    INSERT INTO FinancialTransaction
        (Id, CompanyId, AccountId, TransactionDate, TransactionType,
         Amount, Currency, AmountTRY, Description)
    VALUES
        (NEWID(), @CompanyId, @AccKasa, '2026-01-02', 'INCOME',
         25000, 'TRY', 25000, N'Açılış kasa girişi — seed');

    -- Kasadan günlük masraflar
    INSERT INTO FinancialTransaction
        (Id, CompanyId, AccountId, TransactionDate, TransactionType, Amount, Currency, AmountTRY, Description)
    VALUES
        (NEWID(), @CompanyId, @AccKasa, '2026-04-12', 'EXPENSE', 850,  'TRY', 850,  N'Ofis temizlik malzemesi'),
        (NEWID(), @CompanyId, @AccKasa, '2026-04-20', 'EXPENSE', 1250, 'TRY', 1250, N'Kırtasiye + toner'),
        (NEWID(), @CompanyId, @AccKasa, '2026-05-05', 'EXPENSE', 480,  'TRY', 480,  N'Çay/kahve mutfak'),
        (NEWID(), @CompanyId, @AccKasa, '2026-05-15', 'EXPENSE', 2100, 'TRY', 2100, N'Personel yemek');

    -- Banka EFT örnekleri
    INSERT INTO FinancialTransaction
        (Id, CompanyId, AccountId, TransactionDate, TransactionType,
         Amount, Currency, AmountTRY, PartnerId, Description, InstrumentType, IsReconciled)
    VALUES
        (NEWID(), @CompanyId, @AccGaranti, '2026-04-25', 'INCOME', 50000, 'TRY', 50000, @CusBeta,
         N'EFT — Beta Otomotiv ödemesi (fatura FT-2026-031)', 'EFT', 1),
        (NEWID(), @CompanyId, @AccGaranti, '2026-05-08', 'EXPENSE', 35000, 'TRY', 35000, @SupAydin,
         N'EFT — Aydın Endüstri ödemesi (fatura ALN-2026-018)', 'EFT', 1),
        (NEWID(), @CompanyId, @AccGaranti, '2026-05-20', 'INCOME', 75000, 'TRY', 75000, @CusBeta,
         N'Havale — Beta Otomotiv ödemesi (fatura FT-2026-042)', 'HAVALE', 0);

    PRINT '  Kasa+banka ornek hareketleri yuklendi.';
END

PRINT '=== M11 FINANS SEED TAMAMLANDI ===';
GO
