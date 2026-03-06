-- M09 — LPN (License Plate Number / Palet-Kap) Schema
-- Bu modül stokların "Kap/Palet" bazında gruplanmasını ve taşınmasını sağlar.

CREATE TABLE LPN (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    Code NVARCHAR(50) NOT NULL UNIQUE, -- Palet Barkodu (Örn: PAL-0001 veya SSCC)
    Status NVARCHAR(20) DEFAULT 'AVAILABLE', -- AVAILABLE, IN_USE, LOADED, SHIPPED
    LpnType NVARCHAR(20) DEFAULT 'PALLET', -- PALLET, BOX, CARTON
    
    CurrentWarehouseId UNIQUEIDENTIFIER,
    CurrentBinId UNIQUEIDENTIFIER,
    
    ParentLpnId UNIQUEIDENTIFIER, -- İç içe palet/koli mantığı için
    
    Notes NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    
    CONSTRAINT FK_LPN_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_LPN_WH FOREIGN KEY (CurrentWarehouseId) REFERENCES Warehouse(Id),
    CONSTRAINT FK_LPN_Bin FOREIGN KEY (CurrentBinId) REFERENCES Bin(Id),
    CONSTRAINT FK_LPN_Parent FOREIGN KEY (ParentLpnId) REFERENCES LPN(Id)
);

CREATE INDEX IX_LPN_Code ON LPN(CompanyId, Code);
GO
