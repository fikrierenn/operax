-- ============================================================
-- Plan 35 Faz 2 — Baseline Referans Tanımı Seed (Fresh-Install Operable)
-- Her şirkete (sistem + demo + gerçek) idempotent referans dict tanımları.
-- İdempotent: tip CompanyId+Code'a göre, değer TypeId+Code'a göre NOT EXISTS korumalı.
-- Set-bazlı: tek script tüm şirketleri kapsar (cursor yok).
-- Kapsam: UOM, TAX_RATE, CURRENCY, PAYMENT_METHOD, PAYMENT_TERM,
--         WITHHOLDING (KDV tevkifat 601-625), ACCOUNT_TYPE, TRANSACTION_TYPE, PARTNER_CATEGORY.
-- ============================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;   -- herhangi bir hata tüm batch'i rollback etsin (yarı-temizlik engeli)

-- ------------------------------------------------------------
-- 0) Mükerrer temizliği (TAX_RATE dup vakası — sistem şirketinde 8 satır → 4)
--    Önce tip mükerreri: aynı CompanyId+Code'da fazla tip varsa değerleri en eski tipe taşı, fazlayı sil.
-- ------------------------------------------------------------
;WITH TypeDup AS (
    SELECT Id, CompanyId, Code,
           ROW_NUMBER() OVER (PARTITION BY CompanyId, Code ORDER BY CreatedAt, Id) AS rn,
           FIRST_VALUE(Id) OVER (PARTITION BY CompanyId, Code ORDER BY CreatedAt, Id) AS KeepId
    FROM DictionaryType WHERE IsDeleted = 0
)
UPDATE dv SET dv.TypeId = td.KeepId
FROM DictionaryValue dv
JOIN TypeDup td ON dv.TypeId = td.Id
WHERE td.rn > 1;

DELETE FROM DictionaryType
WHERE Id IN (
    SELECT Id FROM (
        SELECT Id, ROW_NUMBER() OVER (PARTITION BY CompanyId, Code ORDER BY CreatedAt, Id) AS rn
        FROM DictionaryType WHERE IsDeleted = 0
    ) x WHERE x.rn > 1
);

-- Değer mükerreri: aynı TypeId+Code'da fazla aktif değer varsa en eskisi (KeepId) kalır.
-- ÖNCE bağlı FK referanslarını (ItemUOM.UomId, ItemBarcode.UomId) korunacak satıra taşı —
-- aksi halde fiziksel DELETE, silinecek kopyaya bağlı stok-birimi kaydı varsa FK ihlaliyle (547) patlar.
;WITH ValDup AS (
    SELECT Id, TypeId, Code,
           ROW_NUMBER() OVER (PARTITION BY TypeId, Code ORDER BY CreatedAt, Id) AS rn,
           FIRST_VALUE(Id) OVER (PARTITION BY TypeId, Code ORDER BY CreatedAt, Id) AS KeepId
    FROM DictionaryValue WHERE IsDeleted = 0
)
UPDATE iu SET iu.UomId = vd.KeepId
FROM ItemUOM iu JOIN ValDup vd ON iu.UomId = vd.Id
WHERE vd.rn > 1;

;WITH ValDup AS (
    SELECT Id, TypeId, Code,
           ROW_NUMBER() OVER (PARTITION BY TypeId, Code ORDER BY CreatedAt, Id) AS rn,
           FIRST_VALUE(Id) OVER (PARTITION BY TypeId, Code ORDER BY CreatedAt, Id) AS KeepId
    FROM DictionaryValue WHERE IsDeleted = 0
)
UPDATE ib SET ib.UomId = vd.KeepId
FROM ItemBarcode ib JOIN ValDup vd ON ib.UomId = vd.Id
WHERE vd.rn > 1;

-- SONRA fiziksel DELETE — artık silinecek kopyaya bağlı FK kalmadı.
DELETE FROM DictionaryValue
WHERE Id IN (
    SELECT Id FROM (
        SELECT Id, ROW_NUMBER() OVER (PARTITION BY TypeId, Code ORDER BY CreatedAt, Id) AS rn
        FROM DictionaryValue WHERE IsDeleted = 0
    ) x WHERE x.rn > 1
);
GO

