-- =============================================================================
-- M00 Belge Seri Yönetimi — NumberSeries (Plan 10)
-- Tüm otomatik numaralama (cari kod, fatura no, sipariş no, çek no…) tek yerden.
-- Atomik artış: sp_NextNumber (UPDATE OUTPUT — eşzamanlı çakışmaya dayanıklı).
-- Idempotent.
-- =============================================================================
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NumberSeries')
BEGIN
    CREATE TABLE NumberSeries
    (
        Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NumberSeries PRIMARY KEY DEFAULT NEWID(),
        CompanyId   UNIQUEIDENTIFIER NOT NULL,
        DocType     NVARCHAR(40)     NOT NULL,   -- PARTNER_CUST / PARTNER_VEND / SALES_INVOICE / PURCHASE_ORDER …
        Prefix      NVARCHAR(15)     NOT NULL DEFAULT '',
        NextNo      INT              NOT NULL DEFAULT 1,
        Padding     TINYINT          NOT NULL DEFAULT 6,    -- 6 → 000001
        Separator   NVARCHAR(3)      NOT NULL DEFAULT '-',
        ResetYearly BIT              NOT NULL DEFAULT 0,
        CurrentYear INT              NULL,
        IsActive    BIT              NOT NULL DEFAULT 1,
        IsDeleted   BIT              NOT NULL DEFAULT 0,
        CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy   NVARCHAR(450)    NULL,
        UpdatedAt   DATETIME2        NULL,
        UpdatedBy   NVARCHAR(450)    NULL,
        CONSTRAINT UQ_NumberSeries_Company_DocType UNIQUE (CompanyId, DocType)
    );
    PRINT 'NumberSeries tablosu oluşturuldu.';
END
GO

-- Varsayılan seriler — her aktif şirket için (yoksa ekle)
INSERT INTO NumberSeries (Id, CompanyId, DocType, Prefix, NextNo, Padding)
SELECT NEWID(), c.Id, x.DocType, x.Prefix, 1, 6
FROM Company c
CROSS JOIN (VALUES
    ('PARTNER_CUST',    'MUS'),   -- Müşteri
    ('PARTNER_VEND',    'TED'),   -- Tedarikçi
    ('PARTNER_BOTH',    'CAR'),   -- Cari (her ikisi)
    ('SALES_INVOICE',   'SFT'),   -- Satış Faturası
    ('PURCHASE_INVOICE','AFT'),   -- Alış Faturası
    ('SALES_ORDER',     'SSP'),   -- Satış Siparişi
    ('PURCHASE_ORDER',  'ASP'),   -- Alış Siparişi
    ('CHEQUE',          'CEK'),   -- Çek
    ('PROMISSORY_NOTE', 'SNT')    -- Senet
) AS x(DocType, Prefix)
WHERE c.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM NumberSeries ns WHERE ns.CompanyId = c.Id AND ns.DocType = x.DocType);
GO

-- Eski İngilizce prefix'leri Türkçeye normalle (önceden seed edilmiş DB'ler için)
UPDATE NumberSeries SET Prefix = CASE Prefix
        WHEN 'CUS' THEN 'MUS' WHEN 'SUP' THEN 'TED' WHEN 'CARI' THEN 'CAR'
        WHEN 'SI'  THEN 'SFT' WHEN 'ALN' THEN 'AFT'
        WHEN 'SO'  THEN 'SSP' WHEN 'PO'  THEN 'ASP' ELSE Prefix END
WHERE Prefix IN ('CUS', 'SUP', 'CARI', 'SI', 'ALN', 'SO', 'PO');
GO

-- Padding 3 (eski default) → 6 normalle
UPDATE NumberSeries SET Padding = 6 WHERE Padding = 3;
GO

-- Mevcut cari kodlarını yeni Türkçe öneke taşı (kodlar Id ile referanslı → güvenli)
UPDATE Partner SET Code = 'MUS' + SUBSTRING(Code, 4, 50) WHERE Code LIKE 'CUS-%';
UPDATE Partner SET Code = 'TED' + SUBSTRING(Code, 4, 50) WHERE Code LIKE 'SUP-%';
GO

-- Mevcut cari kodlarıyla çakışmayı önle: partner serilerinin NextNo'sunu var olan en yüksek
-- numaranın bir fazlasına çek (kodlar değişmez → her zaman güvenli).
UPDATE ns
   SET NextNo = ISNULL((
        SELECT MAX(TRY_CONVERT(int, SUBSTRING(p.Code, CHARINDEX('-', p.Code) + 1, 10)))
        FROM Partner p
        WHERE p.CompanyId = ns.CompanyId
          AND p.Code LIKE ns.Prefix + '-%'), 0) + 1
FROM NumberSeries ns
WHERE ns.DocType IN ('PARTNER_CUST', 'PARTNER_VEND', 'PARTNER_BOTH')
  AND ns.NextNo = 1;  -- yalnızca taze seri (çalışan seriyi geri sarma)
GO

-- Atomik sıradaki numara — UPDATE OUTPUT ile race-safe. Biçimlenmiş kod döner (Prefix-NNN).
CREATE OR ALTER PROCEDURE dbo.sp_NextNumber
    @CompanyId UNIQUEIDENTIFIER,
    @DocType   NVARCHAR(40),
    @Code      NVARCHAR(40) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @out TABLE (NextNo INT, Prefix NVARCHAR(15), Padding TINYINT, Separator NVARCHAR(3));

    -- Atomik artış: alınan değeri OUTPUT ile yakala (kilit + tek transaction)
    UPDATE NumberSeries
       SET NextNo = NextNo + 1, UpdatedAt = GETUTCDATE()
    OUTPUT deleted.NextNo, deleted.Prefix, deleted.Padding, deleted.Separator INTO @out
     WHERE CompanyId = @CompanyId AND DocType = @DocType AND IsActive = 1 AND IsDeleted = 0;

    -- Seri tanımlı değilse hata (kurulumda seed edilir)
    IF NOT EXISTS (SELECT 1 FROM @out)
        THROW 50050, N'Belge serisi tanımlı değil. Önce Belge Serileri ayarından tanımlayın.', 1;

    SELECT @Code = Prefix + Separator + RIGHT(REPLICATE('0', Padding) + CAST(NextNo AS NVARCHAR(10)), Padding)
    FROM @out;
END
GO
