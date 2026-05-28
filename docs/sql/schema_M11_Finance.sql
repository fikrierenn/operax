-- =============================================================================
-- M11 — Finance Modülü
-- Kasa, Banka, Çek, Senet, Kredi, Kredi Kartı, Ödeme Planı
-- Tüm tablolar idempotent (IF NOT EXISTS koruması ile)
-- =============================================================================
SET NOCOUNT ON;

-- ─── FinancialAccount ──────────────────────────────────────────────
-- Kasa / Banka / Kredi kartı / Kredi / POS hesaplarının tek tablosu
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FinancialAccount')
BEGIN
    CREATE TABLE FinancialAccount (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId     UNIQUEIDENTIFIER NOT NULL,
        Code          NVARCHAR(50)  NOT NULL,
        Name          NVARCHAR(200) NOT NULL,
        AccountType   NVARCHAR(30)  NOT NULL,   -- CASH, BANK, CREDIT_CARD, LOAN, POS
        Currency      NVARCHAR(10)  DEFAULT 'TRY',
        BankName      NVARCHAR(200) NULL,
        BranchName    NVARCHAR(200) NULL,
        BranchCode    NVARCHAR(20)  NULL,
        AccountNumber NVARCHAR(50)  NULL,
        IBAN          NVARCHAR(50)  NULL,
        CreditLimit   DECIMAL(18,2) NULL,        -- kart limiti veya kredi anaparası
        InterestRate  DECIMAL(8,4)  NULL,        -- yıllık % (kredi/kart için)
        OpeningBalance DECIMAL(18,2) DEFAULT 0,  -- açılış bakiyesi
        Notes         NVARCHAR(MAX) NULL,
        IsActive      BIT DEFAULT 1,
        IsDeleted     BIT DEFAULT 0,
        CreatedAt     DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy     UNIQUEIDENTIFIER,
        UpdatedAt     DATETIME2 NULL,
        UpdatedBy     UNIQUEIDENTIFIER,
        CONSTRAINT FK_FinancialAccount_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
    );
    CREATE INDEX IX_FinancialAccount_Type ON FinancialAccount(CompanyId, AccountType) WHERE IsDeleted = 0;
    CREATE UNIQUE INDEX UX_FinancialAccount_Code ON FinancialAccount(CompanyId, Code) WHERE IsDeleted = 0;
    PRINT 'FinancialAccount tablosu olusturuldu.';
END
GO

-- ─── FinancialTransaction ──────────────────────────────────────────
-- Tum nakit/banka/kart hareketleri (bakiye buradan hesaplanir)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FinancialTransaction')
BEGIN
    CREATE TABLE FinancialTransaction (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId       UNIQUEIDENTIFIER NOT NULL,
        AccountId       UNIQUEIDENTIFIER NOT NULL,
        TransactionDate DATETIME2 NOT NULL,
        TransactionType NVARCHAR(20) NOT NULL,    -- INCOME, EXPENSE, TRANSFER_IN, TRANSFER_OUT
        Amount          DECIMAL(18,2) NOT NULL,   -- pozitif (yon TransactionType'tan)
        Currency        NVARCHAR(10) DEFAULT 'TRY',
        ExchangeRate    DECIMAL(18,6) DEFAULT 1,
        AmountTRY       DECIMAL(18,2) NOT NULL,   -- TRY karsiligi
        PartnerId       UNIQUEIDENTIFIER NULL,    -- musteri/tedarikci/personel
        Description     NVARCHAR(500) NULL,
        InstrumentType  NVARCHAR(20)  NULL,       -- CASH, EFT, HAVALE, CHEQUE, NOTE, CARD, LOAN
        InstrumentId    UNIQUEIDENTIFIER NULL,    -- Cheque/Note/LoanPayment id'si
        SourceDocType   NVARCHAR(50)  NULL,       -- INVOICE, PO, SO, MANUAL
        SourceDocId     UNIQUEIDENTIFIER NULL,
        SourceDocNo     NVARCHAR(100) NULL,
        IsReconciled    BIT DEFAULT 0,            -- banka mutabakati yapildi mi
        ReconciledAt    DATETIME2 NULL,
        IsDeleted       BIT DEFAULT 0,
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy       UNIQUEIDENTIFIER,
        CONSTRAINT FK_FinTx_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_FinTx_Account FOREIGN KEY (AccountId) REFERENCES FinancialAccount(Id),
        CONSTRAINT FK_FinTx_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id)
    );
    CREATE INDEX IX_FinTx_Date ON FinancialTransaction(CompanyId, TransactionDate DESC) WHERE IsDeleted = 0;
    CREATE INDEX IX_FinTx_Account ON FinancialTransaction(AccountId, TransactionDate DESC);
    CREATE INDEX IX_FinTx_Source ON FinancialTransaction(SourceDocType, SourceDocId);
    PRINT 'FinancialTransaction tablosu olusturuldu.';
