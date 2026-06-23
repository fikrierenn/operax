-- Plan 26 Faz B: Kırılımlı gider raporlama nesneleri
-- v_ExpenseDistribution genişletme + tvf_ExpenseBreakdown
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- -----------------------------------------------------------------------------
-- v_ExpenseDistribution (genişletildi): ad join + COALESCE(Line, Header) merkez
-- POSTED gider faturası satırları; cari defteri kirletmeden belge katmanından rapor.
-- -----------------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.v_ExpenseDistribution
AS
SELECT
    ei.CompanyId,
    ei.Id            AS InvoiceId,
    ei.DocNo,
    ei.PartnerId,
    p.Name           AS PartnerName,
    ei.InvoiceDate,
    l.ExpenseTypeId,
    et.Name          AS ExpenseTypeName,
    -- Kalem merkezi yoksa başlık merkezinden miras (başlık default + kalem override)
    COALESCE(l.CostCenterId, ei.CostCenterId) AS CostCenterId,
    cc.Name          AS CostCenterName,
    l.Amount         AS NetAmount,
    l.TaxAmount,
    l.TotalAmount
FROM ExpenseInvoice ei
JOIN ExpenseInvoiceLine l ON l.ExpenseInvoiceId = ei.Id
LEFT JOIN Partner p     ON p.Id = ei.PartnerId
LEFT JOIN ExpenseType et ON et.Id = l.ExpenseTypeId
LEFT JOIN CostCenter cc ON cc.Id = COALESCE(l.CostCenterId, ei.CostCenterId)
WHERE ei.Status = 'POSTED';
GO

-- -----------------------------------------------------------------------------
-- tvf_ExpenseBreakdown — gider merkezi × gider tipi kırılımlı toplam
-- GL'siz, belge katmanından. Tarih aralığı InvoiceDate bazlı.
-- COALESCE(Line.CostCenterId, Header.CostCenterId) ile başlık miras.
-- -----------------------------------------------------------------------------
CREATE OR ALTER FUNCTION dbo.tvf_ExpenseBreakdown
(
    @CompanyId UNIQUEIDENTIFIER,
    @FromDate  DATE,
    @ToDate    DATE
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        COALESCE(l.CostCenterId, ei.CostCenterId) AS CostCenterId,
        cc.Name        AS CostCenterName,
        l.ExpenseTypeId,
        et.Name        AS ExpenseTypeName,
        et.UnitOfMeasure AS UnitOfMeasure,                       -- metered birim (kWh/m3; NULL=götürü)
        SUM(l.Quantity)    AS TotalQuantity,                     -- toplam tüketim (metered'da kWh/m3 trendi)
        SUM(l.Amount)      AS NetAmount,
        SUM(l.TaxAmount)   AS TaxAmount,
        SUM(l.TotalAmount) AS TotalAmount,
        -- Birim-maliyet = net tutar / miktar (KDV HARİÇ; farklı KDV oranı kıyası bozmasın diye net üzerinden).
        -- Yalnız miktar>0 olduğunda anlamlı (NULLIF ile 0'a bölme engeli); götürü giderde miktar=1 → net'e eşit.
        CASE WHEN SUM(l.Quantity) > 0 THEN SUM(l.Amount) / SUM(l.Quantity) ELSE 0 END AS UnitCost,
        COUNT(*)           AS LineCount
    FROM ExpenseInvoice ei
    JOIN ExpenseInvoiceLine l ON l.ExpenseInvoiceId = ei.Id
    LEFT JOIN ExpenseType et ON et.Id = l.ExpenseTypeId
    LEFT JOIN CostCenter cc ON cc.Id = COALESCE(l.CostCenterId, ei.CostCenterId)
    WHERE ei.CompanyId = @CompanyId
      AND ei.Status = 'POSTED'
      AND ei.InvoiceDate >= @FromDate
      AND ei.InvoiceDate <= @ToDate
    GROUP BY COALESCE(l.CostCenterId, ei.CostCenterId), cc.Name, l.ExpenseTypeId, et.Name, et.UnitOfMeasure
);
GO
