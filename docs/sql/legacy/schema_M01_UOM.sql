-- M01 — Master Data: UOM Conversion & Barcode Schema

-- ItemUOM: PACK/CASE -> EACH conversions
CREATE TABLE ItemUOM (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ItemId UNIQUEIDENTIFIER NOT NULL,
    UomId UNIQUEIDENTIFIER NOT NULL, -- The non-base UOM (e.g. PACK)
    ConversionRate DECIMAL(18,6) NOT NULL, -- How many BaseUOM (EACH) are in this UOM?
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ItemUOM_Item FOREIGN KEY (ItemId) REFERENCES Item(Id),
    CONSTRAINT FK_ItemUOM_Uom FOREIGN KEY (UomId) REFERENCES DictionaryValue(Id)
);

-- ItemBarcode: Links a barcode to an Item and a specific UOM
CREATE TABLE ItemBarcode (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ItemId UNIQUEIDENTIFIER NOT NULL,
    UomId UNIQUEIDENTIFIER NOT NULL,
    Barcode NVARCHAR(100) NOT NULL,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ItemBarcode_Item FOREIGN KEY (ItemId) REFERENCES Item(Id),
    CONSTRAINT FK_ItemBarcode_Uom FOREIGN KEY (UomId) REFERENCES DictionaryValue(Id)
);

CREATE UNIQUE INDEX IX_ItemBarcode_Barcode ON ItemBarcode(Barcode) WHERE IsActive = 1;

-- Seed UOM Definitions in Dictionary (If not exists)
-- This usually goes to seed_dictionary.sql but adding here for context
/*
INSERT INTO DictionaryValue (TypeId, Code, NameTr, NameEn)
SELECT id, 'PACK', 'Paket', 'Pack' FROM DictionaryType WHERE Code = 'UOM' 
WHERE NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE Code = 'PACK');

INSERT INTO DictionaryValue (TypeId, Code, NameTr, NameEn)
SELECT id, 'CASE', 'Koli', 'Case' FROM DictionaryType WHERE Code = 'UOM'
WHERE NOT EXISTS (SELECT 1 FROM DictionaryValue WHERE Code = 'CASE');
*/
