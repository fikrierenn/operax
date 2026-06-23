-- M10 — Konfigüre Edilebilir Reçete (Parametrik BOM) Schema
-- Bu yapı, binlerce varyantı olan ürünlerin tek bir şablon üzerinden yönetilmesini sağlar.

-- 1. Ürün Modeli (Şablon)
CREATE TABLE ProductModel (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    Code NVARCHAR(50) NOT NULL, -- Örn: DUS-KAB-V1
    Name NVARCHAR(200) NOT NULL, -- Örn: Standart Duşakabin Modeli
    BaseItemId UNIQUEIDENTIFIER, -- Opsiyonel: Ana ürün kartına referans
    
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    IsActive BIT DEFAULT 1,
    
    CONSTRAINT FK_Model_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
);

-- 2. Model Parametreleri (Değişkenler)
CREATE TABLE ProductModelParameter (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductModelId UNIQUEIDENTIFIER NOT NULL,
    Code NVARCHAR(50) NOT NULL, -- Örn: WIDTH, HEIGHT, GLASS_TYPE
    Name NVARCHAR(100) NOT NULL,
    DataType NVARCHAR(20) DEFAULT 'NUMBER', -- NUMBER, STRING, OPTION
    DefaultValue NVARCHAR(100),
    Unit NVARCHAR(20), -- mm, cm, m2
    
    CONSTRAINT FK_Param_Model FOREIGN KEY (ProductModelId) REFERENCES ProductModel(Id)
);

-- 3. Model Reçetesi (Formüllü Bileşenler)
CREATE TABLE ProductModelBOM (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductModelId UNIQUEIDENTIFIER NOT NULL,
    ComponentItemId UNIQUEIDENTIFIER NOT NULL,
    
    -- Miktar Formülü: Örn: "[WIDTH] * [HEIGHT] / 1000000" (m2 hesabı için)
    -- Veya statik miktar: "1"
    QtyFormula NVARCHAR(500) NOT NULL, 
    
    -- Fire Oranı (Yüzdesel): Örn: 5.00 (%5 fire)
    WastePercentage DECIMAL(5,2) DEFAULT 0,
    
    -- Koşul Formülü: Örn: "[GLASS_TYPE] == 'FROSTED'" (Sadece buzlu cam seçilirse bu kalemi kullan)
    ConditionFormula NVARCHAR(500),
    
    CONSTRAINT FK_ModelBOM_Model FOREIGN KEY (ProductModelId) REFERENCES ProductModel(Id),
    CONSTRAINT FK_ModelBOM_Item FOREIGN KEY (ComponentItemId) REFERENCES Item(Id)
);

-- 4. Üretim Emri Konfigürasyonu (Seçilen Değerler)
CREATE TABLE ProductionOrderConfig (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductionOrderId UNIQUEIDENTIFIER NOT NULL,
    ParameterId UNIQUEIDENTIFIER NOT NULL,
    Value NVARCHAR(100) NOT NULL, -- Seçilen veya girilen değer (Örn: 900)
    
    CONSTRAINT FK_Config_Order FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
    CONSTRAINT FK_Config_Param FOREIGN KEY (ParameterId) REFERENCES ProductModelParameter(Id)
);

-- 5. Üretim Emri Satırları (Hesaplanmış İhtiyaçlar)
CREATE TABLE ProductionOrderLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductionOrderId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    QtyRequired DECIMAL(18,6) NOT NULL, -- Formül sonucu hesaplanan miktar
    QtyIssued DECIMAL(18,6) DEFAULT 0, -- Depodan fiilen çıkan (toplanan) miktar
    
    CONSTRAINT FK_POLine_Order FOREIGN KEY (ProductionOrderId) REFERENCES ProductionOrder(Id),
    CONSTRAINT FK_POLine_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);
GO
