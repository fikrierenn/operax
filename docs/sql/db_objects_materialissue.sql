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

        -- Başlık alanlarını TEK okumada çek. UPDLOCK+ROWLOCK: aynı fişi iki kullanıcı eşzamanlı onaylarsa
        -- ikincisi bu satırı bekler (çift-post yarışı önlenir). Okunan değişkenler:
        --   @WarehouseId    = sarfın çıkacağı depo (stok consume + branch türetimi için)
        --   @Status         = mevcut durum (DRAFT/POSTED/CANCELLED — guard kontrolleri)
        --   @DocNo          = fiş no (StockMovement.SourceDocNo izi)
        --   @ReasonCode     = sarf/zayiat nedeni kodu (NULL=normal sarf; set=zayiat → SCRAP + KDV mantığı)
        --   @IsForceMajeure = mücbir sebep mi (Maliye-ilan afet → md.30/c istisnası → KDV korunur)
        DECLARE @WarehouseId UNIQUEIDENTIFIER, @Status NVARCHAR(20), @DocNo NVARCHAR(50),
                @ReasonCode NVARCHAR(20), @IsForceMajeure BIT;
        SELECT @WarehouseId = WarehouseId, @Status = Status, @DocNo = DocNo, @ReasonCode = ReasonCode,
               @IsForceMajeure = IsForceMajeure
        FROM MaterialIssueHeader WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @HeaderId AND CompanyId = @CompanyId;

        -- Hareket tipi: ReasonCode set ise zayiat (SCRAP), değilse normal sarf (ISSUE).
        -- Zayiat VUK ayrımı için StockMovement.MovementType='SCRAP' yazılır; stok düşüşü aynıdır.
        DECLARE @MoveType NVARCHAR(20) = CASE WHEN @ReasonCode IS NOT NULL THEN 'SCRAP' ELSE 'ISSUE' END;

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

        DECLARE @BranchId UNIQUEIDENTIFIER =
            ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @WarehouseId), dbo.fn_DefaultBranchId(@CompanyId));

        -- Stok çıkışı: her satır ATOMİK consume (Plan 44). Satır bini BELİRTİLMİŞSE o bin'den (kullanıcı
        -- seçimi); BELİRTİLMEMİŞSE depo stoğu FEFO/FIFO sırayla bin/lot'lara BÖLÜNÜR (sp_ConsumeInventoryFEFO).
        -- Bin-seviyesi yeterlilik consume içinde applock ile atomik (oversell yok; dağıtık stokta split).
        DECLARE @miUser UNIQUEIDENTIFIER = TRY_CAST(@UserId AS UNIQUEIDENTIFIER);
        DECLARE @mlId UNIQUEIDENTIFIER, @mlItem UNIQUEIDENTIFIER, @mlUom UNIQUEIDENTIFIER,
                @mlQty DECIMAL(18,6), @mlBin UNIQUEIDENTIFIER, @mlCost DECIMAL(18,6);
        DECLARE c_mi CURSOR LOCAL FAST_FORWARD FOR
            SELECT l.Id, l.ItemId, l.UomId, l.QtyBase, l.BinId,   -- ham bin (NULL → FEFO depodan dağıt)
                   ISNULL((SELECT TOP 1 ic.AvgCost FROM ItemCost ic
                           WHERE ic.CompanyId = @CompanyId AND ic.ItemId = l.ItemId), 0)
            FROM MaterialIssueLine l
            WHERE l.HeaderId = @HeaderId;
        OPEN c_mi;
        FETCH NEXT FROM c_mi INTO @mlId, @mlItem, @mlUom, @mlQty, @mlBin, @mlCost;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            IF @mlBin IS NOT NULL
                EXEC dbo.sp_ConsumeInventory                       -- kullanıcı bin'i seçti → o bin
                    @CompanyId = @CompanyId, @WarehouseId = @WarehouseId, @BinId = @mlBin,
                    @ItemId = @mlItem, @UomId = @mlUom, @QtyBase = @mlQty, @LotNo = NULL,
                    @SourceDocType = 'CONSUMPTION', @SourceDocId = @HeaderId, @SourceLineId = @mlId,
                    @SourceDocNo = @DocNo, @UnitCost = @mlCost, @UserId = @miUser,
                    @MovementDate = @now, @BranchId = @BranchId, @MovementType = @MoveType;
            ELSE
                EXEC dbo.sp_ConsumeInventoryFEFO                   -- bin yok → FEFO/FIFO multi-bin tahsis
                    @CompanyId = @CompanyId, @WarehouseId = @WarehouseId,
                    @ItemId = @mlItem, @UomId = @mlUom, @QtyBase = @mlQty,
                    @SourceDocType = 'CONSUMPTION', @SourceDocId = @HeaderId, @SourceLineId = @mlId,
                    @SourceDocNo = @DocNo, @UnitCost = @mlCost, @UserId = @miUser,
                    @MovementDate = @now, @BranchId = @BranchId, @MovementType = @MoveType;
            FETCH NEXT FROM c_mi INTO @mlId, @mlItem, @mlUom, @mlQty, @mlBin, @mlCost;
        END
        CLOSE c_mi; DEALLOCATE c_mi;

        -- ===== İLAVE EDİLECEK KDV HESABI (ön-muhasebe — KDV Kanunu md.30/c) =====
        -- NEDEN: Zayi olan mala ait DAHA ÖNCE İNDİRİLEN KDV, md.30/c gereği indirilemez; indirildiyse
        --        düzeltilir (beyannamede "ilave edilecek KDV"). Operax'ta tam GL fişi (360/391) henüz yok;
        --        bu adım ön-muhasebe: tutarı HESAPLAYIP belgede SAKLAR ki muhasebeci/GL modülü işleyebilsin.
        --
        -- @RequiresKdv: Bu zayiat nedeni KDV düzeltmesi gerektiriyor mu? Kararı KOD'a gömmüyoruz —
        --   sözlükteki (MATERIAL_ISSUE_REASON) RequiresKdvAdjustment flag'inden OKUYORUZ (admin yönetir,
        --   mevzuat-bağlı). DAMAGE/FIRE/SCRAP=1 (olağanüstü zayiat → düzelt), NORMAL_FIRE=0 (üretim firesi
        --   zayiat değil → KDV nötr). Neden NULL ise (normal sarf) hesap hiç çalışmaz → @RequiresKdv 0 kalır.
        DECLARE @RequiresKdv BIT = 0;
        IF @ReasonCode IS NOT NULL
            SELECT @RequiresKdv = ISNULL(dv.RequiresKdvAdjustment, 0)
            FROM DictionaryValue dv
            JOIN DictionaryType dt ON dt.Id = dv.TypeId
            WHERE dt.Code = 'MATERIAL_ISSUE_REASON' AND dt.CompanyId = @CompanyId   -- yalnız bu firmanın sözlüğü
              AND dv.Code = @ReasonCode AND dv.IsDeleted = 0;

        -- @KdvAdj: hesaplanan ilave-KDV tutarı. KOŞUL: neden KDV-düzeltme gerektirir (@RequiresKdv=1)
        --   VE mücbir-sebep DEĞİL (@IsForceMajeure=0). Mücbir sebep (Maliye-ilan deprem/sel/yangın) →
        --   md.30/c istisnası → KDV korunur → tutar 0 kalır. NORMAL_FIRE de buraya hiç girmez (@RequiresKdv=0).
        -- FORMÜL: her satır için (temel-birim miktar × birim-maliyet × KDV-oranı/100), satırlar toplanır.
        --   l.QtyBase   = sarf/zayiat miktarı (temel birim)
        --   ic.AvgCost  = ürünün hareketli-ortalama birim maliyeti (ItemCost; Plan 47'de ŞİRKET-GENEL grain
        --                 = item başına TEK satır → LEFT JOIN fan-out üretmez, KDV şişmez)
        --   it.TaxRate  = ürünün KDV oranı (% — Item.TaxRate, varsayılan 20)
        --   NOT: gerçek lot-bazlı yüklenilen-KDV tutulmadığından emsal-bedel yaklaşımı (ön-muhasebe kabul).
        DECLARE @KdvAdj DECIMAL(18,2) = 0;
        IF @RequiresKdv = 1 AND ISNULL(@IsForceMajeure, 0) = 0
            SELECT @KdvAdj = ISNULL(SUM(l.QtyBase * ISNULL(ic.AvgCost, 0) * ISNULL(it.TaxRate, 0) / 100.0), 0)
            FROM MaterialIssueLine l
            JOIN Item it ON it.Id = l.ItemId
            LEFT JOIN ItemCost ic ON ic.CompanyId = @CompanyId AND ic.ItemId = l.ItemId
            WHERE l.HeaderId = @HeaderId;

        UPDATE MaterialIssueHeader
        SET Status = 'POSTED', KdvAdjustmentAmount = @KdvAdj,
            UpdatedAt = @now, UpdatedBy = TRY_CAST(@UserId AS UNIQUEIDENTIFIER)
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
        -- CancelledBy UNIQUEIDENTIFIER → @UserId NVARCHAR TRY_CAST ile (header UpdatedBy ile simetrik; H5)
        SET IsCancelled = 1, CancelledAt = @now, CancelledBy = TRY_CAST(@UserId AS UNIQUEIDENTIFIER)
        WHERE SourceDocType = 'CONSUMPTION' AND SourceDocId = @HeaderId AND IsCancelled = 0;

        -- İptal → ilave-KDV düzeltmesi de geri alınır (zayiat iptal edildi, KDV düzeltme dayanağı kalktı)
        UPDATE MaterialIssueHeader
        SET Status = 'CANCELLED', KdvAdjustmentAmount = 0,
            UpdatedAt = @now, UpdatedBy = TRY_CAST(@UserId AS UNIQUEIDENTIFIER)
        WHERE Id = @HeaderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
