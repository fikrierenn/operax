-- ============================================================
-- Plan 41 — Statü/Yön/Tip kod uzlaştırma (Faz 4) + CHECK constraint (Faz 5)
-- Idempotent: veri UPDATE'leri doğal idempotent (WHERE eski-değer),
-- CHECK'ler sys.check_constraints NOT EXISTS guard'lı.
-- ============================================================
SET XACT_ABORT ON;

-- ─────────────────────────────────────────────────────────────
-- FAZ 4 — VT-kod uzlaştırma (CHECK öncesi veri temizliği ŞART)
-- ─────────────────────────────────────────────────────────────

-- A1: HAVALE → GIRO (İngilizce kod tutarlılığı; EFT ile ayrı araç kalır)
UPDATE FinancialTransaction SET InstrumentType = 'GIRO' WHERE InstrumentType = 'HAVALE';
UPDATE Partner             SET DefaultPaymentMethod = 'GIRO' WHERE DefaultPaymentMethod = 'HAVALE';

-- A2/PaymentTermPolicy: boş string → NET (kod varsayılanı; CHECK öncesi)
UPDATE Partner SET PaymentTermPolicy = 'NET' WHERE PaymentTermPolicy IS NULL OR PaymentTermPolicy = '';

-- A3: ProductionOrder NEW (yetim) → DRAFT (SP yaşam döngüsü DRAFT→IN_PROGRESS→COMPLETED)
UPDATE ProductionOrder SET Status = 'DRAFT' WHERE Status = 'NEW';

-- A4: ShippingHeader NEW/PENDING (legacy) → DRAFT (DocStatus ile yürür)
UPDATE ShippingHeader SET Status = 'DRAFT' WHERE Status IN ('NEW', 'PENDING');

-- ─────────────────────────────────────────────────────────────
-- FAZ 5 — CHECK constraint (kapalı küme kilidi). Tüm mevcut veri 0-ihlal doğrulandı.
-- NULL'a izin verilir (CHECK yalnız FALSE'ta reddeder). Boş-string '' içeren
-- FinancialTransaction.InstrumentType + UserFieldDefinition.DataSourceType CHECK DIŞI (borç).
-- ─────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Cheque_Status')
    ALTER TABLE Cheque WITH CHECK ADD CONSTRAINT CK_Cheque_Status
        CHECK (Status IN ('PORTFOLIO','IN_BANK','COLLECTED','RETURNED','PAID','ENDORSED'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Cheque_Direction')
    ALTER TABLE Cheque WITH CHECK ADD CONSTRAINT CK_Cheque_Direction
        CHECK (Direction IN ('RECEIVED','ISSUED'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PromissoryNote_Status')
    ALTER TABLE PromissoryNote WITH CHECK ADD CONSTRAINT CK_PromissoryNote_Status
        CHECK (Status IN ('PORTFOLIO','IN_BANK','COLLECTED','RETURNED','PAID','ENDORSED'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PromissoryNote_Direction')
    ALTER TABLE PromissoryNote WITH CHECK ADD CONSTRAINT CK_PromissoryNote_Direction
        CHECK (Direction IN ('RECEIVED','ISSUED'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PaymentPlan_Status')
    ALTER TABLE PaymentPlan WITH CHECK ADD CONSTRAINT CK_PaymentPlan_Status
        CHECK (Status IN ('OPEN','PARTIAL','PAID','OVERDUE','CANCELLED'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PaymentPlan_Direction')
    ALTER TABLE PaymentPlan WITH CHECK ADD CONSTRAINT CK_PaymentPlan_Direction
        CHECK (Direction IN ('RECEIVABLE','PAYABLE'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Loan_Status')
    ALTER TABLE Loan WITH CHECK ADD CONSTRAINT CK_Loan_Status
        CHECK (Status IN ('ACTIVE','CLOSED','RESTRUCTURED'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Loan_CalcMethod')
    ALTER TABLE Loan WITH CHECK ADD CONSTRAINT CK_Loan_CalcMethod
        CHECK (CalcMethod IN ('ANUITE','EQUAL_PRINCIPAL','BALLOON','SPOT','ROTATIVE','KMH','DBS'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ItemSerial_Status')
    ALTER TABLE ItemSerial WITH CHECK ADD CONSTRAINT CK_ItemSerial_Status
        CHECK (Status IN ('IN_STOCK','SHIPPED','SCRAPPED','QUARANTINE'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ItemLot_Status')
    ALTER TABLE ItemLot WITH CHECK ADD CONSTRAINT CK_ItemLot_Status
        CHECK (Status IN ('AVAILABLE','QUARANTINE','BLOCKED'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_LPN_Status')
    ALTER TABLE LPN WITH CHECK ADD CONSTRAINT CK_LPN_Status
        CHECK (Status IN ('IN_USE','AVAILABLE','LOADED','SHIPPED'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_LPN_LpnType')
    ALTER TABLE LPN WITH CHECK ADD CONSTRAINT CK_LPN_LpnType
        CHECK (LpnType IN ('PALLET','BOX','CARTON'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ProductionOrder_Status')
    ALTER TABLE ProductionOrder WITH CHECK ADD CONSTRAINT CK_ProductionOrder_Status
        CHECK (Status IN ('DRAFT','IN_PROGRESS','COMPLETED','CANCELLED'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PickTask_Status')
    ALTER TABLE PickTask WITH CHECK ADD CONSTRAINT CK_PickTask_Status
        CHECK (Status IN ('DRAFT','ASSIGNED','IN_PROGRESS','COMPLETED','CANCELLED'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Budget_Status')
    ALTER TABLE Budget WITH CHECK ADD CONSTRAINT CK_Budget_Status
        CHECK (Status IN ('DRAFT','APPROVED','CLOSED'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Budget_Type')
    ALTER TABLE Budget WITH CHECK ADD CONSTRAINT CK_Budget_Type
        CHECK (Type IN ('OPERATIONAL','CASH_FLOW','INVESTMENT'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Partner_Type')
    ALTER TABLE Partner WITH CHECK ADD CONSTRAINT CK_Partner_Type
        CHECK (Type IN ('CUSTOMER','VENDOR','BOTH'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Partner_RiskCategory')
    ALTER TABLE Partner WITH CHECK ADD CONSTRAINT CK_Partner_RiskCategory
        CHECK (RiskCategory IN ('LOW','MEDIUM','HIGH','BLOCKED'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Partner_PaymentTermPolicy')
    ALTER TABLE Partner WITH CHECK ADD CONSTRAINT CK_Partner_PaymentTermPolicy
        CHECK (PaymentTermPolicy IN ('NET','NET_EOM','INSTALLMENTS'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Branch_BranchType')
    ALTER TABLE Branch WITH CHECK ADD CONSTRAINT CK_Branch_BranchType
        CHECK (BranchType IN ('SUBE','MERKEZ','FABRIKA','MAGAZA','OFIS'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CreditCard_CardType')
    ALTER TABLE CreditCard WITH CHECK ADD CONSTRAINT CK_CreditCard_CardType
        CHECK (CardType IN ('CREDIT','BUSINESS','CORPORATE','DEBIT'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Item_ItemType')
    ALTER TABLE Item WITH CHECK ADD CONSTRAINT CK_Item_ItemType
        CHECK (ItemType IN ('STOCK','CONSUMABLE','SERVICE','FIXED_ASSET'));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UserFieldDefinition_FieldType')
    ALTER TABLE UserFieldDefinition WITH CHECK ADD CONSTRAINT CK_UserFieldDefinition_FieldType
        CHECK (FieldType IN ('BOOLEAN','DATE','NUMBER','SELECT','TEXT'));
GO
