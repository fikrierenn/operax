-- ============================================================
-- Veritabanı uyumluluk seviyesini SQL Server 2022 (160) seviyesine yükseltir.
-- Operax hedef platformu SQL Server 2022'dir; orada model compat 160 olduğundan fresh DB
-- zaten 160 gelir ve bu adım no-op'tur. Sigorta amaçlı: 2022 sunucusunda model manuel olarak
-- <160 bırakılmışsa, oluşan DB de <160 olur ve STRING_SPLIT'in ordinal argümanı (3. parametre,
-- db_objects_pricelist_bulk.sql zincir-iskonto ayrıştırması) "too many arguments" hatası verir.
-- Sürüm-korumalı: yalnız ProductMajorVersion >= 16 (SQL 2022+) ise ALTER dener; daha eski
-- sürümlerde 160 geçersiz olduğu için zarifçe atlanır (eski sürüm desteklenmez).
-- Idempotent + tolerant adım: hata migrate'i durdurmaz.
-- ============================================================

IF CAST(SERVERPROPERTY('ProductMajorVersion') AS INT) >= 16
   AND (SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID()) < 160
BEGIN
    DECLARE @sql NVARCHAR(200) = N'ALTER DATABASE ' + QUOTENAME(DB_NAME()) + N' SET COMPATIBILITY_LEVEL = 160';
    EXEC sp_executesql @sql;
    PRINT 'Compatibility level 160 (SQL Server 2022) seviyesine yükseltildi.';
END
ELSE
    PRINT 'Compatibility level ayarı atlandı (zaten >= 160 ya da sunucu < SQL 2022).';
