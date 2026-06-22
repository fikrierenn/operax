-- =============================================================================
-- STARTER paketi için Stored Procedure'ler
-- Modüller: M02 Maliyet, M03 Satınalma, M04 Satış+Fatura, M11 Finans
-- Tüm SP'ler CREATE OR ALTER (idempotent, tekrar tekrar çalıştırılabilir)
-- =============================================================================
SET NOCOUNT ON;
GO

-- =============================================================================
-- sp_UpdateItemCostMovingAvg — Hareketli ağırlıklı ortalama maliyet güncelleme
-- NE YAPAR: Bir stok hareketi (RECEIPT/ISSUE/ADJUSTMENT) sonrasında ItemCost
--   tablosundaki AvgCost ve OnHandQty değerlerini günceller; kayıt yoksa oluşturur.
-- PARAMETRELERİ:
--   @CompanyId   UNIQUEIDENTIFIER — şirket kimliği
--   @ItemId      UNIQUEIDENTIFIER — malzeme kimliği
--   @WarehouseId UNIQUEIDENTIFIER (NULL) — depo bazlı maliyet; NULL = genel
--   @Qty         DECIMAL(18,6) — hareket miktarı (hep pozitif, yön @MovementType ile)
--   @UnitCost    DECIMAL(18,4) (NULL) — RECEIPT'ta birim maliyet; ISSUE'da NULL
--   @MovementType NVARCHAR(20) — RECEIPT | ISSUE | ADJUSTMENT
-- SIDE EFFECTS: ItemCost (UPSERT — AvgCost, OnHandQty, LastReceiptDate, UpdatedAt)
-- THROW: yok (hata fırlatmaz; eksik kayıt varsa INSERT ile düzeltir)
-- BAĞIMLILIK: sp_ReceivingPost, sp_ShippingPost (her ikisi de bu SP'yi çağırır)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_UpdateItemCostMovingAvg
    @CompanyId   UNIQUEIDENTIFIER,
    @ItemId      UNIQUEIDENTIFIER,
    @WarehouseId UNIQUEIDENTIFIER = NULL,
    @Qty         DECIMAL(18,6),       -- pozitif (RECEIPT) veya pozitif (ISSUE — adet, isaret @MovementType'tan)
    @UnitCost    DECIMAL(18,4) = NULL, -- RECEIPT'ta dolu, ISSUE'da NULL (mevcut AvgCost kullanilir)
    @MovementType NVARCHAR(20)        -- RECEIPT, ISSUE, ADJUSTMENT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Item cost satiri yoksa olustur
    IF NOT EXISTS (
        SELECT 1 FROM ItemCost
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId
          AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL))
    )
    BEGIN
        INSERT INTO ItemCost (Id, CompanyId, ItemId, WarehouseId, AvgCost, OnHandQty)
        VALUES (NEWID(), @CompanyId, @ItemId, @WarehouseId, 0, 0);
    END

    DECLARE @OldAvg DECIMAL(18,4), @OldQty DECIMAL(18,6);
    SELECT @OldAvg = AvgCost, @OldQty = OnHandQty
    FROM ItemCost
    WHERE CompanyId = @CompanyId AND ItemId = @ItemId
      AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL));

    IF @MovementType = 'RECEIPT'
    BEGIN
        -- İş kuralı: Hareketli ağırlıklı ortalama formülü
        DECLARE @NewQty DECIMAL(18,6) = @OldQty + @Qty;
        DECLARE @NewAvg DECIMAL(18,4) =
            CASE WHEN @NewQty > 0
                 THEN ((@OldQty * @OldAvg) + (@Qty * ISNULL(@UnitCost, @OldAvg))) / @NewQty
                 ELSE ISNULL(@UnitCost, @OldAvg)
            END;
        UPDATE ItemCost
        SET AvgCost          = @NewAvg,
            OnHandQty        = @NewQty,
            LastReceiptDate  = GETUTCDATE(),
            UpdatedAt        = GETUTCDATE()
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId
          AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL));
    END
    ELSE IF @MovementType IN ('ISSUE', 'ADJUSTMENT')
    BEGIN
        -- Çıkışta AvgCost değişmez; sadece OnHandQty düşer
        UPDATE ItemCost
        SET OnHandQty = OnHandQty - @Qty,
            UpdatedAt = GETUTCDATE()
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId
          AND ((WarehouseId = @WarehouseId) OR (WarehouseId IS NULL AND @WarehouseId IS NULL));
    END
END
GO

