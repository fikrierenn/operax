# M11 — Finans Modülü: Stored Procedure'ler ve İş Akışları

> Sürüm: v1 · Tarih: 2026-05-28
> Önkoşul: `schema_M11_Finance.sql` uygulandı (FinancialAccount, FinancialTransaction, Cheque, PromissoryNote, Loan, LoanPayment, CreditCard, CreditCardStatement, CreditCardTransaction, PaymentPlan tabloları var)

---

## 1. Bakiye Hesabı (Single-Source View)

Her hesabın güncel bakiyesi `FinancialTransaction` tablosundan akıtılır. Tabloda Balance saklanmaz; yarış koşulları ve mutabakat sorunu çıkarmamak için her zaman view'dan okunur.

```sql
CREATE OR ALTER VIEW v_AccountBalance AS
SELECT
    a.Id AS AccountId,
    a.Code, a.Name, a.AccountType, a.Currency,
    a.OpeningBalance
        + ISNULL(SUM(CASE
              WHEN t.TransactionType IN ('INCOME', 'TRANSFER_IN') THEN  t.AmountTRY
              WHEN t.TransactionType IN ('EXPENSE', 'TRANSFER_OUT') THEN -t.AmountTRY
              ELSE 0 END), 0) AS Balance,
    MAX(t.TransactionDate) AS LastMovementDate
FROM FinancialAccount a
LEFT JOIN FinancialTransaction t
    ON t.AccountId = a.Id AND t.IsDeleted = 0
WHERE a.IsDeleted = 0
GROUP BY a.Id, a.Code, a.Name, a.AccountType, a.Currency, a.OpeningBalance;
```

UI'de hesap kartı bu view'dan okur. Performans için index `IX_FinTx_Account` zaten var; gerekirse indexed view'a çevrilir.

---

## 1.5 Cari Mutabakat Freeze (YAZILI NOT — K9, uygulama M11/sonra)

> **Karar K9 (Fikri, 2026-05-29):** partner+tarih bazlı cari hareket kilidi. Bugün KOD YOK; sadece kayıt.
> Kaynak: `docs/REFERENCE_STUDY.md` §7 (K9) + backlog B14.

**Üçüncü kilit ailesi** (zaman/stok/partner — bkz. plan 14 §2 "kilit aileleri"):
- (1) Zaman bazlı → `AccountingPeriod` (plan 14, ay kapanış/KDV/berat)
- (2) Stok satırı bazlı → sayım freeze (`docs/MODULE_SPECS/M08_CycleCount_Freeze.md`, S7)
- (3) **Partner+tarih bazlı → cari mutabakat freeze (bu bölüm, sonra)**

**Kural:** Müşteriyle/tedarikçiyle **X tarihli bakiye mutabakatı imzalandıysa**, o partnerin **X öncesi** cari
hareketleri (AccountMovement) **kilitlenir**; sonradan geçmişe kayıt girilemez. Granülarite **partner + tarih**
(tüm firma değil — K4; tüm stok değil — K5; belirli partnerin belirli tarihe kadarki cari hareketleri).

**Geçmişe giriş gerekirse:** override + log gerektirir (K8 mekanizması — `PeriodOverrideLog`, `LockType=PARTNER_RECONCILED`).

**Guard:** cari hareket yazan SP'ler `sp_GuardPartnerReconciled(@companyId, @partnerId, @date)` kancasından geçer
(`sp_GuardStockFrozen` kardeşi). Engel mesajı (Türkçe): "Bu cari … tarihine kadar mutabık; geçmişe kayıt için yetki gerekir."

**BUGÜN YAPILMAYACAK:** mutabakat tablosu, guard gövdesi, UI — hepsi sonra. Bu yalnızca kararın kaybolmaması için not.

---

## 2. Çek Yaşam Döngüsü

```
[ PORTFOLIO ] ──Bankaya Ver──→ [ IN_BANK ] ──Tahsil──→ [ COLLECTED ]
       │                            │
       ├──Ciro Et──→ [ ENDORSED ]   ├──Karşılıksız──→ [ RETURNED ]
       │
       └──Verilen Çek Ödendi──→ [ PAID ]
```

### 2.1 `sp_DepositCheque` — Çeki Bankaya Verme

