-- =============================================================================
-- M01 Partner risk/vade yönetimi + M11 Kredi tipleri + Kart-Banka bağlantısı
-- + e-Belge yön değişikliği (outbound submission yerine inbound sync)
-- =============================================================================
SET NOCOUNT ON;

-- ─── Partner risk & ödeme yönetimi ────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'RiskScore' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD RiskScore TINYINT DEFAULT 3;  -- 1 (en duşuk) - 5 (en yuksek)
    PRINT 'Partner.RiskScore eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'RiskCategory' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD RiskCategory NVARCHAR(20) DEFAULT 'MEDIUM';  -- LOW/MEDIUM/HIGH/BLOCKED
    PRINT 'Partner.RiskCategory eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'MaxOverdueDays' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD MaxOverdueDays INT DEFAULT 30;  -- bu gunden cok gecikme = BLOCKED
    PRINT 'Partner.MaxOverdueDays eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'DefaultPaymentMethod' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD DefaultPaymentMethod NVARCHAR(20) DEFAULT 'EFT';
    -- CASH (nakit), EFT (havale), CHEQUE (cek), NOTE (senet), CREDIT_CARD (kart), DBS (otomatik)
    PRINT 'Partner.DefaultPaymentMethod eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PaymentTermPolicy' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD PaymentTermPolicy NVARCHAR(20) DEFAULT 'NET';
    -- NET (X gun vade), NET_EOM (ay sonu + X gun), INSTALLMENTS (taksitli)
    PRINT 'Partner.PaymentTermPolicy eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'EFaturaMukellef' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD EFaturaMukellef BIT DEFAULT 0;
    PRINT 'Partner.EFaturaMukellef eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'EFaturaAlias' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD EFaturaAlias NVARCHAR(200) NULL;  -- defaultpk@..., iletisim adresi
    PRINT 'Partner.EFaturaAlias eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IbanForRefund' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD IbanForRefund NVARCHAR(50) NULL;
    PRINT 'Partner.IbanForRefund eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'OpenBalance' AND Object_ID = OBJECT_ID('Partner'))
BEGIN
    ALTER TABLE Partner ADD OpenBalance DECIMAL(18,2) DEFAULT 0;
    -- view'dan da okunabilir ama performans icin denormalize edilebilir
    PRINT 'Partner.OpenBalance eklendi.';
END
GO

-- ─── Loan: kredi tipi alanı zaten var; default'u "ANUITE"ye cek ─────
-- LoanType kolonu mevcut, fakat varsayilan COMMERCIAL idi; ANUITE mantikli
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'LoanType' AND Object_ID = OBJECT_ID('Loan'))
BEGIN
    -- Daha kapsamli default constraint ekleyemiyoruz (mevcut constraint var)
    -- ama mevcut COMMERCIAL kayitlari oldugu gibi kalir
    PRINT 'Loan.LoanType kolonu mevcut.';
END
GO

-- LoanCalcMethod: tek seferlik hesap formuluyle uretilecek taksitler
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CalcMethod' AND Object_ID = OBJECT_ID('Loan'))
BEGIN
    ALTER TABLE Loan ADD CalcMethod NVARCHAR(30) DEFAULT 'ANUITE';
    -- ANUITE, EQUAL_PRINCIPAL, BALLOON, SPOT, ROTATIVE, KMH, DBS
    PRINT 'Loan.CalcMethod eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'GracePeriodMonths' AND Object_ID = OBJECT_ID('Loan'))
BEGIN
    ALTER TABLE Loan ADD GracePeriodMonths INT DEFAULT 0;
    -- odemesiz donem (sadece faiz odenir)
    PRINT 'Loan.GracePeriodMonths eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'BalloonAmount' AND Object_ID = OBJECT_ID('Loan'))
BEGIN
    ALTER TABLE Loan ADD BalloonAmount DECIMAL(18,2) NULL;
    -- son taksitte odenecek ekstra anapara (balon odemeli)
    PRINT 'Loan.BalloonAmount eklendi.';
END
GO

-- ─── CreditCard: banka hesabı bağlantısı ───────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'LinkedBankAccountId' AND Object_ID = OBJECT_ID('CreditCard'))
BEGIN
    ALTER TABLE CreditCard ADD LinkedBankAccountId UNIQUEIDENTIFIER NULL;
    -- Ekstre vadesinde otomatik dusulecek banka hesabi (FinancialAccount Type=BANK)
    ALTER TABLE CreditCard ADD CONSTRAINT FK_CC_BankAccount
        FOREIGN KEY (LinkedBankAccountId) REFERENCES FinancialAccount(Id);
    PRINT 'CreditCard.LinkedBankAccountId eklendi (banka hesap baglantisi).';
END
GO

-- ─── InvoiceEnvelope: outbound -> inbound sync revize ─────────────
-- Mevcut yapida Status='DRAFT/READY/SENT/ACCEPTED/REJECTED' outbound dusunulmustu.
-- Yeni semantik: dis ERP'den senkronize edilen e-belge kaydi.
-- DirectionType zaten var (OUTBOUND/INBOUND) — INBOUND'a default'u cevirelim.
-- Status kullanim revize: SYNCING, SYNCED, ASSIGN_ERROR
-- Yeni kolon: ExternalDocId (dis ERP icindeki e-belge ID'si)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'ExternalDocId' AND Object_ID = OBJECT_ID('InvoiceEnvelope'))
BEGIN
    ALTER TABLE InvoiceEnvelope ADD ExternalDocId NVARCHAR(100) NULL;
    PRINT 'InvoiceEnvelope.ExternalDocId eklendi (dis ERP icindeki e-belge ID).';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'ExternalSystem' AND Object_ID = OBJECT_ID('InvoiceEnvelope'))
BEGIN
    ALTER TABLE InvoiceEnvelope ADD ExternalSystem NVARCHAR(50) NULL;
    -- LOGO_TIGER, MIKRO_FLY, NETSIS, NEBIM, ZIRVE, ETA, LUCA, BIZIMHESAP, MANUAL
    PRINT 'InvoiceEnvelope.ExternalSystem eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'SyncedAt' AND Object_ID = OBJECT_ID('InvoiceEnvelope'))
BEGIN
    ALTER TABLE InvoiceEnvelope ADD SyncedAt DATETIME2 NULL;
    PRINT 'InvoiceEnvelope.SyncedAt eklendi.';
END
GO
