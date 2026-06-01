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
-- sp_CreateExpenseInvoiceFromReceiving
-- NE YAPAR: POSTED Receiving'den DRAFT ExpenseInvoice + satırlar oluşturur.
--           Receiving.PartnerId → Invoice.PartnerId; birim fiyat PO'dan gelir.
-- PARAMETRELERİ:
--   @ReceivingId   UNIQUEIDENTIFIER — kaynak Receiving
--   @CompanyId     UNIQUEIDENTIFIER
--   @UserId        UNIQUEIDENTIFIER
--   @NewInvoiceId  UNIQUEIDENTIFIER OUTPUT
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CreateExpenseInvoiceFromReceiving
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

        -- Receiving bilgilerini al
        DECLARE @PartnerId UNIQUEIDENTIFIER,
                @Status    NVARCHAR(50),
                @DocNo     NVARCHAR(100);

        SELECT @PartnerId = PartnerId, @Status = Status, @DocNo = DocNo
        FROM ReceivingHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @ReceivingId AND CompanyId = @CompanyId AND IsDeleted = 0;

        IF @PartnerId IS NULL
            THROW 51510, N'Mal kabul belgesi bulunamadı.', 1;

        IF @Status <> 'POSTED'
            THROW 51511, N'Yalnızca onaylanmış mal kabulden fatura oluşturulabilir.', 1;

        -- İş kuralı: zaten fatura bağlıysa yeni açma
        IF EXISTS (
            SELECT 1 FROM ExpenseInvoice
            WHERE ReceivingId = @ReceivingId
              AND Status NOT IN ('CANCELLED')
              AND CompanyId = @CompanyId
        )
            THROW 51512, N'Bu mal kabulden zaten fatura oluşturulmuş.', 1;

        -- Fatura no üret
        DECLARE @InvDocNo NVARCHAR(100) =
            'ALN-' + FORMAT(GETUTCDATE(), 'yyyyMMdd-HHmmss');

        -- Toplam tutarı hesapla (PO fiyatından, yoksa 0)
        DECLARE @Total DECIMAL(18,4) = ISNULL((
            SELECT SUM(rl.QtyBase * ISNULL(pol.Price, 0))
            FROM ReceivingLine rl
            LEFT JOIN PurchaseOrderLine pol ON pol.Id = rl.PurchaseOrderLineId
            WHERE rl.HeaderId = @ReceivingId
        ), 0);

        SET @NewInvoiceId = NEWID();

        -- DRAFT ExpenseInvoice oluştur
        INSERT INTO ExpenseInvoice
            (Id, CompanyId, PartnerId, ReceivingId, DocNo,
             InvoiceDate, TotalAmount, Status)
        VALUES
            (@NewInvoiceId, @CompanyId, @PartnerId, @ReceivingId, @InvDocNo,
             CAST(GETUTCDATE() AS DATE), @Total, 'DRAFT');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
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
