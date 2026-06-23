-- M03 — Purchase Order Schema

CREATE TABLE PurchaseOrderHeader (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    WarehouseId UNIQUEIDENTIFIER NOT NULL,
    PartnerId UNIQUEIDENTIFIER NOT NULL, -- Vendor
    OrderNo NVARCHAR(100) UNIQUE NOT NULL, -- PO-2026-00001
    OrderDate DATETIME2 DEFAULT GETUTCDATE(),
    Status NVARCHAR(50) DEFAULT 'DRAFT', -- DRAFT, APPROVED, RECEIVED, CANCELLED
    Notes NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    UpdatedAt DATETIME2,
    UpdatedBy UNIQUEIDENTIFIER,
    IsDeleted BIT DEFAULT 0,
    CONSTRAINT FK_PurchaseOrderHeader_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_PurchaseOrderHeader_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id),
    CONSTRAINT FK_PurchaseOrderHeader_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id)
);

CREATE TABLE PurchaseOrderLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HeaderId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    UomId UNIQUEIDENTIFIER NOT NULL,
    QtyOrdered DECIMAL(18,6) NOT NULL,
    QtyReceived DECIMAL(18,6) DEFAULT 0,
    Price DECIMAL(18,4),
    Currency NVARCHAR(10),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CONSTRAINT FK_PurchaseOrderLine_Header FOREIGN KEY (HeaderId) REFERENCES PurchaseOrderHeader(Id),
    CONSTRAINT FK_PurchaseOrderLine_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

-- Indexing
CREATE INDEX IX_PurchaseOrder_Header ON PurchaseOrderLine(HeaderId);
CREATE INDEX IX_PurchaseOrder_OrderNo ON PurchaseOrderHeader(CompanyId, OrderNo);
CREATE INDEX IX_PurchaseOrder_Vendor ON PurchaseOrderHeader(CompanyId, PartnerId);
