DECLARE @Inv UNIQUEIDENTIFIER = (SELECT Id FROM PurchaseInvoice WHERE DocNo='ALN-20260601-114957');
DELETE FROM AccountMovement WHERE SourceDocId=@Inv;
DELETE FROM PaymentPlan WHERE SourceDocId=@Inv;
DELETE FROM PurchaseInvoiceLine WHERE InvoiceId=@Inv;
DELETE FROM PurchaseInvoice WHERE Id=@Inv;
