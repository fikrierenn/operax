-- M06 — Shipping Schema

CREATE TABLE ShippingHeader (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    WarehouseId UNIQUEIDENTIFIER NOT NULL,
    DocNo NVARCHAR(100) UNIQUE NOT NULL, -- SHP-2026-00001
    DocDate DATETIME2 DEFAULT GETUTCDATE(),
    Status NVARCHAR(50) DEFAULT 'DRAFT', -- DRAFT, POSTED, CANCELLED
    CarrierName NVARCHAR(200),
    VehiclePlate NVARCHAR(50),
    Notes NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    UpdatedAt DATETIME2,
    UpdatedBy UNIQUEIDENTIFIER,
    IsDeleted BIT DEFAULT 0,
    CONSTRAINT FK_ShippingHeader_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_ShippingHeader_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id)
);

CREATE TABLE ShippingLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HeaderId UNIQUEIDENTIFIER NOT NULL,
    SalesOrderLineId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    UomId UNIQUEIDENTIFIER NOT NULL,
    QtyOriginal DECIMAL(18,6) NOT NULL,
    QtyBase DECIMAL(18,6) NOT NULL, -- QtyOriginal * ConversionRate
    LotNo NVARCHAR(100),
    BinId UNIQUEIDENTIFIER, -- Source Bin
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ShippingLine_Header FOREIGN KEY (HeaderId) REFERENCES ShippingHeader(Id),
    CONSTRAINT FK_ShippingLine_SOLine FOREIGN KEY (SalesOrderLineId) REFERENCES SalesOrderLine(Id),
    CONSTRAINT FK_ShippingLine_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

-- Indexing
CREATE INDEX IX_Shipping_Header ON ShippingLine(HeaderId);
CREATE INDEX IX_Shipping_DocNo ON ShippingHeader(CompanyId, DocNo);
CREATE INDEX IX_Shipping_SOLine ON ShippingLine(SalesOrderLineId);
