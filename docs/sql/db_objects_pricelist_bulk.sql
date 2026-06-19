-- =============================================================================
-- Plan 31 — Fiyat listesi toplu giriş SP'leri (bulk upsert + clone)
-- Bağımlılık: PriceList/PriceListLine/PriceListLineDiscount (Plan 30) + dbo.PriceLineTVP
--   (schema_M02_Costing.sql). Migrate'te schema_M02_Costing'ten SONRA çalışır.
-- =============================================================================

-- =============================================================================
-- sp_PriceListBulkUpsert — Toplu fiyat satırı upsert (grid + Excel import)
-- NE YAPAR: PriceLineTVP demetini tek transaction'da fiyat listesine yazar.
--   ItemCode→ItemId çözer, (PriceListId,ItemId) anahtarıyla MERGE (idempotent),
--   zincir iskontoyu ("10+5+3") STRING_SPLIT ordinal ile PriceListLineDiscount'a
--   yeniden yazar. @DryRun=1 ise YAZMAZ, satır-bazlı hata + özet döner (önizleme).
-- DÖNEN (DryRun=1): result1=hatalar(RowNo,ItemCode,Hata); result2=özet(Toplam,Hatali,Gecerli)
-- DÖNEN (DryRun=0): result1=özet(Yazilan)
-- SIDE EFFECTS (DryRun=0): PriceListLine (MERGE), PriceListLineDiscount (sil+yaz)
-- THROW: 51310 liste yok; 51311 geçersiz satır (gerçek koşuda — önce DryRun ile düzelt)
-- NOT: Aynı ItemCode birden çok satırda → son satır kazanır (RowNo DESC dedup).
--   İskonto zinciri komütatif (Plan 30); Seq ordinal ile giriş sırası korunur.
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PriceListBulkUpsert
    @CompanyId   UNIQUEIDENTIFIER,
    @PriceListId UNIQUEIDENTIFIER,
    @UserId      UNIQUEIDENTIFIER,
    @Lines       dbo.PriceLineTVP READONLY,
    @DryRun      BIT = 0,
    @SyncDelete  BIT = 0   -- 1 = gelen demet TAM durum: demette olmayan satırlar soft-delete (grid Kaydet);
                           -- 0 = yalnız upsert, eksikler korunur (Excel import — kısmi dosya silmesin)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- İş kuralı: liste şirkete ait ve silinmemiş olmalı
    IF NOT EXISTS (SELECT 1 FROM PriceList
                   WHERE Id = @PriceListId AND CompanyId = @CompanyId AND IsDeleted = 0)
        THROW 51310, N'Fiyat listesi bulunamadı veya bu şirkete ait değil.', 1;

    -- Ürün çözümü: @ItemId doluysa (grid yolu) onu kullan; yoksa @ItemCode'dan çöz (Excel yolu).
    -- Item.Code TEKİL DEĞİL (non-unique) → koddan çözümde skaler subquery + MatchCount.
    -- MatchSource: 'ID' geçerli ItemId, 'CODE1' tek kod eşleşme, 'CODE0' yok, 'CODEN' belirsiz.
    SELECT l.RowNo, l.ItemCode, l.UnitPrice, l.MinQty,
           UPPER(LTRIM(RTRIM(l.LineType))) AS LineType, l.DiscountChain,
           CASE
               WHEN l.ItemId IS NOT NULL
                    AND EXISTS (SELECT 1 FROM Item i WHERE i.Id = l.ItemId
                                AND i.CompanyId = @CompanyId AND i.IsDeleted = 0) THEN 'ID'
               WHEN l.ItemId IS NOT NULL THEN 'IDBAD'
               WHEN (SELECT COUNT(*) FROM Item i WHERE i.Code = l.ItemCode
                     AND i.CompanyId = @CompanyId AND i.IsDeleted = 0) = 1 THEN 'CODE1'
               WHEN (SELECT COUNT(*) FROM Item i WHERE i.Code = l.ItemCode
                     AND i.CompanyId = @CompanyId AND i.IsDeleted = 0) > 1 THEN 'CODEN'
               ELSE 'CODE0' END AS MatchSource,
           CASE
               WHEN l.ItemId IS NOT NULL
                    AND EXISTS (SELECT 1 FROM Item i WHERE i.Id = l.ItemId
                                AND i.CompanyId = @CompanyId AND i.IsDeleted = 0) THEN l.ItemId
               WHEN l.ItemId IS NULL
                    AND (SELECT COUNT(*) FROM Item i WHERE i.Code = l.ItemCode
                         AND i.CompanyId = @CompanyId AND i.IsDeleted = 0) = 1
                    THEN (SELECT MIN(i.Id) FROM Item i WHERE i.Code = l.ItemCode
                          AND i.CompanyId = @CompanyId AND i.IsDeleted = 0)
               ELSE NULL END AS ItemId
    INTO #res
    FROM @Lines l;

    -- Satır-bazlı validasyon: kod/kimlik sorunu / geçersiz tip / negatif fiyat
    SELECT RowNo, ItemCode,
           CASE WHEN MatchSource = 'CODE0'                 THEN N'Ürün kodu bulunamadı'
                WHEN MatchSource = 'CODEN'                  THEN N'Ürün kodu tekil değil (birden çok eşleşme)'
                WHEN MatchSource = 'IDBAD'                  THEN N'Ürün kimliği geçersiz'
                WHEN LineType NOT IN ('FIXED','DISCOUNT')  THEN N'Geçersiz satır tipi'
                WHEN UnitPrice < 0                         THEN N'Negatif fiyat'
                ELSE N'Bilinmeyen' END AS Hata
    INTO #err
    FROM #res
    WHERE ItemId IS NULL OR LineType NOT IN ('FIXED','DISCOUNT') OR UnitPrice < 0;

    -- ÖNİZLEME: yazma yok, hata listesi + özet dön
    IF @DryRun = 1
    BEGIN
        SELECT RowNo, ItemCode, Hata FROM #err ORDER BY RowNo;
        SELECT (SELECT COUNT(*) FROM #res) AS Toplam,
               (SELECT COUNT(*) FROM #err) AS Hatali,
               (SELECT COUNT(*) FROM #res) - (SELECT COUNT(*) FROM #err) AS Gecerli;
        RETURN;
    END

    -- İş kuralı: gerçek koşuda hata varsa tümü iptal (atomik — ya hep ya hiç)
    IF EXISTS (SELECT 1 FROM #err)
        THROW 51311, N'Geçersiz satırlar var; içe aktarma iptal edildi. Önce önizleme ile düzeltin.', 1;

    -- Tekilleştir: aynı ürün birden çok satırda → son satır (RowNo DESC) kazanır
    SELECT ItemId, UnitPrice, MinQty, LineType, DiscountChain
    INTO #final
    FROM (
        SELECT *, ROW_NUMBER() OVER (PARTITION BY ItemId ORDER BY RowNo DESC) AS rn
        FROM #res
    ) x
    WHERE x.rn = 1;

    BEGIN TRAN;
        -- 1) Satır upsert — (PriceListId, ItemId) idempotent
        MERGE PriceListLine AS tgt
        USING (SELECT ItemId, UnitPrice, MinQty, LineType FROM #final) AS src
            ON tgt.PriceListId = @PriceListId AND tgt.ItemId = src.ItemId AND tgt.IsDeleted = 0
        WHEN MATCHED THEN
            UPDATE SET UnitPrice = src.UnitPrice, MinQty = src.MinQty, LineType = src.LineType
        WHEN NOT MATCHED THEN
            INSERT (Id, PriceListId, ItemId, UnitPrice, MinQty, LineType)
            VALUES (NEWID(), @PriceListId, src.ItemId, src.UnitPrice, src.MinQty, src.LineType);

        -- 2) İskonto yeniden yaz: gelen TÜM satırların eski kademelerini sil
        --    (chain boşaltıldıysa iskonto kalkar — gelen satır otoritedir)
        DELETE d
        FROM PriceListLineDiscount d
        JOIN PriceListLine l ON l.Id = d.LineId
        WHERE l.PriceListId = @PriceListId
          AND l.ItemId IN (SELECT ItemId FROM #final);

        -- 3) Yeni kademeleri yaz. Geçersiz/sıfır kademeler elendikten SONRA Seq'i
        --    ROW_NUMBER ile 1..n ardışık yeniden numarala (ordinal boşluğu olmasın).
        INSERT INTO PriceListLineDiscount (Id, LineId, Seq, Pct)
        SELECT NEWID(), q.LineId,
               ROW_NUMBER() OVER (PARTITION BY q.LineId ORDER BY q.Ord),
               q.Pct
        FROM (
            SELECT l.Id AS LineId, s.ordinal AS Ord,
                   TRY_CAST(REPLACE(LTRIM(RTRIM(s.value)), ',', '.') AS DECIMAL(9,4)) AS Pct
            FROM #final f
            JOIN PriceListLine l ON l.PriceListId = @PriceListId AND l.ItemId = f.ItemId AND l.IsDeleted = 0
            CROSS APPLY STRING_SPLIT(f.DiscountChain, '+', 1) s
            WHERE f.DiscountChain IS NOT NULL AND LTRIM(RTRIM(s.value)) <> ''
        ) q
        WHERE q.Pct > 0 AND q.Pct <= 100;

        -- 4) Senkron silme (grid Kaydet): gelen demette OLMAYAN satırları soft-delete
        IF @SyncDelete = 1
            UPDATE l SET l.IsDeleted = 1
            FROM PriceListLine l
            WHERE l.PriceListId = @PriceListId AND l.IsDeleted = 0
              AND l.ItemId NOT IN (SELECT ItemId FROM #final);

        -- Audit izi: listenin güncelleme damgası
        UPDATE PriceList SET UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @PriceListId AND CompanyId = @CompanyId;
    COMMIT;

    SELECT (SELECT COUNT(*) FROM #final) AS Yazilan;
END
GO

-- =============================================================================
-- sp_PriceListClone — Bir fiyat listesinin satır + iskontolarını kopyala
-- NE YAPAR: @SourceId listesinin tüm aktif satırlarını + zincir iskonto kademelerini
--   @TargetId listesine kopyalar. Hedefte aynı ürün varsa (PriceListId,ItemId) ATLAR
--   (mevcut hedef satırı korunur — kazara üzerine yazma yok). Başlık KOPYALANMAZ.
-- PARAMETRELERİ: @CompanyId @SourceId @TargetId @UserId
-- THROW: 51312 kaynak/hedef bulunamadı; 51313 kaynak=hedef
-- SIDE EFFECTS: PriceListLine (INSERT), PriceListLineDiscount (INSERT)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_PriceListClone
    @CompanyId UNIQUEIDENTIFIER,
    @SourceId  UNIQUEIDENTIFIER,
    @TargetId  UNIQUEIDENTIFIER,
    @UserId    UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- İş kuralı: kaynak ve hedef farklı + ikisi de şirkete ait
    IF @SourceId = @TargetId
        THROW 51313, N'Kaynak ve hedef liste aynı olamaz.', 1;
    IF NOT EXISTS (SELECT 1 FROM PriceList WHERE Id = @SourceId AND CompanyId = @CompanyId AND IsDeleted = 0)
       OR NOT EXISTS (SELECT 1 FROM PriceList WHERE Id = @TargetId AND CompanyId = @CompanyId AND IsDeleted = 0)
        THROW 51312, N'Kaynak veya hedef fiyat listesi bulunamadı.', 1;

    BEGIN TRAN;
        -- Yeni satır kimliklerini eşleme tablosuna al (yeni LineId ↔ ItemId)
        DECLARE @map TABLE (NewLineId UNIQUEIDENTIFIER, ItemId UNIQUEIDENTIFIER);

        -- 1) Kaynak satırları kopyala (hedefte aynı ürün yoksa)
        INSERT INTO PriceListLine (Id, PriceListId, ItemId, UnitPrice, MinQty, LineType)
        OUTPUT inserted.Id, inserted.ItemId INTO @map (NewLineId, ItemId)
        SELECT NEWID(), @TargetId, s.ItemId, s.UnitPrice, s.MinQty, s.LineType
        FROM PriceListLine s
        WHERE s.PriceListId = @SourceId AND s.IsDeleted = 0
          AND NOT EXISTS (SELECT 1 FROM PriceListLine t
                          WHERE t.PriceListId = @TargetId AND t.ItemId = s.ItemId AND t.IsDeleted = 0);

        -- 2) Kaynak iskonto kademelerini yeni satırlara bağla (ItemId üzerinden eşle)
        INSERT INTO PriceListLineDiscount (Id, LineId, Seq, Pct)
        SELECT NEWID(), m.NewLineId, d.Seq, d.Pct
        FROM PriceListLine s
        JOIN @map m ON m.ItemId = s.ItemId
        JOIN PriceListLineDiscount d ON d.LineId = s.Id
        WHERE s.PriceListId = @SourceId AND s.IsDeleted = 0;

        UPDATE PriceList SET UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE Id = @TargetId AND CompanyId = @CompanyId;
    COMMIT;

    SELECT (SELECT COUNT(*) FROM @map) AS Kopyalanan;
END
GO
