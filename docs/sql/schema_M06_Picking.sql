-- M06 — Picking (Toplama) Schema
-- Bu modül Sevkiyat (M05) ile Envanter (M02) arasında köprü kurar.

-- PickTask: Toplama Emri Başlığı
CREATE TABLE PickTask (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    DocNo NVARCHAR(50) NOT NULL, -- Örn: PCK-2026-0001
    ShipmentId UNIQUEIDENTIFIER, -- Opsiyonel: Sevkiyat bazlı toplama ise
    Status NVARCHAR(20) DEFAULT 'DRAFT', -- DRAFT, ASSIGNED, IN_PROGRESS, COMPLETED, CANCELLED
    AssignedUserId NVARCHAR(450), -- Görevin atandığı personel (AspNetUsers.Id)
    Priority INT DEFAULT 10,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER,
    StartedAt DATETIME2,
    CompletedAt DATETIME2,
    CONSTRAINT FK_PickTask_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id),
    CONSTRAINT FK_PickTask_Shipment FOREIGN KEY (ShipmentId) REFERENCES ShippingHeader(Id)
);

-- PickTaskLine: Toplama Satırları
-- Hangi raftan, hangi ürünün, kaç adet toplanacağını ve fiilen ne yapıldığını tutar.
CREATE TABLE PickTaskLine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PickTaskId UNIQUEIDENTIFIER NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL,
    UomId UNIQUEIDENTIFIER NOT NULL, -- Transaction UOM
    
    -- Hedef Altyapı (Sistemin önerdiği)
    TargetWarehouseId UNIQUEIDENTIFIER NOT NULL,
    TargetBinId UNIQUEIDENTIFIER NOT NULL, -- Önerilen raf
    QtyRequested DECIMAL(18,6) NOT NULL, -- İstenen miktar (Original UOM)
    QtyRequestedBase DECIMAL(18,6) NOT NULL, -- İstenen miktar (Base UOM)
    
    -- Fiili Gerçekleşen (Terminalden onaylanan)
    PickedBinId UNIQUEIDENTIFIER, -- Fiilen okutulan/alınan raf
    QtyPicked DECIMAL(18,6) DEFAULT 0, -- Toplanan miktar (Original UOM)
    QtyPickedBase DECIMAL(18,6) DEFAULT 0, -- Toplanan miktar (Base UOM)
    
    -- İzlenebilirlik Logları (Tam ihtiyacınız olan yer)
    PickedByUserId NVARCHAR(450), -- Bu satırı fiilen kim topladı?
    PickedAt DATETIME2, -- Tam toplama anı
    
    IsCancelled BIT DEFAULT 0,
    CONSTRAINT FK_PickTaskLine_Task FOREIGN KEY (PickTaskId) REFERENCES PickTask(Id),
    CONSTRAINT FK_PickTaskLine_Item FOREIGN KEY (ItemId) REFERENCES Item(Id),
    CONSTRAINT FK_PickTaskLine_TBin FOREIGN KEY (TargetBinId) REFERENCES Bin(Id),
    CONSTRAINT FK_PickTaskLine_PBin FOREIGN KEY (PickedBinId) REFERENCES Bin(Id)
);

-- Indexing
CREATE INDEX IX_PickTask_DocNo ON PickTask(CompanyId, DocNo);
CREATE INDEX IX_PickTaskLine_Task ON PickTaskLine(PickTaskId);
GO