```sql
CREATE OR ALTER PROCEDURE sp_DepositCheque
    @ChequeId    UNIQUEIDENTIFIER,
    @AccountId   UNIQUEIDENTIFIER,
    @DepositDate DATETIME2 = NULL,
    @UserId      UNIQUEIDENTIFIER
AS
BEGIN
    SET XACT_ABORT ON;
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20);
        SELECT @Status = Status FROM Cheque WHERE Id = @ChequeId AND IsDeleted = 0;

        IF @Status IS NULL
            THROW 60001, 'Çek bulunamadı.', 1;
        IF @Status <> 'PORTFOLIO'
            THROW 60002, 'Sadece portföydeki çekler bankaya verilebilir.', 1;

        UPDATE Cheque
        SET Status               = 'IN_BANK',
            DepositedToAccountId = @AccountId,
            DepositedAt          = ISNULL(@DepositDate, GETUTCDATE()),
            UpdatedAt            = GETUTCDATE(),
            UpdatedBy            = @UserId
        WHERE Id = @ChequeId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
```

### 2.2 `sp_CollectCheque` — Çek Tahsil Edildi

İş kuralı: IN_BANK → COLLECTED. Otomatik FinancialTransaction (INCOME) eklenir.

```sql
CREATE OR ALTER PROCEDURE sp_CollectCheque
    @ChequeId UNIQUEIDENTIFIER,
    @CollectDate DATETIME2 = NULL,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET XACT_ABORT ON; SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20), @Amount DECIMAL(18,2), @AccountId UNIQUEIDENTIFIER,
                @PartnerId UNIQUEIDENTIFIER, @CompanyId UNIQUEIDENTIFIER, @ChequeNo NVARCHAR(50);
        SELECT @Status = Status, @Amount = Amount, @AccountId = DepositedToAccountId,
               @PartnerId = PartnerId, @CompanyId = CompanyId, @ChequeNo = ChequeNo
        FROM Cheque WHERE Id = @ChequeId AND IsDeleted = 0;

        IF @Status IS NULL THROW 60001, 'Çek bulunamadı.', 1;
        IF @Status <> 'IN_BANK' THROW 60003, 'Sadece bankaya verilmiş çekler tahsil edilebilir.', 1;

        DECLARE @TxId UNIQUEIDENTIFIER = NEWID(), @Now DATETIME2 = ISNULL(@CollectDate, GETUTCDATE());

        INSERT INTO FinancialTransaction
            (Id, CompanyId, AccountId, TransactionDate, TransactionType,
             Amount, Currency, AmountTRY, PartnerId, Description,
             InstrumentType, InstrumentId, SourceDocType, SourceDocNo, CreatedBy)
        VALUES
            (@TxId, @CompanyId, @AccountId, @Now, 'INCOME',
             @Amount, 'TRY', @Amount, @PartnerId, N'Çek tahsili: ' + @ChequeNo,
             'CHEQUE', @ChequeId, 'CHEQUE_COLLECTION', @ChequeNo, @UserId);

        UPDATE Cheque
        SET Status = 'COLLECTED', CollectedAt = @Now,
            UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @ChequeId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
```

### 2.3 `sp_ReturnCheque` — Karşılıksız Çek

```sql
CREATE OR ALTER PROCEDURE sp_ReturnCheque
    @ChequeId UNIQUEIDENTIFIER, @Reason NVARCHAR(500),
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    -- IN_BANK -> RETURNED, FinancialTransaction yapılmaz (para gelmedi)
    -- Yan etki: Partner cari hesabında alacak hala duruyor (PaymentPlan açık kalır)
    UPDATE Cheque
    SET Status = 'RETURNED', ReturnReason = @Reason,
        UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
    WHERE Id = @ChequeId AND Status IN ('IN_BANK','PORTFOLIO');

    IF @@ROWCOUNT = 0 THROW 60004, 'Çek karşılıksız olarak işaretlenemiyor.', 1;
END
GO
```

### 2.4 `sp_EndorseCheque` — Çek Cirosu (Endorsement)

Çek 3. tarafa devredilir (ödeme amaçlı). Karşı tarafın cari hesabından borç düşülür, çek ENDORSED durumuna geçer.

---

## 3. Senet Yaşam Döngüsü

Çek SP'lerinin aynısı `sp_DepositNote`, `sp_CollectNote`, `sp_ReturnNote`, `sp_EndorseNote` olarak `PromissoryNote` tablosu üzerinde çalışır. Aynı mantık, tablo değişir.

---

## 4. Kredi Modülü

