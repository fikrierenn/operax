-- ============================================================
-- Plan 44 — Stok Çıkış Atomik Primitive (db_objects_consume.sql)
-- TÜM stok çıkışları (shipping/material-issue/picking/production/transfer) buradan geçer.
-- Negatif stok invariantı + concurrency + idempotency tek atomik noktada korunur.
-- ============================================================

-- =============================================================================
-- sp_ConsumeInventory — bir item/warehouse/bin/lot için atomik stok düşümü.
-- NE YAPAR: sp_getapplock (item/bin exclusive) ile eşzamanlı tüketicileri FIFO serialize eder,
--   yeterlilik kontrol eder, yetersizse THROW, yeterli ise ISSUE hareketi yazar (deadlock'suz).
-- IDEMPOTENCY: (SourceDocType, SourceDocId, SourceLineId, MovementType) tekildir; çift çağrı reddedilir.
-- ÇAĞRAN: caller bir TRANSACTION içinde çağırır (primitive kendi tx'ini açmaz — atomiklik caller'da).
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ConsumeInventory
    @CompanyId    UNIQUEIDENTIFIER,
    @WarehouseId  UNIQUEIDENTIFIER,
    @BinId        UNIQUEIDENTIFIER,          -- ZORUNLU (StockMovement.BinId NOT NULL; key-range determinizmi)
    @ItemId       UNIQUEIDENTIFIER,
    @UomId        UNIQUEIDENTIFIER,
    @QtyBase      DECIMAL(18,6),             -- pozitif tüketim miktarı (taban birim)
    @LotNo        NVARCHAR(100) = NULL,      -- şema LotNo NVARCHAR(100) ile hizalı (truncation yok)
    @SourceDocType NVARCHAR(30),
    @SourceDocId  UNIQUEIDENTIFIER,
    @SourceLineId UNIQUEIDENTIFIER,          -- forensic + idempotency anahtarı (ZORUNLU)
    @SourceDocNo  NVARCHAR(50)  = NULL,
    @UnitCost     DECIMAL(18,6) = 0,
    @UserId       UNIQUEIDENTIFIER,        -- StockMovement.CreatedBy uniqueidentifier'dır
    @MovementDate DATETIME2     = NULL,
    @BranchId     UNIQUEIDENTIFIER = NULL,
    @MovementType NVARCHAR(20)  = 'ISSUE'  -- çıkış tipi: ISSUE (sevkiyat/sarf) | TRANSFER (transfer-out)
AS
BEGIN
    SET NOCOUNT ON;

    -- Guard: zorunlu anahtarlar (idempotency + forensic iz olmadan çıkış yazılmaz)
    IF @SourceLineId IS NULL THROW 53000, N'sp_ConsumeInventory: SourceLineId zorunlu.', 1;
    IF @QtyBase IS NULL OR @QtyBase <= 0 THROW 53000, N'sp_ConsumeInventory: QtyBase pozitif olmalı.', 1;
    -- StockMovement.BinId NOT NULL → bin zorunlu (çağıran ISNULL(sl.BinId, picking-bin) ile doldurur).
    -- Bu, key-range kilidinin düz-eşitlikle deterministik alınmasını da sağlar (OR-predikatı yasak).
    IF @BinId IS NULL THROW 53000, N'sp_ConsumeInventory: BinId zorunlu (hedef hücre belirsiz).', 1;

    -- İş kuralı (Plan 47 Faz 2): karantina/bloke lottan çıkış yapılamaz (serbest bırakılana dek).
    -- FEFO bunları zaten atlar; bu son savunma hattı (doğrudan bin/lot consume çağrısı da kapsanır).
    IF @LotNo IS NOT NULL AND EXISTS (
        SELECT 1 FROM ItemLot il
        WHERE il.CompanyId = @CompanyId AND il.ItemId = @ItemId AND il.LotNo = @LotNo
          AND il.Status IN ('QUARANTINE', 'BLOCKED'))
        THROW 53004, N'Lot karantinada/bloke — çıkış yapılamaz. Önce serbest bırakın.', 1;

    -- İdempotency hızlı yol (dostça mesaj): aynı kaynak satır+tip AKTİF olarak zaten işlendiyse reddet.
    -- Asıl garanti UX_StockMovement_Idempotency unique index'tir (race'te ikinci aktif insert 2627 alır).
    -- KASITLI: IsCancelled=0 filtresi → iptal sonrası AYNI satır yeniden tüketilebilir (cancel→re-post
    -- meşru akışı). İptal-edilmiş satır bakiyeden dışlandığı için çift-AKTİF imkânsız, drift YOK.
    -- Idempotency anahtarı (satır,tip,bin,lot) — multi-bin split'te aynı satır farklı bin/lot'a
    -- yazılabilir; yalnız tam (satır,tip,bin,lot) tekrarı reddedilir (UX index ile birebir).
    IF EXISTS (
        SELECT 1 FROM StockMovement
        WHERE SourceDocType = @SourceDocType AND SourceDocId = @SourceDocId
          AND SourceLineId = @SourceLineId AND MovementType = @MovementType
          AND BinId = @BinId AND ((@LotNo IS NULL AND LotNo IS NULL) OR LotNo = @LotNo)
          AND IsCancelled = 0)
        THROW 53002, N'Bu belge satırı bu bin/lot için zaten stoktan düşülmüş (idempotency).', 1;

    -- İş kuralı: ATOMİK yeterlilik kilidi — sp_getapplock (uygulama kilidi) ile aynı item/bin/lot'u
    -- tüketen eşzamanlı işlemler TEMİZ FIFO kuyruğa girer (key-range HOLDLOCK'un deadlock'u YOK).
    -- Plan 44 Faz 5 kanıtı: HOLDLOCK 50 worker'da deadlock üretti; applock'ta 0 deadlock + 0 oversell.
    -- Transaction-owner → COMMIT/ROLLBACK'te otomatik bırakılır (caller transaction içinde çağırır).
    DECLARE @LockKey NVARCHAR(255) = CONCAT('consume:', @CompanyId, ':', @ItemId, ':',
                                            @WarehouseId, ':', @BinId, ':', ISNULL(@LotNo, N''));
    DECLARE @LockRc INT;
    EXEC @LockRc = sp_getapplock @Resource = @LockKey, @LockMode = 'Exclusive',
                                 @LockOwner = 'Transaction', @LockTimeout = 15000;
    IF @LockRc < 0
        THROW 53003, N'Stok kilidi alınamadı (yoğunluk/zaman aşımı). Lütfen tekrar deneyin.', 1;

    -- Artık bu item/bin/lot için tek worker buradayız → bakiye düz okuma yeterli (applock garantisi).
    -- Bakiye = iptal-edilmemiş hareketlerin işaretli toplamı (RECEIPT +, ISSUE -); taban-birim (UomId grain'siz).
    DECLARE @Balance DECIMAL(18,6);
    IF @LotNo IS NULL
        SELECT @Balance = ISNULL(SUM(QtyBase), 0)
        FROM StockMovement
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId AND WarehouseId = @WarehouseId
          AND BinId = @BinId AND LotNo IS NULL AND IsCancelled = 0;
    ELSE
        SELECT @Balance = ISNULL(SUM(QtyBase), 0)
        FROM StockMovement
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId AND WarehouseId = @WarehouseId
          AND BinId = @BinId AND LotNo = @LotNo AND IsCancelled = 0;

    -- İş kuralı: negatif stok invariantı — yetersizse hareket YAZILMAZ, Türkçe THROW.
    IF @Balance < @QtyBase
    BEGIN
        DECLARE @msg NVARCHAR(400) = CONCAT(
            N'Yetersiz stok. Mevcut: ', CONVERT(NVARCHAR(40), @Balance),
            N', İstenen: ', CONVERT(NVARCHAR(40), @QtyBase),
            N' (Item ', CONVERT(NVARCHAR(40), @ItemId), N').');
        THROW 53001, @msg, 1;
    END

    -- Yeterli → ISSUE hareketi (negatif QtyBase). Idempotency index çift-insert'i de kapatır.
    INSERT INTO StockMovement
        (CompanyId, WarehouseId, BinId, ItemId, MovementType,
         QtyBase, UomId, QtyOriginal, MovementDate,
         SourceDocType, SourceDocId, SourceLineId, SourceDocNo, LotNo,
         UnitCost, CreatedBy, BranchId)
    VALUES
        (@CompanyId, @WarehouseId, @BinId, @ItemId, @MovementType,
         -@QtyBase, @UomId, @QtyBase, ISNULL(@MovementDate, GETUTCDATE()),
         @SourceDocType, @SourceDocId, @SourceLineId, @SourceDocNo, @LotNo,
         @UnitCost, @UserId, @BranchId);
END
GO

-- =============================================================================
-- sp_ConsumeInventoryFEFO — multi-bin tahsis (Plan 44 Faz 3). Bin belirtilmeyen çıkışta
-- depo stoğunu FEFO (ItemLot.ExpiryDate, en yakın vade önce) → FIFO (en eski hareket)
-- sırasıyla bin/lot'lara böler; her parça için sp_ConsumeInventory (atomik) çağırır.
-- item+depo applock ile tüm tahsis serialize olur (split bütünlüğü). Caller transaction içinde.
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ConsumeInventoryFEFO
    @CompanyId     UNIQUEIDENTIFIER,
    @WarehouseId   UNIQUEIDENTIFIER,
    @ItemId        UNIQUEIDENTIFIER,
    @UomId         UNIQUEIDENTIFIER,
    @QtyBase       DECIMAL(18,6),
    @SourceDocType NVARCHAR(30),
    @SourceDocId   UNIQUEIDENTIFIER,
    @SourceLineId  UNIQUEIDENTIFIER,
    @SourceDocNo   NVARCHAR(50)  = NULL,
    @UnitCost      DECIMAL(18,6) = 0,
    @UserId        UNIQUEIDENTIFIER,
    @MovementDate  DATETIME2     = NULL,
    @BranchId      UNIQUEIDENTIFIER = NULL,
    @MovementType  NVARCHAR(20)  = 'ISSUE'
AS
BEGIN
    SET NOCOUNT ON;
    IF @QtyBase IS NULL OR @QtyBase <= 0 THROW 53000, N'sp_ConsumeInventoryFEFO: QtyBase pozitif olmalı.', 1;

    -- item+depo exclusive applock → tüm bin tahsisi tek allocator (split atomikliği, deadlock'suz)
    DECLARE @LockKey NVARCHAR(255) = CONCAT('consume-wh:', @CompanyId, ':', @ItemId, ':', @WarehouseId);
    DECLARE @LockRc INT;
    EXEC @LockRc = sp_getapplock @Resource = @LockKey, @LockMode = 'Exclusive',
                                 @LockOwner = 'Transaction', @LockTimeout = 15000;
    IF @LockRc < 0 THROW 53003, N'Stok kilidi alınamadı (yoğunluk/zaman aşımı). Tekrar deneyin.', 1;

    -- Depo toplam yeterlilik (erken + dostça; bin-bazı consume içinde ayrıca atomik).
    -- Plan 47 Faz 2: karantina/bloke lot bakiyesi SERBEST stoğa sayılmaz (FEFO bunları tahsis etmez).
    DECLARE @Total DECIMAL(18,6) = (
        SELECT ISNULL(SUM(QtyBase), 0) FROM StockMovement sm
        WHERE sm.CompanyId = @CompanyId AND sm.ItemId = @ItemId AND sm.WarehouseId = @WarehouseId AND sm.IsCancelled = 0
          AND NOT EXISTS (SELECT 1 FROM ItemLot il
                          WHERE il.CompanyId = @CompanyId AND il.ItemId = @ItemId AND il.LotNo = sm.LotNo
                            AND il.Status IN ('QUARANTINE', 'BLOCKED')));
    IF @Total < @QtyBase
    BEGIN
        DECLARE @m NVARCHAR(400) = CONCAT(N'Yetersiz serbest stok (karantina/bloke lot hariç). Mevcut: ',
            CONVERT(NVARCHAR(40), @Total), N', İstenen: ', CONVERT(NVARCHAR(40), @QtyBase));
        THROW 53001, @m, 1;
    END

    -- FEFO/FIFO sıralı bin/lot bakiyeleri (vadesi yok = en sona)
    DECLARE @remaining DECIMAL(18,6) = @QtyBase, @take DECIMAL(18,6);
    DECLARE @bBin UNIQUEIDENTIFIER, @bLot NVARCHAR(100), @bBal DECIMAL(18,6);
    DECLARE c_fefo CURSOR LOCAL FAST_FORWARD FOR
        WITH BinBal AS (
            SELECT sm.BinId, sm.LotNo, SUM(sm.QtyBase) AS Bal, MIN(sm.MovementDate) AS FirstDate,
                   (SELECT MIN(il.ExpiryDate) FROM ItemLot il
                    WHERE il.CompanyId = @CompanyId AND il.ItemId = @ItemId AND il.LotNo = sm.LotNo) AS Exp
            FROM StockMovement sm
            WHERE sm.CompanyId = @CompanyId AND sm.ItemId = @ItemId
              AND sm.WarehouseId = @WarehouseId AND sm.IsCancelled = 0
              -- Plan 47 Faz 2: karantina/bloke lotu FEFO tahsisinden dışla
              AND NOT EXISTS (SELECT 1 FROM ItemLot il
                              WHERE il.CompanyId = @CompanyId AND il.ItemId = @ItemId AND il.LotNo = sm.LotNo
                                AND il.Status IN ('QUARANTINE', 'BLOCKED'))
            GROUP BY sm.BinId, sm.LotNo
        )
        SELECT BinId, LotNo, Bal FROM BinBal WHERE Bal > 0
        ORDER BY CASE WHEN Exp IS NULL THEN 1 ELSE 0 END, Exp ASC, FirstDate ASC;
    OPEN c_fefo;
    FETCH NEXT FROM c_fefo INTO @bBin, @bLot, @bBal;
    WHILE @@FETCH_STATUS = 0 AND @remaining > 0
    BEGIN
        SET @take = CASE WHEN @bBal >= @remaining THEN @remaining ELSE @bBal END;
        EXEC dbo.sp_ConsumeInventory
            @CompanyId = @CompanyId, @WarehouseId = @WarehouseId, @BinId = @bBin,
            @ItemId = @ItemId, @UomId = @UomId, @QtyBase = @take, @LotNo = @bLot,
            @SourceDocType = @SourceDocType, @SourceDocId = @SourceDocId, @SourceLineId = @SourceLineId,
            @SourceDocNo = @SourceDocNo, @UnitCost = @UnitCost, @UserId = @UserId,
            @MovementDate = @MovementDate, @BranchId = @BranchId, @MovementType = @MovementType;
        SET @remaining = @remaining - @take;
        FETCH NEXT FROM c_fefo INTO @bBin, @bLot, @bBal;
    END
    CLOSE c_fefo; DEALLOCATE c_fefo;

    -- Toplam yeterli olduğu için buraya düşmemeli; düşerse veri tutarsızlığı (savunmacı)
    IF @remaining > 0
        THROW 53001, N'FEFO tahsis tamamlanamadı (bin bakiyeleri toplamla tutarsız).', 1;
END
GO

-- =============================================================================
-- tr_StockMovement_Immutable — Ledger immutability (Plan 44 Faz 4)
-- StockMovement append-only: fiziksel DELETE yasak; UPDATE yalnız iptal bayrağı
-- (IsCancelled/CancelledAt/CancelledBy) için. Qty/Item/Bin/Date/Type vb. immutable.
-- Yetkili reversal SP'leri flag-only UPDATE yapar → geçer. Yetkisiz mutasyon → THROW.
-- (Dönem guard'ı tr_GuardPeriod_StockMovement AFTER INSERT'tedir; bu onun UPDATE/DELETE tamamlayıcısı.)
-- =============================================================================
CREATE OR ALTER TRIGGER dbo.tr_StockMovement_Immutable
ON dbo.StockMovement
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- 0-satır işlem (örn. MovementDate backfill IS NULL = 0 satır) → no-op, izin ver
    IF NOT EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted) RETURN;

    -- DELETE: append-only defter → fiziksel silme yok (iptal için IsCancelled kullanılır)
    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
        THROW 53010, N'StockMovement append-only: fiziksel silme yasak (iptal için IsCancelled=1).', 1;

    -- UPDATE: yalnız iptal bayrağı değişebilir. Immutable kolonlardan biri SET edildiyse RED.
    IF EXISTS (SELECT 1 FROM inserted) AND (
           UPDATE(CompanyId)  OR UPDATE(WarehouseId) OR UPDATE(BinId)       OR UPDATE(ItemId)
        OR UPDATE(MovementType) OR UPDATE(QtyBase)   OR UPDATE(UomId)       OR UPDATE(QtyOriginal)
        OR UPDATE(MovementDate) OR UPDATE(LotNo)     OR UPDATE(SerialNo)    OR UPDATE(UnitCost)
        OR UPDATE(SourceDocType) OR UPDATE(SourceDocId) OR UPDATE(SourceLineId))
        THROW 53011, N'StockMovement immutable: yalnız iptal (IsCancelled) güncellenebilir, hareket alanları değişmez.', 1;
END
GO