-- =============================================================================
-- tvf_PriceListEffective — Plan 30: TEK DOĞRULUK KAYNAĞI fiyat çözümü
-- NE YAPAR: Şirketin aktif + geçerli (bugün) PriceList satırlarını boyutlarıyla
--   (Direction/Cari/Şube/Öncelik/MinQty/tarih) ve zincir-iskonto uygulanmış
--   net efektif fiyatıyla birlikte döner. Resolver (sp_CheckPriceVariance /
--   satış fiyat çözümü) bu TVF üzerinden TOP 1 seçer — tek kaynak.
-- ZİNCİR İSKONTO: PriceListLineDiscount kademeleri ARDIŞIK çarpılır (toplanmaz):
--   100 → 90 → 85,5 → 82,935. Hesap recursive CTE ile TAM DECIMAL (float YASAK —
--   kuruş kayması engellenir). Sonda ROUND(...,4).
-- MEVZUAT (KDVK md.25/a): BasePrice (brüt), TotalDiscountAmount, EffectivePrice
--   (net) AYRI döner — KDV matrahı net üzerinden, iskonto faturada gösterilir.
-- PARAMETRE: @CompanyId UNIQUEIDENTIFIER — şirket kimliği
-- DÖNEN KOLONLAR: CompanyId, PriceListId, PriceListCode, Direction, PartnerId,
--   BranchId, Priority, LineId, ItemId, MinQty, LineType, ValidFrom, ValidTo,
--   Currency, BasePrice, NetMultiplier, EffectivePrice, TotalDiscountAmount
-- NOT: MatchScore TVF'te HESAPLANMAZ — belge bağlamını (cari/şube) bilmediği için
--   skorlama resolver'a (sp_CheckPriceVariance) bırakılır; TVF dimension-agnostik.
-- =============================================================================
CREATE OR ALTER FUNCTION dbo.tvf_PriceListEffective (@CompanyId UNIQUEIDENTIFIER)
RETURNS TABLE
AS
RETURN
(
    WITH Ranked AS (
        -- Kademeleri Seq sırasına göre 1..N yoğun indeksle (rn). Seq'in 1'den başlaması
        -- veya boşluksuz olması ŞART DEĞİL; rn her zaman contiguous → recursive anchor güvenli.
        -- (Aggregate recursive part'ta yasak olduğundan boşluk toleransı burada çözülür.)
        SELECT LineId, Pct,
               ROW_NUMBER() OVER (PARTITION BY LineId ORDER BY Seq) AS rn
        FROM PriceListLineDiscount
    ),
    DiscountChain AS (
        -- Anchor: ilk kademe (rn=1)
        SELECT LineId, rn,
               CAST(1 - Pct / 100.0 AS DECIMAL(28,12)) AS RunningMult
        FROM Ranked
        WHERE rn = 1
        UNION ALL
        -- Sonraki kademe: birikmiş çarpanı bir sonraki rn ile ardışık çarp (toplanmaz)
        SELECT r.LineId, r.rn,
               CAST(dc.RunningMult * (1 - r.Pct / 100.0) AS DECIMAL(28,12))
        FROM Ranked r
        JOIN DiscountChain dc ON dc.LineId = r.LineId AND r.rn = dc.rn + 1
    ),
    FinalMult AS (
        -- Her satırın en yüksek rn'deki birikmiş çarpanı = nihai net çarpan
        SELECT LineId, RunningMult AS NetMultiplier
        FROM (
            SELECT LineId, RunningMult,
                   ROW_NUMBER() OVER (PARTITION BY LineId ORDER BY rn DESC) AS lastRn
            FROM DiscountChain
        ) x
        WHERE x.lastRn = 1
    )
    SELECT
        pl.CompanyId,
        pl.Id            AS PriceListId,
        pl.Code          AS PriceListCode,
        pl.Direction,
        pl.PartnerId,
        pl.BranchId,
        pl.Priority,
        pll.Id           AS LineId,
        pll.ItemId,
        pll.MinQty,
        pll.LineType,
        pl.ValidFrom,
        pl.ValidTo,
        COALESCE(pll.Currency, pl.Currency, 'TRY') AS Currency,
        -- Brüt baz fiyat (mevzuat: KDV matrahı/gösterim için AYRI)
        CAST(pll.UnitPrice AS DECIMAL(18,4)) AS BasePrice,
        -- Nihai net çarpan (iskonto kademesi yoksa 1)
        COALESCE(fm.NetMultiplier, CAST(1 AS DECIMAL(28,12))) AS NetMultiplier,
        -- Net efektif fiyat (zincir uygulanmış, sonda kuruşa ROUND)
        CAST(ROUND(pll.UnitPrice * COALESCE(fm.NetMultiplier, CAST(1 AS DECIMAL(28,12))), 4) AS DECIMAL(18,4)) AS EffectivePrice,
        -- Toplam iskonto tutarı (brüt − net) — faturada gösterim
        CAST(pll.UnitPrice - ROUND(pll.UnitPrice * COALESCE(fm.NetMultiplier, CAST(1 AS DECIMAL(28,12))), 4) AS DECIMAL(18,4)) AS TotalDiscountAmount
    FROM PriceList pl
    JOIN PriceListLine pll ON pll.PriceListId = pl.Id
    LEFT JOIN FinalMult fm ON fm.LineId = pll.Id
    WHERE pl.CompanyId = @CompanyId
      AND pl.IsActive = 1
      AND pl.IsDeleted = 0
      AND pll.IsDeleted = 0
      -- Geçerlilik: bugün ValidFrom/ValidTo aralığında (NULL = sınırsız)
      AND (pl.ValidFrom IS NULL OR pl.ValidFrom <= CAST(GETUTCDATE() AS DATE))
      AND (pl.ValidTo   IS NULL OR pl.ValidTo   >= CAST(GETUTCDATE() AS DATE))
);
GO

-- =============================================================================
-- sp_CheckPriceVariance — PO satır fiyat sapması kontrolü (Plan 30: branch-aware)
-- NE YAPAR: PO satırındaki gerçek fiyatı tvf_PriceListEffective'ten çözülen NET
--   efektif beklenen fiyatla karşılaştırır; sapma varsa PriceVariance DRAFT açar.
-- ÖNCELİK (Plan 30 — CARİ BASKIN): MatchScore = Partner×2 + Branch×1 DESC, sonra
--   Priority DESC → MinQty DESC → ValidFrom DESC → LineId ASC (deterministik tie-break).
--   Katman: T-PB(3) > T-P(2) > T-B(1) > T-G(0). Cari-özel fiyat şube fiyatını ezer.
-- MEVZUAT (KDVK md.25/a): kıyas NET efektif fiyat üzerinden (brüt-net karıştırma yok).
-- PARAMETRELERİ:
--   @CompanyId   UNIQUEIDENTIFIER — şirket kimliği
--   @PoHeaderId  UNIQUEIDENTIFIER — PO başlık kimliği
--   @PoLineId    UNIQUEIDENTIFIER — PO satır kimliği
--   @ItemId      UNIQUEIDENTIFIER — malzeme kimliği
--   @PartnerId   UNIQUEIDENTIFIER — tedarikçi kimliği
--   @ActualPrice DECIMAL(18,4) — PO'daki gerçek birim fiyat
--   @BranchId    UNIQUEIDENTIFIER (NULL) — belgenin şubesi (PO Warehouse'tan); NULL = genel
--   @UserId      UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
--   @VarianceId  UNIQUEIDENTIFIER OUTPUT — oluşan PriceVariance kaydının kimliği (sapma yoksa NULL)
-- SIDE EFFECTS: PriceVariance (INSERT — sapma varsa DRAFT kayıt)
-- THROW: yok (liste fiyatı yoksa sessizce döner)
-- BAĞIMLILIK: tvf_PriceListEffective (tolerans YOK — her sapma variance)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CheckPriceVariance
    @CompanyId      UNIQUEIDENTIFIER,
    @PoHeaderId     UNIQUEIDENTIFIER,
    @PoLineId       UNIQUEIDENTIFIER,
    @ItemId         UNIQUEIDENTIFIER,
    @PartnerId      UNIQUEIDENTIFIER,
    @ActualPrice    DECIMAL(18,4),
    @BranchId       UNIQUEIDENTIFIER = NULL,
    @UserId         UNIQUEIDENTIFIER = NULL,
    @VarianceId     UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @VarianceId = NULL;

    DECLARE @ExpectedPrice DECIMAL(18,4);

    -- İş kuralı: tek kaynak tvf_PriceListEffective'ten Direction=PURCHASE için TOP 1.
    -- Uygun boyutlar: (Cari eşleşir VEYA genel) AND (Şube eşleşir VEYA genel).
    -- MatchScore = Partner×2 + Branch×1 → cari baskın; net efektif fiyat alınır.
    SELECT TOP 1 @ExpectedPrice = pe.EffectivePrice
    FROM dbo.tvf_PriceListEffective(@CompanyId) pe
    WHERE pe.Direction = 'PURCHASE'
      AND pe.ItemId = @ItemId
      AND (pe.PartnerId = @PartnerId OR pe.PartnerId IS NULL)
      AND (pe.BranchId  = @BranchId  OR pe.BranchId  IS NULL)
    ORDER BY
        (CASE WHEN pe.PartnerId = @PartnerId THEN 2 ELSE 0 END
       + CASE WHEN pe.BranchId  = @BranchId  THEN 1 ELSE 0 END) DESC,
        pe.Priority   DESC,
        pe.MinQty     DESC,
        pe.ValidFrom  DESC,
        pe.LineId     ASC;

    -- Liste fiyatı yoksa sapma kontrolü yapılamaz
    IF @ExpectedPrice IS NULL RETURN;

    -- TOLERANS YOK (Plan 27 ilkesi): liste fiyatından HER sapma variance üretir; eşit ise kayıt yok.
    IF @ActualPrice = @ExpectedPrice RETURN;

    -- Sapma hesabı
    DECLARE @Variance DECIMAL(18,4)        = @ActualPrice - @ExpectedPrice;
    DECLARE @VariancePct DECIMAL(8,4)      =
        CASE WHEN @ExpectedPrice > 0 THEN @Variance / @ExpectedPrice * 100 ELSE 0 END;

    -- Sapma var: PriceVariance kaydı
    SET @VarianceId = NEWID();
    INSERT INTO PriceVariance (
        Id, CompanyId, SourceDocType, SourceDocId, SourceLineId,
        ItemId, PartnerId, ExpectedPrice, ActualPrice,
        Variance, VariancePercent, Status, CreatedBy
    )
    VALUES (
        @VarianceId, @CompanyId, 'PURCHASE_ORDER', @PoHeaderId, @PoLineId,
        @ItemId, @PartnerId, @ExpectedPrice, @ActualPrice,
        @Variance, @VariancePct, 'DRAFT', @UserId
    );
END
GO

-- =============================================================================
-- sp_ApprovePriceVariance — Fiyat farkı onayı
-- NE YAPAR: PriceVariance kaydını DRAFT'tan APPROVED'a taşır ve ilgili PO satırının
--   fiyatını ActualPrice (gerçek fiyat) ile günceller.
-- PARAMETRELERİ:
--   @VarianceId UNIQUEIDENTIFIER — onaylanacak PriceVariance kaydının kimliği
--   @UserId     UNIQUEIDENTIFIER (NULL) — onaylayan kullanıcı kimliği
-- SIDE EFFECTS: PriceVariance (UPDATE — Status=APPROVED, ApprovedBy, ApprovedAt);
--   PurchaseOrderLine (UPDATE — Price=ActualPrice, SourceDocType=PURCHASE_ORDER ise)
-- THROW: 51101 — kayıt bulunamadı; 51102 — yalnızca DRAFT onaylanabilir
-- BAĞIMLILIK: yok
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ApprovePriceVariance
    @VarianceId UNIQUEIDENTIFIER,
    @UserId     UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20), @SourceLineId UNIQUEIDENTIFIER,
                @ActualPrice DECIMAL(18,4), @SourceDocType NVARCHAR(50);

        SELECT @Status = Status, @SourceLineId = SourceLineId,
               @ActualPrice = ActualPrice, @SourceDocType = SourceDocType
        FROM PriceVariance WHERE Id = @VarianceId AND IsDeleted = 0;

        IF @Status IS NULL
            THROW 51101, N'Fiyat farkı kaydı bulunamadı.', 1;
        IF @Status <> 'DRAFT'
            THROW 51102, N'Sadece bekleyen (DRAFT) fiyat farkları onaylanabilir.', 1;

        -- PO satır fiyatını gerçek değere güncelle
        IF @SourceDocType = 'PURCHASE_ORDER'
            UPDATE PurchaseOrderLine
            SET Price = @ActualPrice
            WHERE Id = @SourceLineId;

        -- Variance kaydını onayla
        UPDATE PriceVariance
        SET Status     = 'APPROVED',
            ApprovedBy = @UserId,
            ApprovedAt = GETUTCDATE()
        WHERE Id = @VarianceId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_GenerateSalesInvoiceFromShipping — Sevkiyattan otomatik satış faturası üretme
-- NE YAPAR: POSTED sevkiyat için SalesInvoice ve SalesInvoiceLine oluşturur; toplamları
--   hesaplar, tek taksitlik PaymentPlan açar ve cari hesap defterine Debit kaydı yazar.
-- PARAMETRELERİ:
--   @ShippingId   UNIQUEIDENTIFIER — kaynak sevkiyat başlık kimliği
--   @UserId       UNIQUEIDENTIFIER (NULL) — işlemi başlatan kullanıcı
--   @NewInvoiceId UNIQUEIDENTIFIER OUTPUT — oluşturulan fatura kimliği
-- SIDE EFFECTS: SalesInvoice (INSERT); SalesInvoiceLine (INSERT);
--   PaymentPlan (INSERT — RECEIVABLE/OPEN); AccountMovement (INSERT — Debit);
--   ShippingHeader (UPDATE — PartnerId/SalesOrderId, partner eksikse)
-- THROW: 50201 — sevkiyat bulunamadı; 50202 — POSTED değil; 50203 — çift-fatura koruması;
--   70004 — partner belirlenemedi
-- BAĞIMLILIK: sp_GuardPeriodOpen (dönem kilidi), Partner (PaymentTermDays)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_GenerateSalesInvoiceFromShipping
    @ShippingId    UNIQUEIDENTIFIER,
    @UserId        UNIQUEIDENTIFIER = NULL,
    @NewInvoiceId  UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CompanyId   UNIQUEIDENTIFIER,
                @PartnerId   UNIQUEIDENTIFIER,
                @SoId        UNIQUEIDENTIFIER,
                @ShipStatus  NVARCHAR(20);

        SELECT @CompanyId  = CompanyId,
               @PartnerId  = PartnerId,
               @SoId       = SalesOrderId,
               @ShipStatus = Status
        FROM ShippingHeader
        WHERE Id = @ShippingId AND IsDeleted = 0;

        IF @CompanyId IS NULL THROW 50201, N'Sevkiyat bulunamadı.', 1;
        IF @ShipStatus <> 'POSTED' THROW 50202, N'Sadece POSTED sevkiyatlar faturalanabilir.', 1;

        -- Varsayılan satış KDV (Plan 29 parametrik) — ürün oranı (Item.TaxRate) öncelikli, bu fallback. %0 korunur.
        DECLARE @SalesDefaultTax DECIMAL(8,4) = ISNULL(
            (SELECT TRY_CAST(Value AS DECIMAL(8,4)) FROM Parameter
             WHERE CompanyId = @CompanyId AND Code = 'DEFAULT_SALES_TAX_RATE' AND IsDeleted = 0), 20);

        -- Dönem kilidi: fatura oluşturma anının dönemi açık olmalı (Plan 14)
        DECLARE @now DATETIME2 = GETUTCDATE();
        DECLARE @userStr NVARCHAR(450) = CAST(@UserId AS NVARCHAR(450));
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @userStr;

        -- Bu sevkiyat için zaten fatura varsa hata (çift-post koruması)
        IF EXISTS (SELECT 1 FROM SalesInvoice WHERE ShippingId = @ShippingId AND IsDeleted = 0)
            THROW 50203, N'Bu sevkiyat için zaten fatura oluşturulmuş.', 1;

        -- PartnerId boşsa ilk satırdaki SO'dan al
        IF @PartnerId IS NULL
        BEGIN
            SELECT TOP 1 @PartnerId = soh.PartnerId, @SoId = soh.Id
            FROM ShippingLine sl
            JOIN SalesOrderLine sol ON sol.Id = sl.SalesOrderLineId
            JOIN SalesOrderHeader soh ON soh.Id = sol.HeaderId
            WHERE sl.HeaderId = @ShippingId;

            -- Şimdi shipping'e geriye yaz (bir sonraki çağrı için)
            UPDATE ShippingHeader SET PartnerId = @PartnerId, SalesOrderId = @SoId WHERE Id = @ShippingId;
        END

        IF @PartnerId IS NULL
            THROW 50204, N'Sevkiyat müşterisi belirlenemedi (Partner referansı yok).', 1;

        -- Mutabakat kilidi (Plan 19): @PartnerId kesinleşti — onaylı mutabakatlı döneme fatura giremez
        EXEC dbo.sp_GuardPartnerReconciled @CompanyId, @PartnerId, @now;

        -- Fatura numarası üret (basit dizi — gelecekte sp_GenerateDocNo'dan alacak)
        DECLARE @InvoiceNo NVARCHAR(50);
        DECLARE @InvoiceCount INT;
        SELECT @InvoiceCount = ISNULL(MAX(CAST(SUBSTRING(InvoiceNo, 10, 5) AS INT)), 0)
        FROM SalesInvoice
        WHERE CompanyId = @CompanyId AND InvoiceNo LIKE 'INV-' + CAST(YEAR(GETUTCDATE()) AS NVARCHAR(4)) + '-%';
        SET @InvoiceNo = 'INV-' + CAST(YEAR(GETUTCDATE()) AS NVARCHAR(4)) + '-'
                       + RIGHT('00000' + CAST(@InvoiceCount + 1 AS NVARCHAR(5)), 5);

        SET @NewInvoiceId = NEWID();

        DECLARE @PaymentTermDays INT;
        SELECT @PaymentTermDays = ISNULL(PaymentTermDays, 30) FROM Partner WHERE Id = @PartnerId;
        DECLARE @DueDate DATE = DATEADD(DAY, @PaymentTermDays, CAST(GETUTCDATE() AS DATE));

        -- Boş fatura başlığı
        INSERT INTO SalesInvoice (
            Id, CompanyId, InvoiceNo, InvoiceDate, DueDate,
            PartnerId, ShippingId, SalesOrderId,
            Subtotal, TaxAmount, GrandTotal, Status, CreatedBy
        )
        VALUES (
            @NewInvoiceId, @CompanyId, @InvoiceNo, GETUTCDATE(), @DueDate,
            @PartnerId, @ShippingId, @SoId,
            0, 0, 0, 'POSTED', @UserId
        );

        -- Satırları sevkiyat satırlarından doldur (Plan 21: SourceShipmentLineId iz)
        INSERT INTO SalesInvoiceLine (
            Id, InvoiceId, ItemId, UomId, Description,
            Qty, UnitPrice, LineSubtotal, TaxRatePercent, TaxAmount, LineTotal, UnitCost,
            SourceShipmentLineId
        )
        SELECT
            NEWID(),
            @NewInvoiceId,
            sl.ItemId,
            sl.UomId,
            i.Name,
            sl.QtyBase,
            ISNULL(sol.Price, ISNULL(i.SalesPrice, 0))                                 AS UnitPrice,
            sl.QtyBase * ISNULL(sol.Price, ISNULL(i.SalesPrice, 0))                    AS LineSubtotal,
            ISNULL(i.TaxRate, @SalesDefaultTax)                                         AS TaxRatePercent,
            sl.QtyBase * ISNULL(sol.Price, ISNULL(i.SalesPrice, 0)) * ISNULL(i.TaxRate, @SalesDefaultTax) / 100 AS TaxAmount,
            sl.QtyBase * ISNULL(sol.Price, ISNULL(i.SalesPrice, 0)) * (1 + ISNULL(i.TaxRate, @SalesDefaultTax) / 100) AS LineTotal,
            ic.AvgCost                                                                 AS UnitCost,
            sl.Id
        FROM ShippingLine sl
        JOIN Item i ON i.Id = sl.ItemId
        LEFT JOIN SalesOrderLine sol ON sol.Id = sl.SalesOrderLineId
        LEFT JOIN ItemCost ic ON ic.CompanyId = @CompanyId AND ic.ItemId = sl.ItemId
        WHERE sl.HeaderId = @ShippingId;

        -- Plan 21: faturalanan miktarı işaretle (kısmi/N:1 izi, çift-sayım önleme)
        UPDATE sl SET sl.InvoicedQty = sl.QtyBase
        FROM ShippingLine sl WHERE sl.HeaderId = @ShippingId;

        -- Başlık toplamlarını güncelle
        UPDATE SalesInvoice
        SET Subtotal   = (SELECT ISNULL(SUM(LineSubtotal), 0) FROM SalesInvoiceLine WHERE InvoiceId = @NewInvoiceId),
            TaxAmount  = (SELECT ISNULL(SUM(TaxAmount), 0)    FROM SalesInvoiceLine WHERE InvoiceId = @NewInvoiceId),
            GrandTotal = (SELECT ISNULL(SUM(LineTotal), 0)    FROM SalesInvoiceLine WHERE InvoiceId = @NewInvoiceId)
        WHERE Id = @NewInvoiceId;

        -- PaymentPlan kaydı oluştur (tek taksit varsayımıyla; çoklu taksit için ileride genişler)
        INSERT INTO PaymentPlan (
            Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
            PartnerId, Direction, InstallmentNo, TotalInstallments, DueDate, Amount, Status
        )
        SELECT
            NEWID(), @CompanyId, 'SALES_INVOICE', @NewInvoiceId, @InvoiceNo,
            @PartnerId, 'RECEIVABLE', 1, 1, @DueDate, GrandTotal, 'OPEN'
        FROM SalesInvoice WHERE Id = @NewInvoiceId;

        -- Cari hesap defteri: satış faturası → Debit (müşteri bize borçlandı)
        -- NetAmount = Subtotal (KDV hariç) — iç raporlar bu değeri kullanır
        -- Çift-post koruması: UX_AccountMovement_Source (SourceDocType, SourceDocId) unique
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit,
             NetAmount, TaxAmount, DueDate, Currency,
             SourceDocType, SourceDocId, SourceDocNo,
             Description, CreatedBy)
        SELECT
            NEWID(), @CompanyId, @PartnerId, @now,
            GrandTotal, 0,
            Subtotal, TaxAmount, @DueDate, 'TRY',
            'SALES_INVOICE', @NewInvoiceId, @InvoiceNo,
            N'Satış Faturası', CAST(@UserId AS NVARCHAR(450))
        FROM SalesInvoice WHERE Id = @NewInvoiceId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_GenerateSalesInvoiceFromShippings — Plan 21: N irsaliye → 1 fatura (DEFERRED N:1)
-- NE YAPAR: birden çok POSTED sevkiyatı tek faturada birleştirir. Aynı cari zorunlu;
--   7-gün VUK guard (MIN sevk tarihi + 7 ≥ fatura). Stok YAZMAZ (irsaliyede yazıldı);
--   yalnızca SalesInvoice + AccountMovement + PaymentPlan üretir, InvoicedQty günceller.
-- PARAMETRELERİ:
--   @CompanyId, @PartnerId, @ShippingIdsJson (JSON dizi: ["guid",...]), @UserId, @NewInvoiceId OUT
-- SIDE EFFECTS: SalesInvoice/Line (INSERT), ShippingLine.InvoicedQty (UPDATE),
--   AccountMovement (Debit), PaymentPlan (RECEIVABLE/OPEN)
-- THROW: 50210-50219 (PageModel catch: >= 50000 && < 60000)
-- BAĞIMLILIK: sp_GuardPeriodOpen, sp_GuardPartnerReconciled, OPENJSON
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_GenerateSalesInvoiceFromShippings
    @CompanyId       UNIQUEIDENTIFIER,
    @PartnerId       UNIQUEIDENTIFIER,
    @ShippingIdsJson NVARCHAR(MAX),
    @UserId          UNIQUEIDENTIFIER = NULL,
    @NewInvoiceId    UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @ships TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
        INSERT INTO @ships (Id)
        SELECT TRY_CAST(value AS UNIQUEIDENTIFIER) FROM OPENJSON(@ShippingIdsJson)
        WHERE TRY_CAST(value AS UNIQUEIDENTIFIER) IS NOT NULL;

        IF NOT EXISTS (SELECT 1 FROM @ships)
            THROW 50210, N'Birleştirilecek sevkiyat seçilmedi.', 1;

        -- Guard: tüm sevkiyatlar aktif firmaya + aynı cariye ait + POSTED
        IF EXISTS (
            SELECT 1 FROM @ships s
            LEFT JOIN ShippingHeader sh ON sh.Id = s.Id
            WHERE sh.Id IS NULL OR sh.IsDeleted = 1
               OR sh.CompanyId <> @CompanyId OR sh.PartnerId <> @PartnerId
               OR sh.Status <> 'POSTED'
        )
            THROW 50211, N'Seçili sevkiyatlar aynı cariye ait, onaylı ve geçerli olmalıdır.', 1;

        -- Varsayılan satış KDV (Plan 29 parametrik) — ürün oranı (Item.TaxRate) öncelikli, bu fallback. %0 korunur.
        DECLARE @SalesDefaultTax DECIMAL(8,4) = ISNULL(
            (SELECT TRY_CAST(Value AS DECIMAL(8,4)) FROM Parameter
             WHERE CompanyId = @CompanyId AND Code = 'DEFAULT_SALES_TAX_RATE' AND IsDeleted = 0), 20);

        DECLARE @now DATETIME2 = GETUTCDATE();
        DECLARE @userStr NVARCHAR(450) = CAST(@UserId AS NVARCHAR(450));
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @userStr;
        EXEC dbo.sp_GuardPartnerReconciled @CompanyId, @PartnerId, @now;

        -- 7-GÜN VUK guard (md.231/5): en eski sevk + 7 gün
        DECLARE @minShipDate DATETIME2;
        SELECT @minShipDate = MIN(sh.DocDate) FROM @ships s JOIN ShippingHeader sh ON sh.Id = s.Id;
        IF DATEDIFF(DAY, @minShipDate, @now) > 7
            THROW 50212, N'En eski sevkiyattan bu yana 7 günü aştı (VUK md.231/5); fatura ayrı kesilmeli.', 1;

        IF NOT EXISTS (
            SELECT 1 FROM ShippingLine sl JOIN @ships s ON s.Id = sl.HeaderId
            WHERE sl.QtyBase - sl.InvoicedQty > 0.000001)
            THROW 50213, N'Seçili sevkiyatlarda faturalanmamış satır yok.', 1;

        DECLARE @InvoiceNo NVARCHAR(50), @InvoiceCount INT;
        SELECT @InvoiceCount = ISNULL(MAX(CAST(SUBSTRING(InvoiceNo, 10, 5) AS INT)), 0)
        FROM SalesInvoice
        WHERE CompanyId = @CompanyId AND InvoiceNo LIKE 'INV-' + CAST(YEAR(@now) AS NVARCHAR(4)) + '-%';
        SET @InvoiceNo = 'INV-' + CAST(YEAR(@now) AS NVARCHAR(4)) + '-'
                       + RIGHT('00000' + CAST(@InvoiceCount + 1 AS NVARCHAR(5)), 5);

        SET @NewInvoiceId = NEWID();

        DECLARE @PaymentTermDays INT;
        SELECT @PaymentTermDays = ISNULL(PaymentTermDays, 30) FROM Partner WHERE Id = @PartnerId;
        DECLARE @DueDate DATE = DATEADD(DAY, @PaymentTermDays, CAST(@now AS DATE));

        -- Fatura başlığı: ShippingId NULL (N:1 — bağ satırda)
        INSERT INTO SalesInvoice (
            Id, CompanyId, InvoiceNo, InvoiceDate, IssueDate, DueDate,
            PartnerId, ShippingId, SalesOrderId,
            Subtotal, TaxAmount, GrandTotal, Status, CreatedBy)
        VALUES (
            @NewInvoiceId, @CompanyId, @InvoiceNo, @now, @now, @DueDate,
            @PartnerId, NULL, NULL, 0, 0, 0, 'POSTED', @UserId);

        INSERT INTO SalesInvoiceLine (
            Id, InvoiceId, ItemId, UomId, Description,
            Qty, UnitPrice, LineSubtotal, TaxRatePercent, TaxAmount, LineTotal, UnitCost,
            SourceShipmentLineId)
        SELECT
            NEWID(), @NewInvoiceId, sl.ItemId, sl.UomId, i.Name,
            (sl.QtyBase - sl.InvoicedQty),
            ISNULL(sol.Price, ISNULL(i.SalesPrice, 0)),
            (sl.QtyBase - sl.InvoicedQty) * ISNULL(sol.Price, ISNULL(i.SalesPrice, 0)),
            ISNULL(i.TaxRate, @SalesDefaultTax),
            (sl.QtyBase - sl.InvoicedQty) * ISNULL(sol.Price, ISNULL(i.SalesPrice, 0)) * ISNULL(i.TaxRate, @SalesDefaultTax) / 100,
            (sl.QtyBase - sl.InvoicedQty) * ISNULL(sol.Price, ISNULL(i.SalesPrice, 0)) * (1 + ISNULL(i.TaxRate, @SalesDefaultTax) / 100),
            ic.AvgCost,
            sl.Id
        FROM ShippingLine sl
        JOIN @ships s ON s.Id = sl.HeaderId
        JOIN Item i ON i.Id = sl.ItemId
        LEFT JOIN SalesOrderLine sol ON sol.Id = sl.SalesOrderLineId
        LEFT JOIN ItemCost ic ON ic.CompanyId = @CompanyId AND ic.ItemId = sl.ItemId
        WHERE sl.QtyBase - sl.InvoicedQty > 0.000001;

        UPDATE sl SET sl.InvoicedQty = sl.QtyBase
        FROM ShippingLine sl JOIN @ships s ON s.Id = sl.HeaderId
        WHERE sl.QtyBase - sl.InvoicedQty > 0.000001;

        UPDATE SalesInvoice
        SET Subtotal   = (SELECT ISNULL(SUM(LineSubtotal), 0) FROM SalesInvoiceLine WHERE InvoiceId = @NewInvoiceId),
            TaxAmount  = (SELECT ISNULL(SUM(TaxAmount), 0)    FROM SalesInvoiceLine WHERE InvoiceId = @NewInvoiceId),
            GrandTotal = (SELECT ISNULL(SUM(LineTotal), 0)    FROM SalesInvoiceLine WHERE InvoiceId = @NewInvoiceId)
        WHERE Id = @NewInvoiceId;

        INSERT INTO PaymentPlan (
            Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
            PartnerId, Direction, InstallmentNo, TotalInstallments, DueDate, Amount, Status)
        SELECT
            NEWID(), @CompanyId, 'SALES_INVOICE', @NewInvoiceId, @InvoiceNo,
            @PartnerId, 'RECEIVABLE', 1, 1, @DueDate, GrandTotal, 'OPEN'
        FROM SalesInvoice WHERE Id = @NewInvoiceId;

        -- Cari defter: fatura → Debit (irsaliye AM yazmaz)
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit,
             NetAmount, TaxAmount, DueDate, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        SELECT
            NEWID(), @CompanyId, @PartnerId, @now,
            GrandTotal, 0, Subtotal, TaxAmount, @DueDate, 'TRY',
            'SALES_INVOICE', @NewInvoiceId, @InvoiceNo,
            N'Satış Faturası (birleşik)', @userStr
        FROM SalesInvoice WHERE Id = @NewInvoiceId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_DepositCheque — Çeki bankaya tahsile verme
-- NE YAPAR: PORTFOLIO statüsündeki çeki IN_BANK'a taşır; hangi banka hesabına verildiğini
--   ve tarihi kaydeder.
-- PARAMETRELERİ:
--   @ChequeId    UNIQUEIDENTIFIER — işlem görecek çek kimliği
--   @AccountId   UNIQUEIDENTIFIER — çekin tahsile verileceği banka hesabı kimliği
--   @DepositDate DATETIME2 (NULL) — tahsil tarihi; NULL ise GETUTCDATE()
--   @UserId      UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
-- SIDE EFFECTS: Cheque (UPDATE — Status=IN_BANK, DepositedToAccountId, DepositedAt)
-- THROW: 60001 — çek bulunamadı; 60002 — statü PORTFOLIO değil
-- BAĞIMLILIK: yok
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_DepositCheque
    @ChequeId    UNIQUEIDENTIFIER,
    @AccountId   UNIQUEIDENTIFIER,
    @CompanyId   UNIQUEIDENTIFIER,
    @DepositDate DATETIME2 = NULL,
    @UserId      UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Status NVARCHAR(20);
    SELECT @Status = Status FROM Cheque WHERE Id = @ChequeId AND CompanyId = @CompanyId AND IsDeleted = 0;

    IF @Status IS NULL THROW 55001, N'Çek bulunamadı.', 1;
    IF @Status <> 'PORTFOLIO' THROW 55002, N'Sadece portföydeki çekler bankaya verilebilir.', 1;

    UPDATE Cheque
    SET Status               = 'IN_BANK',
        DepositedToAccountId = @AccountId,
        DepositedAt          = ISNULL(@DepositDate, GETUTCDATE()),
        UpdatedAt            = GETUTCDATE(),
        UpdatedBy            = @UserId
    WHERE Id = @ChequeId;
END
GO

-- =============================================================================
-- sp_CollectCheque — Çek tahsili (banka tarafından ödeme alındı)
-- NE YAPAR: IN_BANK statüsündeki çeki COLLECTED'a taşır; FinancialTransaction (INCOME)
--   ve AccountMovement (Credit — müşteri borcu kapandı) kaydı oluşturur.
-- PARAMETRELERİ:
--   @ChequeId    UNIQUEIDENTIFIER — tahsil edilecek çek kimliği
--   @CollectDate DATETIME2 (NULL) — tahsilat tarihi; NULL ise GETUTCDATE()
--   @UserId      UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
-- SIDE EFFECTS: Cheque (UPDATE — Status=COLLECTED, CollectedAt);
--   FinancialTransaction (INSERT — INCOME); AccountMovement (INSERT — Credit)
-- THROW: 50600 — çek bulunamadı; 50603 — statü IN_BANK değil; 50605 — banka hesabı yok
-- BAĞIMLILIK: sp_GuardPeriodOpen (dönem kilidi)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CollectCheque
    @ChequeId    UNIQUEIDENTIFIER,
    @CompanyId   UNIQUEIDENTIFIER,
    @CollectDate DATETIME2 = NULL,
    @UserId      UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20), @Amount DECIMAL(18,2),
                @AccountId UNIQUEIDENTIFIER, @PartnerId UNIQUEIDENTIFIER,
                @ChequeNo NVARCHAR(50);

        SELECT @Status = Status, @Amount = Amount, @AccountId = DepositedToAccountId,
               @PartnerId = PartnerId, @ChequeNo = ChequeNo
        FROM Cheque WHERE Id = @ChequeId AND CompanyId = @CompanyId AND IsDeleted = 0;

        IF @Status IS NULL THROW 50600, N'Çek bulunamadı.', 1;
        IF @Status <> 'IN_BANK'
            THROW 50603, N'Sadece bankaya verilmiş çekler tahsil edilebilir.', 1;
        IF @AccountId IS NULL
            THROW 50605, N'Çekin tahsile verildiği banka hesabı bulunamadı.', 1;

        DECLARE @TxId UNIQUEIDENTIFIER = NEWID();
        DECLARE @Now  DATETIME2        = ISNULL(@CollectDate, GETUTCDATE());

        -- Dönem kilidi: tahsilat anının dönemi açık olmalı (Plan 14)
        DECLARE @userStr NVARCHAR(450) = CAST(@UserId AS NVARCHAR(450));
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @Now, @userStr;

        -- Mutabakat kilidi (Plan 19): çek borçlusu cariyle onaylı mutabakat varsa kilit
        EXEC dbo.sp_GuardPartnerReconciled @CompanyId, @PartnerId, @Now;

        INSERT INTO FinancialTransaction (
            Id, CompanyId, AccountId, TransactionDate, TransactionType,
            Amount, Currency, AmountTRY, PartnerId, Description,
            InstrumentType, InstrumentId, SourceDocType, SourceDocNo, CreatedBy
        )
        VALUES (
            @TxId, @CompanyId, @AccountId, @Now, 'INCOME',
            @Amount, 'TRY', @Amount, @PartnerId, N'Çek tahsili: ' + @ChequeNo,
            'CHEQUE', @ChequeId, 'CHEQUE_COLLECTION', @ChequeNo, @UserId
        );

        UPDATE Cheque
        SET Status      = 'COLLECTED',
            CollectedAt = @Now,
            UpdatedAt   = GETUTCDATE(),
            UpdatedBy   = @UserId
        WHERE Id = @ChequeId;

        -- Cari hesap defteri: çek tahsili → Credit (müşterinin borcu kapandı)
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        VALUES (
            NEWID(), @CompanyId, @PartnerId, @Now,
            0, @Amount, 'TRY',
            'CHEQUE_IN', @ChequeId, @ChequeNo,
            N'Çek Tahsili: ' + @ChequeNo, CAST(@UserId AS NVARCHAR(450))
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_ReturnCheque — Karşılıksız çek işlemi
-- NE YAPAR: IN_BANK veya PORTFOLIO statüsündeki çeki RETURNED'a taşır; iade sebebini
--   kaydeder. Finansal hareket üretmez (stornaj gerekmez, çek hiç tahsil edilmedi).
-- PARAMETRELERİ:
--   @ChequeId UNIQUEIDENTIFIER — karşılıksız işaretlenecek çek kimliği
--   @Reason   NVARCHAR(500) — iade gerekçesi (zorunlu)
--   @UserId   UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
-- SIDE EFFECTS: Cheque (UPDATE — Status=RETURNED, ReturnReason)
-- THROW: 60004 — çek statüsü IN_BANK veya PORTFOLIO değil (güncelleme satırı 0)
-- BAĞIMLILIK: yok
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ReturnCheque
    @ChequeId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @Reason    NVARCHAR(500),
    @UserId    UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Cheque
    SET Status       = 'RETURNED',
        ReturnReason = @Reason,
        UpdatedAt    = GETUTCDATE(),
        UpdatedBy    = @UserId
    WHERE Id = @ChequeId AND CompanyId = @CompanyId AND Status IN ('IN_BANK', 'PORTFOLIO');

    IF @@ROWCOUNT = 0
        THROW 55004, N'Çek karşılıksız olarak işaretlenemiyor (statü uygun değil).', 1;
END
GO

-- =============================================================================
-- sp_CloseStatement — Kredi kartı dönem ekstresi kapatma
-- NE YAPAR: Belirtilen periyottaki ekstresiz slipleri toplar, CreditCardStatement
--   oluşturur, önceki dönem devrini açılış bakiyesi olarak ekler ve slipleri ekstreye bağlar.
-- PARAMETRELERİ:
--   @CardId         UNIQUEIDENTIFIER — kredi kartı kimliği
--   @PeriodStart    DATE — ekstre dönemi başlangıcı
--   @PeriodEnd      DATE — ekstre dönemi bitişi
--   @UserId         UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
--   @NewStatementId UNIQUEIDENTIFIER OUTPUT — oluşturulan ekstre kimliği
-- SIDE EFFECTS: CreditCardStatement (INSERT — IsClosed=1);
--   CreditCardTransaction (UPDATE — StatementId set edilir)
-- THROW: 51320 — kredi kartı bulunamadı
-- BAĞIMLILIK: CreditCard (DueDay), CreditCardStatement (önceki kapanış bakiyesi)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CloseStatement
    @CardId          UNIQUEIDENTIFIER,
    @CompanyId       UNIQUEIDENTIFIER,
    @PeriodStart     DATE,
    @PeriodEnd       DATE,
    @UserId          UNIQUEIDENTIFIER = NULL,
    @NewStatementId  UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- CompanyId zorunlu — başka firmanın kartını kapatamaz (IDOR koruması)
        DECLARE @DueDay INT;
        SELECT @DueDay = DueDay FROM CreditCard WHERE Id = @CardId AND CompanyId = @CompanyId;
        IF @DueDay IS NULL THROW 51320, N'Kredi kartı bulunamadı.', 1;

        -- Periyot içi ekstreye girmemiş slip toplamı
        DECLARE @Total DECIMAL(18,2) = ISNULL((
            SELECT SUM(Amount) FROM CreditCardTransaction
            WHERE CardId = @CardId AND StatementId IS NULL
              AND CAST(TransactionDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd), 0);

        -- Önceki kapanmamış bakiye (devir)
        DECLARE @Opening DECIMAL(18,2) = ISNULL((
            SELECT TOP 1 ClosingBalance - PaidAmount FROM CreditCardStatement
            WHERE CardId = @CardId ORDER BY PeriodEnd DESC), 0);

        SET @NewStatementId = NEWID();
        DECLARE @DueDate DATE = DATEFROMPARTS(
            YEAR(DATEADD(MONTH, 1, @PeriodEnd)), MONTH(DATEADD(MONTH, 1, @PeriodEnd)), @DueDay);

        INSERT INTO CreditCardStatement (
            Id, CardId, PeriodStart, PeriodEnd, StatementDate, DueDate,
            OpeningBalance, TotalDebit, TotalCredit, ClosingBalance,
            MinPayment, PaidAmount, IsClosed)
        VALUES (
            @NewStatementId, @CardId, @PeriodStart, @PeriodEnd, @PeriodEnd, @DueDate,
            @Opening, @Total, 0, @Opening + @Total,
            (@Opening + @Total) * 0.20, 0, 1);

        UPDATE CreditCardTransaction
        SET StatementId = @NewStatementId
        WHERE CardId = @CardId AND StatementId IS NULL
          AND CAST(TransactionDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_PayCreditCardStatement — Kredi kartı ekstre ödemesi
-- NE YAPAR: Banka hesabından EXPENSE hareketi yazar, ekstre PaidAmount değerini artırır
--   ve kartın AvailableLimit'ini ödeme tutarı kadar iade eder.
-- PARAMETRELERİ:
--   @StatementId   UNIQUEIDENTIFIER — ödenecek ekstre kimliği
--   @FromAccountId UNIQUEIDENTIFIER — ödemenin yapılacağı banka hesabı kimliği
--   @Amount        DECIMAL(18,2) — ödeme tutarı (pozitif, ekstre borcunu aşamaz)
--   @PayDate       DATETIME2 (NULL) — ödeme tarihi; NULL ise GETUTCDATE()
--   @UserId        UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
-- SIDE EFFECTS: FinancialTransaction (INSERT — EXPENSE); CreditCardStatement (UPDATE — PaidAmount);
--   CreditCard (UPDATE — AvailableLimit artırılır)
-- THROW: 51321 — ekstre bulunamadı; 51322 — tutar pozitif olmalı; 51323 — borcu aşıyor
-- BAĞIMLILIK: CreditCard, CreditCardStatement
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PayCreditCardStatement
    @StatementId    UNIQUEIDENTIFIER,
    @CompanyId      UNIQUEIDENTIFIER,
    @FromAccountId  UNIQUEIDENTIFIER,
    @Amount         DECIMAL(18,2),
    @PayDate        DATETIME2 = NULL,
    @UserId         UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi (Plan 14): ödeme tarihinin dönemi açık olmalı; LOCKED/CLOSED → THROW
        DECLARE @guardDate DATETIME2 = ISNULL(@PayDate, GETUTCDATE());
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @guardDate, @UserId;

        -- CompanyId zorunlu — başka firmanın ekstresini ödeyemez (IDOR koruması)
        DECLARE @CardId UNIQUEIDENTIFIER, @Closing DECIMAL(18,2), @Paid DECIMAL(18,2),
                @CardName NVARCHAR(200);
        SELECT @CardId = s.CardId, @Closing = s.ClosingBalance, @Paid = s.PaidAmount,
               @CardName = c.CardNoMasked
        FROM CreditCardStatement s
        JOIN CreditCard c ON c.Id = s.CardId AND c.CompanyId = @CompanyId
        WHERE s.Id = @StatementId;
        IF @CardId IS NULL THROW 51321, N'Ekstre bulunamadı.', 1;
        IF @Amount <= 0 THROW 51322, N'Ödeme tutarı pozitif olmalıdır.', 1;
        IF @Paid + @Amount > @Closing + 0.01 THROW 51323, N'Ödeme tutarı ekstre borcunu aşamaz.', 1;

        -- FromAccountId bu firmaya ait olmalı (IDOR: başka firmanın bankasından ödeme yapılamaz)
        IF NOT EXISTS (SELECT 1 FROM FinancialAccount WHERE Id = @FromAccountId AND CompanyId = @CompanyId)
            THROW 51324, N'Ödeme hesabı bu firmaya ait değil.', 1;

        INSERT INTO FinancialTransaction (
            Id, CompanyId, AccountId, TransactionDate, TransactionType,
            Amount, Currency, AmountTRY, Description,
            InstrumentType, InstrumentId, SourceDocType, CreatedBy)
        VALUES (
            NEWID(), @CompanyId, @FromAccountId, ISNULL(@PayDate, GETUTCDATE()), 'EXPENSE',
            @Amount, 'TRY', @Amount, N'Kredi kartı ekstre ödemesi: ' + @CardName,
            'CARD', @StatementId, 'CARD_STATEMENT_PAYMENT', @UserId);

        -- isolation-guard:ignore: @StatementId ve @CardId yukarıdaki JOIN'de CompanyId = @CompanyId ile doğrulandı
        UPDATE CreditCardStatement SET PaidAmount = PaidAmount + @Amount WHERE Id = @StatementId;
        UPDATE CreditCard SET AvailableLimit = AvailableLimit + @Amount WHERE Id = @CardId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_DepositNote — Senedi bankaya tahsile verme
-- NE YAPAR: PORTFOLIO statüsündeki senedi IN_BANK'a taşır; hangi banka hesabına verildiğini
--   ve tarihi kaydeder. sp_DepositCheque'nin senet karşılığıdır.
-- PARAMETRELERİ:
--   @NoteId      UNIQUEIDENTIFIER — işlem görecek senet kimliği
--   @AccountId   UNIQUEIDENTIFIER — senedin tahsile verileceği banka hesabı kimliği
--   @DepositDate DATETIME2 (NULL) — tahsil tarihi; NULL ise GETUTCDATE()
--   @UserId      UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
-- SIDE EFFECTS: PromissoryNote (UPDATE — Status=IN_BANK, DepositedToAccountId, DepositedAt)
-- THROW: 51310 — senet bulunamadı; 51311 — statü PORTFOLIO değil
-- BAĞIMLILIK: yok
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_DepositNote
    @NoteId      UNIQUEIDENTIFIER,
    @AccountId   UNIQUEIDENTIFIER,
    @CompanyId   UNIQUEIDENTIFIER,
    @DepositDate DATETIME2 = NULL,
    @UserId      UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Status NVARCHAR(20);
    SELECT @Status = Status FROM PromissoryNote WHERE Id = @NoteId AND CompanyId = @CompanyId AND IsDeleted = 0;

    IF @Status IS NULL THROW 51310, N'Senet bulunamadı.', 1;
    IF @Status <> 'PORTFOLIO' THROW 51311, N'Sadece portföydeki senetler bankaya verilebilir.', 1;

    UPDATE PromissoryNote
    SET Status               = 'IN_BANK',
        DepositedToAccountId = @AccountId,
        DepositedAt          = ISNULL(@DepositDate, GETUTCDATE()),
        UpdatedAt            = GETUTCDATE(),
        UpdatedBy            = @UserId
    WHERE Id = @NoteId;
END
GO

-- =============================================================================
-- sp_CollectNote — Senet tahsili (banka tarafından ödeme alındı)
-- NE YAPAR: IN_BANK statüsündeki senedi COLLECTED'a taşır; FinancialTransaction (INCOME)
--   ve AccountMovement (Credit — müşteri borcu kapandı) kaydı oluşturur.
-- PARAMETRELERİ:
--   @NoteId      UNIQUEIDENTIFIER — tahsil edilecek senet kimliği
--   @CollectDate DATETIME2 (NULL) — tahsilat tarihi; NULL ise GETUTCDATE()
--   @UserId      UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
-- SIDE EFFECTS: PromissoryNote (UPDATE — Status=COLLECTED, CollectedAt);
--   FinancialTransaction (INSERT — INCOME); AccountMovement (INSERT — Credit)
-- THROW: 50610 — senet bulunamadı; 50612 — statü IN_BANK değil; 50613 — banka hesabı yok
-- BAĞIMLILIK: sp_GuardPeriodOpen (dönem kilidi)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CollectNote
    @NoteId      UNIQUEIDENTIFIER,
    @CompanyId   UNIQUEIDENTIFIER,
    @CollectDate DATETIME2 = NULL,
    @UserId      UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20), @Amount DECIMAL(18,2),
                @AccountId UNIQUEIDENTIFIER, @PartnerId UNIQUEIDENTIFIER,
                @NoteNo NVARCHAR(50);

        SELECT @Status = Status, @Amount = Amount, @AccountId = DepositedToAccountId,
               @PartnerId = PartnerId, @NoteNo = NoteNo
        FROM PromissoryNote WHERE Id = @NoteId AND CompanyId = @CompanyId AND IsDeleted = 0;

        IF @Status IS NULL THROW 50610, N'Senet bulunamadı.', 1;
        IF @Status <> 'IN_BANK' THROW 50612, N'Sadece bankaya verilmiş senetler tahsil edilebilir.', 1;
        IF @AccountId IS NULL THROW 50613, N'Senetin tahsile verildiği banka hesabı bulunamadı.', 1;

        DECLARE @TxId UNIQUEIDENTIFIER = NEWID();
        DECLARE @Now  DATETIME2        = ISNULL(@CollectDate, GETUTCDATE());

        -- Dönem kilidi: tahsilat anının dönemi açık olmalı (Plan 14)
        DECLARE @userStr NVARCHAR(450) = CAST(@UserId AS NVARCHAR(450));
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @Now, @userStr;

        -- Mutabakat kilidi (Plan 19): senet borçlusu cariyle onaylı mutabakat varsa kilit
        EXEC dbo.sp_GuardPartnerReconciled @CompanyId, @PartnerId, @Now;

        INSERT INTO FinancialTransaction (
            Id, CompanyId, AccountId, TransactionDate, TransactionType,
            Amount, Currency, AmountTRY, PartnerId, Description,
            InstrumentType, InstrumentId, SourceDocType, SourceDocNo, CreatedBy)
        VALUES (
            @TxId, @CompanyId, @AccountId, @Now, 'INCOME',
            @Amount, 'TRY', @Amount, @PartnerId, N'Senet tahsili: ' + @NoteNo,
            'NOTE', @NoteId, 'NOTE_COLLECTION', @NoteNo, @UserId);

        UPDATE PromissoryNote
        SET Status = 'COLLECTED', CollectedAt = @Now, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @NoteId;

        -- Cari hesap defteri: senet tahsili → Credit (müşterinin borcu kapandı)
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        VALUES (
            NEWID(), @CompanyId, @PartnerId, @Now,
            0, @Amount, 'TRY',
            'NOTE_IN', @NoteId, @NoteNo,
            N'Senet Tahsili: ' + @NoteNo, CAST(@UserId AS NVARCHAR(450))
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_ReturnNote — Karşılıksız senet işlemi
-- NE YAPAR: IN_BANK veya PORTFOLIO statüsündeki senedi RETURNED'a taşır; iade sebebini
--   kaydeder. sp_ReturnCheque'nin senet karşılığıdır; finansal hareket üretmez.
-- PARAMETRELERİ:
--   @NoteId UNIQUEIDENTIFIER — karşılıksız işaretlenecek senet kimliği
--   @Reason NVARCHAR(500) — iade gerekçesi (zorunlu)
--   @UserId UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
-- SIDE EFFECTS: PromissoryNote (UPDATE — Status=RETURNED, ReturnReason)
-- THROW: 51314 — senet statüsü IN_BANK veya PORTFOLIO değil
-- BAĞIMLILIK: yok
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ReturnNote
    @NoteId    UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @Reason    NVARCHAR(500),
    @UserId    UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE PromissoryNote
    SET Status = 'RETURNED', ReturnReason = @Reason,
        UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
    WHERE Id = @NoteId AND CompanyId = @CompanyId AND Status IN ('IN_BANK', 'PORTFOLIO');

    IF @@ROWCOUNT = 0 THROW 51314, N'Senet karşılıksız olarak işaretlenemiyor (statü uygun değil).', 1;
END
GO

-- =============================================================================
-- sp_CreateLoan — Banka kredisi açma ve taksit takvimi oluşturma
-- NE YAPAR: Yeni Loan kaydı oluşturur; ANUITE/EQUAL_PRINCIPAL/BALLOON/SPOT yöntemlerinde
--   taksit takvimini (LoanPayment) matematiksel olarak hesaplayarak yazar. ROTATIVE/KMH/DBS
--   için taksit tablosu üretilmez. Banka hesabına açılış tutarı INCOME olarak işlenir.
--   Desteklenen hesap yöntemleri: ANUITE | EQUAL_PRINCIPAL | BALLOON | SPOT | ROTATIVE | KMH | DBS
-- PARAMETRELERİ:
--   @CompanyId         UNIQUEIDENTIFIER — şirket kimliği
--   @LoanNo            NVARCHAR(50) — kredi referans numarası
--   @BankName          NVARCHAR(200) — banka adı
--   @AccountId         UNIQUEIDENTIFIER — kredi tutarının yatacağı banka hesabı
--   @Principal         DECIMAL(18,2) — anapara tutarı
--   @InterestRate      DECIMAL(8,4) — yıllık faiz oranı (yüzde)
--   @TermMonths        INT — vade ay sayısı
--   @StartDate         DATE — kredi başlangıç tarihi
--   @CalcMethod        NVARCHAR(30) — hesap yöntemi (varsayılan: ANUITE)
--   @GracePeriodMonths INT — ödemesiz dönem ay sayısı (varsayılan: 0)
--   @BalloonAmount     DECIMAL(18,2) (NULL) — BALLOON yöntemi için son taksit tutarı
--   @UserId            UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
--   @NewLoanId         UNIQUEIDENTIFIER OUTPUT — oluşturulan kredi kimliği
-- SIDE EFFECTS: Loan (INSERT); LoanPayment (INSERT — taksit sayısı kadar, tablo yönteme göre);
--   FinancialTransaction (INSERT — INCOME, ROTATIVE/KMH/DBS hariç)
-- THROW: 51301 — anapara ≤ 0; 51302 — faiz negatif; 51303 — geçersiz yöntem;
--   51304 — vade ay sayısı gerekli; 51305 — balon tutarı eksik; 51306 — balon ≥ anapara;
--   51307 — geçersiz ödemesiz dönem
-- BAĞIMLILIK: yok
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CreateLoan
    @CompanyId        UNIQUEIDENTIFIER,
    @LoanNo           NVARCHAR(50),
    @BankName         NVARCHAR(200),
    @AccountId        UNIQUEIDENTIFIER,
    @Principal        DECIMAL(18,2),
    @InterestRate     DECIMAL(8,4),
    @TermMonths       INT,
    @StartDate        DATE,
    @CalcMethod       NVARCHAR(30) = 'ANUITE',
    @GracePeriodMonths INT          = 0,
    @BalloonAmount    DECIMAL(18,2) = NULL,
    @UserId           UNIQUEIDENTIFIER = NULL,
    @NewLoanId        UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi (Plan 14): kredi başlangıç tarihinin dönemi açık olmalı; LOCKED/CLOSED → THROW
        DECLARE @guardDate DATETIME2 = @StartDate;
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @guardDate, @UserId;

        -- Guard'lar
        IF @Principal <= 0
            THROW 51301, N'Anapara tutarı pozitif olmalıdır.', 1;
        IF @InterestRate < 0
            THROW 51302, N'Faiz oranı negatif olamaz.', 1;
        IF @CalcMethod NOT IN ('ANUITE','EQUAL_PRINCIPAL','BALLOON','SPOT','ROTATIVE','KMH','DBS')
            THROW 51303, N'Geçersiz kredi hesap yöntemi.', 1;
        IF @CalcMethod IN ('ANUITE','EQUAL_PRINCIPAL','BALLOON') AND (@TermMonths IS NULL OR @TermMonths <= 0)
            THROW 51304, N'Bu kredi tipinde vade ay sayısı pozitif olmalıdır.', 1;
        IF @CalcMethod = 'BALLOON' AND (@BalloonAmount IS NULL OR @BalloonAmount <= 0)
            THROW 51305, N'Balon ödemeli kredide @BalloonAmount belirtilmelidir.', 1;
        IF @CalcMethod = 'BALLOON' AND @BalloonAmount >= @Principal
            THROW 51306, N'Balon tutarı anaparadan büyük veya eşit olamaz.', 1;
        IF @GracePeriodMonths < 0 OR @GracePeriodMonths >= ISNULL(@TermMonths, 0)
            THROW 51307, N'Geçersiz ödemesiz dönem süresi.', 1;

        SET @NewLoanId = NEWID();

        DECLARE @MonthlyRate    DECIMAL(18,8) = @InterestRate / 100.0 / 12.0;
        DECLARE @MonthlyPayment DECIMAL(18,2) = 0;
        DECLARE @EndDate        DATE;

        -- ── Taksit hesap dağıtımı ─────────────────────────────────────
        IF @CalcMethod = 'ANUITE'
        BEGIN
            -- M = P * (r(1+r)^n) / ((1+r)^n - 1)
            SET @MonthlyPayment =
                CASE WHEN @MonthlyRate = 0
                     THEN @Principal / @TermMonths
                     ELSE @Principal * (@MonthlyRate * POWER(1 + @MonthlyRate, @TermMonths)) /
                          (POWER(1 + @MonthlyRate, @TermMonths) - 1)
                END;
            SET @EndDate = DATEADD(MONTH, @TermMonths, @StartDate);
        END
        ELSE IF @CalcMethod = 'EQUAL_PRINCIPAL'
        BEGIN
            -- Anapara payı sabit = P/n; faiz azalan bakiye üzerinden; MonthlyPayment 1. taksit referansı
            SET @MonthlyPayment = @Principal / @TermMonths + (@Principal * @MonthlyRate);
            SET @EndDate = DATEADD(MONTH, @TermMonths, @StartDate);
        END
        ELSE IF @CalcMethod = 'BALLOON'
        BEGIN
            -- İlk n-1 küçük + son taksit @BalloonAmount + kalan
            -- Aylık faiz: @Principal * @MonthlyRate; aylık anapara minimum (P - balon) / (n-1)
            SET @MonthlyPayment = (@Principal * @MonthlyRate) + ((@Principal - @BalloonAmount) / (@TermMonths - 1));
            SET @EndDate = DATEADD(MONTH, @TermMonths, @StartDate);
        END
        ELSE IF @CalcMethod = 'SPOT'
        BEGIN
            -- Tek taksit vade sonunda: P + (P * r * t/12)
            DECLARE @SpotInterest DECIMAL(18,2) = @Principal * (@InterestRate / 100.0) * (@TermMonths / 12.0);
            SET @MonthlyPayment = @Principal + @SpotInterest;
            SET @EndDate = DATEADD(MONTH, @TermMonths, @StartDate);
        END
        ELSE  -- ROTATIVE, KMH, DBS — taksit tablosu yok
        BEGIN
            SET @MonthlyPayment = 0;
            SET @EndDate = CASE WHEN @TermMonths IS NULL OR @TermMonths = 0
                                THEN DATEADD(YEAR, 1, @StartDate)
                                ELSE DATEADD(MONTH, @TermMonths, @StartDate)
                           END;
        END

        -- ── Loan başlık kaydı ─────────────────────────────────────────
        INSERT INTO Loan (
            Id, CompanyId, LoanNo, BankName, AccountId, LoanType, CalcMethod,
            Principal, InterestRate, TermMonths, StartDate, EndDate,
            MonthlyPayment, OutstandingBalance, GracePeriodMonths, BalloonAmount,
            Status, CreatedBy
        )
        VALUES (
            @NewLoanId, @CompanyId, @LoanNo, @BankName, @AccountId, 'COMMERCIAL', @CalcMethod,
            @Principal, @InterestRate, @TermMonths, @StartDate, @EndDate,
            @MonthlyPayment, @Principal, @GracePeriodMonths, @BalloonAmount,
            'ACTIVE', @UserId
        );

        -- ── Taksit takvimi üretimi (sadece anüite/eşit-anapara/balon/spot) ──
        IF @CalcMethod IN ('ANUITE','EQUAL_PRINCIPAL','BALLOON','SPOT')
        BEGIN
            DECLARE @i INT = 1;
            DECLARE @Bal DECIMAL(18,2) = @Principal;
            DECLARE @InterestThisMonth  DECIMAL(18,2);
            DECLARE @PrincipalThisMonth DECIMAL(18,2);
            DECLARE @TotalThisMonth     DECIMAL(18,2);

            WHILE @i <= @TermMonths
            BEGIN
                SET @InterestThisMonth = @Bal * @MonthlyRate;

                IF @i <= @GracePeriodMonths
                BEGIN
                    -- Ödemesiz dönem: sadece faiz ödenir, anapara değişmez
                    SET @PrincipalThisMonth = 0;
                    SET @TotalThisMonth     = @InterestThisMonth;
                END
                ELSE IF @CalcMethod = 'ANUITE'
                BEGIN
                    SET @PrincipalThisMonth = @MonthlyPayment - @InterestThisMonth;
                    SET @TotalThisMonth     = @MonthlyPayment;
                END
                ELSE IF @CalcMethod = 'EQUAL_PRINCIPAL'
                BEGIN
                    SET @PrincipalThisMonth = @Principal / @TermMonths;
                    SET @TotalThisMonth     = @PrincipalThisMonth + @InterestThisMonth;
                END
                ELSE IF @CalcMethod = 'BALLOON'
                BEGIN
                    IF @i < @TermMonths
                    BEGIN
                        -- Standart küçük taksit
                        SET @PrincipalThisMonth = (@Principal - @BalloonAmount) / (@TermMonths - 1);
                        SET @TotalThisMonth     = @PrincipalThisMonth + @InterestThisMonth;
                    END
                    ELSE
                    BEGIN
                        -- Son taksit: balon + kalan
                        SET @PrincipalThisMonth = @Bal;
                        SET @TotalThisMonth     = @PrincipalThisMonth + @InterestThisMonth;
                    END
                END
                ELSE IF @CalcMethod = 'SPOT'
                BEGIN
                    IF @i < @TermMonths
                    BEGIN
                        -- SPOT'ta tek taksit son ayda
                        SET @PrincipalThisMonth = 0;
                        SET @InterestThisMonth  = 0;
                        SET @TotalThisMonth     = 0;
                    END
                    ELSE
                    BEGIN
                        SET @PrincipalThisMonth = @Principal;
                        SET @InterestThisMonth  = @MonthlyPayment - @Principal;
                        SET @TotalThisMonth     = @MonthlyPayment;
                    END
                END

                INSERT INTO LoanPayment (
                    Id, LoanId, InstallmentNo, DueDate, PrincipalAmount, InterestAmount, TotalAmount
                )
                VALUES (
                    NEWID(), @NewLoanId, @i, DATEADD(MONTH, @i, @StartDate),
                    @PrincipalThisMonth, @InterestThisMonth, @TotalThisMonth
                );

                SET @Bal -= @PrincipalThisMonth;
                SET @i  += 1;
            END
        END
        -- ROTATIVE / KMH / DBS: taksit tablosu üretilmez; kullanım anında işlenir

        -- ── Kredi tutarı banka hesabına gelir olarak işlenir ──────────
        -- KMH ve ROTATIVE'de açılışta para gelmez; limit tahsisi sayılır
        IF @AccountId IS NOT NULL AND @CalcMethod NOT IN ('ROTATIVE','KMH','DBS')
        BEGIN
            INSERT INTO FinancialTransaction (
                Id, CompanyId, AccountId, TransactionDate, TransactionType,
                Amount, Currency, AmountTRY, Description,
                InstrumentType, InstrumentId, CreatedBy
            )
            VALUES (
                NEWID(), @CompanyId, @AccountId, @StartDate, 'INCOME',
                @Principal, 'TRY', @Principal,
                N'Kredi açılışı: ' + @LoanNo + N' (' + @CalcMethod + N')',
                'LOAN', @NewLoanId, @UserId
            );
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_PayLoanInstallment — Kredi taksit ödemesi
-- NE YAPAR: LoanPayment kaydını ödenmiş olarak işaretler, banka hesabından EXPENSE
--   hareketi yazar, kredinin anapara bakiyesini düşürür. Tüm taksitler ödendiyse
--   Loan.Status'u CLOSED'a getirir.
-- PARAMETRELERİ:
--   @PaymentId     UNIQUEIDENTIFIER — ödenecek LoanPayment kaydının kimliği
--   @PayDate       DATETIME2 (NULL) — ödeme tarihi; NULL ise GETUTCDATE()
--   @FromAccountId UNIQUEIDENTIFIER — ödemenin yapılacağı banka hesabı kimliği
--   @UserId        UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
-- SIDE EFFECTS: FinancialTransaction (INSERT — EXPENSE); LoanPayment (UPDATE — IsPaid=1,
--   PaidAmount, PaidAt, FinancialTransactionId); Loan (UPDATE — OutstandingBalance düşer;
--   tüm taksitler kapandıysa Status=CLOSED)
-- THROW: 60010 — taksit bulunamadı veya zaten ödenmiş
-- BAĞIMLILIK: Loan, LoanPayment
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PayLoanInstallment
    @PaymentId      UNIQUEIDENTIFIER,
    @CompanyId      UNIQUEIDENTIFIER,
    @PayDate        DATETIME2 = NULL,
    @FromAccountId  UNIQUEIDENTIFIER,
    @UserId         UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi (Plan 14): taksit ödeme tarihinin dönemi açık olmalı; LOCKED/CLOSED → THROW
        DECLARE @guardDate DATETIME2 = ISNULL(@PayDate, GETUTCDATE());
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @guardDate, @UserId;

        DECLARE @LoanId UNIQUEIDENTIFIER, @Total DECIMAL(18,2),
                @InstNo INT, @LoanNo NVARCHAR(50), @PrincipalPaid DECIMAL(18,2);

        -- IDOR koruması: taksit, aktif firmaya ait bir krediye bağlı olmalı (JOIN Loan + CompanyId)
        SELECT @LoanId = lp.LoanId, @Total = lp.TotalAmount, @InstNo = lp.InstallmentNo,
               @PrincipalPaid = lp.PrincipalAmount, @LoanNo = l.LoanNo
        FROM LoanPayment lp
        JOIN Loan l ON l.Id = lp.LoanId
        WHERE lp.Id = @PaymentId AND lp.IsPaid = 0 AND l.CompanyId = @CompanyId;

        IF @LoanId IS NULL
            THROW 55010, N'Ödeme bulunamadı veya zaten ödenmiş.', 1;

        DECLARE @TxId UNIQUEIDENTIFIER = NEWID();
        INSERT INTO FinancialTransaction (
            Id, CompanyId, AccountId, TransactionDate, TransactionType,
            Amount, Currency, AmountTRY, Description, InstrumentType, InstrumentId,
            SourceDocType, SourceDocNo, CreatedBy
        )
        VALUES (
            @TxId, @CompanyId, @FromAccountId, ISNULL(@PayDate, GETUTCDATE()), 'EXPENSE',
            @Total, 'TRY', @Total,
            N'Kredi taksit ödemesi: ' + @LoanNo + N' / ' + CAST(@InstNo AS NVARCHAR(10)),
            'LOAN', @LoanId, 'LOAN_INSTALLMENT',
            @LoanNo + '/' + CAST(@InstNo AS NVARCHAR(10)), @UserId
        );

        UPDATE LoanPayment
        SET IsPaid                 = 1,
            PaidAmount             = @Total,
            PaidAt                 = GETUTCDATE(),
            FinancialTransactionId = @TxId
        WHERE Id = @PaymentId;

        UPDATE Loan
        SET OutstandingBalance = OutstandingBalance - @PrincipalPaid,
            UpdatedAt          = GETUTCDATE(),
            UpdatedBy          = @UserId
        WHERE Id = @LoanId;

        -- Tüm taksitler ödendiyse kredi CLOSED
        IF NOT EXISTS (SELECT 1 FROM LoanPayment WHERE LoanId = @LoanId AND IsPaid = 0)
            UPDATE Loan SET Status = 'CLOSED' WHERE Id = @LoanId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- tvf_AccountBalance — Şirkete ait hesap bakiyeleri (CompanyId parametreli iTVF)
-- v_AccountBalance view yerine geçti: CompanyId filtresi imzada zorunlu.
-- =============================================================================
IF OBJECT_ID('dbo.v_AccountBalance', 'V') IS NOT NULL DROP VIEW dbo.v_AccountBalance;
GO
CREATE OR ALTER FUNCTION dbo.tvf_AccountBalance
(
    @CompanyId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        a.Id AS AccountId,
        a.Code,
        a.Name,
        a.AccountType,
        a.Currency,
        a.OpeningBalance
            + ISNULL(SUM(CASE
                  WHEN t.TransactionType IN ('INCOME', 'TRANSFER_IN')     THEN  t.AmountTRY
                  WHEN t.TransactionType IN ('EXPENSE', 'TRANSFER_OUT')   THEN -t.AmountTRY
                  ELSE 0 END), 0) AS Balance,
        MAX(t.TransactionDate) AS LastMovementDate,
        COUNT(t.Id) AS TransactionCount
    FROM FinancialAccount a
    LEFT JOIN FinancialTransaction t
        ON t.AccountId = a.Id AND t.IsDeleted = 0
    WHERE a.CompanyId = @CompanyId AND a.IsDeleted = 0
    GROUP BY a.Id, a.Code, a.Name, a.AccountType, a.Currency, a.OpeningBalance
);
GO

-- =============================================================================
-- tvf_PaymentPlanAging — Yaşlandırma (alacak/borç), CompanyId parametreli iTVF
-- v_PaymentPlanAging view yerine geçti: CompanyId filtresi imzada zorunlu.
-- =============================================================================
IF OBJECT_ID('dbo.v_PaymentPlanAging', 'V') IS NOT NULL DROP VIEW dbo.v_PaymentPlanAging;
GO
CREATE OR ALTER FUNCTION dbo.tvf_PaymentPlanAging
(
    @CompanyId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        pp.PartnerId,
        p.Name AS PartnerName,
        pp.Direction,
        SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) <= 0
                 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS NotDue,
        SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) BETWEEN 1 AND 30
                 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS Days1_30,
        SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) BETWEEN 31 AND 60
                 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS Days31_60,
        SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) BETWEEN 61 AND 90
                 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS Days61_90,
        SUM(CASE WHEN DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) > 90
                 THEN pp.Amount - pp.PaidAmount ELSE 0 END) AS Over90,
        SUM(pp.Amount - pp.PaidAmount) AS TotalOpen,
        -- Yöne göre açık sipariş tutarı: alacak (RECEIVABLE) → açık SO, borç (PAYABLE) → açık PO.
        -- Henüz sevk/mal kabul yapılmamış kısım: (QtyOrdered - QtySevk/Kabul) * Price.
        CASE WHEN pp.Direction = 'RECEIVABLE' THEN
            ISNULL((SELECT SUM((sol.QtyOrdered - sol.QtyShipped) * sol.Price)
                    FROM SalesOrderHeader soh
                    JOIN SalesOrderLine sol ON sol.HeaderId = soh.Id
                    WHERE soh.PartnerId = pp.PartnerId AND soh.CompanyId = @CompanyId
                      AND soh.Status IN ('APPROVED', 'POSTED') AND soh.IsDeleted = 0
                      AND sol.QtyOrdered > sol.QtyShipped), 0)
             WHEN pp.Direction = 'PAYABLE' THEN
            ISNULL((SELECT SUM((pol.QtyOrdered - pol.QtyReceived) * pol.Price)
                    FROM PurchaseOrderHeader poh
                    JOIN PurchaseOrderLine pol ON pol.HeaderId = poh.Id
                    WHERE poh.PartnerId = pp.PartnerId AND poh.CompanyId = @CompanyId
                      AND poh.Status IN ('APPROVED', 'POSTED') AND poh.IsDeleted = 0
                      AND pol.QtyOrdered > pol.QtyReceived), 0)
             ELSE 0 END AS OpenOrderAmount
    FROM PaymentPlan pp
    JOIN Partner p ON p.Id = pp.PartnerId
    WHERE pp.CompanyId = @CompanyId
      AND pp.Status IN ('OPEN', 'PARTIAL', 'OVERDUE') AND pp.IsDeleted = 0
    GROUP BY pp.PartnerId, p.Name, pp.Direction
);
GO