### 4.1 `sp_CreateLoan` — Kredi Açılışı

```sql
CREATE OR ALTER PROCEDURE sp_CreateLoan
    @CompanyId UNIQUEIDENTIFIER, @LoanNo NVARCHAR(50), @BankName NVARCHAR(200),
    @AccountId UNIQUEIDENTIFIER, @Principal DECIMAL(18,2),
    @InterestRate DECIMAL(8,4), @TermMonths INT, @StartDate DATE,
    @UserId UNIQUEIDENTIFIER, @NewLoanId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET XACT_ABORT ON; SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        SET @NewLoanId = NEWID();
        DECLARE @MonthlyPayment DECIMAL(18,2);
        DECLARE @MonthlyRate DECIMAL(18,8) = @InterestRate / 100.0 / 12.0;

        -- İş kuralı: Anüite formülü
        -- M = P * (r(1+r)^n) / ((1+r)^n - 1)
        SET @MonthlyPayment =
            CASE WHEN @MonthlyRate = 0
                 THEN @Principal / @TermMonths
                 ELSE @Principal * (@MonthlyRate * POWER(1 + @MonthlyRate, @TermMonths)) /
                      (POWER(1 + @MonthlyRate, @TermMonths) - 1)
            END;

        INSERT INTO Loan (Id, CompanyId, LoanNo, BankName, AccountId, Principal, InterestRate,
                          TermMonths, StartDate, EndDate, MonthlyPayment, OutstandingBalance,
                          Status, CreatedBy)
        VALUES (@NewLoanId, @CompanyId, @LoanNo, @BankName, @AccountId, @Principal, @InterestRate,
                @TermMonths, @StartDate, DATEADD(MONTH, @TermMonths, @StartDate),
                @MonthlyPayment, @Principal, 'ACTIVE', @UserId);

        -- Taksit takvimi oluşturulur (anapara + faiz dağılımı azalan bakiye yöntemiyle)
        DECLARE @i INT = 1, @Bal DECIMAL(18,2) = @Principal;
        DECLARE @InterestThisMonth DECIMAL(18,2), @PrincipalThisMonth DECIMAL(18,2);

        WHILE @i <= @TermMonths
        BEGIN
            SET @InterestThisMonth = @Bal * @MonthlyRate;
            SET @PrincipalThisMonth = @MonthlyPayment - @InterestThisMonth;
            INSERT INTO LoanPayment (LoanId, InstallmentNo, DueDate, PrincipalAmount,
                                     InterestAmount, TotalAmount)
            VALUES (@NewLoanId, @i, DATEADD(MONTH, @i, @StartDate),
                    @PrincipalThisMonth, @InterestThisMonth, @MonthlyPayment);
            SET @Bal -= @PrincipalThisMonth;
            SET @i += 1;
        END

        -- Kredi tutarı banka hesabına gelir olarak işlenir
        INSERT INTO FinancialTransaction (Id, CompanyId, AccountId, TransactionDate, TransactionType,
                                          Amount, Currency, AmountTRY, Description, InstrumentType,
                                          InstrumentId, CreatedBy)
        VALUES (NEWID(), @CompanyId, @AccountId, @StartDate, 'INCOME',
                @Principal, 'TRY', @Principal, N'Kredi açılış: ' + @LoanNo,
                'LOAN', @NewLoanId, @UserId);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION; THROW;
    END CATCH
END
GO
```

### 4.2 `sp_PayLoanInstallment` — Kredi Taksit Ödeme

