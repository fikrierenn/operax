-- Plan 25 Faz B: Sarf Fişi SP'leri (MaterialIssue)
-- Sarf tüketimi = StockMovement ISSUE + maliyet. AccountMovement YAZILMAZ (iç tüketim, partner yok).
-- Reversal = flag-only (Plan 22 dersi — ters satır YOK, çift-sayım önlemi).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- =============================================================================
-- sp_MaterialIssuePost — Sarf fişi onaylama (DRAFT → POSTED)
-- NE YAPAR: Her satır için StockMovement ISSUE (eksi QtyBase) yazar; UnitCost = anlık Moving Avg.
--           BinId NULL ise depo picking bin fallback. AccountMovement'a DOKUNMAZ (gider GL'de, ertelendi).
--           BranchId Warehouse'tan türetilir (Plan 23). SourceDocType=CONSUMPTION (GL köprüsü).
-- PARAMETRELERİ: @HeaderId, @CompanyId, @UserId (NVARCHAR)
-- THROW: 51550-51559
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_MaterialIssuePost
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Dönem kilidi (Plan 14)
        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        DECLARE @WarehouseId UNIQUEIDENTIFIER, @Status NVARCHAR(20), @DocNo NVARCHAR(50);
        SELECT @WarehouseId = WarehouseId, @Status = Status, @DocNo = DocNo
        FROM MaterialIssueHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId;

        IF @WarehouseId IS NULL
            THROW 51550, N'Sarf fişi bulunamadı.', 1;
        IF @Status = 'POSTED'
            THROW 51551, N'Sarf fişi zaten onaylanmış.', 1;
        IF @Status = 'CANCELLED'
            THROW 51552, N'İptal edilmiş sarf fişi onaylanamaz.', 1;
        IF NOT EXISTS (SELECT 1 FROM MaterialIssueLine WHERE HeaderId = @HeaderId)
            THROW 51553, N'Sarf fişinde kalem yok.', 1;

        -- İş kuralı: negatif stok engeli — depo toplam bakiyesi sarf miktarını karşılamalı
        -- (Serbest/manuel mod ileride parametre ile gevşetilebilir; varsayılan: engelle)
        DECLARE @ShortItem NVARCHAR(200);
        SELECT TOP 1 @ShortItem = i.Name
        FROM MaterialIssueLine l
        JOIN Item i ON i.Id = l.ItemId
        OUTER APPLY (
            SELECT ISNULL(SUM(inv.QtyBalance), 0) AS OnHand
            FROM tvf_InventoryBalance(@CompanyId) inv
            WHERE inv.WarehouseId = @WarehouseId AND inv.ItemId = l.ItemId
        ) bal
        WHERE l.HeaderId = @HeaderId AND bal.OnHand < l.QtyBase;
        IF @ShortItem IS NOT NULL
            THROW 51554, N'Yetersiz stok — bu üründe depo bakiyesi sarf miktarını karşılamıyor.', 1;

        -- Depo picking bin fallback (son çare — stoklu bin bulunamazsa)
        DECLARE @PickingBinId UNIQUEIDENTIFIER;
        SELECT TOP 1 @PickingBinId = Id
        FROM Bin WHERE WarehouseId = @WarehouseId AND IsPickingArea = 1;

        DECLARE @BranchId UNIQUEIDENTIFIER =
            ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @WarehouseId), dbo.fn_DefaultBranchId(@CompanyId));

        -- Stok çıkışı: her satır ISSUE (eksi QtyBase), UnitCost = anlık Moving Avg maliyet.
        -- BinId NULL ise stok OLAN bin seçilir (en çok stoklu); picking bin yalnız son çare.
        -- (Sarf storage bin'den çıkmalı; picking bin'e fallback negatif stok yaratırdı.)
        -- NOT: Tek-bin varsayımı — talep tek bin bakiyesini aşarsa bin negatife düşebilir
        -- (depo toplamı yukarıda guard'lı). Multi-bin split tahsis kapsam dışı (basit sarf).
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal, UnitCost,
             SourceDocType, SourceDocId, SourceDocNo, CreatedBy, BranchId)
        SELECT
            @CompanyId, @WarehouseId,
            ISNULL(l.BinId, ISNULL(
                (SELECT TOP 1 inv.BinId FROM tvf_InventoryBalance(@CompanyId) inv
                 WHERE inv.WarehouseId = @WarehouseId AND inv.ItemId = l.ItemId AND inv.QtyBalance > 0
                 ORDER BY inv.QtyBalance DESC),
                @PickingBinId)),
            l.ItemId, 'ISSUE',
            -l.QtyBase, l.UomId, l.Qty, ISNULL(ic.AvgCost, 0),
            'CONSUMPTION', @HeaderId, @DocNo, @UserId, @BranchId
        FROM MaterialIssueLine l
        LEFT JOIN ItemCost ic ON ic.CompanyId = @CompanyId AND ic.ItemId = l.ItemId
        WHERE l.HeaderId = @HeaderId;

        UPDATE MaterialIssueHeader
        SET Status = 'POSTED', UpdatedAt = @now, UpdatedBy = TRY_CAST(@UserId AS UNIQUEIDENTIFIER)
        WHERE Id = @HeaderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_MaterialIssueReverse — Sarf fişi iptali (POSTED → CANCELLED)
-- NE YAPAR: ISSUE hareketlerini IsCancelled=1 flag ile kapatır (ters satır YOK — çift-sayım önlemi).
--           Bakiye SUM(QtyBase WHERE IsCancelled=0) ile stok geri gelir.
-- THROW: 51560-51562
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_MaterialIssueReverse
    @HeaderId  UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @now DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @now, @UserId;

        DECLARE @Status NVARCHAR(20);
        SELECT @Status = Status
        FROM MaterialIssueHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId;

        IF @Status IS NULL
            THROW 51560, N'Sarf fişi bulunamadı.', 1;
        IF @Status = 'CANCELLED'
            THROW 51561, N'Sarf fişi zaten iptal edilmiş.', 1;
        IF @Status <> 'POSTED'
            THROW 51562, N'Yalnızca onaylanmış sarf fişi iptal edilebilir.', 1;

        -- Flag-only iptal: ISSUE hareketlerini kapat (ters satır YAZILMAZ — çift-sayım önlemi)
        UPDATE StockMovement
        SET IsCancelled = 1, CancelledAt = @now, CancelledBy = @UserId
        WHERE SourceDocType = 'CONSUMPTION' AND SourceDocId = @HeaderId AND IsCancelled = 0;

        UPDATE MaterialIssueHeader
        SET Status = 'CANCELLED', UpdatedAt = @now, UpdatedBy = TRY_CAST(@UserId AS UNIQUEIDENTIFIER)
        WHERE Id = @HeaderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