-- =============================================================================
-- Plan 09 — Cari Hesap Defteri (AccountMovement) sorgu fonksiyonları
-- =============================================================================

-- fn_PartnerBalanceAsOf — bir tarihten ÖNCEKİ net bakiye (DEVİR). SUM(Debit-Credit).
CREATE OR ALTER FUNCTION dbo.fn_PartnerBalanceAsOf
(
    @CompanyId UNIQUEIDENTIFIER,
    @PartnerId UNIQUEIDENTIFIER,
    @AsOf      DATETIME2
)
RETURNS DECIMAL(18,2)
AS
BEGIN
    DECLARE @bal DECIMAL(18,2);
    SELECT @bal = ISNULL(SUM(Debit - Credit), 0)
    FROM AccountMovement
    WHERE CompanyId = @CompanyId AND PartnerId = @PartnerId
      AND MovementDate < @AsOf;
    RETURN ISNULL(@bal, 0);
END
GO

-- tvf_PartnerBalance — cari bazlı net bakiye + toplam debit/credit (tüm zaman). KPI kaynağı.
-- NetBalance > 0: cari bize borçlu (alacağımız). < 0: biz cariye borçluyuz.
CREATE OR ALTER FUNCTION dbo.tvf_PartnerBalance
(
    @CompanyId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        am.PartnerId,
        SUM(am.Debit)             AS TotalDebit,
        SUM(am.Credit)            AS TotalCredit,
        SUM(am.Debit - am.Credit) AS NetBalance
    FROM AccountMovement am
    WHERE am.CompanyId = @CompanyId
    GROUP BY am.PartnerId
);
GO

