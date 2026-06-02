-- Plan 29 — İş-ayarı parametreleri seed (idempotent, her şirkete). Admin > Parametreler'den düzenlenir.
-- INVOICE_AGING_DAYS Plan 28'de ayrı seed edildi; burada tekrar edilmez.
-- Değerler InvariantCulture (nokta ondalık) saklanır — ParameterStore kültür-bağımsız okur.

DECLARE @params TABLE (ModuleCode NVARCHAR(50), Code NVARCHAR(100), Value NVARCHAR(MAX), Descr NVARCHAR(500));
INSERT INTO @params (ModuleCode, Code, Value, Descr) VALUES
 ('M03','DEFAULT_PURCHASE_TAX_RATE',   '20', N'Alış belgelerinde varsayılan KDV oranı (%) — ürün kendi oranını (Item.TaxRate) taşıyorsa o önceliklidir, bu yalnız fallback.'),
 ('M04','DEFAULT_SALES_TAX_RATE',      '20', N'Satış belgelerinde varsayılan KDV oranı (%) — ürün kendi oranını taşıyorsa o önceliklidir, bu yalnız fallback. Alış oranından farklı olabilir.'),
 ('M00','DEFAULT_PAYMENT_TERM_DAYS',   '30', N'Yeni cari/sipariş için varsayılan ödeme vadesi (gün).'),
 ('M11','AGING_BUCKET_2_DAYS',         '30', N'Cari yaşlandırma 1. eşik (gün): 0-bu değer arası ilk kova.'),
 ('M11','AGING_BUCKET_3_DAYS',         '60', N'Cari yaşlandırma 2. eşik (gün).'),
 ('M11','AGING_BUCKET_4_DAYS',         '90', N'Cari yaşlandırma 3. eşik (gün); üstü en yaşlı kova.'),
 ('M11','PARTNER_RECON_DEADLINE_MONTHS','1', N'Cari mutabakat sessiz onay süresi (ay): bu süre dolunca otomatik kabul.'),
 ('M16','AI_INFERENCE_TIMEOUT_SECONDS','120',N'Yerel AI (fiyat farkı gerekçe denetimi) zaman aşımı (saniye).');

INSERT INTO Parameter (Id, CompanyId, ModuleCode, Code, Value, Description)
SELECT NEWID(), c.Id, x.ModuleCode, x.Code, x.Value, x.Descr
FROM Company c
CROSS JOIN @params x
WHERE NOT EXISTS (
    SELECT 1 FROM Parameter p
    WHERE p.CompanyId = c.Id AND p.Code = x.Code AND p.IsDeleted = 0);
GO