END
GO

-- ─── Cheque ────────────────────────────────────────────────────────
-- Alinan ve verilen ceklerin tek tablosu (Direction ile ayrilir)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Cheque')
BEGIN
    CREATE TABLE Cheque (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId     UNIQUEIDENTIFIER NOT NULL,
        Direction     NVARCHAR(10) NOT NULL,    -- RECEIVED (musteri cek), ISSUED (kestigimiz cek)
        ChequeNo      NVARCHAR(50) NOT NULL,
        BankName      NVARCHAR(200) NOT NULL,
        BranchName    NVARCHAR(200) NULL,
        BranchCode    NVARCHAR(20)  NULL,
        AccountNo     NVARCHAR(50)  NULL,
        DrawerName    NVARCHAR(200) NOT NULL,    -- kesideci adi
        DrawerTaxNo   NVARCHAR(50)  NULL,
        Amount        DECIMAL(18,2) NOT NULL,
        Currency      NVARCHAR(10) DEFAULT 'TRY',
        ChequeDate    DATE NOT NULL,             -- cek uzerindeki keside tarihi
        DueDate       DATE NOT NULL,             -- vade tarihi
        Status        NVARCHAR(20) DEFAULT 'PORTFOLIO',
                                                 -- PORTFOLIO, IN_BANK (tahsile verildi), COLLECTED,
                                                 -- RETURNED (karsiliksiz), ENDORSED (cirolandi), PAID (verilen cek odendi)
        PartnerId     UNIQUEIDENTIFIER NULL,     -- kimden alindi / kime verildi
        DepositedToAccountId UNIQUEIDENTIFIER NULL,  -- hangi bankaya tahsile verildi
        DepositedAt   DATETIME2 NULL,
        CollectedAt   DATETIME2 NULL,
        ReturnReason  NVARCHAR(500) NULL,
        SourceDocType NVARCHAR(50)  NULL,        -- INVOICE_RECEIVED, INVOICE_PAID
        SourceDocId   UNIQUEIDENTIFIER NULL,
        SourceDocNo   NVARCHAR(100) NULL,
        Notes         NVARCHAR(MAX) NULL,
        IsDeleted     BIT DEFAULT 0,
        CreatedAt     DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy     UNIQUEIDENTIFIER,
        UpdatedAt     DATETIME2 NULL,
        UpdatedBy     UNIQUEIDENTIFIER,
        CONSTRAINT FK_Cheque_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_Cheque_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id),
        CONSTRAINT FK_Cheque_DepositAccount FOREIGN KEY (DepositedToAccountId) REFERENCES FinancialAccount(Id)
    );
    CREATE INDEX IX_Cheque_Status ON Cheque(CompanyId, Status, DueDate) WHERE IsDeleted = 0;
    CREATE INDEX IX_Cheque_Direction ON Cheque(CompanyId, Direction, DueDate) WHERE IsDeleted = 0;
    PRINT 'Cheque tablosu olusturuldu.';