-- tvf_AccountLedger — bir cariye ait [From..To] hareketleri (DEVİR hariç, tarih sırası).
-- DEVİR ayrıca fn_PartnerBalanceAsOf(@From) ile alınır; yürüyen bakiye UI'da hesaplanır.
CREATE OR ALTER FUNCTION dbo.tvf_AccountLedger
(
    @CompanyId UNIQUEIDENTIFIER,
    @PartnerId UNIQUEIDENTIFIER,
    @From      DATETIME2,
    @To        DATETIME2
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        am.Id, am.MovementDate, am.SourceDocType, am.SourceDocNo,
        am.Description, am.Debit, am.Credit
    FROM AccountMovement am
    WHERE am.CompanyId = @CompanyId AND am.PartnerId = @PartnerId
      AND am.MovementDate >= @From
      AND am.MovementDate < DATEADD(DAY, 1, @To)
);
GO

-- =============================================================================
-- sp_ReceivingPost — Mal kabul onaylama (DRAFT → POSTED)
-- NE YAPAR: Mal kabul belgesini POSTED yapar; her satır için StockMovement (RECEIPT)
--   oluşturur, ilgili PO satırlarının QtyReceived değerini artırır, tüm satırlar
--   tamamlandıysa PO'yu RECEIVED'a taşır ve Moving Average maliyet günceller.
-- PARAMETRELERİ:
--   @HeaderId  UNIQUEIDENTIFIER — onaylanacak mal kabul başlık kimliği
--   @CompanyId UNIQUEIDENTIFIER — şirket kimliği
--   @UserId    NVARCHAR(450) — işlemi yapan kullanıcı kimliği (string)
-- SIDE EFFECTS: StockMovement (INSERT — RECEIPT, her satır için); ReceivingHeader (UPDATE — POSTED);
--   PurchaseOrderLine (UPDATE — QtyReceived artırılır); PurchaseOrderHeader (UPDATE — RECEIVED,
--   tüm satırlar tamamlandıysa); ItemCost (UPSERT — AvgCost, OnHandQty her ürün için)
-- THROW: 50001 — mal kabul bulunamadı; sp_ValidateStatusTransition hataları
-- BAĞIMLILIK: sp_ValidateStatusTransition, sp_UpdateItemCostMovingAvg
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ReceivingPost
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi (Plan 14): onay anının dönemi açık olmalı; LOCKED/CLOSED → THROW (post↔reverse simetrisi)
        DECLARE @nowGuard DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowGuard, @UserId;

        DECLARE @WarehouseId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50),
                @Status NVARCHAR(20), @POId UNIQUEIDENTIFIER;

        SELECT @WarehouseId = WarehouseId, @DocNo = DocNo,
               @Status = Status, @POId = PurchaseOrderId
        FROM ReceivingHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId;

        IF @WarehouseId IS NULL THROW 50001, N'Mal kabul belgesi bulunamadı.', 1;

        EXEC dbo.sp_ValidateStatusTransition @CompanyId, 'RECEIVING', @Status, 'POSTED', @UserId;

        -- Mal kabul bin'i: önce kabul alanı (IsReceivingArea), yoksa depodaki herhangi bir bin
        DECLARE @ReceivingBinId UNIQUEIDENTIFIER;
        SELECT TOP 1 @ReceivingBinId = Id
        FROM Bin WHERE WarehouseId = @WarehouseId AND IsReceivingArea = 1;

        IF @ReceivingBinId IS NULL
            SELECT TOP 1 @ReceivingBinId = Id
            FROM Bin WHERE WarehouseId = @WarehouseId AND IsActive = 1 AND IsDeleted = 0
            ORDER BY SortNo, Code;

        -- İş kuralı: depoda hiç hücre (bin) tanımlı değilse stok hareketi yazılamaz
        IF @ReceivingBinId IS NULL
            THROW 50012, N'Seçilen depoda hücre (bin) tanımlı değil. Önce depo hücresi oluşturun.', 1;

        -- İade/karantina bin'i (Plan 28): sipariş fazlası buraya; yoksa kabul bin'ine düş.
        -- TASARIM (sql-sp-reviewer): iade RECEIPT fiziksel stok bakiyesini (tvf_InventoryBalance) artırır
        -- ama ItemCost.OnHandQty + AvgCost'a GİRMEZ (yalnız kabul/QtyBase maliyetlenir) — iade malı tedarikçiye
        -- geri döneceği için COGS havuzuna katılmaz. Karantina bin'i ayrı sayılır; bakiye≠maliyet-OnHand drift'i kasıtlı.
        DECLARE @ReturnBinId UNIQUEIDENTIFIER;
        SELECT TOP 1 @ReturnBinId = Id FROM Bin
        WHERE WarehouseId = @WarehouseId AND IsReturnArea = 1 AND IsActive = 1 AND IsDeleted = 0;
        IF @ReturnBinId IS NULL SET @ReturnBinId = @ReceivingBinId;

        -- StockMovement: her satira PO'dan UnitCost ata
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, LotNo,
             UnitCost, CreatedBy, BranchId)
        SELECT
            @CompanyId, @WarehouseId, @ReceivingBinId, rl.ItemId, 'RECEIPT',
            rl.QtyBase, rl.UomId, rl.QtyBase, 'RECEIVING', @HeaderId, @DocNo, rl.LotNo,
            ISNULL(pol.Price, 0), @UserId,
            ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @WarehouseId), dbo.fn_DefaultBranchId(@CompanyId))
        FROM ReceivingLine rl
        LEFT JOIN PurchaseOrderLine pol ON pol.Id = rl.PurchaseOrderLineId
        WHERE rl.HeaderId = @HeaderId AND rl.QtyBase > 0;

        -- Plan 28: sipariş fazlası (ReturnQty) → iade/karantina bin'ine ayrı RECEIPT (RECEIVING_RETURN).
        -- PO QtyReceived'a SAYILMAZ (sadece kabul edilen sayılır). Tedarikçiye iade/red için ayrı durur.
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, LotNo,
             UnitCost, CreatedBy, BranchId)
        SELECT
            @CompanyId, @WarehouseId, @ReturnBinId, rl.ItemId, 'RECEIPT',
            rl.ReturnQty, rl.UomId, rl.ReturnQty, 'RECEIVING_RETURN', @HeaderId, @DocNo, rl.LotNo,
            ISNULL(pol.Price, 0), @UserId,
            ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @WarehouseId), dbo.fn_DefaultBranchId(@CompanyId))
        FROM ReceivingLine rl
        LEFT JOIN PurchaseOrderLine pol ON pol.Id = rl.PurchaseOrderLineId
        WHERE rl.HeaderId = @HeaderId AND rl.ReturnQty > 0;

        UPDATE pol
        SET pol.QtyReceived = pol.QtyReceived + rl.QtyBase
        FROM PurchaseOrderLine pol
        JOIN ReceivingLine rl ON rl.PurchaseOrderLineId = pol.Id
        WHERE rl.HeaderId = @HeaderId;

        IF @POId IS NOT NULL
           AND NOT EXISTS (
               SELECT 1 FROM PurchaseOrderLine
               WHERE HeaderId = @POId AND QtyReceived < QtyOrdered
           )
            UPDATE PurchaseOrderHeader SET Status = 'RECEIVED' WHERE Id = @POId;

        UPDATE ReceivingHeader
        SET Status = 'POSTED', UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @HeaderId;

        -- WIRE: Her urun icin maliyet guncelle (Moving Avg)
        DECLARE @ItemId UNIQUEIDENTIFIER, @Qty DECIMAL(18,6), @UC DECIMAL(18,4);
        DECLARE c_lines CURSOR LOCAL FAST_FORWARD FOR
            SELECT rl.ItemId, SUM(rl.QtyBase),
                   ISNULL(AVG(CAST(pol.Price AS DECIMAL(18,4))), 0)
            FROM ReceivingLine rl
            LEFT JOIN PurchaseOrderLine pol ON pol.Id = rl.PurchaseOrderLineId
            WHERE rl.HeaderId = @HeaderId
            GROUP BY rl.ItemId;
        OPEN c_lines;
        FETCH NEXT FROM c_lines INTO @ItemId, @Qty, @UC;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            EXEC dbo.sp_UpdateItemCostMovingAvg
                @CompanyId = @CompanyId, @ItemId = @ItemId,
                @WarehouseId = @WarehouseId, @Qty = @Qty,
                @UnitCost = @UC, @MovementType = 'RECEIPT';
            FETCH NEXT FROM c_lines INTO @ItemId, @Qty, @UC;
        END
        CLOSE c_lines; DEALLOCATE c_lines;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'c_lines') >= -1 BEGIN CLOSE c_lines; DEALLOCATE c_lines; END
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_ShippingPost — Sevkiyat onaylama (DRAFT → POSTED)
-- NE YAPAR: Sevkiyat belgesini POSTED yapar; her satır için StockMovement (ISSUE — negatif
--   QtyBase) oluşturur; SO satırlarının QtyShipped değerini artırır; ItemCost OnHandQty'yi
--   düşürür. Parameter.InvoiceMode='INSTANT' ise sp_GenerateSalesInvoiceFromShipping çağrılır.
-- PARAMETRELERİ:
--   @HeaderId  UNIQUEIDENTIFIER — onaylanacak sevkiyat başlık kimliği
--   @CompanyId UNIQUEIDENTIFIER — şirket kimliği
--   @UserId    NVARCHAR(450) — işlemi yapan kullanıcı kimliği (string)
-- SIDE EFFECTS: StockMovement (INSERT — ISSUE, her satır için); ShippingHeader (UPDATE — POSTED);
--   SalesOrderLine (UPDATE — QtyShipped artırılır); ItemCost (UPDATE — OnHandQty düşer);
--   SalesInvoice + PaymentPlan + AccountMovement (InvoiceMode=INSTANT ise, dolaylı)
-- THROW: 50001 — sevkiyat bulunamadı; sp_ValidateStatusTransition hataları
-- BAĞIMLILIK: sp_ValidateStatusTransition, sp_UpdateItemCostMovingAvg,
--   sp_GenerateSalesInvoiceFromShipping (koşullu), Parameter (InvoiceMode)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ShippingPost
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi (Plan 14): onay anının dönemi açık olmalı; LOCKED/CLOSED → THROW (post↔reverse simetrisi)
        DECLARE @nowGuard DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowGuard, @UserId;

        DECLARE @WarehouseId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50), @Status NVARCHAR(20);
        SELECT @WarehouseId = WarehouseId, @DocNo = DocNo, @Status = Status
        FROM ShippingHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId;

        IF @WarehouseId IS NULL THROW 50001, N'Sevkiyat belgesi bulunamadı.', 1;
        EXEC dbo.sp_ValidateStatusTransition @CompanyId, 'SHIPMENT', @Status, 'POSTED', @UserId;

        DECLARE @PickingBinId UNIQUEIDENTIFIER;
        SELECT TOP 1 @PickingBinId = Id
        FROM Bin WHERE WarehouseId = @WarehouseId AND IsPickingArea = 1;

        -- Stok hareketi: her satir icin ISSUE (eksi tabanlanan QtyBase)
        -- UnitCost ItemCost.AvgCost'tan alinir (satış maliyeti / COGS)
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, LotNo,
             UnitCost, CreatedBy, BranchId)
        SELECT
            @CompanyId, @WarehouseId, ISNULL(sl.BinId, @PickingBinId), sl.ItemId, 'ISSUE',
            -sl.QtyBase, sl.UomId, sl.QtyBase, 'SHIPPING', @HeaderId, @DocNo, sl.LotNo,
            ISNULL(ic.AvgCost, 0), @UserId,
            ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @WarehouseId), dbo.fn_DefaultBranchId(@CompanyId))
        FROM ShippingLine sl
        LEFT JOIN ItemCost ic ON ic.CompanyId = @CompanyId AND ic.ItemId = sl.ItemId
        WHERE sl.HeaderId = @HeaderId;

        -- SO satirlarini guncelle
        UPDATE sol
        SET sol.QtyShipped = sol.QtyShipped + sl.QtyBase
        FROM SalesOrderLine sol
        JOIN ShippingLine sl ON sl.SalesOrderLineId = sol.Id
        WHERE sl.HeaderId = @HeaderId;

        -- Shipping POSTED
        UPDATE ShippingHeader
        SET Status = 'POSTED', PostedAt = GETUTCDATE(),
            UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @HeaderId;

        -- WIRE: ItemCost OnHandQty dusur
        DECLARE @ItemId UNIQUEIDENTIFIER, @Qty DECIMAL(18,6);
        DECLARE c_lines CURSOR LOCAL FAST_FORWARD FOR
            SELECT ItemId, SUM(QtyBase) FROM ShippingLine WHERE HeaderId = @HeaderId
            GROUP BY ItemId;
        OPEN c_lines;
        FETCH NEXT FROM c_lines INTO @ItemId, @Qty;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            EXEC dbo.sp_UpdateItemCostMovingAvg
                @CompanyId = @CompanyId, @ItemId = @ItemId,
                @WarehouseId = @WarehouseId, @Qty = @Qty,
                @UnitCost = NULL, @MovementType = 'ISSUE';
            FETCH NEXT FROM c_lines INTO @ItemId, @Qty;
        END
        CLOSE c_lines; DEALLOCATE c_lines;

        -- WIRE: Parameter.InvoiceMode = INSTANT ise otomatik fatura
        DECLARE @InvoiceMode NVARCHAR(50);
        SELECT @InvoiceMode = Value FROM Parameter
        WHERE CompanyId = @CompanyId AND Code = 'InvoiceMode';

        IF @InvoiceMode = 'INSTANT'
        BEGIN
            DECLARE @NewInvoiceId UNIQUEIDENTIFIER;
            EXEC dbo.sp_GenerateSalesInvoiceFromShipping
                @ShippingId = @HeaderId, @UserId = NULL,
                @NewInvoiceId = @NewInvoiceId OUTPUT;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'c_lines') >= -1 BEGIN CLOSE c_lines; DEALLOCATE c_lines; END
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_GeneratePaymentPlanFromPO — PO'dan tedarikçi ödeme planı üretme
-- NE YAPAR: POSTED PO için tedarikçiye ait PAYABLE ödeme planı taksitlerini oluşturur.
--   Mevcut PaymentPlan kayıtları önce silinir (idempotent). Taksit sayısı ve vade,
--   PO başlığındaki PaymentInstallments ve PaymentTermDays alanlarından alınır.
-- PARAMETRELERİ:
--   @PoHeaderId UNIQUEIDENTIFIER — kaynak PO başlık kimliği
--   @UserId     UNIQUEIDENTIFIER (NULL) — işlemi başlatan kullanıcı
-- SIDE EFFECTS: PaymentPlan (DELETE mevcut; INSERT — PAYABLE/OPEN, taksit sayısı kadar)
-- THROW: 71001 — PO bulunamadı; 71002 — sipariş toplamı sıfır
-- BAĞIMLILIK: PurchaseOrderHeader, PurchaseOrderLine, Partner (PaymentTermDays yedek)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_GeneratePaymentPlanFromPO
    @PoHeaderId UNIQUEIDENTIFIER,
    @UserId     UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CompanyId UNIQUEIDENTIFIER, @PartnerId UNIQUEIDENTIFIER,
                @OrderNo NVARCHAR(50), @OrderDate DATETIME2,
                @PaymentTermDays INT, @Installments INT, @TotalAmount DECIMAL(18,2);

        SELECT @CompanyId = h.CompanyId, @PartnerId = h.PartnerId,
               @OrderNo = h.OrderNo, @OrderDate = h.OrderDate,
               @PaymentTermDays = ISNULL(h.PaymentTermDays, ISNULL(p.PaymentTermDays, 30)),
               @Installments = ISNULL(h.PaymentInstallments, 1),
               @TotalAmount = (SELECT SUM(QtyOrdered * Price) FROM PurchaseOrderLine WHERE HeaderId = @PoHeaderId)
        FROM PurchaseOrderHeader h
        JOIN Partner p ON p.Id = h.PartnerId
        WHERE h.Id = @PoHeaderId;

        IF @CompanyId IS NULL THROW 56001, N'Satınalma siparişi bulunamadı.', 1;
        IF @TotalAmount IS NULL OR @TotalAmount = 0 THROW 56002, N'Sipariş toplamı sıfır.', 1;

        -- Mevcut PaymentPlan kayıtları varsa sil
        DELETE FROM PaymentPlan
        WHERE SourceDocType = 'PURCHASE_ORDER' AND SourceDocId = @PoHeaderId;

        -- Taksit bazli olusum
        DECLARE @InstallmentAmount DECIMAL(18,2) = @TotalAmount / @Installments;
        DECLARE @i INT = 1;
        DECLARE @DueDate DATE;

        WHILE @i <= @Installments
        BEGIN
            SET @DueDate = DATEADD(DAY, @PaymentTermDays * @i / @Installments, CAST(@OrderDate AS DATE));

            INSERT INTO PaymentPlan
                (Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
                 PartnerId, Direction, InstallmentNo, TotalInstallments,
                 DueDate, Amount, Status)
            VALUES
                (NEWID(), @CompanyId, 'PURCHASE_ORDER', @PoHeaderId, @OrderNo,
                 @PartnerId, 'PAYABLE', @i, @Installments,
                 @DueDate, @InstallmentAmount, 'OPEN');

            SET @i += 1;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION; THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_PoPost — Satınalma siparişi onaylama (DRAFT → POSTED)