-- ------------------------------------------------------------
-- 1) DictionaryType — her şirkete eksik baseline tipleri ekle
-- ------------------------------------------------------------
INSERT INTO DictionaryType (Id, CompanyId, Code, NameTr, NameEn, IsSystem)
SELECT NEWID(), c.Id, t.Code, t.NameTr, t.NameEn, 1
FROM Company c
CROSS JOIN (VALUES
    ('UOM',              N'Birim',           N'Unit'),
    ('TAX_RATE',         N'KDV Oranı',       N'Tax Rate'),
    ('CURRENCY',         N'Para Birimi',     N'Currency'),
    ('PAYMENT_METHOD',   N'Ödeme Yöntemi',   N'Payment Method'),
    ('PAYMENT_TERM',     N'Ödeme Vadesi',    N'Payment Term'),
    ('WITHHOLDING',      N'KDV Tevkifat Kodu', N'Withholding Code'),
    ('ACCOUNT_TYPE',     N'Hesap Tipi',      N'Account Type'),
    ('TRANSACTION_TYPE', N'Hareket Tipi',    N'Transaction Type'),
    ('PARTNER_CATEGORY', N'Cari Kategori',   N'Partner Category')
) AS t(Code, NameTr, NameEn)
WHERE NOT EXISTS (
    SELECT 1 FROM DictionaryType dt
    WHERE dt.CompanyId = c.Id AND dt.Code = t.Code AND dt.IsDeleted = 0
);
GO

-- ------------------------------------------------------------
-- 2) UOM — birim + UN/ECE kodu + tam-sayı bayrağı (e-Belge: Adet=C62 zorunlu)
-- ------------------------------------------------------------
INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive, UnEceCode, IsWholeNumber)
SELECT NEWID(), dt.CompanyId, dt.Id, v.Code, v.NameTr, v.NameEn, v.OrderNo, 1, v.UnEce, v.IsWhole
FROM DictionaryType dt
CROSS JOIN (VALUES
    ('EACH',   N'Adet',        N'Each',          1,  'C62', CAST(1 AS BIT)),
    ('PACK',   N'Paket',       N'Pack',          2,  'PK',  CAST(1 AS BIT)),
    ('CASE',   N'Koli',        N'Case',          3,  'CS',  CAST(1 AS BIT)),
    ('BOX',    N'Kutu',        N'Box',           4,  'BX',  CAST(1 AS BIT)),
    ('PALLET', N'Palet',       N'Pallet',        5,  'PF',  CAST(1 AS BIT)),
    ('DOZEN',  N'Düzine',      N'Dozen',         6,  'DZN', CAST(1 AS BIT)),
    ('KG',     N'Kilogram',    N'Kilogram',      7,  'KGM', CAST(0 AS BIT)),
    ('GRAM',   N'Gram',        N'Gram',          8,  'GRM', CAST(0 AS BIT)),
    ('TON',    N'Ton',         N'Tonne',         9,  'TNE', CAST(0 AS BIT)),
    ('LITRE',  N'Litre',       N'Litre',         10, 'LTR', CAST(0 AS BIT)),
    ('ML',     N'Mililitre',   N'Millilitre',    11, 'MLT', CAST(0 AS BIT)),
    ('METRE',  N'Metre',       N'Metre',         12, 'MTR', CAST(0 AS BIT)),
    ('CM',     N'Santimetre',  N'Centimetre',    13, 'CMT', CAST(0 AS BIT)),
    ('M2',     N'Metrekare',   N'Square Metre',  14, 'MTK', CAST(0 AS BIT)),
    ('M3',     N'Metreküp',    N'Cubic Metre',   15, 'MTQ', CAST(0 AS BIT))
) AS v(Code, NameTr, NameEn, OrderNo, UnEce, IsWhole)
WHERE dt.Code = 'UOM' AND dt.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM DictionaryValue dv WHERE dv.TypeId = dt.Id AND dv.Code = v.Code AND dv.IsDeleted = 0);
GO

