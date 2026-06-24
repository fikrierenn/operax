-- ============================================================
-- Plan 42 — Enum etiketleri sözlük seed (tek kaynak, global/IsSystem)
-- Idempotent: tip + değer NOT EXISTS guard'lı; POSTED/APPROVED drift UPDATE ile düzeltilir.
-- Yaşam-döngüsü-bazlı tipler (modül değil). CompanyId=0 → global (servis şirket→global fallback).
-- ============================================================
SET XACT_ABORT ON;
SET NOCOUNT ON;
DECLARE @G UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- ---- Tip tanımları (Code, NameTr, NameEn) ----
DECLARE @T TABLE (Code NVARCHAR(40), NameTr NVARCHAR(100), NameEn NVARCHAR(100));
INSERT INTO @T VALUES
 ('SOURCE_DOC_TYPE','Kaynak Belge Tipi','Source Document Type'),
 ('MOV_TYPE','Stok Hareket Tipi','Stock Movement Type'),
 ('INSTRUMENT_TYPE','Finansal Araç Tipi','Instrument Type'),
 ('ITEM_TYPE','Ürün Tipi','Item Type'),
 ('PRODUCTION_STATUS','Üretim Emri Durumu','Production Status'),
 ('PICKTASK_STATUS','Toplama Görevi Durumu','Pick Task Status'),
 ('RECEIVING_STATUS','Mal Kabul Durumu','Receiving Status'),
 ('CHEQUE_STATUS','Çek/Senet Durumu','Cheque Status'),
 ('LOAN_STATUS','Kredi Durumu','Loan Status'),
 ('LOAN_CALC_METHOD','Kredi Hesap Yöntemi','Loan Calc Method'),
 ('CARD_TYPE','Kredi Kartı Tipi','Card Type'),
 ('RISK_CATEGORY','Risk Kategorisi','Risk Category'),
 ('BRANCH_TYPE','Şube Tipi','Branch Type'),
 ('PAYMENT_PLAN_STATUS','Ödeme Planı Durumu','Payment Plan Status'),
 ('SERIAL_STATUS','Seri No Durumu','Serial Status'),
 ('LOT_STATUS','Lot Durumu','Lot Status'),
 ('LPN_STATUS','Taşıma Birimi Durumu','LPN Status'),
 ('LPN_TYPE','Taşıma Birimi Tipi','LPN Type'),
 ('BUDGET_STATUS','Bütçe Durumu','Budget Status'),
 ('BUDGET_TYPE','Bütçe Tipi','Budget Type'),
 ('UDF_FIELD_TYPE','UDF Alan Tipi','UDF Field Type'),
 ('PAYMENT_TERM_POLICY','Ödeme Vade Politikası','Payment Term Policy'),
 ('PARTNER_TYPE','Cari Tipi','Partner Type');

-- Eksik tipleri ekle (mevcut STATUS/ACCOUNT_TYPE/PAYMENT_METHOD/TRANSACTION_TYPE/CURRENCY/UOM... dokunma)
INSERT INTO DictionaryType (Id, CompanyId, Code, NameTr, NameEn, IsSystem, CreatedAt, IsDeleted)
SELECT NEWID(), @G, t.Code, t.NameTr, t.NameEn, 1, GETUTCDATE(), 0
FROM @T t
WHERE NOT EXISTS (SELECT 1 FROM DictionaryType d WHERE d.Code = t.Code);

-- ---- Değer tanımları (TypeCode, Code, NameTr, NameEn, OrderNo) ----
DECLARE @V TABLE (TypeCode NVARCHAR(40), Code NVARCHAR(40), NameTr NVARCHAR(120), NameEn NVARCHAR(120), OrderNo INT);
INSERT INTO @V VALUES
-- DOC_STATUS (mevcut STATUS tipine eksik kodları ekle)
 ('STATUS','RECEIVED','Teslim Alındı','Received',40),
 ('STATUS','COMPLETED','Tamamlandı','Completed',50),
 ('STATUS','COUNTING','Sayılıyor','Counting',60),
 ('STATUS','PAID','Ödendi','Paid',70),
 ('STATUS','PARTIAL','Kısmi','Partial',80),
 ('STATUS','REJECTED','Reddedildi','Rejected',90),
 ('STATUS','PENDING','Bekliyor','Pending',100),
 ('STATUS','CLOSED','Kapandı','Closed',110),
 ('STATUS','CLOSED_PARTIAL','Kısmen Kapandı','Partially Closed',120),