END
GO

-- ─── PromissoryNote (Senet) ────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PromissoryNote')
BEGIN
    CREATE TABLE PromissoryNote (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId     UNIQUEIDENTIFIER NOT NULL,
        Direction     NVARCHAR(10) NOT NULL,    -- RECEIVED, ISSUED
        NoteNo        NVARCHAR(50) NOT NULL,
        DrawerName    NVARCHAR(200) NOT NULL,
        DrawerTaxNo   NVARCHAR(50)  NULL,
        DrawerAddress NVARCHAR(500) NULL,
        AvalistName   NVARCHAR(200) NULL,        -- kefil
        Amount        DECIMAL(18,2) NOT NULL,
        Currency      NVARCHAR(10) DEFAULT 'TRY',
        IssueDate     DATE NOT NULL,             -- tanzim tarihi
        DueDate       DATE NOT NULL,
        Status        NVARCHAR(20) DEFAULT 'PORTFOLIO',
                                                 -- PORTFOLIO, IN_BANK, COLLECTED, RETURNED, ENDORSED, PAID
        PartnerId     UNIQUEIDENTIFIER NULL,
        DepositedToAccountId UNIQUEIDENTIFIER NULL,
        DepositedAt   DATETIME2 NULL,
        CollectedAt   DATETIME2 NULL,
        ReturnReason  NVARCHAR(500) NULL,
        SourceDocType NVARCHAR(50)  NULL,
        SourceDocId   UNIQUEIDENTIFIER NULL,
        SourceDocNo   NVARCHAR(100) NULL,
        Notes         NVARCHAR(MAX) NULL,
        IsDeleted     BIT DEFAULT 0,
        CreatedAt     DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy     UNIQUEIDENTIFIER,
        UpdatedAt     DATETIME2 NULL,
        UpdatedBy     UNIQUEIDENTIFIER,
        CONSTRAINT FK_Note_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_Note_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id),
        CONSTRAINT FK_Note_DepositAccount FOREIGN KEY (DepositedToAccountId) REFERENCES FinancialAccount(Id)
    );
    CREATE INDEX IX_Note_Status ON PromissoryNote(CompanyId, Status, DueDate) WHERE IsDeleted = 0;
    CREATE INDEX IX_Note_Direction ON PromissoryNote(CompanyId, Direction, DueDate) WHERE IsDeleted = 0;
    PRINT 'PromissoryNote tablosu olusturuldu.';
END
GO

-- ─── Loan (Banka Kredisi) ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Loan')
BEGIN
    CREATE TABLE Loan (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId       UNIQUEIDENTIFIER NOT NULL,
        LoanNo          NVARCHAR(50) NOT NULL,
        BankName        NVARCHAR(200) NOT NULL,
        BranchName      NVARCHAR(200) NULL,
        AccountId       UNIQUEIDENTIFIER NULL,    -- bagli oldugu banka hesabi
        LoanType        NVARCHAR(30) DEFAULT 'COMMERCIAL', -- COMMERCIAL, INVESTMENT, OVERDRAFT
        Principal       DECIMAL(18,2) NOT NULL,    -- anapara
        InterestRate    DECIMAL(8,4)  NOT NULL,    -- yillik %
        TermMonths      INT NOT NULL,              -- vade ay
        StartDate       DATE NOT NULL,
        EndDate         DATE NOT NULL,
        MonthlyPayment  DECIMAL(18,2) NOT NULL,
        OutstandingBalance DECIMAL(18,2) NOT NULL, -- kalan anapara
        Currency        NVARCHAR(10) DEFAULT 'TRY',
        Status          NVARCHAR(20) DEFAULT 'ACTIVE', -- ACTIVE, CLOSED, OVERDUE, RESTRUCTURED
        Notes           NVARCHAR(MAX) NULL,
        IsDeleted       BIT DEFAULT 0,
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        CreatedBy       UNIQUEIDENTIFIER,
        UpdatedAt       DATETIME2 NULL,
        UpdatedBy       UNIQUEIDENTIFIER,
        CONSTRAINT FK_Loan_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_Loan_Account FOREIGN KEY (AccountId) REFERENCES FinancialAccount(Id)
    );
    CREATE INDEX IX_Loan_Status ON Loan(CompanyId, Status) WHERE IsDeleted = 0;
    PRINT 'Loan tablosu olusturuldu.';