-- NE YAPAR: PO'yu POSTED statüsüne taşır ve ardından sp_GeneratePaymentPlanFromPO
--   çağırarak tedarikçiye ödeme planı üretir.
-- PARAMETRELERİ:
--   @PoHeaderId UNIQUEIDENTIFIER — onaylanacak PO başlık kimliği
--   @CompanyId  UNIQUEIDENTIFIER — şirket kimliği
--   @UserId     UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
-- SIDE EFFECTS: PurchaseOrderHeader (UPDATE — Status=POSTED, PostedAt);
--   PaymentPlan (INSERT — dolaylı, sp_GeneratePaymentPlanFromPO üzerinden)
-- THROW: 72001 — PO bulunamadı; sp_ValidateStatusTransition hataları;
--   sp_GeneratePaymentPlanFromPO hataları
-- BAĞIMLILIK: sp_ValidateStatusTransition, sp_GeneratePaymentPlanFromPO
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PoPost
    @PoHeaderId UNIQUEIDENTIFIER,
    @CompanyId  UNIQUEIDENTIFIER,
    @UserId     UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20);
        SELECT @Status = Status FROM PurchaseOrderHeader WITH (UPDLOCK)
        WHERE Id = @PoHeaderId AND CompanyId = @CompanyId;

        IF @Status IS NULL THROW 56010, N'Satınalma siparişi bulunamadı.', 1;

        EXEC dbo.sp_ValidateStatusTransition @CompanyId, 'PURCHASE_ORDER', @Status, 'POSTED', @UserId;

        UPDATE PurchaseOrderHeader
        SET Status = 'POSTED', PostedAt = GETUTCDATE(),
            UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @PoHeaderId;

        EXEC dbo.sp_GeneratePaymentPlanFromPO @PoHeaderId = @PoHeaderId, @UserId = @UserId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION; THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_AutoClosePayments — Tahsilat/ödemeyi FIFO mantığıyla ödeme planlarına dağıtma
