-- Plan 28 Faz B — sp_ReceivingTerminalScan: el-terminali barkod okutma, 3 mod.
-- SINGLE_PO: ürün PO'da mı + kalan miktar; fazla → ReturnQty (iade alanı).
-- BULK_SUPPLIER: tedarikçinin açık PO havuzu; fazla → ReturnQty. PO satır bağı YOK (fatura aşaması).
-- FREE: serbest (yetki C# katmanında). Tarama yalnız DRAFT belgeye yazar (stok POST'ta).
-- THROW: 51570-51579. Çıktı: Accepted, Excess, ItemName (SELECT).
CREATE OR ALTER PROCEDURE dbo.sp_ReceivingTerminalScan
    @HeaderId  UNIQUEIDENTIFIER,
    @ItemId    UNIQUEIDENTIFIER,
    @Qty       DECIMAL(18,6),
    @LotNo     NVARCHAR(100) = NULL,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Mode NVARCHAR(20), @PoId UNIQUEIDENTIFIER, @PartnerId UNIQUEIDENTIFIER, @Status NVARCHAR(20);
        SELECT @Mode = ReceivingMode, @PoId = PurchaseOrderId, @PartnerId = PartnerId, @Status = Status
        FROM ReceivingHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId AND IsDeleted = 0;

        IF @Mode IS NULL THROW 51570, N'Mal kabul belgesi bulunamadı.', 1;
        IF @Status <> 'DRAFT' THROW 51571, N'Yalnızca taslak belgeye okutma yapılır.', 1;
        IF @Qty IS NULL OR @Qty <= 0 THROW 51572, N'Miktar 0''dan büyük olmalı.', 1;

        DECLARE @BaseUom UNIQUEIDENTIFIER = (SELECT BaseUomId FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId);
        IF @BaseUom IS NULL THROW 51573, N'Ürün bulunamadı.', 1;

        DECLARE @Accepted DECIMAL(18,6) = 0, @Excess DECIMAL(18,6) = 0, @PoLineId UNIQUEIDENTIFIER = NULL;

        IF @Mode = 'SINGLE_PO'
        BEGIN
            IF @PoId IS NULL THROW 51574, N'Belge siparişe bağlı değil.', 1;
            -- Ürün bu siparişte mi?
            SELECT TOP 1 @PoLineId = Id FROM PurchaseOrderLine WHERE HeaderId = @PoId AND ItemId = @ItemId;
            IF @PoLineId IS NULL THROW 51575, N'Bu ürün seçili siparişte yok.', 1;

            DECLARE @Ordered DECIMAL(18,6) = (SELECT QtyOrdered FROM PurchaseOrderLine WHERE Id = @PoLineId);
            -- Bu PO satırına şu ana dek (iptal olmayan tüm mal kabullerde) okutulan
            DECLARE @RcvSoFar DECIMAL(18,6) = (
                SELECT ISNULL(SUM(rl.QtyBase), 0)
                FROM ReceivingLine rl JOIN ReceivingHeader rh ON rh.Id = rl.HeaderId
                WHERE rl.PurchaseOrderLineId = @PoLineId AND rh.IsDeleted = 0 AND rh.Status <> 'CANCELLED');
            DECLARE @Remaining DECIMAL(18,6) = @Ordered - @RcvSoFar;
            IF @Remaining < 0 SET @Remaining = 0;
            SET @Accepted = CASE WHEN @Qty <= @Remaining THEN @Qty ELSE @Remaining END;
            SET @Excess   = @Qty - @Accepted;
        END
        ELSE IF @Mode = 'BULK_SUPPLIER'
        BEGIN
            IF @PartnerId IS NULL THROW 51576, N'Belge tedarikçiye bağlı değil.', 1;
            -- Tedarikçinin açık siparişlerinde bu ürünün kalanı (havuz). PO satır bağı YOK — fatura aşaması.
            DECLARE @Pool DECIMAL(18,6) = (
                SELECT ISNULL(SUM(pol.QtyOrdered - ISNULL(pol.QtyReceived, 0)), 0)
                FROM PurchaseOrderLine pol JOIN PurchaseOrderHeader poh ON poh.Id = pol.HeaderId
                WHERE poh.PartnerId = @PartnerId AND poh.CompanyId = @CompanyId
                  AND poh.Status IN ('POSTED','APPROVED') AND poh.IsDeleted = 0
                  AND pol.ItemId = @ItemId AND (pol.QtyOrdered - ISNULL(pol.QtyReceived,0)) > 0);
            -- Bu belgede bu ürüne dek okutulan (havuzdan düş)
            DECLARE @ScannedHere DECIMAL(18,6) = (
                SELECT ISNULL(SUM(QtyBase + ReturnQty), 0) FROM ReceivingLine
                WHERE HeaderId = @HeaderId AND ItemId = @ItemId);
            DECLARE @PoolRemaining DECIMAL(18,6) = @Pool - @ScannedHere;
            IF @PoolRemaining < 0 SET @PoolRemaining = 0;
            SET @Accepted = CASE WHEN @Qty <= @PoolRemaining THEN @Qty ELSE @PoolRemaining END;
            SET @Excess   = @Qty - @Accepted;
        END
        ELSE  -- FREE
        BEGIN
            SET @Accepted = @Qty;
            SET @Excess   = 0;
        END

        -- Upsert satır: SINGLE_PO'da PO satırı bazlı, diğerlerinde ürün bazlı tek satır
        DECLARE @LineId UNIQUEIDENTIFIER = (
            SELECT TOP 1 Id FROM ReceivingLine
            WHERE HeaderId = @HeaderId AND ItemId = @ItemId
              AND ((@PoLineId IS NULL AND PurchaseOrderLineId IS NULL) OR PurchaseOrderLineId = @PoLineId));

        IF @LineId IS NOT NULL
            UPDATE ReceivingLine
            SET QtyBase = QtyBase + @Accepted, QtyOriginal = QtyOriginal + @Accepted + @Excess,
                ReturnQty = ReturnQty + @Excess, LotNo = ISNULL(@LotNo, LotNo)
            WHERE Id = @LineId;
        ELSE
            INSERT INTO ReceivingLine (Id, HeaderId, PurchaseOrderLineId, ItemId, UomId, QtyOriginal, QtyBase, ReturnQty, LotNo)
            VALUES (NEWID(), @HeaderId, @PoLineId, @ItemId, @BaseUom, @Accepted + @Excess, @Accepted, @Excess, @LotNo);

        COMMIT TRANSACTION;

        SELECT @Accepted AS Accepted, @Excess AS Excess,
               (SELECT Name FROM Item WHERE Id = @ItemId) AS ItemName;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
