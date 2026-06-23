-- M07 — Transfer Schema
-- Depo içi raf transferleri ve depolar arası stok hareketleri.

CREATE TABLE StockTransfer (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    DocNo NVARCHAR(50) NOT NULL,
    Status NVARCHAR(20) DEFAULT 'DRAFT', -- DRAFT, POSTED, CANCELLED
    TransferType NVARCHAR(20) NOT NULL, -- 'BIN_TO_BIN' veya 'WH_TO_WH'
    
    FromWarehouseId UNIQUEIDENTIFIER NOT NULL,
    ToWarehouseId UNIQUEIDENTIFIER NOT NULL,
    
    Notes NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    PostedAt DATETIME2,
    
    CONSTRAINT FK_Transfer_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
);

CREATE TABLE StockTransferLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TransferId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    UomId UNIQUEIDENTIFIER NOT NULL,
    
    FromBinId UNIQUEIDENTIFIER, -- Kaynak Raf
    ToBinId UNIQUEIDENTIFIER,   -- Hedef Raf
    
    Qty DECIMAL(18,6) NOT NULL,
    QtyBase DECIMAL(18,6) NOT NULL,
    
    LotNo NVARCHAR(50),
    CONSTRAINT FK_TLine_Header FOREIGN KEY (TransferId) REFERENCES StockTransfer(Id),
    CONSTRAINT FK_TLine_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
);

CREATE INDEX IX_StockTransfer_DocNo ON StockTransfer(CompanyId, DocNo);
GO