-- SOURCE_DOC_TYPE
 ('SOURCE_DOC_TYPE','SALES_INVOICE','Satış Faturası','Sales Invoice',10),
 ('SOURCE_DOC_TYPE','PURCHASE_INVOICE','Alış Faturası','Purchase Invoice',20),
 ('SOURCE_DOC_TYPE','COLLECTION','Tahsilat','Collection',30),
 ('SOURCE_DOC_TYPE','PAYMENT','Ödeme','Payment',40),
 ('SOURCE_DOC_TYPE','INCOME','Tahsilat','Income',50),
 ('SOURCE_DOC_TYPE','EXPENSE','Ödeme','Expense',60),
 ('SOURCE_DOC_TYPE','CHEQUE_COLLECTION','Çek Tahsilatı','Cheque Collection',70),
 ('SOURCE_DOC_TYPE','LOAN_INSTALLMENT','Kredi Taksiti','Loan Installment',80),
 ('SOURCE_DOC_TYPE','REVERSAL','İptal / Ters Kayıt','Reversal',90),
 ('SOURCE_DOC_TYPE','CANCELLED','İptal','Cancelled',100),
 ('SOURCE_DOC_TYPE','FX_DIFF','Kur Farkı','FX Difference',110),
 ('SOURCE_DOC_TYPE','BACKFILL','Devir','Carry Forward',120),
 ('SOURCE_DOC_TYPE','SEED','Açılış','Opening',130),
 ('SOURCE_DOC_TYPE','AVANS','Avans','Advance',140),
 ('SOURCE_DOC_TYPE','RECEIVING','Mal Kabul','Receiving',150),
 ('SOURCE_DOC_TYPE','SHIPPING','Sevkiyat','Shipping',160),
 ('SOURCE_DOC_TYPE','TRANSFER','Transfer','Transfer',170),
 ('SOURCE_DOC_TYPE','CYCLE_COUNT','Sayım','Cycle Count',180),
 ('SOURCE_DOC_TYPE','COUNT','Sayım','Count',185),
 ('SOURCE_DOC_TYPE','PRODUCTION','Üretim','Production',190),
 ('SOURCE_DOC_TYPE','CONSUMPTION','Sarfiyat','Consumption',200),
 ('SOURCE_DOC_TYPE','PICKING','Toplama','Picking',210),
 ('SOURCE_DOC_TYPE','PURCHASE_ORDER','Satınalma Siparişi','Purchase Order',220),
 ('SOURCE_DOC_TYPE','SALES_ORDER','Satış Siparişi','Sales Order',230),
-- MOV_TYPE
 ('MOV_TYPE','RECEIPT','Giriş','Receipt',10),
 ('MOV_TYPE','ISSUE','Çıkış','Issue',20),
 ('MOV_TYPE','TRANSFER','Transfer','Transfer',30),
 ('MOV_TYPE','COUNT_ADJ','Sayım Düzeltmesi','Count Adjustment',40),
 ('MOV_TYPE','PRODUCTION','Üretim','Production',50),
-- INSTRUMENT_TYPE
 ('INSTRUMENT_TYPE','CASH','Nakit','Cash',10),
 ('INSTRUMENT_TYPE','EFT','EFT','EFT',20),
 ('INSTRUMENT_TYPE','GIRO','Havale','Giro',30),
 ('INSTRUMENT_TYPE','CHEQUE','Çek','Cheque',40),
 ('INSTRUMENT_TYPE','NOTE','Senet','Promissory Note',50),
 ('INSTRUMENT_TYPE','CARD','Kart','Card',60),
 ('INSTRUMENT_TYPE','LOAN','Kredi','Loan',70),
 ('INSTRUMENT_TYPE','CC','Kredi Kartı','Credit Card',80),
-- ITEM_TYPE
 ('ITEM_TYPE','STOCK','Stok','Stock',10),
 ('ITEM_TYPE','CONSUMABLE','Sarf Malzeme','Consumable',20),
 ('ITEM_TYPE','SERVICE','Hizmet','Service',30),
 ('ITEM_TYPE','FIXED_ASSET','Demirbaş','Fixed Asset',40),
-- PRODUCTION_STATUS
 ('PRODUCTION_STATUS','DRAFT','Taslak','Draft',10),
 ('PRODUCTION_STATUS','IN_PROGRESS','Üretimde','In Progress',20),
 ('PRODUCTION_STATUS','COMPLETED','Tamamlandı','Completed',30),
 ('PRODUCTION_STATUS','CANCELLED','İptal','Cancelled',40),
