-- M00 Ek: AuditLog — Platform denetim kayıtları
-- Sprint 1'de eklendi. seed_core.sql ile birlikte çalıştırılır.

CREATE TABLE AuditLog (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId   UNIQUEIDENTIFIER NOT NULL,
    UserId      UNIQUEIDENTIFIER NULL,                      -- NULL: sistem tarafından yapılan işlem
    UserName    NVARCHAR(256)   NOT NULL DEFAULT '',
    Action      NVARCHAR(100)   NOT NULL,                   -- CREATE · UPDATE · DELETE · POST · LOGIN · LOGOUT
    EntityType  NVARCHAR(200)   NOT NULL,                   -- ReceivingHeader · SalesOrder · IdentityUser vb.
    EntityId    UNIQUEIDENTIFIER NULL,
    Details     NVARCHAR(MAX)   NULL,                       -- JSON veya serbest metin
    IpAddress   NVARCHAR(50)    NULL,
    CreatedAt   DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_AuditLog_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
);

-- Şirkete göre zaman damgalı sorgular için (son X kayıt getirme)
CREATE INDEX IX_AuditLog_Company_Date ON AuditLog(CompanyId, CreatedAt DESC);

-- Belirli bir kayıt üzerindeki geçmişi getirmek için
CREATE INDEX IX_AuditLog_Entity ON AuditLog(CompanyId, EntityType, EntityId) WHERE EntityId IS NOT NULL;