END
GO

-- ─── LoanPayment (Kredi Taksiti) ───────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LoanPayment')
BEGIN
    CREATE TABLE LoanPayment (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        LoanId          UNIQUEIDENTIFIER NOT NULL,
        InstallmentNo   INT NOT NULL,             -- taksit numarasi (1, 2, ...)
        DueDate         DATE NOT NULL,
        PrincipalAmount DECIMAL(18,2) NOT NULL,    -- anapara payi
        InterestAmount  DECIMAL(18,2) NOT NULL,    -- faiz payi
        TotalAmount     DECIMAL(18,2) NOT NULL,    -- toplam taksit
        PaidAmount      DECIMAL(18,2) DEFAULT 0,
        PaidAt          DATETIME2 NULL,
        IsPaid          BIT DEFAULT 0,
        FinancialTransactionId UNIQUEIDENTIFIER NULL,  -- odeme hareketi referansi
        Notes           NVARCHAR(MAX) NULL,
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_LoanPayment_Loan FOREIGN KEY (LoanId) REFERENCES Loan(Id)
    );
    CREATE INDEX IX_LoanPayment_Due ON LoanPayment(LoanId, DueDate, IsPaid);
    PRINT 'LoanPayment tablosu olusturuldu.';
END
GO

-- ─── CreditCard ────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CreditCard')
BEGIN
    CREATE TABLE CreditCard (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId       UNIQUEIDENTIFIER NOT NULL,
        AccountId       UNIQUEIDENTIFIER NOT NULL,  -- FinancialAccount referansi (Type=CREDIT_CARD)
        CardNoMasked    NVARCHAR(20) NOT NULL,      -- son 4 hane (****1234)
        HolderName      NVARCHAR(200) NOT NULL,
        BankName        NVARCHAR(200) NOT NULL,
        CardType        NVARCHAR(30) DEFAULT 'CREDIT', -- CREDIT, DEBIT, BUSINESS, CORPORATE
        CreditLimit     DECIMAL(18,2) NOT NULL,
        AvailableLimit  DECIMAL(18,2) NOT NULL,     -- mevcut kullanilabilir limit
        StatementDay    INT NOT NULL,               -- ayin kacinda ekstre kesilir (1-28)
        DueDay          INT NOT NULL,               -- ayin kacinda son odeme (1-28)
        InterestRate    DECIMAL(8,4) NULL,          -- gecikme faizi
        Currency        NVARCHAR(10) DEFAULT 'TRY',
        ExpiresAt       DATE NULL,
        IsActive        BIT DEFAULT 1,
        IsDeleted       BIT DEFAULT 0,
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt       DATETIME2 NULL,
        CONSTRAINT FK_CreditCard_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_CreditCard_Account FOREIGN KEY (AccountId) REFERENCES FinancialAccount(Id)
    );
    PRINT 'CreditCard tablosu olusturuldu.';
END
GO