-- PICKTASK_STATUS
 ('PICKTASK_STATUS','DRAFT','Oluşturuldu','Created',10),
 ('PICKTASK_STATUS','RELEASED','Havuzda','Released',15),
 ('PICKTASK_STATUS','ASSIGNED','Atandı','Assigned',20),
 ('PICKTASK_STATUS','IN_PROGRESS','Toplanıyor','In Progress',30),
 ('PICKTASK_STATUS','COMPLETED','Tamamlandı','Completed',40),
 ('PICKTASK_STATUS','CANCELLED','İptal','Cancelled',50),
-- RECEIVING_STATUS (mal kabul: POSTED='Tamamlandı' — global STATUS='İşlendi' kanonik korunur, modül-özel ezme)
 ('RECEIVING_STATUS','DRAFT','Taslak','Draft',10),
 ('RECEIVING_STATUS','POSTED','Tamamlandı','Completed',20),
 ('RECEIVING_STATUS','CANCELLED','İptal','Cancelled',30),
-- CHEQUE_STATUS
 ('CHEQUE_STATUS','PORTFOLIO','Portföyde','Portfolio',10),
 ('CHEQUE_STATUS','IN_BANK','Bankada','In Bank',20),
 ('CHEQUE_STATUS','COLLECTED','Tahsil Edildi','Collected',30),
 ('CHEQUE_STATUS','RETURNED','Karşılıksız','Returned',40),
 ('CHEQUE_STATUS','PAID','Ödendi','Paid',50),
 ('CHEQUE_STATUS','ENDORSED','Ciro Edildi','Endorsed',60),
-- LOAN_STATUS
 ('LOAN_STATUS','ACTIVE','Aktif','Active',10),
 ('LOAN_STATUS','CLOSED','Kapandı','Closed',20),
 ('LOAN_STATUS','RESTRUCTURED','Yapılandırıldı','Restructured',30),
-- LOAN_CALC_METHOD
 ('LOAN_CALC_METHOD','ANUITE','Anüite','Annuity',10),
 ('LOAN_CALC_METHOD','EQUAL_PRINCIPAL','Eşit Anapara','Equal Principal',20),
 ('LOAN_CALC_METHOD','BALLOON','Balon Ödemeli','Balloon',30),
 ('LOAN_CALC_METHOD','SPOT','Spot','Spot',40),
 ('LOAN_CALC_METHOD','ROTATIVE','Rotatif','Rotative',50),
 ('LOAN_CALC_METHOD','KMH','KMH','Overdraft',60),
 ('LOAN_CALC_METHOD','DBS','DBS','Direct Debit',70),
-- CARD_TYPE
 ('CARD_TYPE','CREDIT','Kredi Kartı','Credit',10),
 ('CARD_TYPE','BUSINESS','Ticari Kart','Business',20),
 ('CARD_TYPE','CORPORATE','Kurumsal Kart','Corporate',30),
 ('CARD_TYPE','DEBIT','Banka Kartı','Debit',40),
-- RISK_CATEGORY
 ('RISK_CATEGORY','LOW','Düşük','Low',10),
 ('RISK_CATEGORY','MEDIUM','Orta','Medium',20),
 ('RISK_CATEGORY','HIGH','Yüksek','High',30),
 ('RISK_CATEGORY','BLOCKED','Bloke','Blocked',40),
-- BRANCH_TYPE
 ('BRANCH_TYPE','SUBE','Şube','Branch',10),
 ('BRANCH_TYPE','MERKEZ','Merkez','Headquarters',20),
 ('BRANCH_TYPE','FABRIKA','Fabrika','Factory',30),
 ('BRANCH_TYPE','MAGAZA','Mağaza','Store',40),
 ('BRANCH_TYPE','OFIS','Ofis','Office',50),
-- PAYMENT_PLAN_STATUS
 ('PAYMENT_PLAN_STATUS','OPEN','Açık','Open',10),
 ('PAYMENT_PLAN_STATUS','PARTIAL','Kısmi','Partial',20),
 ('PAYMENT_PLAN_STATUS','PAID','Ödendi','Paid',30),
 ('PAYMENT_PLAN_STATUS','OVERDUE','Gecikmiş','Overdue',40),
 ('PAYMENT_PLAN_STATUS','CANCELLED','İptal','Cancelled',50),
