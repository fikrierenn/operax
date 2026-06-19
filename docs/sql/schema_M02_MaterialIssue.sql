-- Plan 25 Faz A: Sarf Fişi (MaterialIssue) şema
-- Genel/idari sarf tüketimi — ProductionConsumption'dan AYRI (üretim emri gerektirmez).
-- Idempotent (IF NOT EXISTS korumalı)

-- Sarf fişi başlık
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MaterialIssueHeader')
BEGIN
    CREATE TABLE MaterialIssueHeader (
        Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId     UNIQUEIDENTIFIER NOT NULL,
        WarehouseId   UNIQUEIDENTIFIER NOT NULL,        -- sarfın çıkacağı depo (Post SP başlıktan okur)
        DocNo         NVARCHAR(50)     NOT NULL,
        IssueDate     DATE             NOT NULL DEFAULT CAST(GETUTCDATE() AS DATE),
        CostCenterId  UNIQUEIDENTIFIER NULL,            -- masraf merkezi etiketi (opsiyonel; GL yok)
        Notes         NVARCHAR(MAX)    NULL,
        Status        NVARCHAR(20)     NOT NULL DEFAULT 'DRAFT',  -- DRAFT/POSTED/CANCELLED
        CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy     UNIQUEIDENTIFIER NULL,
        UpdatedAt     DATETIME2        NULL,
        UpdatedBy     UNIQUEIDENTIFIER NULL,
        CONSTRAINT FK_MaterialIssue_Company    FOREIGN KEY (CompanyId)    REFERENCES Company(Id),
        CONSTRAINT FK_MaterialIssue_Warehouse  FOREIGN KEY (WarehouseId)  REFERENCES Warehouse(Id),
        CONSTRAINT FK_MaterialIssue_CostCenter FOREIGN KEY (CostCenterId) REFERENCES CostCenter(Id)
    );
    CREATE INDEX IX_MaterialIssue_Company ON MaterialIssueHeader(CompanyId, Status);
END

-- Sarf fişi satır
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MaterialIssueLine')
BEGIN
    CREATE TABLE MaterialIssueLine (
        Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        HeaderId     UNIQUEIDENTIFIER NOT NULL,
        ItemId       UNIQUEIDENTIFIER NOT NULL,
        UomId        UNIQUEIDENTIFIER NOT NULL,
        Qty          DECIMAL(18,6)    NOT NULL,        -- orijinal birim miktar
        QtyBase      DECIMAL(18,6)    NOT NULL,        -- temel birim (Qty * ConversionRate)
        BinId        UNIQUEIDENTIFIER NULL,            -- çıkış rafı (NULL → Post SP picking bin fallback)
        CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_MaterialIssueLine_Header FOREIGN KEY (HeaderId) REFERENCES MaterialIssueHeader(Id),
        CONSTRAINT FK_MaterialIssueLine_Item   FOREIGN KEY (ItemId)   REFERENCES Item(Id)
    );
    CREATE INDEX IX_MaterialIssueLine_Header ON MaterialIssueLine(HeaderId);
END
