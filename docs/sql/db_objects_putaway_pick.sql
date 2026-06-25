-- =============================================================================
-- Plan 33 Faz B — WMS terminal SP'leri (SQL-First; C# orkestrasyonu kaldırıldı)
-- AC1 sp_PutawayPost  : mal kabul rafından depolama rafına bin-to-bin yerleme
-- AC2 sp_PickConfirm   : el terminali barkod onayı + toplama satırı tamamlama
-- İki SP de TRY/CATCH + ROLLBACK + THROW (50000-59999) + dönem kilidi taşır.
-- =============================================================================

-- =============================================================================
-- sp_PutawayPost — Adresleme (Putaway): mal kabul alanından depolama rafına taşıma
-- NE YAPAR: Tek-satır bin-to-bin transfer; StockTransfer başlığı POSTED açar,
--           çıkış (kaynak raf, eksi) + giriş (hedef raf, artı) StockMovement yazar.
-- İŞ KURALI: Kaynak rafta yeterli stok yoksa REDDEDİLİR (negatif stok engeli).
-- THROW: 51570 yetersiz stok · 51571 geçersiz miktar · 51572 raf bulunamadı
-- BAĞIMLILIK: sp_GuardPeriodOpen, tvf_InventoryBalance, fn_DefaultBranchId
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PutawayPost
    @ItemId    UNIQUEIDENTIFIER,
    @FromBinId UNIQUEIDENTIFIER,
    @ToBinId   UNIQUEIDENTIFIER,
    @Qty       DECIMAL(18,4),
    @CompanyId UNIQUEIDENTIFIER,
    @UserId    NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Guard: miktar pozitif olmalı
        IF @Qty IS NULL OR @Qty <= 0
            THROW 51571, N'Yerleme miktarı sıfırdan büyük olmalı.', 1;

        -- Kaynak ve hedef rafın deposunu türet (raf → depo); raf yoksa hata
        DECLARE @FromWarehouseId UNIQUEIDENTIFIER, @ToWarehouseId UNIQUEIDENTIFIER, @BaseUomId UNIQUEIDENTIFIER;

        SELECT @FromWarehouseId = w.Id
        FROM Bin b JOIN Warehouse w ON w.Id = b.WarehouseId
        WHERE b.Id = @FromBinId AND w.CompanyId = @CompanyId;

        SELECT @ToWarehouseId = w.Id
        FROM Bin b JOIN Warehouse w ON w.Id = b.WarehouseId
        WHERE b.Id = @ToBinId AND w.CompanyId = @CompanyId;

        IF @FromWarehouseId IS NULL OR @ToWarehouseId IS NULL
            THROW 51572, N'Kaynak veya hedef raf bulunamadı.', 1;

        -- Ürünün temel birimi — ürün yoksa/birim tanımsızsa erken Türkçe hata (IMP-1)
        SELECT @BaseUomId = BaseUomId FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId;
        IF @BaseUomId IS NULL
            THROW 51573, N'Ürün bulunamadı veya temel birim tanımsız.', 1;

        -- İş kuralı: kaynak rafta yeterli stok yoksa yerleme reddedilir (negatif stok engeli)
        DECLARE @Available DECIMAL(18,4) = (
            SELECT ISNULL(SUM(QtyBalance), 0)
            FROM tvf_InventoryBalance(@CompanyId)
            WHERE ItemId = @ItemId AND BinId = @FromBinId);
        IF @Available < @Qty
            THROW 51570, N'Kaynak rafta yeterli stok yok.', 1;

        -- Dönem kilidi (onay anı)
        DECLARE @nowT DATETIME2 = GETUTCDATE();
        EXEC dbo.sp_GuardPeriodOpen @CompanyId, @nowT, @UserId;

        DECLARE @TransferId UNIQUEIDENTIFIER = NEWID();
        DECLARE @DocNo NVARCHAR(50) = CONCAT('RPL-', FORMAT(@nowT, 'yyyyMMddHHmm'));

        -- Transfer başlığı: aynı depo içi bin-to-bin, doğrudan POSTED
        -- CreatedBy UNIQUEIDENTIFIER → @UserId NVARCHAR TRY_CAST ile (implicit cast riski, CRIT-1)
        INSERT INTO StockTransfer
            (Id, CompanyId, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes, CreatedBy)
        VALUES
            (@TransferId, @CompanyId, @DocNo, 'POSTED', 'BIN_TO_BIN',
             @FromWarehouseId, @ToWarehouseId, N'ADRESLEME (PUTAWAY)', TRY_CAST(@UserId AS UNIQUEIDENTIFIER));

        -- Çıkış hareketi (kaynak raf, eksi)
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy, BranchId)
        VALUES
            (@CompanyId, @FromWarehouseId, @FromBinId, @ItemId, 'TRANSFER',
             -@Qty, @BaseUomId, @Qty, 'TRANSFER', @TransferId, @DocNo, @UserId,
             ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @FromWarehouseId), dbo.fn_DefaultBranchId(@CompanyId)));

        -- Giriş hareketi (hedef raf, artı)
        INSERT INTO StockMovement
            (CompanyId, WarehouseId, BinId, ItemId, MovementType,
             QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy, BranchId)
        VALUES
            (@CompanyId, @ToWarehouseId, @ToBinId, @ItemId, 'TRANSFER',
             @Qty, @BaseUomId, @Qty, 'TRANSFER', @TransferId, @DocNo, @UserId,
             ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @ToWarehouseId), dbo.fn_DefaultBranchId(@CompanyId)));

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- =============================================================================
-- sp_PickConfirm — El terminali KILAVUZLU toplama onayı (atomik) — Plan 56 Faz C
-- NE YAPAR: (1) Raf doğrula (@BinBarcode verilirse TargetBin.Code ile eşleşmeli — yanlış raf yakalanır)
--           (2) Ürün doğrula (barkod ürün/ItemBarcode ile) (3) Miktar uygula (@ActualQty, default tam)
--           kısmi (short-pick) ise satır AÇIK kalır ama ExceptionNote='SHORT' ile akıştan düşer.
--           İstisna (@ExceptionType SKIP/DAMAGED) ürün/miktar atlanır, yalnız iz bırakılır.
--           DRAFT→IN_PROGRESS; tüm satırlar tamamlanınca (veya istisna-işaretli) COMPLETED.
-- GERİYE UYUMLU: @BinBarcode/@ActualQty/@ExceptionType NULL → eski tam-pick davranışı.
-- NOT: StockMovement YAZMAZ (gerçek çıkış sp_ShippingPost — çift-düşüm fix korunur).
-- THROW: 51580 satır yok · 51581 ürün barkod eşleşmedi · 51582 yanlış raf · 51583 miktar geçersiz
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PickConfirm
    @TaskId        UNIQUEIDENTIFIER,
    @LineId        UNIQUEIDENTIFIER,
    @Barcode       NVARCHAR(100),
    @CompanyId     UNIQUEIDENTIFIER,
    @UserId        NVARCHAR(450),
    @BinBarcode    NVARCHAR(100)  = NULL,   -- raf doğrulama (NULL = atla)
    @ActualQty     DECIMAL(18,6)  = NULL,   -- toplanan miktar (NULL = tam istenen)
    @ExceptionType NVARCHAR(20)   = NULL,   -- NULL=normal · SHORT · SKIP · DAMAGED
    @ExceptionNote NVARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Satır + ürün + hedef raf (görev şirket filtresiyle — IDOR koruması)
        DECLARE @ItemId UNIQUEIDENTIFIER, @ItemCode NVARCHAR(100),
                @BinCode NVARCHAR(100), @ReqBase DECIMAL(18,6);
        SELECT @ItemId = l.ItemId, @ItemCode = i.Code,
               @BinCode = b.Code, @ReqBase = l.QtyRequestedBase
        FROM PickTaskLine l
        JOIN PickTask pt ON pt.Id = l.PickTaskId
        JOIN Item i ON i.Id = l.ItemId
        LEFT JOIN Bin b ON b.Id = l.TargetBinId
        WHERE l.Id = @LineId AND l.PickTaskId = @TaskId AND pt.CompanyId = @CompanyId;

        IF @ItemId IS NULL
            THROW 51580, N'Toplama satırı bulunamadı.', 1;

        -- İSTİSNA (atla/hasar): ürün/miktar doğrulaması atlanır, yalnız iz bırakılır (satır açıktan düşer)
        IF @ExceptionType IN ('SKIP', 'DAMAGED')
        BEGIN
            UPDATE PickTaskLine
            SET ExceptionNote = @ExceptionType + ISNULL(N': ' + @ExceptionNote, N'')
            WHERE Id = @LineId;
        END
        ELSE
        BEGIN
            -- (1) RAF DOĞRULA: @BinBarcode verildiyse hedef rafla eşleşmeli (yanlış raftan toplama engeli)
            IF @BinBarcode IS NOT NULL AND @BinCode IS NOT NULL
               AND UPPER(LTRIM(RTRIM(@BinBarcode))) <> UPPER(@BinCode)
                THROW 51582, N'Yanlış raf! Beklenen rafa gidin.', 1;

            -- (2) ÜRÜN DOĞRULA: barkod ürün kodu VEYA ItemBarcode ile eşleşmeli
            DECLARE @Match BIT = 0;
            IF UPPER(@Barcode) = UPPER(@ItemCode)
                SET @Match = 1;
            ELSE IF EXISTS (SELECT 1 FROM ItemBarcode WHERE ItemId = @ItemId AND Barcode = @Barcode)
                SET @Match = 1;
            IF @Match = 0
                THROW 51581, N'Barkod eşleşmedi! Beklenen ürün kodu farklı.', 1;

            -- (3) MİKTAR: default tam istenen; girilmişse o (short-pick = istenenden az)
            DECLARE @Qty DECIMAL(18,6) = ISNULL(@ActualQty, @ReqBase);
            IF @Qty <= 0 OR @Qty > @ReqBase
                THROW 51583, N'Geçersiz miktar (0 ile istenen arası olmalı).', 1;

            -- Satırı işle: tam → tamamlandı; kısmi → ExceptionNote='SHORT' (satır açık ama akıştan düşer)
            -- Plan 57: QtyPicked (orijinal birim, oranla) + toplama izi (bin/kullanıcı/zaman) de yazılır —
            -- sp_ShippingPost pick-driven ledger reconcile'ı bu gerçek değerlere dayanır.
            UPDATE PickTaskLine
            SET QtyPickedBase  = @Qty,
                QtyPicked      = CAST(@Qty * QtyRequested / NULLIF(QtyRequestedBase, 0) AS DECIMAL(18,6)),
                PickedBinId    = TargetBinId,
                PickedByUserId = @UserId,
                PickedAt       = GETUTCDATE(),
                ExceptionNote  = CASE WHEN @Qty < @ReqBase
                                      THEN N'SHORT' + ISNULL(N': ' + @ExceptionNote, N'')
                                      ELSE ExceptionNote END
            WHERE Id = @LineId;
        END

        -- DRAFT görev ilk işlemle IN_PROGRESS
        UPDATE PickTask
        SET Status = 'IN_PROGRESS', AssignedUserId = @UserId
        WHERE Id = @TaskId AND Status = 'DRAFT';

        -- Tüm satırlar İŞLENDİ (tam toplandı VEYA istisna-işaretli) ise görev COMPLETED
        IF NOT EXISTS (
            SELECT 1 FROM PickTaskLine
            WHERE PickTaskId = @TaskId
              AND QtyPickedBase < QtyRequestedBase
              AND ExceptionNote IS NULL)
            UPDATE PickTask SET Status = 'COMPLETED' WHERE Id = @TaskId;

        -- Plan 56 Faz D: görev tamamlandı → bağlı sevkiyat PICKING→PICKED (toplandı, sevke hazır).
        -- Stok HÂLÂ çıkmadı (yalnız sp_ShippingPost) — staging görünürlüğü; kullanıcı POSTED yapar.
        UPDATE sh SET sh.Status = 'PICKED', sh.UpdatedAt = GETUTCDATE()
        FROM ShippingHeader sh
        JOIN PickTask pt ON pt.ShipmentId = sh.Id
        WHERE pt.Id = @TaskId AND pt.Status = 'COMPLETED' AND sh.Status = 'PICKING';

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