-- NE YAPAR: Gelen tutar, ilgili partnerin açık (OPEN/PARTIAL/OVERDUE) PaymentPlan
--   kayıtlarına vade sırasıyla (FIFO) dağıtılır; fazla tutar PREPAYMENT olarak kaydedilir.
-- PARAMETRELERİ:
--   @CompanyId     UNIQUEIDENTIFIER — şirket kimliği
--   @PartnerId     UNIQUEIDENTIFIER — ilgili cari hesap kimliği
--   @Direction     NVARCHAR(10) — RECEIVABLE (tahsilat) | PAYABLE (ödeme)
--   @Amount        DECIMAL(18,2) — dağıtılacak toplam tutar
--   @TransactionId UNIQUEIDENTIFIER — referans FinancialTransaction kimliği
--   @UserId        UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
-- SIDE EFFECTS: PaymentPlan (UPDATE — PaidAmount, Status=PAID/PARTIAL;
--   INSERT — PREPAYMENT, kalan tutar varsa)
-- THROW: yok (cursor boşsa sessizce avans kaydı açar)
-- BAĞIMLILIK: PaymentPlan (FIFO cursor)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_AutoClosePayments
    @CompanyId    UNIQUEIDENTIFIER,
    @PartnerId    UNIQUEIDENTIFIER,
    @Direction    NVARCHAR(10),         -- RECEIVABLE (tahsilat) / PAYABLE (ödeme)
    @Amount       DECIMAL(18,2),
    @TransactionId UNIQUEIDENTIFIER,    -- referans: FinancialTransaction.Id
    @UserId       UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- İş kuralı: girdi doğrulama (PaymentPlan/FIFO aralığı 51250-51299)
        IF @Amount <= 0
            THROW 51250, N'Dağıtılacak tutar pozitif olmalıdır.', 1;
        IF @Direction NOT IN ('RECEIVABLE', 'PAYABLE')
            THROW 51251, N'Geçersiz yön: RECEIVABLE veya PAYABLE olmalıdır.', 1;

        DECLARE @Remaining DECIMAL(18,2) = @Amount;
        DECLARE @PpId UNIQUEIDENTIFIER, @PpRemaining DECIMAL(18,2), @PpAmount DECIMAL(18,2),
                @PpPaid DECIMAL(18,2), @PpSrcType NVARCHAR(30), @PpSrcId UNIQUEIDENTIFIER;

        -- Ödeme/tahsilat hareketinin AM karşılığı (reconciliation için)
        -- INCOME→COLLECTION (Credit), EXPENSE→PAYMENT (Debit) — SourceDocId=@TransactionId
        DECLARE @PayAmId UNIQUEIDENTIFIER;
        SELECT TOP 1 @PayAmId = Id FROM AccountMovement
        WHERE CompanyId = @CompanyId AND SourceDocId = @TransactionId
          AND SourceDocType IN ('COLLECTION','PAYMENT')
        ORDER BY CreatedAt;

        -- FIFO: en eski vade ilk kapatılır
        DECLARE c_pp CURSOR LOCAL FAST_FORWARD FOR
            SELECT Id, Amount - PaidAmount, Amount, PaidAmount, SourceDocType, SourceDocId
            FROM PaymentPlan
            WHERE CompanyId = @CompanyId
              AND PartnerId = @PartnerId
              AND Direction = @Direction
              AND Status IN ('OPEN', 'PARTIAL', 'OVERDUE')
              AND IsDeleted = 0
            ORDER BY DueDate ASC, CreatedAt ASC;

        OPEN c_pp;
        FETCH NEXT FROM c_pp INTO @PpId, @PpRemaining, @PpAmount, @PpPaid, @PpSrcType, @PpSrcId;

        WHILE @@FETCH_STATUS = 0 AND @Remaining > 0
        BEGIN
            DECLARE @ApplyAmount DECIMAL(18,2) =
                CASE WHEN @Remaining >= @PpRemaining THEN @PpRemaining ELSE @Remaining END;

            UPDATE PaymentPlan
            SET PaidAmount             = @PpPaid + @ApplyAmount,
                Status                 = CASE WHEN (@PpPaid + @ApplyAmount) >= @PpAmount THEN 'PAID' ELSE 'PARTIAL' END,
                PaidAt                 = CASE WHEN (@PpPaid + @ApplyAmount) >= @PpAmount THEN GETUTCDATE() ELSE PaidAt END,
                FinancialTransactionId = COALESCE(FinancialTransactionId, @TransactionId),
                UpdatedAt              = GETUTCDATE()
            WHERE Id = @PpId;

            -- Açık-kalem kapama (plan 18): faturanın AM hareketi ↔ ödeme AM hareketi eşleştir
            -- Fatura AM: SourceDocId=PaymentPlan.SourceDocId (başlık-bazlı, simetri kuruldu)
            DECLARE @InvAmId UNIQUEIDENTIFIER;
            SELECT TOP 1 @InvAmId = Id FROM AccountMovement
            WHERE CompanyId = @CompanyId AND SourceDocId = @PpSrcId
              AND SourceDocType = @PpSrcType
            ORDER BY CreatedAt;

            IF @InvAmId IS NOT NULL AND @PayAmId IS NOT NULL
            BEGIN
                -- DebitMovementId = Debit>0 olan, CreditMovementId = Credit>0 olan
                -- RECEIVABLE: fatura(Debit) + tahsilat(Credit) · PAYABLE: ödeme(Debit) + fatura(Credit)
                DECLARE @reconDebit UNIQUEIDENTIFIER =
                    CASE WHEN @Direction = 'RECEIVABLE' THEN @InvAmId ELSE @PayAmId END;
                DECLARE @reconCredit UNIQUEIDENTIFIER =
                    CASE WHEN @Direction = 'RECEIVABLE' THEN @PayAmId ELSE @InvAmId END;

                -- Aşım guard (manuel reconcile ile drift koruması): fatura AM açık tutarı aşılamaz
                -- Hem manuel hem auto-close aynı faturayı kapatabilir → AccountReconciliation SUM kontrolü
                DECLARE @invVal DECIMAL(18,2) =
                    (SELECT CASE WHEN @Direction='RECEIVABLE' THEN Debit ELSE Credit END
                     FROM AccountMovement WHERE Id = @InvAmId);
                DECLARE @invUsed DECIMAL(18,2) = ISNULL((
                    SELECT SUM(Amount) FROM AccountReconciliation
                    WHERE (DebitMovementId = @InvAmId OR CreditMovementId = @InvAmId) AND IsReversal = 0), 0);

                -- Yalnızca açık tutar kadar eşleştir (aşımı kırp; kalan başka plana FIFO devam eder)
                DECLARE @reconAmount DECIMAL(18,2) =
                    CASE WHEN @invUsed + @ApplyAmount > @invVal THEN @invVal - @invUsed ELSE @ApplyAmount END;

                IF @reconAmount > 0
                    INSERT INTO dbo.AccountReconciliation
                        (Id, CompanyId, PartnerId, DebitMovementId, CreditMovementId,
                         Amount, MovementDate, IsReversal, CreatedBy)
                    VALUES
                        (NEWID(), @CompanyId, @PartnerId, @reconDebit, @reconCredit,
                         @reconAmount, GETUTCDATE(), 0,
                         CAST(@UserId AS NVARCHAR(450)));
            END

            SET @Remaining -= @ApplyAmount;

            FETCH NEXT FROM c_pp INTO @PpId, @PpRemaining, @PpAmount, @PpPaid, @PpSrcType, @PpSrcId;
        END

        CLOSE c_pp;
        DEALLOCATE c_pp;

        -- Kalan tutar varsa avans olarak kaydedilir (bekleyen ödeme planı yok)
        IF @Remaining > 0
        BEGIN
            INSERT INTO PaymentPlan (
                Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
                PartnerId, Direction, InstallmentNo, TotalInstallments,
                DueDate, Amount, PaidAmount, Status, PaidAt, FinancialTransactionId
            )
            VALUES (
                NEWID(), @CompanyId, 'PREPAYMENT', @TransactionId,
                'AVANS-' + CAST(YEAR(GETUTCDATE()) AS NVARCHAR(4)) + '-' + CONVERT(NVARCHAR(8), GETUTCDATE(), 112),
                @PartnerId, @Direction, 1, 1,
                CAST(GETUTCDATE() AS DATE), @Remaining, @Remaining, 'PAID', GETUTCDATE(), @TransactionId
            );
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local','c_pp') >= -1
        BEGIN
            CLOSE c_pp;
            DEALLOCATE c_pp;
        END
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_RecordPaymentAndAutoClose — Tahsilat/ödeme kaydı ve otomatik plan kapatma
-- NE YAPAR: FinancialTransaction ekler, AccountMovement'a cari defter hareketi yazar.
--   Partner.AutoClosePayments=1 ise sp_AutoClosePayments çağırarak açık planları FIFO
--   sırayla kapatır; 0 ise yalnızca ham hareket kaydı oluşturur.
-- PARAMETRELERİ:
--   @CompanyId      UNIQUEIDENTIFIER — şirket kimliği
--   @AccountId      UNIQUEIDENTIFIER — işlemin yapıldığı finansal hesap kimliği
--   @PartnerId      UNIQUEIDENTIFIER — ilgili cari hesap kimliği
--   @Amount         DECIMAL(18,2) — işlem tutarı
--   @Currency       NVARCHAR(10) — para birimi (varsayılan: TRY)
--   @TxType         NVARCHAR(20) — INCOME (tahsilat) | EXPENSE (ödeme)
--   @Description    NVARCHAR(500) (NULL) — açıklama
--   @InstrumentType NVARCHAR(20) — ödeme aracı türü (varsayılan: CASH)
--   @InstrumentId   UNIQUEIDENTIFIER (NULL) — ödeme aracı kimliği (çek/senet Id)
--   @UserId         UNIQUEIDENTIFIER (NULL) — işlemi yapan kullanıcı
--   @NewTxId        UNIQUEIDENTIFIER OUTPUT — oluşturulan FinancialTransaction kimliği
-- SIDE EFFECTS: FinancialTransaction (INSERT); AccountMovement (INSERT — Debit veya Credit);
--   PaymentPlan (dolaylı — AutoClosePayments=1 ise sp_AutoClosePayments üzerinden)
-- THROW: sp_GuardPeriodOpen hataları (dönem kilitli ise)
-- BAĞIMLILIK: sp_GuardPeriodOpen (dönem kilidi), sp_AutoClosePayments (koşullu),
--   Partner (AutoClosePayments bayrağı)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_RecordPaymentAndAutoClose
    @CompanyId    UNIQUEIDENTIFIER,
    @AccountId    UNIQUEIDENTIFIER,
    @PartnerId    UNIQUEIDENTIFIER,
    @Amount       DECIMAL(18,2),
    @Currency     NVARCHAR(10) = 'TRY',
    @TxType       NVARCHAR(20),         -- INCOME (tahsilat) / EXPENSE (ödeme)
    @Description  NVARCHAR(500) = NULL,
    @InstrumentType NVARCHAR(20) = 'CASH',
    @InstrumentId UNIQUEIDENTIFIER = NULL,
    @UserId       UNIQUEIDENTIFIER = NULL,
    @NewTxId      UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        SET @NewTxId = NEWID();

        -- Dönem kilidi: ödeme/tahsilat anının dönemi açık olmalı (Plan 14)
        DECLARE @txNow DATETIME2 = GETUTCDATE();
        DECLARE @txUserStr NVARCHAR(450) = CAST(@UserId AS NVARCHAR(450));
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @txNow, @txUserStr;

        -- Mutabakat kilidi (Plan 19): onaylı mutabakatlı döneme kayıt giremez
        EXEC dbo.sp_GuardPartnerReconciled @CompanyId, @PartnerId, @txNow;

        INSERT INTO FinancialTransaction (
            Id, CompanyId, AccountId, TransactionDate, TransactionType,
            Amount, Currency, AmountTRY, PartnerId, Description,
            InstrumentType, InstrumentId, CreatedBy
        )
        VALUES (
            @NewTxId, @CompanyId, @AccountId, @txNow, @TxType,
            @Amount, @Currency, @Amount, @PartnerId, @Description,
            @InstrumentType, @InstrumentId, @UserId
        );

        -- Cari hesap defteri: INCOME (tahsilat) → Credit / EXPENSE (ödeme) → Debit
        -- SourceDocNo yoksa FinancialTransaction Id string olarak yazılır
        DECLARE @TxDocNo NVARCHAR(50);
        SELECT @TxDocNo = SourceDocNo FROM FinancialTransaction WHERE Id = @NewTxId;

        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        VALUES (
            NEWID(), @CompanyId, @PartnerId, @txNow,
            CASE WHEN @TxType = 'EXPENSE' THEN @Amount ELSE 0 END,  -- ödeme → Debit
            CASE WHEN @TxType = 'INCOME'  THEN @Amount ELSE 0 END,  -- tahsilat → Credit
            @Currency,
            CASE WHEN @TxType = 'INCOME' THEN 'COLLECTION' ELSE 'PAYMENT' END,
            @NewTxId, @TxDocNo,
            ISNULL(@Description, CASE WHEN @TxType = 'INCOME' THEN N'Tahsilat' ELSE N'Ödeme' END),
            CAST(@UserId AS NVARCHAR(450))
        );

        -- Auto-close: sadece partner bayrağı açıksa otomatik dağıt
        DECLARE @AutoFlag BIT = 0;
        SELECT @AutoFlag = ISNULL(AutoClosePayments, 0) FROM Partner WHERE Id = @PartnerId;

        IF @AutoFlag = 1
        BEGIN
            DECLARE @Direction NVARCHAR(10) =
                CASE WHEN @TxType = 'INCOME' THEN 'RECEIVABLE' ELSE 'PAYABLE' END;

            EXEC dbo.sp_AutoClosePayments
                @CompanyId    = @CompanyId,
                @PartnerId    = @PartnerId,
                @Direction    = @Direction,
                @Amount       = @Amount,
                @TransactionId = @NewTxId,
                @UserId       = @UserId;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- v_PartnerVadeAnalysis — Ortalama vade analizi
