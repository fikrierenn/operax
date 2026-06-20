-- Plan 05: Belge Zinciri SP'leri — Downstream Create
-- Her SP: kaynak belgeden DRAFT hedef belge + satırlar oluşturur.
-- İdempotent: CREATE OR ALTER
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- =============================================================================
-- sp_CreateReceivingFromPO
-- NE YAPAR: POSTED PO'dan DRAFT ReceivingHeader + ReceivingLine'ları oluşturur.
--           Miktar: QtyOrdered - QtyReceived (kalan). Tüm satırlar teslim alınmışsa THROW.
-- PARAMETRELERİ:
--   @PoId          UNIQUEIDENTIFIER — kaynak PO
--   @CompanyId     UNIQUEIDENTIFIER — firma izolasyonu
--   @UserId        UNIQUEIDENTIFIER — oluşturan kullanıcı
--   @NewHeaderId   UNIQUEIDENTIFIER OUTPUT — oluşturulan Receiving ID
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CreateReceivingFromPO
    @PoId        UNIQUEIDENTIFIER,
    @CompanyId   UNIQUEIDENTIFIER,
    @UserId      UNIQUEIDENTIFIER,
    @NewHeaderId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- PO bilgilerini al ve sahiplik doğrula
        DECLARE @WarehouseId UNIQUEIDENTIFIER,
                @PartnerId   UNIQUEIDENTIFIER,
                @Status      NVARCHAR(50),
                @OrderNo     NVARCHAR(100);

        SELECT @WarehouseId = WarehouseId, @PartnerId = PartnerId,
               @Status = Status, @OrderNo = OrderNo
        FROM PurchaseOrderHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @PoId AND CompanyId = @CompanyId AND IsDeleted = 0;

        IF @WarehouseId IS NULL
            THROW 51500, N'Satınalma siparişi bulunamadı.', 1;

        -- İş kuralı: yalnızca POSTED PO'dan mal kabul açılabilir
        IF @Status NOT IN ('POSTED', 'APPROVED')
            THROW 51501, N'Yalnızca onaylanmış siparişten mal kabul oluşturulabilir.', 1;

        -- İş kuralı: kalan miktar > 0 olan satır olmalı
        IF NOT EXISTS (
            SELECT 1 FROM PurchaseOrderLine
            WHERE HeaderId = @PoId
              AND (QtyOrdered - ISNULL(QtyReceived, 0)) > 0.0001
        )
            THROW 51502, N'Tüm satırlar teslim alınmış; yeni mal kabul açılamaz.', 1;

        -- Belge no üret (RCV-YYYYMMDD-HHmmss)
        DECLARE @DocNo NVARCHAR(100) =
            'RCV-' + FORMAT(GETUTCDATE(), 'yyyyMMdd-HHmmss');

        SET @NewHeaderId = NEWID();

        -- DRAFT Receiving başlığı oluştur
        INSERT INTO ReceivingHeader
            (Id, CompanyId, WarehouseId, PartnerId, PurchaseOrderId,
             DocNo, Status, CreatedBy)
        VALUES
            (@NewHeaderId, @CompanyId, @WarehouseId, @PartnerId, @PoId,
             @DocNo, 'DRAFT', @UserId);

        -- Kalan miktar > 0 olan PO satırlarını kopyala
        INSERT INTO ReceivingLine
            (HeaderId, PurchaseOrderLineId, ItemId, UomId, QtyOriginal, QtyBase)
        SELECT
            @NewHeaderId,
            pol.Id,
            pol.ItemId,
            pol.UomId,
            pol.QtyOrdered - ISNULL(pol.QtyReceived, 0),   -- kalan miktar
            pol.QtyOrdered - ISNULL(pol.QtyReceived, 0)    -- QtyBase = QtyOriginal (temel birim varsayılır)
        FROM PurchaseOrderLine pol
        WHERE pol.HeaderId = @PoId
          AND (pol.QtyOrdered - ISNULL(pol.QtyReceived, 0)) > 0.0001;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_CreatePurchaseInvoiceFromReceiving (Plan 24 Faz C — eski sp_CreateExpenseInvoiceFromReceiving yerine)