-- ------------------------------------------------------------
-- 3) TAX_RATE — KDV oranları (her şirkete; setup_tax yalnız sistem şirketine basıyordu)
-- ------------------------------------------------------------
INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive)
SELECT NEWID(), dt.CompanyId, dt.Id, v.Code, v.NameTr, v.NameEn, v.OrderNo, 1
FROM DictionaryType dt
CROSS JOIN (VALUES
    ('0',  N'%0',  N'0%',  1),
    ('1',  N'%1',  N'1%',  2),
    ('10', N'%10', N'10%', 3),
    ('20', N'%20', N'20%', 4)
) AS v(Code, NameTr, NameEn, OrderNo)
WHERE dt.Code = 'TAX_RATE' AND dt.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM DictionaryValue dv WHERE dv.TypeId = dt.Id AND dv.Code = v.Code AND dv.IsDeleted = 0);
GO

-- ------------------------------------------------------------
-- 4) CURRENCY — para birimleri (ISO 4217)
-- ------------------------------------------------------------
INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive)
SELECT NEWID(), dt.CompanyId, dt.Id, v.Code, v.NameTr, v.NameEn, v.OrderNo, 1
FROM DictionaryType dt
CROSS JOIN (VALUES
    ('TRY', N'Türk Lirası',      N'Turkish Lira',    1),
    ('USD', N'Amerikan Doları',  N'US Dollar',       2),
    ('EUR', N'Euro',             N'Euro',            3),
    ('GBP', N'İngiliz Sterlini', N'British Pound',   4),
    ('CHF', N'İsviçre Frangı',   N'Swiss Franc',     5),
    ('JPY', N'Japon Yeni',       N'Japanese Yen',    6)
) AS v(Code, NameTr, NameEn, OrderNo)
WHERE dt.Code = 'CURRENCY' AND dt.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM DictionaryValue dv WHERE dv.TypeId = dt.Id AND dv.Code = v.Code AND dv.IsDeleted = 0);
GO

-- ------------------------------------------------------------
-- 5) PAYMENT_METHOD — ödeme yöntemleri
-- ------------------------------------------------------------
INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive)
SELECT NEWID(), dt.CompanyId, dt.Id, v.Code, v.NameTr, v.NameEn, v.OrderNo, 1
FROM DictionaryType dt
CROSS JOIN (VALUES
    ('CASH',           N'Nakit',         N'Cash',            1),
    ('BANK_TRANSFER',  N'Havale / EFT',  N'Bank Transfer',   2),
    ('CREDIT_CARD',    N'Kredi Kartı',   N'Credit Card',     3),
    ('CHEQUE',         N'Çek',           N'Cheque',          4),
    ('PROMISSORY_NOTE',N'Senet',         N'Promissory Note', 5),
    ('OFFSET',         N'Mahsup',        N'Offset',          6),
    ('OTHER',          N'Diğer',         N'Other',           7)
) AS v(Code, NameTr, NameEn, OrderNo)
WHERE dt.Code = 'PAYMENT_METHOD' AND dt.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM DictionaryValue dv WHERE dv.TypeId = dt.Id AND dv.Code = v.Code AND dv.IsDeleted = 0);
GO

-- ------------------------------------------------------------
-- 6) PAYMENT_TERM — ödeme vadeleri
-- ------------------------------------------------------------
INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive)
SELECT NEWID(), dt.CompanyId, dt.Id, v.Code, v.NameTr, v.NameEn, v.OrderNo, 1
FROM DictionaryType dt
CROSS JOIN (VALUES
    ('CASH',  N'Peşin',     N'Cash',     1),
    ('NET7',  N'7 Gün',     N'Net 7',    2),
    ('NET15', N'15 Gün',    N'Net 15',   3),
    ('NET30', N'30 Gün',    N'Net 30',   4),
    ('NET45', N'45 Gün',    N'Net 45',   5),
    ('NET60', N'60 Gün',    N'Net 60',   6),
    ('NET90', N'90 Gün',    N'Net 90',   7),
    ('EOM',   N'Ay Sonu',   N'End of Month', 8)
) AS v(Code, NameTr, NameEn, OrderNo)
WHERE dt.Code = 'PAYMENT_TERM' AND dt.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM DictionaryValue dv WHERE dv.TypeId = dt.Id AND dv.Code = v.Code AND dv.IsDeleted = 0);
GO

