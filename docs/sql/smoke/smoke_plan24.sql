-- Smoke Plan 24: Receiving → PurchaseInvoice → Post → AccountMovement borç
DECLARE @NewInv UNIQUEIDENTIFIER;
EXEC sp_CreatePurchaseInvoiceFromReceiving
    @ReceivingId  = 'c1000001-0000-0000-0000-000000000001',
    @CompanyId    = 'd1e1b1a5-0000-0000-0000-000000000001',
    @UserId       = '992FACC3-4A6E-4526-A8FD-3110CA52B47A',
    @NewInvoiceId = @NewInv OUTPUT;

SELECT 'Fatura olusturuldu' AS Adim, @NewInv AS InvoiceId;
SELECT DocNo, SupplierInvoiceNo, Subtotal, TaxAmount, GrandTotal, Status,
       (SELECT COUNT(*) FROM PurchaseInvoiceLine WHERE InvoiceId=@NewInv) AS SatirSayisi
FROM PurchaseInvoice WHERE Id=@NewInv;

-- Tedarikçi belge no/tarih doldur (POSTED öncesi zorunlu)
UPDATE PurchaseInvoice SET SupplierInvoiceNo='TED-2026-555', SupplierInvoiceDate='2026-05-30'
WHERE Id=@NewInv;

-- Onayla
EXEC sp_PurchaseInvoicePost
    @InvoiceId = @NewInv,
    @CompanyId = 'd1e1b1a5-0000-0000-0000-000000000001',
    @UserId    = '992FACC3-4A6E-4526-A8FD-3110CA52B47A';

SELECT 'Onaylandi' AS Adim, Status FROM PurchaseInvoice WHERE Id=@NewInv;
SELECT 'AccountMovement borc' AS Adim, Credit, Debit, SourceDocType
FROM AccountMovement WHERE SourceDocId=@NewInv AND SourceDocType='PURCHASE_INVOICE';
SELECT 'PaymentPlan' AS Adim, Direction, Amount, Status
FROM PaymentPlan WHERE SourceDocId=@NewInv;

-- Temizlik: reverse + sil
EXEC sp_PurchaseInvoiceReverse
    @InvoiceId = @NewInv,
    @CompanyId = 'd1e1b1a5-0000-0000-0000-000000000001',
    @UserId    = '992FACC3-4A6E-4526-A8FD-3110CA52B47A';
SELECT 'Iptal edildi' AS Adim, Status FROM PurchaseInvoice WHERE Id=@NewInv;