```sql
CREATE OR ALTER PROCEDURE sp_PayLoanInstallment
    @PaymentId UNIQUEIDENTIFIER, @PayDate DATETIME2 = NULL,
    @FromAccountId UNIQUEIDENTIFIER, @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET XACT_ABORT ON; SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @LoanId UNIQUEIDENTIFIER, @Total DECIMAL(18,2), @InstNo INT,
                @CompanyId UNIQUEIDENTIFIER, @LoanNo NVARCHAR(50), @PrincipalPaid DECIMAL(18,2);
        SELECT @LoanId = LoanId, @Total = TotalAmount, @InstNo = InstallmentNo,
               @PrincipalPaid = PrincipalAmount
        FROM LoanPayment WHERE Id = @PaymentId AND IsPaid = 0;
        IF @LoanId IS NULL THROW 60010, 'Ödeme bulunamadı veya zaten ödenmiş.', 1;

        SELECT @CompanyId = CompanyId, @LoanNo = LoanNo FROM Loan WHERE Id = @LoanId;

        DECLARE @TxId UNIQUEIDENTIFIER = NEWID();
        INSERT INTO FinancialTransaction (Id, CompanyId, AccountId, TransactionDate, TransactionType,
                                          Amount, Currency, AmountTRY, Description, InstrumentType,
                                          InstrumentId, SourceDocType, SourceDocNo, CreatedBy)
        VALUES (@TxId, @CompanyId, @FromAccountId, ISNULL(@PayDate, GETUTCDATE()), 'EXPENSE',
                @Total, 'TRY', @Total,
                N'Kredi taksit ödemesi: ' + @LoanNo + N' / ' + CAST(@InstNo AS NVARCHAR(10)),
                'LOAN', @LoanId, 'LOAN_INSTALLMENT', @LoanNo + '/' + CAST(@InstNo AS NVARCHAR(10)),
                @UserId);

        UPDATE LoanPayment SET IsPaid = 1, PaidAmount = @Total, PaidAt = GETUTCDATE(),
                              FinancialTransactionId = @TxId WHERE Id = @PaymentId;

        UPDATE Loan SET OutstandingBalance = OutstandingBalance - @PrincipalPaid,
                       UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @LoanId;

        -- Tüm taksitler ödendiyse kredi CLOSED
        IF NOT EXISTS (SELECT 1 FROM LoanPayment WHERE LoanId = @LoanId AND IsPaid = 0)
            UPDATE Loan SET Status = 'CLOSED' WHERE Id = @LoanId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION; THROW;
    END CATCH
END
GO
```

---

## 5. Kredi Kartı

### 5.1 `sp_RecordCardTransaction` — Slip Kaydı

Mağazadan bir POS slipi düştüğünde (genelde marketplace/banka entegrasyonu üzerinden). InstallmentCount > 1 ise çoklu taksit kaydı oluşturulur.

### 5.2 `sp_CloseStatement` — Ekstre Kapatma

Banka ekstre tarihinde Hangfire job otomatik çağırır. Period içindeki tüm CreditCardTransaction'ları toplar, CreditCardStatement kaydı oluşturur.

### 5.3 `sp_PayCreditCardStatement` — Ekstre Ödeme

Banka hesabından kart hesabına virman + Statement.PaidAmount güncelleme. Eksik ödeme: kalan tutar minimum < ödeme < total ise faiz hesap mekanizması (Phase 2).

---

## 6. Ödeme Planı

### 6.1 `sp_GeneratePaymentPlanFromPO` (M03.V1 bağlı)

PO POSTED → `PaymentTermDays / PaymentInstallments` parametrelerine göre PaymentPlan kayıtları (PAYABLE).

### 6.2 `sp_GeneratePaymentPlanFromInvoice`

SalesInvoice POSTED → PaymentPlan (RECEIVABLE).

### 6.3 `sp_RecordPayment` — Multi-Instrument Ödeme Kaydetme

Tek ekran üzerinden bir cariye birden fazla araç ile ödeme yapma (örn: 1000 TL nakit + 5000 TL çek + 2000 TL kart).

```sql
CREATE OR ALTER PROCEDURE sp_RecordPayment
    @CompanyId UNIQUEIDENTIFIER, @PartnerId UNIQUEIDENTIFIER,
    @Direction NVARCHAR(10),     -- PAY (ödeme yapıyoruz) / RECEIVE (tahsil ediyoruz)
    @PaymentLines NVARCHAR(MAX), -- JSON: [{instrument, accountId, amount, chequeData?}, ...]
    @UserId UNIQUEIDENTIFIER
AS
-- Body: JSON parse + her satır için FinancialTransaction + (Cheque/Note kaydı) +
-- ilgili PaymentPlan satırına paylaştırma
```

---

## 7. Nakit Projeksiyon