-- ─── CreditCardStatement (Aylik Ekstre) ────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CreditCardStatement')
BEGIN
    CREATE TABLE CreditCardStatement (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CardId          UNIQUEIDENTIFIER NOT NULL,
        PeriodStart     DATE NOT NULL,
        PeriodEnd       DATE NOT NULL,
        StatementDate   DATE NOT NULL,
        DueDate         DATE NOT NULL,
        OpeningBalance  DECIMAL(18,2) DEFAULT 0,
        TotalDebit      DECIMAL(18,2) DEFAULT 0,
        TotalCredit     DECIMAL(18,2) DEFAULT 0,
        ClosingBalance  DECIMAL(18,2) NOT NULL,
        MinPayment      DECIMAL(18,2) NOT NULL,
        PaidAmount      DECIMAL(18,2) DEFAULT 0,
        IsClosed        BIT DEFAULT 0,
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_CCS_Card FOREIGN KEY (CardId) REFERENCES CreditCard(Id)
    );
    CREATE INDEX IX_CCS_CardPeriod ON CreditCardStatement(CardId, PeriodEnd DESC);
    PRINT 'CreditCardStatement tablosu olusturuldu.';
END
GO

-- ─── CreditCardTransaction (Slip islemleri) ────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CreditCardTransaction')
BEGIN
    CREATE TABLE CreditCardTransaction (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CardId          UNIQUEIDENTIFIER NOT NULL,
        StatementId     UNIQUEIDENTIFIER NULL,       -- hangi ekstreye dustu (gelmemis ise NULL)
        TransactionDate DATETIME2 NOT NULL,
        MerchantName    NVARCHAR(200) NOT NULL,
        Category        NVARCHAR(100) NULL,          -- yakit, ofis, vb
        Amount          DECIMAL(18,2) NOT NULL,
        Currency        NVARCHAR(10) DEFAULT 'TRY',
        InstallmentCount INT DEFAULT 1,              -- taksit sayisi
        InstallmentNo   INT DEFAULT 1,               -- bu islem kacinci taksit
        Reference       NVARCHAR(100) NULL,
        Notes           NVARCHAR(MAX) NULL,
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_CCT_Card FOREIGN KEY (CardId) REFERENCES CreditCard(Id),
        CONSTRAINT FK_CCT_Statement FOREIGN KEY (StatementId) REFERENCES CreditCardStatement(Id)
    );
    CREATE INDEX IX_CCT_CardDate ON CreditCardTransaction(CardId, TransactionDate DESC);
    PRINT 'CreditCardTransaction tablosu olusturuldu.';
END
GO

-- ─── PaymentPlan (Fatura/Siparis Vade Plani) ───────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaymentPlan')
BEGIN
    CREATE TABLE PaymentPlan (
        Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId       UNIQUEIDENTIFIER NOT NULL,
        SourceDocType   NVARCHAR(50) NOT NULL,        -- PURCHASE_ORDER, SALES_ORDER, EXPENSE_INVOICE, SALES_INVOICE
        SourceDocId     UNIQUEIDENTIFIER NOT NULL,
        SourceDocNo     NVARCHAR(100) NOT NULL,
        PartnerId       UNIQUEIDENTIFIER NULL,
        Direction       NVARCHAR(10) NOT NULL,        -- PAYABLE (verecek), RECEIVABLE (alacak)
        InstallmentNo   INT NOT NULL,
        TotalInstallments INT NOT NULL,
        DueDate         DATE NOT NULL,
        Amount          DECIMAL(18,2) NOT NULL,
        PaidAmount      DECIMAL(18,2) DEFAULT 0,
        Status          NVARCHAR(20) DEFAULT 'OPEN',  -- OPEN, PARTIAL, PAID, OVERDUE
        PaidAt          DATETIME2 NULL,
        FinancialTransactionId UNIQUEIDENTIFIER NULL,
        Notes           NVARCHAR(MAX) NULL,
        IsDeleted       BIT DEFAULT 0,
        CreatedAt       DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt       DATETIME2 NULL,
        CONSTRAINT FK_PaymentPlan_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
        CONSTRAINT FK_PaymentPlan_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id)
    );
    CREATE INDEX IX_PaymentPlan_Due ON PaymentPlan(CompanyId, DueDate, Status) WHERE IsDeleted = 0;
    CREATE INDEX IX_PaymentPlan_Source ON PaymentPlan(SourceDocType, SourceDocId);
    PRINT 'PaymentPlan tablosu olusturuldu.';
END
GO