-- ------------------------------------------------------------
-- 7) WITHHOLDING — KDV tevkifat kodları (GİB KDV tevkifat kod listesi 601-625)
--    Açıklamalar GİB e-Fatura/e-Arşiv kılavuzu tevkifat tablosundan; oran bilgisi ad içinde informatif.
-- ------------------------------------------------------------
INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive)
SELECT NEWID(), dt.CompanyId, dt.Id, v.Code, v.NameTr, v.NameEn, v.OrderNo, 1
FROM DictionaryType dt
CROSS JOIN (VALUES
    ('601', N'Yapım işleri ve bununla birlikte mühendislik-mimarlık ve etüt-proje hizmetleri', N'Construction works', 1),
    ('602', N'Etüt, plan-proje, danışmanlık, denetim ve benzeri hizmetler',                     N'Survey/consulting',  2),
    ('603', N'Makine, teçhizat, demirbaş ve taşıtlara ait tadil, bakım ve onarım hizmetleri',    N'Maintenance/repair',  3),
    ('604', N'Yemek servis hizmeti',                                                             N'Catering',            4),
    ('605', N'Organizasyon hizmeti',                                                             N'Organization svc',    5),
    ('606', N'İşgücü temin hizmetleri',                                                          N'Labor supply',        6),
    ('607', N'Özel güvenlik hizmeti',                                                            N'Private security',    7),
    ('608', N'Yapı denetim hizmeti',                                                             N'Building inspection', 8),
    ('609', N'Fason tekstil, konfeksiyon, çanta ve ayakkabı dikim işleri',                       N'Textile contract',    9),
    ('610', N'Turistik mağazalara verilen müşteri bulma/götürme hizmetleri',                     N'Tourist referral',    10),
    ('611', N'Spor kulüplerinin yayın, reklam ve isim hakkı gelirleri',                          N'Sports club rights',  11),
    ('612', N'Temizlik, çevre ve bahçe bakım hizmetleri',                                        N'Cleaning/garden',     12),
    ('613', N'Servis taşımacılığı hizmeti',                                                      N'Shuttle transport',   13),
    ('614', N'Her türlü baskı ve basım hizmetleri',                                              N'Printing',            14),
    ('615', N'Yukarıda belirlenenler dışındaki hizmetler (kamu)',                                N'Other public svc',    15),
    ('616', N'Hurda metalden elde edilen külçe teslimleri',                                      N'Scrap ingot',         16),
    ('617', N'Bakır, çinko, alüminyum ve kurşun ürünlerinin teslimi',                            N'Copper/zinc/etc',     17),
    ('618', N'İstisnadan vazgeçenlerin hurda ve atık teslimi',                                   N'Scrap (opt-out)',     18),
    ('619', N'Metal, plastik, kağıt, cam hurda ve atıklarından elde edilen hammadde teslimi',    N'Recycled material',   19),
    ('620', N'Pamuk, tiftik, yün ve yapağı ile ham post ve deri teslimleri',                     N'Raw fiber/hide',      20),
    ('621', N'Ağaç ve orman ürünleri teslimi',                                                   N'Wood/forest',         21),
    ('622', N'Yük taşımacılığı hizmeti',                                                          N'Freight transport',   22),
    ('623', N'Ticari reklam hizmetleri',                                                          N'Commercial ads',      23),
    ('624', N'Demir-çelik ürünlerinin teslimi',                                                  N'Iron-steel goods',    24),
    ('625', N'Diğer teslim ve hizmetler',                                                        N'Other deliveries',    25)
) AS v(Code, NameTr, NameEn, OrderNo)
WHERE dt.Code = 'WITHHOLDING' AND dt.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM DictionaryValue dv WHERE dv.TypeId = dt.Id AND dv.Code = v.Code AND dv.IsDeleted = 0);
GO

