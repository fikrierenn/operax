-- =============================================================================
-- M11 Gelen Belge Kayıt No (Plan 11)
-- Gelen belgelerde iki numara: Evrak No (dış, kullanıcı girer) + Kayıt No (bizim, seriden).
-- ExpenseInvoice.RegistryNo (AFT-000001) · Cheque.RegistryNo (CEK-000001, RECEIVED için).
-- Idempotent.
-- =============================================================================
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'RegistryNo' AND Object_ID = OBJECT_ID('ExpenseInvoice'))
BEGIN
    ALTER TABLE ExpenseInvoice ADD RegistryNo NVARCHAR(20) NULL;  -- bizim iç kayıt no (seriden)
    PRINT 'ExpenseInvoice.RegistryNo eklendi.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'RegistryNo' AND Object_ID = OBJECT_ID('Cheque'))
BEGIN
    ALTER TABLE Cheque ADD RegistryNo NVARCHAR(20) NULL;          -- alınan çek için iç kayıt no
    PRINT 'Cheque.RegistryNo eklendi.';
END
GO
