-- Plan 27 Faz A — PriceVariance fatura↔sipariş farkı + AI gerekçe denetimi kolonları.
-- Idempotent: yalnızca eksik kolonlar eklenir.

-- Satınalmacının override gerekçesi (zorunlu metin, AI ile denetlenir)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'OverrideReason' AND Object_ID = OBJECT_ID('PriceVariance'))
BEGIN
    ALTER TABLE PriceVariance ADD OverrideReason NVARCHAR(MAX) NULL;
    PRINT 'PriceVariance.OverrideReason eklendi.';
END
GO

-- AI değerlendirmesi: PLAUSIBLE / IMPLAUSIBLE / UNCHECKED (AI kapalı/erişilemez)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'AiVerdict' AND Object_ID = OBJECT_ID('PriceVariance'))
BEGIN
    ALTER TABLE PriceVariance ADD AiVerdict NVARCHAR(20) NULL;
    PRINT 'PriceVariance.AiVerdict eklendi.';
END
GO

-- AI'ın kısa gerekçe değerlendirmesi (denetim izi)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'AiComment' AND Object_ID = OBJECT_ID('PriceVariance'))
BEGIN
    ALTER TABLE PriceVariance ADD AiComment NVARCHAR(500) NULL;
    PRINT 'PriceVariance.AiComment eklendi.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'AiCheckedAt' AND Object_ID = OBJECT_ID('PriceVariance'))
BEGIN
    ALTER TABLE PriceVariance ADD AiCheckedAt DATETIME2 NULL;
    PRINT 'PriceVariance.AiCheckedAt eklendi.';
END
GO