```sql
CREATE OR ALTER FUNCTION tvf_CashProjection
(
    @CompanyId UNIQUEIDENTIFIER,
    @Days INT
)
RETURNS @Result TABLE (
    EventDate DATE, EventType NVARCHAR(50), Source NVARCHAR(50),
    SourceNo NVARCHAR(100), Amount DECIMAL(18,2), Currency NVARCHAR(10)
)
AS
BEGIN
    -- 1. Açık çekler (vade < @Days)
    INSERT INTO @Result
    SELECT DueDate, 'CHEQUE_IN', Direction, ChequeNo, Amount, Currency
    FROM Cheque
    WHERE CompanyId = @CompanyId AND Status IN ('PORTFOLIO','IN_BANK')
      AND DueDate <= DATEADD(DAY, @Days, GETUTCDATE());

    -- 2. Kredi taksitleri
    INSERT INTO @Result
    SELECT lp.DueDate, 'LOAN_PAY', l.LoanNo, CAST(lp.InstallmentNo AS NVARCHAR(10)),
           lp.TotalAmount, l.Currency
    FROM LoanPayment lp
    JOIN Loan l ON l.Id = lp.LoanId
    WHERE l.CompanyId = @CompanyId AND lp.IsPaid = 0
      AND lp.DueDate <= DATEADD(DAY, @Days, GETUTCDATE());

    -- 3. PaymentPlan (PO + SO)
    INSERT INTO @Result
    SELECT DueDate,
           CASE Direction WHEN 'PAYABLE' THEN 'PO_PAY' ELSE 'SO_RECEIVE' END,
           SourceDocType, SourceDocNo, Amount, 'TRY'
    FROM PaymentPlan
    WHERE CompanyId = @CompanyId AND Status IN ('OPEN','PARTIAL')
      AND DueDate <= DATEADD(DAY, @Days, GETUTCDATE());

    RETURN;
END
GO
```

UI: `/finance/cash-projection?days=30` — bekleyen ödeme/tahsilat akışı zaman çizelgesi olarak.

---

## 8. Yaşlandırma (Aging) Raporu

```sql
CREATE OR ALTER VIEW v_PaymentPlanAging AS
SELECT
    p.PartnerId, pt.Name AS PartnerName,
    pp.Direction,
    SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) <= 0  THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS NotDue,
    SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) BETWEEN 1 AND 30  THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS Days1_30,
    SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) BETWEEN 31 AND 60 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS Days31_60,
    SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) BETWEEN 61 AND 90 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS Days61_90,
    SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) > 90 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS Over90
FROM PaymentPlan pp
JOIN Partner p ON p.Id = pp.PartnerId
JOIN Partner pt ON pt.Id = pp.PartnerId
WHERE pp.Status IN ('OPEN','PARTIAL','OVERDUE') AND pp.IsDeleted = 0
GROUP BY p.PartnerId, pt.Name, pp.Direction;
```

UI: `/finance/aging` — alacak ve borç ayrı sekmeler. Tıklanırsa cari ekstreye giriş.

---

## 9. Banka Mutabakatı

`FinancialTransaction.IsReconciled` flag'i. Banka ekstresi (`bank_statement.csv` veya MT940/MT103) yüklenir, eşleşen kayıtlar `ReconciledAt` ile işaretlenir. Mutabakat ekranı `/finance/reconciliation/{accountId}` — sağ panel sistem, sol panel ekstre, drag-drop ile eşleştirme.

---

## 10. UI Ekran Listesi

| Yol | Açıklama |
|---|---|
| `/finance/accounts` | Tüm hesaplar (kasa/banka/kart/kredi) — type chip ile filtreli |
| `/finance/accounts/{id}` | Hesap ekstresi (FinancialTransaction listesi + bakiye chart) |
| `/finance/cheques?direction=RECEIVED` | Alınan çekler portföyü |
| `/finance/cheques?direction=ISSUED` | Verilen çekler |
| `/finance/cheques/{id}` | Çek detay + statü butonları (DepositToBank, MarkCollected, MarkReturned, Endorse) |
| `/finance/notes` | Senet portföyü (aynı şablon) |
| `/finance/loans` | Kredi listesi + kalan bakiye |
| `/finance/loans/{id}` | Taksit takvimi + öde butonu |
| `/finance/credit-cards` | Kart listesi |
| `/finance/credit-cards/{id}` | Kart detay + ekstre listesi |
| `/finance/credit-cards/{id}/statements/{stId}` | Ekstre detay + slip listesi |
| `/finance/payments/new` | Multi-instrument ödeme kaydetme |
| `/finance/payment-plan` | Vade planı listesi (PAYABLE/RECEIVABLE filtre) |
| `/finance/cash-projection` | Nakit projeksiyon (30/60/90 gün) |
| `/finance/aging` | Yaşlandırma raporu |
| `/finance/reconciliation/{accountId}` | Banka mutabakat ekranı |
