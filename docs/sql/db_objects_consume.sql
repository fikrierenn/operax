-- ============================================================
-- Plan 44 — Stok Çıkış Atomik Primitive (db_objects_consume.sql)
-- TÜM stok çıkışları (shipping/material-issue/picking/production/transfer) buradan geçer.
-- Negatif stok invariantı + concurrency + idempotency tek atomik noktada korunur.
-- ============================================================

-- =============================================================================
-- sp_ConsumeInventory — bir item/warehouse/bin/lot için atomik stok düşümü.
-- NE YAPAR: bakiyeyi UPDLOCK+HOLDLOCK ile kilitler (eşzamanlı tüketiciler serialize olur),
--   yeterlilik kontrol eder, yetersizse THROW, yeterli ise ISSUE hareketi yazar.
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
    @LotNo        NVARCHAR(50)  = NULL,
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

    -- İdempotency hızlı yol (dostça mesaj): aynı kaynak satır+tip zaten işlendiyse reddet.
    -- Asıl garanti UX_StockMovement_Idempotency unique index'tir (race'i de kapatır).
    IF EXISTS (
        SELECT 1 FROM StockMovement
        WHERE SourceDocType = @SourceDocType AND SourceDocId = @SourceDocId
          AND SourceLineId = @SourceLineId AND MovementType = @MovementType AND IsCancelled = 0)
        THROW 53002, N'Bu belge satırı zaten stoktan düşülmüş (idempotency).', 1;

    -- İş kuralı: ATOMİK yeterlilik kilidi. UPDLOCK+HOLDLOCK = key-range; aynı item/bin/lot'u tüketen
    -- eşzamanlı işlemler bu noktada serialize olur → oversell fiziksel olarak imkânsızlaşır.
    -- KRİTİK: kilit garantisi düz-eşitlik predikatına bağlı (SARGable, IX_StockMovement_Consume prefix
    -- seek → deterministik key-range). OR-predikatı (@x IS NULL OR ...) plan-sniffing ile kilit
    -- granülaritesini bozar → bu yüzden LotNo NULL/dolu için ayrı düz-eşitlik dalı kullanılır.
    -- Bakiye = iptal-edilmemiş hareketlerin işaretli toplamı (RECEIPT +, ISSUE -); taban-birim (UomId grain'siz).
    DECLARE @Balance DECIMAL(18,6);
    IF @LotNo IS NULL
        SELECT @Balance = ISNULL(SUM(QtyBase), 0)
        FROM StockMovement WITH (UPDLOCK, HOLDLOCK)
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId AND WarehouseId = @WarehouseId
          AND BinId = @BinId AND LotNo IS NULL AND IsCancelled = 0;
    ELSE
        SELECT @Balance = ISNULL(SUM(QtyBase), 0)
        FROM StockMovement WITH (UPDLOCK, HOLDLOCK)
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
