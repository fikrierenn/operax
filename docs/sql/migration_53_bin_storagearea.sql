-- ============================================================
-- Plan 50 Faz 1 (C1) — Bin.IsStorageArea kolonu (CRITICAL fresh-install bug)
-- Warehouses/Details.cshtml.cs raf-ekle INSERT'i IsStorageArea kolonunu yazıyor
-- ama kolon hiçbir şemada yoktu → fresh-install'da "Invalid column name" ile patlıyordu.
-- Bu migration kolonu mevcut DB'lere idempotent ekler; schema_all.sql CREATE TABLE
-- de fresh kurulum için kolonu içerir (çift kapsama).
-- Anlam: raf depolama alanı mı (stok tutar mı). IsPicking/IsReceiving ile birlikte
-- ayrık-olmayan bayrak. Varsayılan 1 — genel raf stok tutar (handler da 1 yazıyor).
-- Idempotent.
-- ============================================================
SET XACT_ABORT ON;
SET NOCOUNT ON;

-- Raf depolama alanı bayrağı; mevcut raflar varsayılan olarak depolama kabul edilir
IF COL_LENGTH('Bin', 'IsStorageArea') IS NULL
    ALTER TABLE Bin ADD IsStorageArea BIT NOT NULL DEFAULT 1;
GO