-- ------------------------------------------------------------
-- 8) ACCOUNT_TYPE — finansal hesap tipleri (schema_M11_Finance: CASH, BANK, CREDIT_CARD, LOAN, POS)
-- ------------------------------------------------------------
INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive)
SELECT NEWID(), dt.CompanyId, dt.Id, v.Code, v.NameTr, v.NameEn, v.OrderNo, 1
FROM DictionaryType dt
CROSS JOIN (VALUES
    ('CASH',        N'Kasa',         N'Cash',         1),
    ('BANK',        N'Banka',        N'Bank',         2),
    ('CREDIT_CARD', N'Kredi Kartı',  N'Credit Card',  3),
    ('LOAN',        N'Kredi',        N'Loan',         4),
    ('POS',         N'POS Cihazı',   N'POS Device',   5)
) AS v(Code, NameTr, NameEn, OrderNo)
WHERE dt.Code = 'ACCOUNT_TYPE' AND dt.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM DictionaryValue dv WHERE dv.TypeId = dt.Id AND dv.Code = v.Code AND dv.IsDeleted = 0);
GO

-- ------------------------------------------------------------
-- 9) TRANSACTION_TYPE — finansal hareket tipleri (schema_M11_Finance: INCOME, EXPENSE, TRANSFER_IN, TRANSFER_OUT)
-- ------------------------------------------------------------
INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive)
SELECT NEWID(), dt.CompanyId, dt.Id, v.Code, v.NameTr, v.NameEn, v.OrderNo, 1
FROM DictionaryType dt
CROSS JOIN (VALUES
    ('INCOME',       N'Tahsilat / Gelir', N'Income',       1),
    ('EXPENSE',      N'Ödeme / Gider',    N'Expense',      2),
    ('TRANSFER_IN',  N'Virman Giriş',     N'Transfer In',  3),
    ('TRANSFER_OUT', N'Virman Çıkış',     N'Transfer Out', 4)
) AS v(Code, NameTr, NameEn, OrderNo)
WHERE dt.Code = 'TRANSACTION_TYPE' AND dt.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM DictionaryValue dv WHERE dv.TypeId = dt.Id AND dv.Code = v.Code AND dv.IsDeleted = 0);
GO

-- ------------------------------------------------------------
-- 10) PARTNER_CATEGORY — cari kategorileri
-- ------------------------------------------------------------
INSERT INTO DictionaryValue (Id, CompanyId, TypeId, Code, NameTr, NameEn, OrderNo, IsActive)
SELECT NEWID(), dt.CompanyId, dt.Id, v.Code, v.NameTr, v.NameEn, v.OrderNo, 1
FROM DictionaryType dt
CROSS JOIN (VALUES
    ('CUSTOMER', N'Müşteri',             N'Customer',          1),
    ('SUPPLIER', N'Tedarikçi',           N'Supplier',          2),
    ('BOTH',     N'Müşteri + Tedarikçi', N'Customer+Supplier', 3),
    ('CARRIER',  N'Taşıyıcı',            N'Carrier',           4),
    ('EMPLOYEE', N'Personel',            N'Employee',          5),
    ('OTHER',    N'Diğer',               N'Other',             6)
) AS v(Code, NameTr, NameEn, OrderNo)
WHERE dt.Code = 'PARTNER_CATEGORY' AND dt.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM DictionaryValue dv WHERE dv.TypeId = dt.Id AND dv.Code = v.Code AND dv.IsDeleted = 0);
GO

PRINT 'seed_reference.sql tamamlandı — 9 baseline referans tipi tüm şirketlere idempotent yazıldı.';