-- SERIAL_STATUS
 ('SERIAL_STATUS','IN_STOCK','Stokta','In Stock',10),
 ('SERIAL_STATUS','SHIPPED','Sevk Edildi','Shipped',20),
 ('SERIAL_STATUS','SCRAPPED','Hurda','Scrapped',30),
 ('SERIAL_STATUS','QUARANTINE','Karantina','Quarantine',40),
-- LOT_STATUS
 ('LOT_STATUS','AVAILABLE','Kullanılabilir','Available',10),
 ('LOT_STATUS','QUARANTINE','Karantina','Quarantine',20),
 ('LOT_STATUS','BLOCKED','Bloke','Blocked',30),
-- LPN_STATUS
 ('LPN_STATUS','IN_USE','Kullanımda','In Use',10),
 ('LPN_STATUS','AVAILABLE','Boş / Hazır','Available',20),
 ('LPN_STATUS','LOADED','Yüklendi','Loaded',30),
 ('LPN_STATUS','SHIPPED','Sevk Edildi','Shipped',40),
-- LPN_TYPE
 ('LPN_TYPE','PALLET','Palet','Pallet',10),
 ('LPN_TYPE','BOX','Kutu','Box',20),
 ('LPN_TYPE','CARTON','Koli','Carton',30),
-- BUDGET_STATUS
 ('BUDGET_STATUS','DRAFT','Taslak','Draft',10),
 ('BUDGET_STATUS','APPROVED','Onaylı','Approved',20),
 ('BUDGET_STATUS','CLOSED','Kapalı','Closed',30),
-- BUDGET_TYPE
 ('BUDGET_TYPE','OPERATIONAL','Operasyonel','Operational',10),
 ('BUDGET_TYPE','CASH_FLOW','Nakit Akış','Cash Flow',20),
 ('BUDGET_TYPE','INVESTMENT','Yatırım','Investment',30),
-- UDF_FIELD_TYPE
 ('UDF_FIELD_TYPE','BOOLEAN','Evet/Hayır','Boolean',10),
 ('UDF_FIELD_TYPE','DATE','Tarih','Date',20),
 ('UDF_FIELD_TYPE','NUMBER','Sayı','Number',30),
 ('UDF_FIELD_TYPE','SELECT','Seçim Listesi','Select',40),
 ('UDF_FIELD_TYPE','TEXT','Metin','Text',50),
-- PAYMENT_TERM_POLICY
 ('PAYMENT_TERM_POLICY','NET','Net Gün','Net Days',10),
 ('PAYMENT_TERM_POLICY','NET_EOM','Ay Sonu','Net EOM',20),
 ('PAYMENT_TERM_POLICY','INSTALLMENTS','Taksitli','Installments',30),
-- PARTNER_TYPE
 ('PARTNER_TYPE','CUSTOMER','Müşteri','Customer',10),
 ('PARTNER_TYPE','VENDOR','Tedarikçi','Vendor',20),
 ('PARTNER_TYPE','BOTH','Her İkisi','Both',30);

-- Eksik değerleri ekle (TypeId koddan çözülür; global, NOT EXISTS guard)
INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive, CreatedAt, IsDeleted)
SELECT NEWID(), @G, dt.Id, v.Code, v.NameTr, v.NameEn, v.OrderNo, 1, GETUTCDATE(), 0
FROM @V v
JOIN DictionaryType dt ON dt.Code = v.TypeCode
WHERE NOT EXISTS (
    SELECT 1 FROM DictionaryValue dv
    WHERE dv.TypeId = dt.Id AND dv.Code = v.Code AND dv.CompanyId = @G);

-- DRIFT düzeltme: STATUS tipinde POSTED→İşlendi, APPROVED→Onaylandı (kanonik; kullanıcı kararı 2026-06-22)
UPDATE dv SET dv.NameTr = N'İşlendi', dv.NameEn = 'Posted'
FROM DictionaryValue dv JOIN DictionaryType dt ON dt.Id = dv.TypeId
WHERE dt.Code = 'STATUS' AND dv.Code = 'POSTED';

UPDATE dv SET dv.NameTr = N'Onaylandı', dv.NameEn = 'Approved'
FROM DictionaryValue dv JOIN DictionaryType dt ON dt.Id = dv.TypeId
WHERE dt.Code = 'STATUS' AND dv.Code = 'APPROVED';
GO