-- Müşteri: kesilen fatura vadesi vs gerçekleşen tahsilat tarihi farkı (gün ortalama)
-- Tedarikçi: alınan fatura vadesi vs gerçekleşen ödeme tarihi farkı
-- =============================================================================
CREATE OR ALTER VIEW dbo.v_PartnerVadeAnalysis AS
SELECT
    pp.CompanyId,
    pp.PartnerId,
    p.Name AS PartnerName,
    pp.Direction,
    COUNT(*) AS PaidCount,
    AVG(CAST(DATEDIFF(DAY, pp.DueDate, pp.PaidAt) AS DECIMAL(10, 2))) AS AvgDelayDays,
    -- pozitif = geç ödendi, negatif = erken ödendi/tahsil edildi
    AVG(CAST(pp.Amount AS DECIMAL(18, 2))) AS AvgInvoiceAmount,
    SUM(pp.Amount) AS TotalPaidAmount,
    MIN(pp.DueDate) AS FirstDue,
    MAX(pp.PaidAt)  AS LastPayment
FROM PaymentPlan pp
JOIN Partner p ON p.Id = pp.PartnerId
WHERE pp.Status = 'PAID' AND pp.PaidAt IS NOT NULL AND pp.IsDeleted = 0
GROUP BY pp.CompanyId, pp.PartnerId, p.Name, pp.Direction;
GO