-- NE YAPAR: POSTED Receiving'den DRAFT PurchaseInvoice + SATIRLAR oluşturur (boş fatura bug'ı çözüldü).
--           Mal satırı ItemId/UomId/Qty/UnitPrice (PO fiyatından) ile kopyalanır.
--           SourceReceivingLineId ile satır-bazlı bağ (N:1 hazır). InvoiceDate ön-dolu (POSTED'de override).
-- PARAMETRELERİ: @ReceivingId, @CompanyId, @UserId, @NewInvoiceId OUTPUT
-- THROW: 51510-51512
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CreatePurchaseInvoiceFromReceiving
    @ReceivingId  UNIQUEIDENTIFIER,
    @CompanyId    UNIQUEIDENTIFIER,
    @UserId       UNIQUEIDENTIFIER,
    @NewInvoiceId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @PartnerId UNIQUEIDENTIFIER, @Status NVARCHAR(50), @DocNo NVARCHAR(100);

        SELECT @PartnerId = PartnerId, @Status = Status, @DocNo = DocNo
        FROM ReceivingHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @ReceivingId AND CompanyId = @CompanyId AND IsDeleted = 0;

        IF @PartnerId IS NULL
            THROW 51510, N'Mal kabul belgesi bulunamadı.', 1;
        IF @Status <> 'POSTED'
            THROW 51511, N'Yalnızca onaylanmış mal kabulden fatura oluşturulabilir.', 1;

        -- İş kuralı (Plan 28 Faz C — kısmi faturalama): faturalanmamış kalan miktar var mı?
        -- Tüm satırlar faturalandıysa (InvoicedQty=QtyBase) yeni fatura açma. Kabul fazlası (ReturnQty) faturalanmaz.
        IF NOT EXISTS (
            SELECT 1 FROM ReceivingLine
            WHERE HeaderId = @ReceivingId AND (QtyBase - InvoicedQty) > 0.000001
        )
            THROW 51512, N'Bu mal kabuldeki tüm satırlar zaten faturalanmış.', 1;

        DECLARE @InvDocNo NVARCHAR(50) = 'ALN-' + FORMAT(GETUTCDATE(), 'yyyyMMdd-HHmmss');
        SET @NewInvoiceId = NEWID();

        -- DRAFT başlık (tutarlar satır eklendikten sonra güncellenir)
        -- SupplierInvoiceNo/Date ön-dolu DocNo/bugün — kullanıcı POSTED öncesi gerçek değerle değiştirir
        INSERT INTO PurchaseInvoice
            (Id, CompanyId, PartnerId, DocNo, SupplierInvoiceNo, SupplierInvoiceDate,
             InvoiceDate, Subtotal, TaxAmount, GrandTotal, Status, CreatedBy)
        VALUES
            (@NewInvoiceId, @CompanyId, @PartnerId, @InvDocNo, @InvDocNo, CAST(GETUTCDATE() AS DATE),
             CAST(GETUTCDATE() AS DATE), 0, 0, 0, 'DRAFT', TRY_CAST(@UserId AS UNIQUEIDENTIFIER));

        -- Varsayılan alış KDV oranı (Plan 29 — parametrik). Ürün kendi oranını (Item.TaxRate) taşır;
        -- bu YALNIZ ürün oranı bulunamazsa fallback. %0 geçerli orandır → ISNULL ile korunur (0 ezilmez).
        DECLARE @PurchaseDefaultTax DECIMAL(8,4) = ISNULL(
            (SELECT TRY_CAST(Value AS DECIMAL(8,4)) FROM Parameter
             WHERE CompanyId = @CompanyId AND Code = 'DEFAULT_PURCHASE_TAX_RATE' AND IsDeleted = 0), 20);

        -- ============================================================================
        -- SATIR ÜRETİMİ (Plan 28 Faz C) — iki yol:
        --   (1) SINGLE_PO satırı (rl.PurchaseOrderLineId DOLU): doğrudan o PO satırına bağla, fiyat PO'dan.
        --       QtyReceived terminal/sp_ReceivingPost'ta zaten güncellendi → BURADA DOKUNULMAZ (çift sayım).
        --   (2) BULK satırı (rl.PurchaseOrderLineId NULL): tedarikçinin açık PO satırlarına FIFO (en eski PO
        --       önce) tahsis; her tüketilen PO satırı için ayrı fatura satırı + PO QtyReceived += tahsis
        --       (BULK'ta atıf fatura anında olur). Açık PO bitince kalan → UNLINKED (fiyat 0, kullanıcı doldurur).
        -- Yalnız faturalanmamış kalan (QtyBase - InvoicedQty) işlenir (kısmi faturalama).
        -- ============================================================================
        DECLARE @rlId UNIQUEIDENTIFIER, @rlItem UNIQUEIDENTIFIER, @rlUom UNIQUEIDENTIFIER,
                @rlPoLine UNIQUEIDENTIFIER, @rlRemain DECIMAL(18,6);

        DECLARE rcv_cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT Id, ItemId, UomId, PurchaseOrderLineId, (QtyBase - InvoicedQty)
            FROM ReceivingLine
            WHERE HeaderId = @ReceivingId AND (QtyBase - InvoicedQty) > 0.000001;
        OPEN rcv_cur;
        FETCH NEXT FROM rcv_cur INTO @rlId, @rlItem, @rlUom, @rlPoLine, @rlRemain;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            -- Bu çağrıda işlenen kalan (BULK iç loop @rlRemain'i tükettiği için ayrı tut — additive InvoicedQty)
            DECLARE @rlProcessed DECIMAL(18,6) = @rlRemain;
            -- KDV oranı üründen gelir (%0 dahil geçerli); ürün bulunamazsa parametrik fallback
            DECLARE @taxRate DECIMAL(8,4) = ISNULL((SELECT TaxRate FROM Item WHERE Id = @rlItem), @PurchaseDefaultTax);
            IF @rlPoLine IS NOT NULL
            BEGIN
                -- (1) SINGLE_PO — doğrudan bağ, fiyat PO satırından
                DECLARE @spPrice DECIMAL(18,4) = (SELECT ISNULL(Price,0) FROM PurchaseOrderLine WHERE Id = @rlPoLine);
                INSERT INTO PurchaseInvoiceLine
                    (InvoiceId, ItemId, UomId, Qty, UnitPrice, LineSubtotal, TaxRatePercent,
                     TaxAmount, LineTotal, SourceReceivingLineId, PurchaseOrderLineId, SourceLinkType)
                VALUES
                    (@NewInvoiceId, @rlItem, @rlUom, @rlRemain, @spPrice, @rlRemain * @spPrice, @taxRate,
                     @rlRemain * @spPrice * @taxRate / 100.0, @rlRemain * @spPrice * (1 + @taxRate / 100.0), @rlId, @rlPoLine, 'LINKED');
            END
            ELSE
            BEGIN
                -- (2) BULK — FIFO tahsis (en eski açık PO satırı önce)
                DECLARE @poId UNIQUEIDENTIFIER, @poRemain DECIMAL(18,6), @poPrice DECIMAL(18,4), @take DECIMAL(18,6);
                -- UPDLOCK: eşzamanlı iki fatura aynı açık PO havuzunu tüketip QtyReceived'i aşmasın (CRIT-1)
                DECLARE po_cur CURSOR LOCAL FAST_FORWARD FOR
                    SELECT pol.Id, (pol.QtyOrdered - pol.QtyReceived), ISNULL(pol.Price,0)
                    FROM PurchaseOrderLine pol WITH (UPDLOCK, ROWLOCK)
                    JOIN PurchaseOrderHeader poh ON poh.Id = pol.HeaderId
                    WHERE poh.CompanyId = @CompanyId AND poh.PartnerId = @PartnerId
                      AND poh.IsDeleted = 0 AND poh.Status IN ('POSTED','APPROVED')
                      AND pol.ItemId = @rlItem AND (pol.QtyOrdered - pol.QtyReceived) > 0.000001
                    ORDER BY poh.OrderDate ASC, poh.CreatedAt ASC;  -- FIFO: en eski sipariş önce
                OPEN po_cur;
                FETCH NEXT FROM po_cur INTO @poId, @poRemain, @poPrice;
                WHILE @@FETCH_STATUS = 0 AND @rlRemain > 0.000001
                BEGIN
                    SET @take = CASE WHEN @rlRemain <= @poRemain THEN @rlRemain ELSE @poRemain END;
                    INSERT INTO PurchaseInvoiceLine
                        (InvoiceId, ItemId, UomId, Qty, UnitPrice, LineSubtotal, TaxRatePercent,
                         TaxAmount, LineTotal, SourceReceivingLineId, PurchaseOrderLineId, SourceLinkType)
                    VALUES
                        (@NewInvoiceId, @rlItem, @rlUom, @take, @poPrice, @take * @poPrice, @taxRate,
                         @take * @poPrice * @taxRate / 100.0, @take * @poPrice * (1 + @taxRate / 100.0), @rlId, @poId, 'LINKED');
                    -- BULK atfı: PO QtyReceived fatura anında güncellenir (kabulde PO bağı yoktu)
                    UPDATE PurchaseOrderLine SET QtyReceived = QtyReceived + @take WHERE Id = @poId;
                    SET @rlRemain = @rlRemain - @take;
                    FETCH NEXT FROM po_cur INTO @poId, @poRemain, @poPrice;
                END
                CLOSE po_cur; DEALLOCATE po_cur;

                -- Açık PO satırı yetmedi → kalan UNLINKED (fiyat 0, kullanıcı POSTED öncesi doldurur)
                IF @rlRemain > 0.000001
                    INSERT INTO PurchaseInvoiceLine
                        (InvoiceId, ItemId, UomId, Qty, UnitPrice, LineSubtotal, TaxRatePercent,
                         TaxAmount, LineTotal, SourceReceivingLineId, PurchaseOrderLineId, SourceLinkType)
                    VALUES
                        (@NewInvoiceId, @rlItem, @rlUom, @rlRemain, 0, 0, @taxRate, 0, 0, @rlId, NULL, 'UNLINKED');
            END

            -- Kısmi faturalama izi: işlenen kalan eklenir (additive — gelecekte kısmi-kısmi için güvenli)
            UPDATE ReceivingLine SET InvoicedQty = InvoicedQty + @rlProcessed WHERE Id = @rlId;

            FETCH NEXT FROM rcv_cur INTO @rlId, @rlItem, @rlUom, @rlPoLine, @rlRemain;
        END
        CLOSE rcv_cur; DEALLOCATE rcv_cur;

        -- Başlık tutarlarını satırlardan topla
        UPDATE PurchaseInvoice
        SET Subtotal   = (SELECT ISNULL(SUM(LineSubtotal), 0) FROM PurchaseInvoiceLine WHERE InvoiceId = @NewInvoiceId),
            TaxAmount  = (SELECT ISNULL(SUM(TaxAmount), 0)    FROM PurchaseInvoiceLine WHERE InvoiceId = @NewInvoiceId),
            GrandTotal = (SELECT ISNULL(SUM(LineTotal), 0)    FROM PurchaseInvoiceLine WHERE InvoiceId = @NewInvoiceId)
        WHERE Id = @NewInvoiceId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Hata anında açık kalmış cursor'ları kapat (LOCAL ama mid-loop THROW'da temizle)
        IF CURSOR_STATUS('local','po_cur') >= 0  BEGIN CLOSE po_cur;  DEALLOCATE po_cur;  END
        IF CURSOR_STATUS('local','rcv_cur') >= 0 BEGIN CLOSE rcv_cur; DEALLOCATE rcv_cur; END
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Eski yanlış SP'yi kaldır (ExpenseInvoice gider modeli mal satırı taşıyamıyordu — Plan 24)
IF OBJECT_ID('dbo.sp_CreateExpenseInvoiceFromReceiving', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_CreateExpenseInvoiceFromReceiving;
GO

-- =============================================================================
-- sp_CreateShippingFromSO
-- NE YAPAR: POSTED SO'dan DRAFT ShippingHeader + ShippingLine'ları oluşturur.
--           Miktar: SalesOrderLine.QtyOrdered - QtyShipped (kalan).
-- PARAMETRELERİ:
--   @SoId          UNIQUEIDENTIFIER — kaynak SO
--   @CompanyId     UNIQUEIDENTIFIER
--   @UserId        UNIQUEIDENTIFIER
--   @NewHeaderId   UNIQUEIDENTIFIER OUTPUT
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CreateShippingFromSO
    @SoId        UNIQUEIDENTIFIER,
    @CompanyId   UNIQUEIDENTIFIER,
    @UserId      UNIQUEIDENTIFIER,
    @NewHeaderId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- SO bilgilerini al
        DECLARE @WarehouseId UNIQUEIDENTIFIER,
                @PartnerId   UNIQUEIDENTIFIER,
                @Status      NVARCHAR(50),
                @OrderNo     NVARCHAR(100);

        SELECT @WarehouseId = WarehouseId, @PartnerId = PartnerId,
               @Status = Status, @OrderNo = OrderNo
        FROM SalesOrderHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @SoId AND CompanyId = @CompanyId AND IsDeleted = 0;

        IF @WarehouseId IS NULL
            THROW 51520, N'Satış siparişi bulunamadı.', 1;

        IF @Status NOT IN ('POSTED', 'APPROVED')
            THROW 51521, N'Yalnızca onaylanmış siparişten sevkiyat oluşturulabilir.', 1;

        -- Kalan miktar > 0 olan satır olmalı
        IF NOT EXISTS (
            SELECT 1 FROM SalesOrderLine
            WHERE HeaderId = @SoId
              AND (QtyOrdered - ISNULL(QtyShipped, 0)) > 0.0001
        )
            THROW 51522, N'Tüm satırlar sevk edilmiş; yeni sevkiyat açılamaz.', 1;

        DECLARE @ShipDocNo NVARCHAR(100) =
            'SHP-' + FORMAT(GETUTCDATE(), 'yyyyMMdd-HHmmss');

        SET @NewHeaderId = NEWID();

        -- DRAFT ShippingHeader oluştur
        INSERT INTO ShippingHeader
            (Id, CompanyId, WarehouseId, PartnerId, SalesOrderId,
             DocNo, Status, CreatedBy)
        VALUES
            (@NewHeaderId, @CompanyId, @WarehouseId, @PartnerId, @SoId,
             @ShipDocNo, 'DRAFT', @UserId);

        -- Kalan miktarlı SO satırlarını kopyala
        -- QtyOriginal NOT NULL — kalan miktar hem QtyOriginal hem QtyBase'e yazılır
        INSERT INTO ShippingLine
            (HeaderId, SalesOrderLineId, ItemId, UomId, QtyOriginal, QtyBase)
        SELECT
            @NewHeaderId,
            sol.Id,
            sol.ItemId,
            sol.UomId,
            sol.QtyOrdered - ISNULL(sol.QtyShipped, 0),   -- QtyOriginal
            sol.QtyOrdered - ISNULL(sol.QtyShipped, 0)    -- QtyBase = QtyOriginal
        FROM SalesOrderLine sol
        WHERE sol.HeaderId = @SoId
          AND (sol.QtyOrdered - ISNULL(sol.QtyShipped, 0)) > 0.0001;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_PurchaseInvoicePost — Mal alım faturası onaylama (DRAFT → POSTED)
-- NE YAPAR: PurchaseInvoice'ı POSTED yapar; cari deftere tek Credit (biz tedarikçiye borçlu);
--           PaymentPlan PAYABLE açar. Stok hareketi YAZMAZ (mal zaten Receiving'de girdi).
--           Cari besleme YALNIZCA burada (irsaliye/mal kabul beslemez — R0/K3 drift kapanır).
-- PARAMETRELERİ: @InvoiceId, @CompanyId, @UserId (NVARCHAR)
-- THROW: 51530-51539
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PurchaseInvoicePost
    @InvoiceId UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi (Plan 14) — tedarikçi belge tarihi ile (KDV/dönem doğru)
        DECLARE @Status      NVARCHAR(20),
                @PartnerId   UNIQUEIDENTIFIER,
                @DocNo       NVARCHAR(50),
                @SupInvNo    NVARCHAR(50),
                @SupInvDate  DATE,
                @DueDate     DATE,
                @Grand       DECIMAL(18,4),
                @Subtotal    DECIMAL(18,4),
                @TaxAmt      DECIMAL(18,4),
                @Currency    NVARCHAR(3);   -- ISO 4217; AccountMovement.Currency NVARCHAR(3) ile hizalı

        SELECT @Status = Status, @PartnerId = PartnerId, @DocNo = DocNo,
               @SupInvNo = SupplierInvoiceNo, @SupInvDate = SupplierInvoiceDate,
               @DueDate = DueDate, @Grand = GrandTotal, @Subtotal = Subtotal,
               @TaxAmt = TaxAmount, @Currency = ISNULL(Currency, 'TRY')
        FROM PurchaseInvoice WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @InvoiceId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51530, N'Alış faturası bulunamadı.', 1;
        IF @Status = 'POSTED'
            THROW 51531, N'Fatura zaten onaylanmış.', 1;
        IF @Status = 'CANCELLED'
            THROW 51532, N'İptal edilmiş fatura onaylanamaz.', 1;
        IF @PartnerId IS NULL
            THROW 51533, N'Fatura tedarikçi bilgisi eksik.', 1;
        -- Mevzuat: tedarikçi yasal belge no + tarihi zorunlu (BA/BS + KDV)
        IF @SupInvNo IS NULL OR LTRIM(RTRIM(@SupInvNo)) = ''
            THROW 51534, N'Tedarikçi fatura numarası zorunludur.', 1;
        IF @SupInvDate IS NULL
            THROW 51535, N'Tedarikçi fatura tarihi zorunludur.', 1;
        -- İş kuralı: birim fiyatı 0 (veya negatif) kalem ile fatura onaylanamaz
        IF EXISTS (SELECT 1 FROM PurchaseInvoiceLine WHERE InvoiceId = @InvoiceId AND UnitPrice <= 0)
            THROW 51536, N'Birim fiyatı girilmemiş kalem var. Tüm kalemlere fiyat giriniz.', 1;

        -- Dönem guard: cari borç tedarikçi belge tarihine ait
        DECLARE @nowDt DATETIME2 = CAST(@SupInvDate AS DATETIME2);
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowDt, @UserId;

        -- Vade tarihi yoksa tedarikçi kartından türet (Partner.PaymentTermDays); o da yoksa parametrik varsayılan.
        -- Vade = fatura tarihi + ödeme vadesi (gün). Hardcoded +30 yerine cari-özel + Plan 29 fallback.
        IF @DueDate IS NULL
        BEGIN
            DECLARE @TermDays INT = (SELECT PaymentTermDays FROM Partner WHERE Id = @PartnerId AND CompanyId = @CompanyId);
            IF @TermDays IS NULL
                SET @TermDays = ISNULL((SELECT TRY_CAST(Value AS INT) FROM Parameter
                    WHERE CompanyId = @CompanyId AND Code = 'DEFAULT_PAYMENT_TERM_DAYS' AND IsDeleted = 0), 30);
            SET @DueDate = CAST(DATEADD(DAY, @TermDays, @nowDt) AS DATE);
        END

        -- Mutabakat kilidi (Plan 19)
        EXEC dbo.sp_GuardPartnerReconciled @CompanyId, @PartnerId, @nowDt;

        UPDATE PurchaseInvoice
        SET Status = 'POSTED', UpdatedBy = TRY_CAST(@UserId AS UNIQUEIDENTIFIER), UpdatedAt = GETUTCDATE()
        WHERE Id = @InvoiceId;

        -- Ödeme planı: alış → PAYABLE (tedarikçiye borçluyuz)
        INSERT INTO PaymentPlan
            (Id, CompanyId, SourceDocType, SourceDocId, SourceDocNo,
             PartnerId, Direction, InstallmentNo, TotalInstallments,
             DueDate, Amount, Status)
        VALUES
            (NEWID(), @CompanyId, 'PURCHASE_INVOICE', @InvoiceId, @DocNo,
             @PartnerId, 'PAYABLE', 1, 1,
             ISNULL(CAST(@DueDate AS DATETIME2), DATEADD(DAY, 30, @nowDt)), @Grand, 'OPEN');

        -- Cari hesap defteri: tek Credit (biz tedarikçiye borçluyuz) — cari besleme TEK NOKTA
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit,
             NetAmount, TaxAmount, DueDate, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        VALUES
            (NEWID(), @CompanyId, @PartnerId, @nowDt, 0, @Grand,
             @Subtotal, @TaxAmt, CAST(@DueDate AS DATETIME2), @Currency,
             'PURCHASE_INVOICE', @InvoiceId, @DocNo, N'Alış Faturası: ' + @SupInvNo, @UserId);

        -- Plan 27: Fatura↔Sipariş fiyat farkı (TOLERANS YOK — anlaşılan PO fiyatı bağlayıcı).
        -- Zincir: PurchaseInvoiceLine → ReceivingLine → PurchaseOrderLine.Price.
        -- Fatura fiyatı PO fiyatından her sapmada PriceVariance DRAFT açılır (onay yine başarılı).
        -- PO bağı olmayan (manuel) satır fark üretmez (pol.Price NULL → atlanır).
        INSERT INTO PriceVariance
            (Id, CompanyId, SourceDocType, SourceDocId, SourceLineId,
             ItemId, PartnerId, ExpectedPrice, ActualPrice,
             Variance, VariancePercent, Status, CreatedBy)
        SELECT
            NEWID(), @CompanyId, 'PURCHASE_INVOICE', @InvoiceId, pil.Id,
            pil.ItemId, @PartnerId, pol.Price, pil.UnitPrice,
            pil.UnitPrice - pol.Price,
            CASE WHEN pol.Price > 0 THEN (pil.UnitPrice - pol.Price) / pol.Price * 100 ELSE 0 END,
            'DRAFT', TRY_CAST(@UserId AS UNIQUEIDENTIFIER)
        FROM PurchaseInvoiceLine pil
        JOIN ReceivingLine    rl  ON rl.Id  = pil.SourceReceivingLineId
        -- PO satırı: SINGLE_PO'da ReceivingLine'dan, BULK→LINKED'de fatura satırının tahsis edildiği PO'dan (IMP-1)
        JOIN PurchaseOrderLine pol ON pol.Id = COALESCE(rl.PurchaseOrderLineId, pil.PurchaseOrderLineId)
        WHERE pil.InvoiceId = @InvoiceId
          AND pol.Price IS NOT NULL
          AND pil.UnitPrice <> pol.Price;

        -- Plan 27 #2: PO bağı OLMAYAN satırlar (manuel/UNLINKED) → fiyat listesi (PriceList) sapması.
        -- TOLERANS YOK (Plan 27 ilkesi): referans fiyattan HER sapma variance üretir, satınalmacı gerekçelendirir.
        -- Öncelik: tedarikçi-özel liste > genel liste; geçerlilik tarihi + miktar kademesi gözetilir.
        INSERT INTO PriceVariance
            (Id, CompanyId, SourceDocType, SourceDocId, SourceLineId,
             ItemId, PartnerId, ExpectedPrice, ActualPrice,
             Variance, VariancePercent, Status, CreatedBy)
        SELECT
            NEWID(), @CompanyId, 'PURCHASE_INVOICE', @InvoiceId, pil.Id,
            pil.ItemId, @PartnerId, ex.ExpectedPrice, pil.UnitPrice,
            pil.UnitPrice - ex.ExpectedPrice,
            CASE WHEN ex.ExpectedPrice > 0 THEN (pil.UnitPrice - ex.ExpectedPrice) / ex.ExpectedPrice * 100 ELSE 0 END,
            'DRAFT', TRY_CAST(@UserId AS UNIQUEIDENTIFIER)
        FROM PurchaseInvoiceLine pil
        CROSS APPLY (
            SELECT TOP 1 pll.UnitPrice AS ExpectedPrice
            FROM PriceList pl
            JOIN PriceListLine pll ON pll.PriceListId = pl.Id
            WHERE pl.CompanyId = @CompanyId AND pl.Direction = 'PURCHASE' AND pl.IsActive = 1
              AND pll.ItemId = pil.ItemId
              AND (pl.PartnerId = @PartnerId OR pl.PartnerId IS NULL)
              AND (pl.ValidFrom IS NULL OR pl.ValidFrom <= CAST(GETUTCDATE() AS DATE))
              AND (pl.ValidTo   IS NULL OR pl.ValidTo   >= CAST(GETUTCDATE() AS DATE))
            ORDER BY CASE WHEN pl.PartnerId = @PartnerId THEN 0 ELSE 1 END, pll.MinQty DESC
        ) ex
        WHERE pil.InvoiceId = @InvoiceId
          AND pil.PurchaseOrderLineId IS NULL   -- yalnız PO bağı olmayan satır
          AND pil.UnitPrice > 0
          AND ex.ExpectedPrice IS NOT NULL
          AND pil.UnitPrice <> ex.ExpectedPrice;   -- TOLERANS YOK: her sapma variance

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_PurchaseInvoiceReverse — Mal alım faturası iptali (POSTED → CANCELLED)
-- NE YAPAR: AccountMovement ters-satır (REVERSAL), PaymentPlan iptal. Tahsilat/ödeme varsa reddet.
-- THROW: 51540-51549
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PurchaseInvoiceReverse
    @InvoiceId UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20), @PartnerId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50),
                @Grand DECIMAL(18,4), @Subtotal DECIMAL(18,4), @TaxAmt DECIMAL(18,4),
                @SupInvDate DATE, @Currency NVARCHAR(3);

        SELECT @Status = Status, @PartnerId = PartnerId, @DocNo = DocNo,
               @Grand = GrandTotal, @Subtotal = Subtotal, @TaxAmt = TaxAmount,
               @SupInvDate = SupplierInvoiceDate, @Currency = ISNULL(Currency, 'TRY')
        FROM PurchaseInvoice WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @InvoiceId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51540, N'Alış faturası bulunamadı.', 1;
        IF @Status = 'CANCELLED'
            THROW 51541, N'Fatura zaten iptal edilmiş.', 1;
        IF @Status <> 'POSTED'
            THROW 51542, N'Yalnızca onaylanmış faturalar iptal edilebilir.', 1;

        -- İmmutability: ödenmiş ödeme varsa reddet
        IF EXISTS (
            SELECT 1 FROM PaymentPlan
            WHERE SourceDocType = 'PURCHASE_INVOICE' AND SourceDocId = @InvoiceId
              AND Status IN ('PAID','PARTIAL') AND FinancialTransactionId IS NOT NULL
        )
            THROW 51543, N'Bağlı ödeme mevcut; önce ödemeyi iptal edin.', 1;

        DECLARE @nowDt DATETIME2 = CAST(@SupInvDate AS DATETIME2);

        -- Dönem kilidi (Plan 14): ters AccountMovement belge tarihine (@SupInvDate) yazıldığından guard da o
        -- dönemi denetler — onay-anı değil hareket dönemi (kardeş sp_CorrectPurchaseInvoiceLine ile simetri).
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowDt, @UserId;

        UPDATE PurchaseInvoice
        SET Status = 'CANCELLED', UpdatedBy = TRY_CAST(@UserId AS UNIQUEIDENTIFIER), UpdatedAt = GETUTCDATE()
        WHERE Id = @InvoiceId;

        -- Açık PaymentPlan iptal
        UPDATE PaymentPlan
        SET Status = 'CANCELLED'
        WHERE SourceDocType = 'PURCHASE_INVOICE' AND SourceDocId = @InvoiceId AND Status = 'OPEN';

        -- Plan 28 Faz C tahsis geri alma (IMP-1): iptal sonrası mal kabul yeniden faturalanabilmeli.
        -- (1) BULK atfı geri al — fatura anında artırılan QtyReceived (yalnız kaynak kabul satırı PO-bağsız=BULK olanlar;
        --     SINGLE_PO'da QtyReceived kabulde sayıldı, fatura iptalinde DOKUNULMAZ). Çoklu satır → toplulaştır.
        UPDATE pol
        SET pol.QtyReceived = pol.QtyReceived - x.TotalQty
        FROM PurchaseOrderLine pol
        JOIN (
            SELECT pil.PurchaseOrderLineId, SUM(pil.Qty) AS TotalQty
            FROM PurchaseInvoiceLine pil
            JOIN ReceivingLine rl ON rl.Id = pil.SourceReceivingLineId
            WHERE pil.InvoiceId = @InvoiceId AND pil.PurchaseOrderLineId IS NOT NULL
              AND rl.PurchaseOrderLineId IS NULL          -- yalnız BULK kaynaklı
            GROUP BY pil.PurchaseOrderLineId
        ) x ON x.PurchaseOrderLineId = pol.Id;

        -- (2) Kısmi faturalama izi geri al — kaynak kabul satırlarının InvoicedQty'sini düş (toplulaştır)
        UPDATE rl
        SET rl.InvoicedQty = rl.InvoicedQty - x.TotalQty
        FROM ReceivingLine rl
        JOIN (
            SELECT pil.SourceReceivingLineId, SUM(pil.Qty) AS TotalQty
            FROM PurchaseInvoiceLine pil
            WHERE pil.InvoiceId = @InvoiceId AND pil.SourceReceivingLineId IS NOT NULL
            GROUP BY pil.SourceReceivingLineId
        ) x ON x.SourceReceivingLineId = rl.Id;

        -- AccountMovement ters kayıt: orijinal Credit'i nötrlemek için Debit
        -- DueDate ters satırda yazılmaz (vadesiz) — bakiye SUM(Debit-Credit) ile doğru nötrlenir
        -- Para birimi faturadan okunur (H2): döviz faturada 'TRY' sabiti bakiyeyi nötrlemezdi
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit,
             NetAmount, TaxAmount, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        VALUES
            (NEWID(), @CompanyId, @PartnerId, @nowDt, @Grand, 0,
             -@Subtotal, -@TaxAmt, @Currency,
             'REVERSAL', @InvoiceId, @DocNo, N'Alış Faturası İptali: ' + @DocNo, @UserId);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_CorrectPurchaseInvoiceLine — Alış faturası satır fiyat DÜZELTME (POSTED kalır)
-- Plan 27. Veri-giriş hatası düzeltme: tedarikçinin gerçek faturasını yanlış girdik.
-- TTK md.65: ledger silinmez → AccountMovement TERS-kayıt (eski) + YENİ kayıt (doğru) = append-only.
-- Fatura POSTED + aynı DocNo + tedarikçi fatura no/tarih DEĞİŞMEZ. Ödeme varsa REDDEDİLİR.
-- THROW: 51560-51569
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CorrectPurchaseInvoiceLine
    @InvoiceId    UNIQUEIDENTIFIER,
    @LineId       UNIQUEIDENTIFIER,
    @NewUnitPrice DECIMAL(18,4),
    @CompanyId    UNIQUEIDENTIFIER,
    @UserId       NVARCHAR(450),
    @Reason       NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20), @PartnerId UNIQUEIDENTIFIER, @DocNo NVARCHAR(50),
                @SupInvNo NVARCHAR(50), @SupInvDate DATE, @DueDate DATE, @Currency NVARCHAR(3),
                @OldGrand DECIMAL(18,4), @OldSub DECIMAL(18,4), @OldTax DECIMAL(18,4);

        SELECT @Status = Status, @PartnerId = PartnerId, @DocNo = DocNo,
               @SupInvNo = SupplierInvoiceNo, @SupInvDate = SupplierInvoiceDate, @DueDate = DueDate,
               @Currency = ISNULL(Currency,'TRY'),
               @OldGrand = GrandTotal, @OldSub = Subtotal, @OldTax = TaxAmount
        FROM PurchaseInvoice WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @InvoiceId AND CompanyId = @CompanyId;

        IF @Status IS NULL          THROW 51560, N'Alış faturası bulunamadı.', 1;
        IF @Status <> 'POSTED'      THROW 51561, N'Yalnızca onaylanmış fatura düzeltilebilir.', 1;
        IF @NewUnitPrice < 0        THROW 51562, N'Birim fiyat negatif olamaz.', 1;
        IF @Reason IS NULL OR LTRIM(RTRIM(@Reason)) = ''
            THROW 51563, N'Düzeltme gerekçesi zorunludur.', 1;
        -- İş kuralı: ödenmiş/kısmi ödenmiş plan varsa düzeltme reddedilir (önce ödeme iptali)
        IF EXISTS (SELECT 1 FROM PaymentPlan
                   WHERE SourceDocType='PURCHASE_INVOICE' AND SourceDocId=@InvoiceId
                     AND Status IN ('PAID','PARTIAL') AND FinancialTransactionId IS NOT NULL)
            THROW 51564, N'Bağlı ödeme mevcut; önce ödemeyi iptal edin.', 1;
        IF NOT EXISTS (SELECT 1 FROM PurchaseInvoiceLine WHERE Id=@LineId AND InvoiceId=@InvoiceId)
            THROW 51565, N'Düzeltilecek fatura kalemi bulunamadı.', 1;

        -- Dönem kilidi: tedarikçi belge tarihiyle (KDV/dönem doğru)
        DECLARE @nowDt DATETIME2 = CAST(@SupInvDate AS DATETIME2);
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowDt, @UserId;

        -- 1) Satır fiyatını + bağlı tutarları güncelle
        UPDATE PurchaseInvoiceLine
        SET UnitPrice    = @NewUnitPrice,
            LineSubtotal = Qty * @NewUnitPrice,
            TaxAmount    = ROUND(Qty * @NewUnitPrice * TaxRatePercent / 100.0, 2),
            LineTotal    = ROUND(Qty * @NewUnitPrice * (1 + TaxRatePercent / 100.0), 2)
        WHERE Id = @LineId AND InvoiceId = @InvoiceId;

        -- 1b) Düzeltme sonrası fiyat farkını yeniden hesapla (Plan 27 — fatura fiyatı değişti).
        --     Kaynak PO satır fiyatıyla kıyas; fark = yeni fiyat − PO fiyatı (tolerans yok).
        DECLARE @PoPrice DECIMAL(18,4) = (
            SELECT pol.Price
            FROM PurchaseInvoiceLine pil
            JOIN ReceivingLine rl   ON rl.Id  = pil.SourceReceivingLineId
            -- SINGLE_PO ReceivingLine'dan, BULK→LINKED fatura satırından (IMP-1 — düzeltmede de denetim)
            JOIN PurchaseOrderLine pol ON pol.Id = COALESCE(rl.PurchaseOrderLineId, pil.PurchaseOrderLineId)
            WHERE pil.Id = @LineId);
        IF @PoPrice IS NOT NULL
        BEGIN
            DECLARE @newVar    DECIMAL(18,4) = @NewUnitPrice - @PoPrice;
            DECLARE @newVarPct DECIMAL(8,4)  = CASE WHEN @PoPrice > 0 THEN (@NewUnitPrice - @PoPrice) / @PoPrice * 100 ELSE 0 END;
            IF EXISTS (SELECT 1 FROM PriceVariance
                       WHERE SourceDocType='PURCHASE_INVOICE' AND SourceDocId=@InvoiceId AND SourceLineId=@LineId AND IsDeleted=0)
            BEGIN
                IF @NewUnitPrice = @PoPrice
                    -- Fark kalmadı → mevcut farkı kapat (soft-delete; ekstre/izleme temiz)
                    UPDATE PriceVariance SET IsDeleted = 1
                    WHERE SourceDocType='PURCHASE_INVOICE' AND SourceDocId=@InvoiceId AND SourceLineId=@LineId AND IsDeleted=0;
                ELSE
                    -- Fark sürüyor → tutarları güncelle (APPROVED gerekçe korunur, sayılar tazelenir)
                    UPDATE PriceVariance SET ActualPrice=@NewUnitPrice, Variance=@newVar, VariancePercent=@newVarPct
                    WHERE SourceDocType='PURCHASE_INVOICE' AND SourceDocId=@InvoiceId AND SourceLineId=@LineId AND IsDeleted=0;
            END
            ELSE IF @NewUnitPrice <> @PoPrice
                -- Önceden fark yoktu, düzeltme fark yarattı → yeni DRAFT variance
                INSERT INTO PriceVariance
                    (Id, CompanyId, SourceDocType, SourceDocId, SourceLineId, ItemId, PartnerId,
                     ExpectedPrice, ActualPrice, Variance, VariancePercent, Status, CreatedBy)
                SELECT NEWID(), @CompanyId, 'PURCHASE_INVOICE', @InvoiceId, @LineId, pil.ItemId, @PartnerId,
                       @PoPrice, @NewUnitPrice, @newVar, @newVarPct, 'DRAFT', TRY_CAST(@UserId AS UNIQUEIDENTIFIER)
                FROM PurchaseInvoiceLine pil WHERE pil.Id = @LineId;
        END

        -- 2) Başlık toplamlarını kalemlerden yeniden hesapla
        DECLARE @NewSub DECIMAL(18,4), @NewTax DECIMAL(18,4), @NewGrand DECIMAL(18,4);
        SELECT @NewSub = ISNULL(SUM(LineSubtotal),0), @NewTax = ISNULL(SUM(TaxAmount),0)
        FROM PurchaseInvoiceLine WHERE InvoiceId = @InvoiceId;
        SET @NewGrand = @NewSub + @NewTax;

        UPDATE PurchaseInvoice
        SET Subtotal=@NewSub, TaxAmount=@NewTax, GrandTotal=@NewGrand, UpdatedAt=GETUTCDATE(),
            UpdatedBy=TRY_CAST(@UserId AS UNIQUEIDENTIFIER)
        WHERE Id = @InvoiceId;

        -- 3) Cari defter: AccountMovement append-only (document-immutability.md §1.b).
        --    Yerinde UPDATE YASAK — orijinal hareketin izi silinmemeli (VUK/e-defter bağlayıcı).
        --    Düzeltme = ters REVERSAL satır (eski Credit'i Debit ile nötrler) + yeni doğru Credit satır.
        --    Net bakiye = SUM(Credit-Debit) → eski iptal + yeni doğru = sadece yeni tutar kalır.
        --    Currency faturadan (@Currency) — döviz faturada nötrleme doğru çalışır (H1: CompanyId INSERT'te).
        INSERT INTO dbo.AccountMovement
            (Id, CompanyId, PartnerId, MovementDate, Debit, Credit,
             NetAmount, TaxAmount, Currency,
             SourceDocType, SourceDocId, SourceDocNo, Description, CreatedBy)
        VALUES
            -- Ters satır: eski tutarı nötrle (Debit=@OldGrand)
            (NEWID(), @CompanyId, @PartnerId, @nowDt, @OldGrand, 0,
             -@OldSub, -@OldTax, @Currency,
             'REVERSAL', @InvoiceId, @DocNo, N'Fatura Düzeltme (eski iptal): ' + @DocNo, @UserId),
            -- Yeni doğru satır: düzeltilmiş tutar (Credit=@NewGrand)
            (NEWID(), @CompanyId, @PartnerId, @nowDt, 0, @NewGrand,
             @NewSub, @NewTax, @Currency,
             'PURCHASE_INVOICE', @InvoiceId, @DocNo, N'Fatura Düzeltme (yeni tutar): ' + @DocNo, @UserId);

        -- 4) Açık ödeme planını yeni tutara çek (ödenmemiş)
        UPDATE PaymentPlan SET Amount = @NewGrand
        WHERE SourceDocType='PURCHASE_INVOICE' AND SourceDocId=@InvoiceId AND Status='OPEN';

        -- 5) Denetim izi (gerekçe)
        INSERT INTO AuditLog (Id, CompanyId, UserId, Action, EntityType, EntityId, Details, CreatedAt)
        VALUES (NEWID(), @CompanyId, TRY_CAST(@UserId AS UNIQUEIDENTIFIER), 'CORRECT_INVOICE_LINE',
                'PurchaseInvoice', @InvoiceId,
                N'Eski tutar ' + CONVERT(NVARCHAR,@OldGrand) + N' → yeni ' + CONVERT(NVARCHAR,@NewGrand)
                + N'. Gerekçe: ' + @Reason, GETUTCDATE());

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
