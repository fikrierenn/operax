-- M01/M09/M10 — Reçete ve Üretim Planlama Şeması
-- Hammadde toplama akışı için gerekli tablolar.

-- 1. Ürün Reçetesi (BOM - Bill of Materials)
-- Hangi ürünü üretmek için hangi hammaddelerden ne kadar lazım?
CREATE TABLE ItemBOM (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ParentItemId UNIQUEIDENTIFIER NOT NULL, -- Üretilecek Mamül
    ChildItemId UNIQUEIDENTIFIER NOT NULL,  -- Kullanılacak Hammadde/Sarf
    QtyRequired DECIMAL(18,6) NOT NULL,    -- Gereken miktar (Base Unit)
    IsActive BIT DEFAULT 1,
    CONSTRAINT FK_BOM_Parent FOREIGN KEY (ParentItemId) REFERENCES Item(Id),
    CONSTRAINT FK_BOM_Child FOREIGN KEY (ChildItemId) REFERENCES Item(Id)
);

-- 2. Üretim Planı / İş Emri (M09/M10)
-- ProductionOrder tablosu daha önce eklenmişti, onu hammadde satırlarıyla genişletiyoruz.
CREATE TABLE ProductionOrderLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductionOrderId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL, -- Hammadde
    QtyRequired DECIMAL(18,6) NOT NULL, -- Planlanan hammadde miktarı
    QtyIssued DECIMAL(18,6) DEFAULT 0,  -- Depodan üretim alanına fiilen çıkan (toplanan)
    PickTaskId UNIQUEIDENTIFIER,        -- Bu hammaddeyi toplamak için açılan emir
    CONSTRAINT FK_POLine_Header FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
    CONSTRAINT FK_POLine_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

-- ProductionOrder tablosuna Planlama alanları ekleyelim (Eğer henüz yoksa)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductionOrder') AND name = 'PlanDate')
BEGIN
    ALTER TABLE ProductionOrder ADD 
        PlanDate DATETIME2,
        PickStatus NVARCHAR(20) DEFAULT 'PENDING'; -- PENDING, PICKING, PICKED
END
GO