-- =============================================================================
-- tvf_LoanSummaryAsOf — Kredi kapama tablosu (tarih bazlı snapshot)
-- Belirtilen tarihe kadar her kredi için:
--   • Vadesi gelmiş taksitlerden ödenmiş / ödenmemiş anapara ve faiz
--   • Kalan anapara (OutstandingPrincipal)
--   • Sonraki vade tarihi ve tutarı
--   • Geciken (ödenmemiş + vadesi geçmiş) taksit adedi
-- =============================================================================
CREATE OR ALTER FUNCTION dbo.tvf_LoanSummaryAsOf
(
    @CompanyId  UNIQUEIDENTIFIER,
    @AsOfDate   DATE
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        l.Id                 AS LoanId,
        l.LoanNo,
        l.BankName,
        l.CalcMethod,
        l.Status,
        l.Principal,
        l.InterestRate,
        l.StartDate,
        l.EndDate,
        l.TermMonths,
        l.MonthlyPayment,

        -- Vadesi gelen taksit sayısı (@AsOfDate dahil)
        (SELECT COUNT(*) FROM LoanPayment lp
            WHERE lp.LoanId = l.Id AND lp.DueDate <= @AsOfDate)              AS DueInstallmentCount,

        -- Vadesi gelen ve ödenen
        (SELECT COUNT(*) FROM LoanPayment lp
            WHERE lp.LoanId = l.Id AND lp.DueDate <= @AsOfDate AND lp.IsPaid = 1) AS PaidInstallmentCount,

        -- Vadesi gelen ve ödenmemiş (gecikme)
        (SELECT COUNT(*) FROM LoanPayment lp
            WHERE lp.LoanId = l.Id AND lp.DueDate <= @AsOfDate AND lp.IsPaid = 0) AS OverdueInstallmentCount,

        -- Tüm taksit sayısı
        (SELECT COUNT(*) FROM LoanPayment lp WHERE lp.LoanId = l.Id)         AS TotalInstallmentCount,

        -- Bugüne kadar ödenen anapara
        ISNULL((SELECT SUM(lp.PrincipalAmount) FROM LoanPayment lp
                WHERE lp.LoanId = l.Id AND lp.IsPaid = 1), 0)                AS PaidPrincipal,

        -- Bugüne kadar ödenen faiz
        ISNULL((SELECT SUM(lp.InterestAmount) FROM LoanPayment lp
                WHERE lp.LoanId = l.Id AND lp.IsPaid = 1), 0)                AS PaidInterest,

        -- Kalan anapara borcu (Loan.OutstandingBalance senkron tutuluyor)
        l.OutstandingBalance                                                  AS OutstandingPrincipal,

        -- Kalan faiz (ödenmemiş tüm taksitlerin faiz toplamı)
        ISNULL((SELECT SUM(lp.InterestAmount) FROM LoanPayment lp
                WHERE lp.LoanId = l.Id AND lp.IsPaid = 0), 0)                AS RemainingInterest,

        -- Toplam kalan borç (anapara + faiz)
        l.OutstandingBalance +
        ISNULL((SELECT SUM(lp.InterestAmount) FROM LoanPayment lp
                WHERE lp.LoanId = l.Id AND lp.IsPaid = 0), 0)                AS TotalRemainingDebt,

        -- Vadesi geçmiş gecikme tutarı (ödenmemiş taksitlerin tutarı, AsOfDate'e kadar)
        ISNULL((SELECT SUM(lp.TotalAmount) FROM LoanPayment lp
                WHERE lp.LoanId = l.Id AND lp.IsPaid = 0 AND lp.DueDate <= @AsOfDate), 0)
                                                                              AS OverdueAmount,

        -- Sonraki ödeme tarihi + tutarı
        (SELECT TOP 1 lp.DueDate FROM LoanPayment lp
            WHERE lp.LoanId = l.Id AND lp.IsPaid = 0
            ORDER BY lp.DueDate)                                              AS NextDueDate,

        (SELECT TOP 1 lp.TotalAmount FROM LoanPayment lp
            WHERE lp.LoanId = l.Id AND lp.IsPaid = 0
            ORDER BY lp.DueDate)                                              AS NextDueAmount

    FROM Loan l
    WHERE l.CompanyId = @CompanyId AND l.IsDeleted = 0
);
GO

-- =============================================================================
-- tvf_FinancialPosition — Mali durum kapama tablosu (bilanço benzeri snapshot)
-- @AsOfDate tarihinde:
--   Pozitif (varlık): hesap bakiyeleri (kasa+banka), müşteri alacakları, çek portföyü
--   Negatif (yükümlülük): kredi kalan anapara+faiz, tedarikçi borçları, kredi kartı kullanılan limit
--   Net pozisyon = sum(varlık) - sum(yükümlülük)
-- =============================================================================
CREATE OR ALTER FUNCTION dbo.tvf_FinancialPosition
(
    @CompanyId  UNIQUEIDENTIFIER,
    @AsOfDate   DATE
)
RETURNS @Result TABLE
(
    Category    NVARCHAR(50),         -- ASSET / LIABILITY
    GroupName   NVARCHAR(100),        -- Kasa, Banka, Kredi, vb.
    ItemCode    NVARCHAR(100),        -- Hesap kodu / kredi no / cari kodu
    ItemName    NVARCHAR(300),
    Currency    NVARCHAR(10),
    Amount      DECIMAL(18,2),        -- TRY karşılığı; ASSET pozitif, LIABILITY pozitif (görsel negatif)
    SortOrder   INT
)
AS
BEGIN
    -- VARLIK 1: Kasa hesapları
    INSERT INTO @Result
    SELECT 'ASSET', N'Kasa', a.Code, a.Name, a.Currency,
           a.OpeningBalance + ISNULL(
               (SELECT SUM(CASE WHEN ft.TransactionType IN ('INCOME','TRANSFER_IN') THEN ft.AmountTRY
                                WHEN ft.TransactionType IN ('EXPENSE','TRANSFER_OUT') THEN -ft.AmountTRY
                                ELSE 0 END)
                FROM FinancialTransaction ft
                WHERE ft.AccountId = a.Id AND ft.IsDeleted = 0
                  AND CAST(ft.TransactionDate AS DATE) <= @AsOfDate), 0),
           10
    FROM FinancialAccount a
    WHERE a.CompanyId = @CompanyId AND a.AccountType = 'CASH' AND a.IsDeleted = 0;

    -- VARLIK 2: Banka hesapları
    INSERT INTO @Result
    SELECT 'ASSET', N'Banka', a.Code, a.Name, a.Currency,
           a.OpeningBalance + ISNULL(
               (SELECT SUM(CASE WHEN ft.TransactionType IN ('INCOME','TRANSFER_IN') THEN ft.AmountTRY
                                WHEN ft.TransactionType IN ('EXPENSE','TRANSFER_OUT') THEN -ft.AmountTRY
                                ELSE 0 END)
                FROM FinancialTransaction ft
                WHERE ft.AccountId = a.Id AND ft.IsDeleted = 0
                  AND CAST(ft.TransactionDate AS DATE) <= @AsOfDate), 0),
           20
    FROM FinancialAccount a
    WHERE a.CompanyId = @CompanyId AND a.AccountType = 'BANK' AND a.IsDeleted = 0;

    -- VARLIK 3: Çek portföyü (PORTFOLIO + IN_BANK, tahsil edilmemiş)
    INSERT INTO @Result
    SELECT 'ASSET', N'Çek Portföyü', c.ChequeNo,
           N'Çek ' + c.BankName + N' — ' + ISNULL(c.DrawerName, ''),
           c.Currency, c.Amount, 30
    FROM Cheque c
    WHERE c.CompanyId = @CompanyId AND c.IsDeleted = 0
      AND c.Direction = 'RECEIVED'
      AND c.Status IN ('PORTFOLIO','IN_BANK')
      AND c.ChequeDate <= @AsOfDate;

    -- VARLIK 4: Senet portföyü (alınan)
    INSERT INTO @Result
    SELECT 'ASSET', N'Senet Portföyü', n.NoteNo,
           N'Senet — ' + ISNULL(n.DrawerName, ''),
           n.Currency, n.Amount, 35
    FROM PromissoryNote n
    WHERE n.CompanyId = @CompanyId AND n.IsDeleted = 0
      AND n.Direction = 'RECEIVED'
      AND n.Status IN ('PORTFOLIO','IN_BANK')
      AND n.IssueDate <= @AsOfDate;

    -- VARLIK 5: Müşteri alacakları (PaymentPlan RECEIVABLE açık)
    INSERT INTO @Result
    SELECT 'ASSET', N'Müşteri Alacakları', p.Code, p.Name, 'TRY',
           SUM(pp.Amount - pp.PaidAmount), 40
    FROM PaymentPlan pp
    JOIN Partner p ON p.Id = pp.PartnerId
    WHERE pp.CompanyId = @CompanyId AND pp.IsDeleted = 0
      AND pp.Direction = 'RECEIVABLE'
      AND pp.Status IN ('OPEN','PARTIAL','OVERDUE')
      AND CAST(pp.CreatedAt AS DATE) <= @AsOfDate
    GROUP BY p.Code, p.Name
    HAVING SUM(pp.Amount - pp.PaidAmount) > 0;

    -- YÜKÜMLÜLÜK 1: Kredi kalan anapara + faiz
    INSERT INTO @Result
    SELECT 'LIABILITY', N'Banka Kredileri', l.LoanNo,
           l.BankName + N' (' + l.CalcMethod + N')', 'TRY',
           l.OutstandingPrincipal + l.RemainingInterest, 110
    FROM dbo.tvf_LoanSummaryAsOf(@CompanyId, @AsOfDate) l
    WHERE l.Status = 'ACTIVE';

    -- YÜKÜMLÜLÜK 2: Verilen çek (henüz ödenmemiş)
    INSERT INTO @Result
    SELECT 'LIABILITY', N'Verilen Çekler', c.ChequeNo,
           N'Çek ' + c.BankName + N' → ' + ISNULL(c.DrawerName, ''),
           c.Currency, c.Amount, 120
    FROM Cheque c
    WHERE c.CompanyId = @CompanyId AND c.IsDeleted = 0
      AND c.Direction = 'ISSUED'
      AND c.Status NOT IN ('PAID','RETURNED')
      AND c.ChequeDate <= @AsOfDate;

    -- YÜKÜMLÜLÜK 3: Verilen senet
    INSERT INTO @Result
    SELECT 'LIABILITY', N'Verilen Senetler', n.NoteNo,
           N'Senet → ' + ISNULL(n.DrawerName, ''),
           n.Currency, n.Amount, 125
    FROM PromissoryNote n
    WHERE n.CompanyId = @CompanyId AND n.IsDeleted = 0
      AND n.Direction = 'ISSUED'
      AND n.Status NOT IN ('PAID','RETURNED')
      AND n.IssueDate <= @AsOfDate;

    -- YÜKÜMLÜLÜK 4: Tedarikçi borçları (PaymentPlan PAYABLE açık)
    INSERT INTO @Result
    SELECT 'LIABILITY', N'Tedarikçi Borçları', p.Code, p.Name, 'TRY',
           SUM(pp.Amount - pp.PaidAmount), 130
    FROM PaymentPlan pp
    JOIN Partner p ON p.Id = pp.PartnerId
    WHERE pp.CompanyId = @CompanyId AND pp.IsDeleted = 0
      AND pp.Direction = 'PAYABLE'
      AND pp.Status IN ('OPEN','PARTIAL','OVERDUE')
      AND CAST(pp.CreatedAt AS DATE) <= @AsOfDate
    GROUP BY p.Code, p.Name
    HAVING SUM(pp.Amount - pp.PaidAmount) > 0;

    -- YÜKÜMLÜLÜK 5: Kredi kartı kullanılan limit (FinancialAccount Type=CREDIT_CARD üzerinden)
    INSERT INTO @Result
    SELECT 'LIABILITY', N'Kredi Kartı Kullanımı', a.Code, a.Name, a.Currency,
           CASE WHEN ISNULL(
                    (SELECT SUM(CASE WHEN ft.TransactionType = 'EXPENSE' THEN ft.AmountTRY
                                     WHEN ft.TransactionType = 'INCOME' THEN -ft.AmountTRY
                                     ELSE 0 END)
                     FROM FinancialTransaction ft
                     WHERE ft.AccountId = a.Id AND ft.IsDeleted = 0
                       AND CAST(ft.TransactionDate AS DATE) <= @AsOfDate), 0) > 0
                THEN ISNULL(
                    (SELECT SUM(CASE WHEN ft.TransactionType = 'EXPENSE' THEN ft.AmountTRY
                                     WHEN ft.TransactionType = 'INCOME' THEN -ft.AmountTRY
                                     ELSE 0 END)
                     FROM FinancialTransaction ft
                     WHERE ft.AccountId = a.Id AND ft.IsDeleted = 0
                       AND CAST(ft.TransactionDate AS DATE) <= @AsOfDate), 0)
                ELSE 0 END,
           140
    FROM FinancialAccount a
    WHERE a.CompanyId = @CompanyId AND a.AccountType = 'CREDIT_CARD' AND a.IsDeleted = 0;

    RETURN;
END
GO

-- =============================================================================
-- v_ChequePortfolio — Çek portföyü görünümü (durum + vade)
-- =============================================================================
CREATE OR ALTER VIEW dbo.v_ChequePortfolio AS
SELECT
    c.Id, c.CompanyId,
    c.Direction, c.ChequeNo,
    c.BankName, c.BranchName,
    c.DrawerName, c.Amount, c.Currency,
    c.ChequeDate, c.DueDate,
    c.Status, c.PartnerId,
    p.Name AS PartnerName,
    DATEDIFF(DAY, GETUTCDATE(), c.DueDate) AS DaysToDue,
    CASE
        WHEN c.Status = 'COLLECTED' THEN 'TAHSİL EDİLDİ'
        WHEN c.Status = 'RETURNED'  THEN 'KARŞILIKSIZ'
        WHEN c.Status = 'PAID'      THEN 'ÖDENDİ'
        WHEN DATEDIFF(DAY, GETUTCDATE(), c.DueDate) < 0  THEN 'GECİKMİŞ'
        WHEN DATEDIFF(DAY, GETUTCDATE(), c.DueDate) <= 7 THEN 'YAKINDA VADELİ'
        ELSE 'VADELİ'
    END AS DisplayStatus
FROM Cheque c
LEFT JOIN Partner p ON p.Id = c.PartnerId
WHERE c.IsDeleted = 0;
GO

-- =============================================================================
-- sp_ExpenseInvoicePost — Masraf/alış faturası onaylama (DRAFT → POSTED)
-- NE YAPAR: ExpenseInvoice belgesini POSTED yapar ve her fatura satırı için
--   AccountMovement'a satır bazlı Credit kaydı yazar (biz cariye borçlandık).
--   CostCenterId ve ExpenseTypeId ile gider merkezi/tipi bazlı raporlamaya destek verir.
-- PARAMETRELERİ:
--   @InvoiceId  UNIQUEIDENTIFIER — onaylanacak masraf faturası kimliği
--   @CompanyId  UNIQUEIDENTIFIER — şirket kimliği
--   @UserId     NVARCHAR(450) — işlemi yapan kullanıcı kimliği (string)
-- SIDE EFFECTS: ExpenseInvoice (UPDATE — Status=POSTED);
--   AccountMovement (INSERT — Credit, satır sayısı kadar; SourceDocId=Line.Id)
-- THROW: 70100 — fatura bulunamadı; 70101 — zaten onaylı; 70102 — iptal edilmiş;
--   70103 — tedarikçi bilgisi eksik; sp_GuardPeriodOpen hataları
-- BAĞIMLILIK: sp_GuardPeriodOpen (dönem kilidi), ExpenseInvoiceLine
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ExpenseInvoicePost
    @InvoiceId  UNIQUEIDENTIFIER,
    @CompanyId  UNIQUEIDENTIFIER,
    @UserId     NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi (Plan 14)
        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        -- Belge kontrolü + kilit
        DECLARE @Status    NVARCHAR(20),
                @PartnerId UNIQUEIDENTIFIER,
                @DocNo     NVARCHAR(50),
                @DueDate   DATETIME2,
                @Amount    DECIMAL(18,2),
                @Currency  NVARCHAR(3);

        SELECT @Status    = Status,
               @PartnerId = PartnerId,
               @DocNo     = DocNo,
               @DueDate   = DueDate,
               @Amount    = TotalAmount,
               @Currency  = ISNULL(Currency, 'TRY')
        FROM ExpenseInvoice WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @InvoiceId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 50710, N'Masraf faturası bulunamadı.', 1;
        IF @Status = 'POSTED'
            THROW 50711, N'Fatura zaten onaylanmış.', 1;
        IF @Status = 'CANCELLED'
            THROW 50712, N'İptal edilmiş fatura onaylanamaz.', 1;
        IF @PartnerId IS NULL
            THROW 50713, N'Fatura tedarikçi bilgisi eksik.', 1;

        -- Mutabakat kilidi (Plan 19): cari mutabakatı yapılmış döneme kayıt giremez
        EXEC dbo.sp_GuardPartnerReconciled @CompanyId, @PartnerId, @now;

        -- Fatura onayla
        UPDATE ExpenseInvoice
        SET Status = 'POSTED', UpdatedBy = @UserId
        WHERE Id = @InvoiceId;

        -- Ödeme planı: alış faturası → PAYABLE (biz cariye borçluyuz)
        -- sp_AutoClosePayments FIFO için gerekli; yoksa ödeme "avans" olarak düşer
        -- sp_ExpenseInvoiceReverse immutability guard'ı da bu kayda bakar
        INSERT INTO PaymentPlan
            (Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
             PartnerId, Direction, InstallmentNo, TotalInstallments,
             DueDate, Amount, Status)
        VALUES
            (NEWID(), @CompanyId, 'PURCHASE_INVOICE', @InvoiceId, @DocNo,
             @PartnerId, 'PAYABLE', 1, 1,
             ISNULL(@DueDate, DATEADD(DAY, 30, @now)), @Amount, 'OPEN');

        -- Cari hesap defteri: alış faturası → BAŞLIK bazlı tek Credit (biz cariye borçluyuz)
        -- SourceDocId = InvoiceId (satış SALES_INVOICE ile simetrik; PaymentPlan + reconcile eşleşir)
        -- NetAmount/TaxAmount satırlardan SUM; gider analitiği (CostCenter/ExpenseType) AM'de DEĞİL,
        -- v_ExpenseDistribution view'ında ExpenseInvoiceLine'dan okunur (reference-researcher B1)
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit,
             NetAmount, TaxAmount, DueDate, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        SELECT
            NEWID(), @CompanyId, @PartnerId, @now,
            0, @Amount,
            ISNULL(SUM(l.Amount), 0), ISNULL(SUM(l.TaxAmount), 0), @DueDate, @Currency,
            'PURCHASE_INVOICE', @InvoiceId, @DocNo, N'Alış Faturası', @UserId
        FROM ExpenseInvoiceLine l
        WHERE l.ExpenseInvoiceId = @InvoiceId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- tvf_UninvoicedShipments — Plan 21: faturalanmamış sevkiyat satırları (N:1 adayı)
-- POSTED sevkiyat + RemainingQty>0. UI partner bazlı GROUP BY ile birleştirme listesi.
-- Statü: SUM(InvoicedQty)=0 → TO_BILL · kısmi → PARTIAL · tam → satır listede çıkmaz.
-- =============================================================================
CREATE OR ALTER FUNCTION dbo.tvf_UninvoicedShipments
(
    @CompanyId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        sh.Id              AS ShippingId,
        sh.DocNo           AS ShippingNo,
        sh.DocDate         AS ShipDate,
        sh.PartnerId,
        p.Name             AS PartnerName,
        sl.Id              AS ShippingLineId,
        sl.ItemId,
        i.Name             AS ItemName,
        sl.QtyBase,
        sl.InvoicedQty,
        sl.QtyBase - sl.InvoicedQty AS RemainingQty,
        CASE WHEN sl.InvoicedQty = 0 THEN 'TO_BILL' ELSE 'PARTIAL' END AS LineStatus,
        DATEDIFF(DAY, sh.DocDate, GETUTCDATE()) AS AgeDays  -- 7-gün VUK uyarısı için
    FROM ShippingHeader sh
    JOIN ShippingLine sl ON sl.HeaderId = sh.Id
    JOIN Item i ON i.Id = sl.ItemId
    JOIN Partner p ON p.Id = sh.PartnerId
    WHERE sh.CompanyId = @CompanyId
      AND sh.Status = 'POSTED'
      AND sh.IsDeleted = 0
      AND sl.QtyBase - sl.InvoicedQty > 0.000001
);
GO

-- =============================================================================
-- sp_PartnerStatement — Cari Hesap Ekstresi (Plan 39 Faz 1)
-- NE YAPAR: yazdırılabilir cari ekstre için 3 sonuç kümesi döner (salt-okuma, ledger'a dokunmaz):
--   1) Devir/açılış bakiyesi (@From öncesi net bakiye) — tek satır
--   2) Dönem hareketleri + yürüyen bakiye (running balance window function ile, C#'ta döngü yok)
--   3) Yaşlandırma kovaları (FIFO: tahsilatlar en eski borçları kapatır; kapanmamış borçlar @To'ya göre yaşlandırılır)
-- PARAMETRELERİ: @CompanyId, @PartnerId, @From, @To
-- BAĞIMLILIK: fn_PartnerBalanceAsOf (devir), AccountMovement (cari defter)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PartnerStatement
    @CompanyId     UNIQUEIDENTIFIER,
    @PartnerId     UNIQUEIDENTIFIER,
    @From          DATETIME2,
    @To            DATETIME2,
    @StatementType VARCHAR(10) = 'ALL'   -- 'ALL' tüm hareket (devir+running) · 'OPEN' açık kalem (Plan 39 Faz 2: FIFO kapanmamış borçlar)
AS
BEGIN
    SET NOCOUNT ON;

    -- Devir: @From öncesi tüm hareketin net bakiyesi (SARGable: MovementDate < @From)
    DECLARE @Opening DECIMAL(18,2) = dbo.fn_PartnerBalanceAsOf(@CompanyId, @PartnerId, @From);

    -- 1) Açılış/Devir — OPEN'de devir yoktur (liste açık kalemin kendisidir, kümülatif 0'dan başlar)
    SELECT CASE WHEN @StatementType = 'OPEN' THEN CAST(0 AS DECIMAL(18,2)) ELSE @Opening END AS OpeningBalance;

    -- 2) Dönem hareketleri + yürüyen bakiye
    IF @StatementType = 'OPEN'
    BEGIN
        -- Açık kalem: @To'ya kadar FIFO kapanmamış borçlar (tahsilat en eski borcu kapatır).
        -- ALL'daki yaşlandırma FIFO'sunun aynısı; doküman kolonları taşınır, RunningBalance = kümülatif kalan açık tutar.
        ;WITH AllMov AS (
            SELECT Id, MovementDate, SourceDocType, SourceDocNo, Description, Debit, Credit
            FROM AccountMovement
            WHERE CompanyId = @CompanyId AND PartnerId = @PartnerId
              AND MovementDate < DATEADD(DAY, 1, @To)
        ),
        TotCredit AS (SELECT ISNULL(SUM(Credit), 0) AS C FROM AllMov),
        Debits AS (
            SELECT Id, MovementDate, SourceDocType, SourceDocNo, Description, Debit,
                   SUM(Debit) OVER (ORDER BY MovementDate, Id ROWS UNBOUNDED PRECEDING) - Debit AS PriorDebit
            FROM AllMov WHERE Debit > 0
        ),
        Unpaid AS (
            SELECT Id, MovementDate, SourceDocType, SourceDocNo, Description,
                   Debit - CASE
                       WHEN (SELECT C FROM TotCredit) - PriorDebit >= Debit THEN Debit
                       WHEN (SELECT C FROM TotCredit) - PriorDebit <= 0     THEN 0
                       ELSE (SELECT C FROM TotCredit) - PriorDebit
                   END AS UnpaidAmt
            FROM Debits
        )
        SELECT
            Id, MovementDate, SourceDocType, SourceDocNo, Description,
            UnpaidAmt AS Debit, CAST(0 AS DECIMAL(18,2)) AS Credit,
            SUM(UnpaidAmt) OVER (ORDER BY MovementDate, Id ROWS UNBOUNDED PRECEDING) AS RunningBalance
        FROM Unpaid
        WHERE UnpaidAmt > 0
        ORDER BY MovementDate, Id;
    END
    ELSE
    BEGIN
        SELECT
            am.Id, am.MovementDate, am.SourceDocType, am.SourceDocNo, am.Description,
            am.Debit, am.Credit,
            @Opening + SUM(am.Debit - am.Credit) OVER
                (ORDER BY am.MovementDate, am.Id ROWS UNBOUNDED PRECEDING) AS RunningBalance
        FROM AccountMovement am
        WHERE am.CompanyId = @CompanyId AND am.PartnerId = @PartnerId
          AND am.MovementDate >= @From
          AND am.MovementDate < DATEADD(DAY, 1, @To)
        ORDER BY am.MovementDate, am.Id;
    END

    -- 3) Yaşlandırma (FIFO): @To'ya kadar tüm hareketler; tahsilat (Credit) en eski borcu (Debit) kapatır,
    --    kapanmamış borç tutarı kendi tarihinin yaşına göre kovaya düşer.
    ;WITH AllMov AS (
        SELECT Id, MovementDate, Debit, Credit
        FROM AccountMovement
        WHERE CompanyId = @CompanyId AND PartnerId = @PartnerId
          AND MovementDate < DATEADD(DAY, 1, @To)
    ),
    TotCredit AS (SELECT ISNULL(SUM(Credit), 0) AS C FROM AllMov),
    Debits AS (
        -- Her borç hareketinin kendinden önceki borç toplamı (FIFO sırası).
        -- Tie-break Id: aynı tarihli borçlarda PriorDebit deterministik (ledger ile aynı sıra).
        SELECT MovementDate, Debit,
               SUM(Debit) OVER (ORDER BY MovementDate, Id ROWS UNBOUNDED PRECEDING) - Debit AS PriorDebit
        FROM AllMov WHERE Debit > 0
    ),
    Unpaid AS (
        -- Kapanmamış borç = Debit - kapatılan. Kapatılan = clamp(C - PriorDebit, 0, Debit)
        SELECT MovementDate,
               Debit - CASE
                   WHEN (SELECT C FROM TotCredit) - PriorDebit >= Debit THEN Debit
                   WHEN (SELECT C FROM TotCredit) - PriorDebit <= 0     THEN 0
                   ELSE (SELECT C FROM TotCredit) - PriorDebit
               END AS UnpaidAmt
        FROM Debits
    )
    SELECT
        ISNULL(SUM(CASE WHEN DATEDIFF(DAY, MovementDate, @To) <= 30                       THEN UnpaidAmt ELSE 0 END), 0) AS B0_30,
        ISNULL(SUM(CASE WHEN DATEDIFF(DAY, MovementDate, @To) BETWEEN 31 AND 60           THEN UnpaidAmt ELSE 0 END), 0) AS B31_60,
        ISNULL(SUM(CASE WHEN DATEDIFF(DAY, MovementDate, @To) BETWEEN 61 AND 90           THEN UnpaidAmt ELSE 0 END), 0) AS B61_90,
        ISNULL(SUM(CASE WHEN DATEDIFF(DAY, MovementDate, @To) > 90                        THEN UnpaidAmt ELSE 0 END), 0) AS B90Plus
    FROM Unpaid;
END
GO
