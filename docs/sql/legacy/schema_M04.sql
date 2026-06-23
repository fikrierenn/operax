-- M04 — Receiving Schema (Linked to PO)

CREATE TABLE ReceivingHeader (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    WarehouseId UNIQUEIDENTIFIER NOT NULL,
    PartnerId UNIQUEIDENTIFIER NOT NULL, -- Vendor
    PurchaseOrderId UNIQUEIDENTIFIER, -- Optional: Link to PO
    DocNo NVARCHAR(100) UNIQUE NOT NULL, -- RCV-2026-00001
    DocDate DATETIME2 DEFAULT GETUTCDATE(),
    Status NVARCHAR(50) DEFAULT 'DRAFT', -- DRAFT, POSTED, CANCELLED
    Notes NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    UpdatedAt DATETIME2,
    UpdatedBy UNIQUEIDENTIFIER,
    IsDeleted BIT DEFAULT 0,
    CONSTRAINT FK_ReceivingHeader_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_ReceivingHeader_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse(Id),
    CONSTRAINT FK_ReceivingHeader_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id),
    CONSTRAINT FK_ReceivingHeader_PO FOREIGN KEY (PurchaseOrderId) REFERENCES PurchaseOrderHeader(Id)
);

CREATE TABLE ReceivingLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HeaderId UNIQUEIDENTIFIER NOT NULL,
    PurchaseOrderLineId UNIQUEIDENTIFIER, -- Optional: Link to PO Line
    ItemId UNIQUEIDENTIFIER NOT NULL,
    UomId UNIQUEIDENTIFIER NOT NULL,
    QtyOriginal DECIMAL(18,6) NOT NULL,
    QtyBase DECIMAL(18,6) NOT NULL, -- QtyOriginal * ConversionRate
    LotNo NVARCHAR(100),
    SerialNo NVARCHAR(100),
    ExpiryDate DATETIME2,
    BinId UNIQUEIDENTIFIER, -- Target Bin (Phase 2+: Putaway)
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ReceivingLine_Header FOREIGN KEY (HeaderId) REFERENCES ReceivingHeader(Id),
    CONSTRAINT FK_ReceivingLine_Item FOREIGN KEY (ItemId) REFERENCES Item(Id),
    CONSTRAINT FK_ReceivingLine_POLine FOREIGN KEY (PurchaseOrderLineId) REFERENCES PurchaseOrderLine(Id)
);

-- Indexing
CREATE INDEX IX_Receiving_Header ON ReceivingLine(HeaderId);
CREATE INDEX IX_Receiving_DocNo ON ReceivingHeader(CompanyId, DocNo);
CREATE INDEX IX_Receiving_PO ON ReceivingHeader(PurchaseOrderId);
