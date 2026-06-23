-- M05 — Sales Order Schema

CREATE TABLE SalesOrderHeader (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    WarehouseId UNIQUEIDENTIFIER NOT NULL,
    PartnerId UNIQUEIDENTIFIER NOT NULL, -- Customer
    OrderNo NVARCHAR(100) UNIQUE NOT NULL, -- SO-2026-00001
    OrderDate DATETIME2 DEFAULT GETUTCDATE(),
    RequestedDeliveryDate DATETIME2,
    Status NVARCHAR(50) DEFAULT 'DRAFT', -- DRAFT, APPROVED, SHIPPED, CANCELLED
    Notes NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    UpdatedAt DATETIME2,
    UpdatedBy UNIQUEIDENTIFIER,
    IsDeleted BIT DEFAULT 0,
    CONSTRAINT FK_SalesOrderHeader_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_SalesOrderHeader_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id),
    CONSTRAINT FK_SalesOrderHeader_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id)
);

CREATE TABLE SalesOrderLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HeaderId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    UomId UNIQUEIDENTIFIER NOT NULL,
    QtyOrdered DECIMAL(18,6) NOT NULL,
    QtyReserved DECIMAL(18,6) DEFAULT 0,
    QtyShipped DECIMAL(18,6) DEFAULT 0,
    Price DECIMAL(18,4),
    Currency NVARCHAR(10),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CONSTRAINT FK_SalesOrderLine_Header FOREIGN KEY (HeaderId) REFERENCES SalesOrderHeader(Id),
    CONSTRAINT FK_SalesOrderLine_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

-- Indexing
CREATE INDEX IX_SalesOrder_Header ON SalesOrderLine(HeaderId);
CREATE INDEX IX_SalesOrder_OrderNo ON SalesOrderHeader(CompanyId, OrderNo);
CREATE INDEX IX_SalesOrder_Customer ON SalesOrderHeader(CompanyId, PartnerId);
