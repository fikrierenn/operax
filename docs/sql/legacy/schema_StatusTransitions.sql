-- M00 — Status Transitions Schema

CREATE TABLE StatusTransition (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    DocumentType NVARCHAR(100) NOT NULL, -- e.g., 'RECEIVING', 'SHIPMENT', 'COUNT'
    FromStatusCode NVARCHAR(100) NOT NULL,
    ToStatusCode NVARCHAR(100) NOT NULL,
    RoleId NVARCHAR(450), -- Optional: Required role to perform this transition
    ActionNameTr NVARCHAR(200),
    ActionNameEn NVARCHAR(200),
    OrderNo INT DEFAULT 0,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    IsDeleted BIT DEFAULT 0,
    CONSTRAINT FK_StatusTransition_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
);

CREATE INDEX IX_StatusTransition_Doc ON StatusTransition(CompanyId, DocumentType) WHERE IsDeleted = 0;
