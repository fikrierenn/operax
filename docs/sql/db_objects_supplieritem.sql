-- =============================================================================
-- Plan 32 — Tedarikçi-Ürün Kataloğu SP'leri (bulk upsert + tercih ata)
-- Bağımlılık: SupplierItem + dbo.SupplierItemTVP (schema_M01_SupplierItem.sql).
--   Migrate'te schema'dan SONRA çalışır. Grid bir ürünün TÜM tedarikçilerini yönetir.
-- =============================================================================

-- =============================================================================
-- sp_SupplierItemBulkUpsert — Bir ürünün tedarikçi satırlarını toplu upsert (Item kartı grid)
-- NE YAPAR: @ItemId ürününün tedarikçilerini SupplierItemTVP demetinden tek transaction'da
--   yazar. (CompanyId,ItemId,PartnerId) anahtarıyla MERGE (idempotent). @SyncDelete=1 ise
--   demette olmayan tedarikçiler soft-delete (grid otoritedir).
-- TERCİH KURALI: demette en çok BİR IsPreferred=1; merge öncesi eski tercih sıfırlanır
--   (UX_SupplierItem_Preferred filtered unique çakışmasını önler).
-- PARAMETRELER: @CompanyId @ItemId @UserId @Lines(TVP) @SyncDelete
-- THROW: 51410 ürün yok; 51411 geçersiz tedarikçi; 51412 birden çok tercih
-- DÖNEN: result1 = (Yazilan)
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_SupplierItemBulkUpsert
    @CompanyId  UNIQUEIDENTIFIER,
    @ItemId     UNIQUEIDENTIFIER,
    @UserId     UNIQUEIDENTIFIER,
    @Lines      dbo.SupplierItemTVP READONLY,
    @SyncDelete BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- İş kuralı: ürün şirkete ait ve silinmemiş olmalı
    IF NOT EXISTS (SELECT 1 FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId AND IsDeleted = 0)
        THROW 51410, N'Ürün bulunamadı veya bu şirkete ait değil.', 1;

    -- İş kuralı: en çok bir tercih edilen tedarikçi
    IF (SELECT COUNT(*) FROM @Lines WHERE IsPreferred = 1 AND PartnerId IS NOT NULL) > 1
        THROW 51412, N'Bir ürüne yalnız bir tercih edilen tedarikçi seçilebilir.', 1;

    -- İş kuralı: tüm tedarikçiler şirkete ait olmalı (PartnerId boş satırlar elenir)
    IF EXISTS (
        SELECT 1 FROM @Lines l
        WHERE l.PartnerId IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM Partner p
                          WHERE p.Id = l.PartnerId AND p.CompanyId = @CompanyId AND p.IsDeleted = 0))
        THROW 51411, N'Geçersiz tedarikçi (cari bulunamadı).', 1;

    -- Tekilleştir: aynı tedarikçi birden çok satırda → son satır (RowNo DESC) kazanır
    SELECT PartnerId, SupplierItemCode, SupplierItemName, LeadTimeDays,
           ISNULL(MinOrderQty, 0) AS MinOrderQty, LastPrice,
           COALESCE(NULLIF(LTRIM(RTRIM(Currency)), ''), 'TRY') AS Currency,
           ISNULL(IsPreferred, 0) AS IsPreferred
    INTO #final
    FROM (
        SELECT *, ROW_NUMBER() OVER (PARTITION BY PartnerId ORDER BY RowNo DESC) AS rn
        FROM @Lines
        WHERE PartnerId IS NOT NULL
    ) x
    WHERE x.rn = 1;

    BEGIN TRAN;
        -- 1) Eski tercihi sıfırla (filtered preferred UQ çakışmasını önle)
        UPDATE SupplierItem
        SET IsPreferred = 0, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId AND IsDeleted = 0 AND IsPreferred = 1;

        -- 2) Upsert — (CompanyId, ItemId, PartnerId) idempotent
        MERGE SupplierItem AS tgt
        USING (SELECT * FROM #final) AS src
            ON tgt.CompanyId = @CompanyId AND tgt.ItemId = @ItemId
               AND tgt.PartnerId = src.PartnerId AND tgt.IsDeleted = 0
        WHEN MATCHED THEN
            UPDATE SET SupplierItemCode = src.SupplierItemCode,
                       SupplierItemName = src.SupplierItemName,
                       LeadTimeDays     = src.LeadTimeDays,
                       MinOrderQty      = src.MinOrderQty,
                       LastPrice        = src.LastPrice,
                       Currency         = src.Currency,
                       IsPreferred      = src.IsPreferred,
                       UpdatedAt        = GETUTCDATE(), UpdatedBy = @UserId
        WHEN NOT MATCHED THEN
            INSERT (Id, CompanyId, PartnerId, ItemId, SupplierItemCode, SupplierItemName,
                    LeadTimeDays, MinOrderQty, LastPrice, Currency, IsPreferred, CreatedBy)
            VALUES (NEWID(), @CompanyId, src.PartnerId, @ItemId, src.SupplierItemCode, src.SupplierItemName,
                    src.LeadTimeDays, src.MinOrderQty, src.LastPrice, src.Currency, src.IsPreferred, @UserId);

        -- 3) Senkron silme (grid Kaydet): demette OLMAYAN tedarikçileri soft-delete
        IF @SyncDelete = 1
            UPDATE SupplierItem
            SET IsDeleted = 1, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
            WHERE CompanyId = @CompanyId AND ItemId = @ItemId AND IsDeleted = 0
              AND PartnerId NOT IN (SELECT PartnerId FROM #final);
    COMMIT;

    SELECT (SELECT COUNT(*) FROM #final) AS Yazilan;
END
GO

-- =============================================================================
-- sp_SetPreferredSupplier — Bir ürün için tercih edilen tedarikçiyi tekil ayarla
-- NE YAPAR: @ItemId ürününde @PartnerId tedarikçisini tercih yapar, diğerlerini sıfırlar
--   (exclusive). UX_SupplierItem_Preferred filtered unique ile uyumlu.
-- PARAMETRELER: @CompanyId @ItemId @PartnerId @UserId
-- THROW: 51413 tedarikçi-ürün eşlemesi yok
-- =============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_SetPreferredSupplier
    @CompanyId UNIQUEIDENTIFIER,
    @ItemId    UNIQUEIDENTIFIER,
    @PartnerId UNIQUEIDENTIFIER,
    @UserId    UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- İş kuralı: eşleme mevcut olmalı
    IF NOT EXISTS (SELECT 1 FROM SupplierItem
                   WHERE CompanyId = @CompanyId AND ItemId = @ItemId
                     AND PartnerId = @PartnerId AND IsDeleted = 0)
        THROW 51413, N'Tedarikçi-ürün eşlemesi bulunamadı.', 1;

    BEGIN TRAN;
        -- Önce hepsini sıfırla (filtered UQ çakışması olmasın), sonra hedefi işaretle
        UPDATE SupplierItem
        SET IsPreferred = 0, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId AND IsDeleted = 0 AND IsPreferred = 1;

        UPDATE SupplierItem
        SET IsPreferred = 1, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
        WHERE CompanyId = @CompanyId AND ItemId = @ItemId
          AND PartnerId = @PartnerId AND IsDeleted = 0;
    COMMIT;
END
GO
