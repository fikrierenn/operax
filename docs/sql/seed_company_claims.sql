-- Sprint 1: Admin ve test kullanıcılarına company claim ekle
-- Operax Demo LTD şirketi ile ilişkilendirir
-- Mevcut claim yoksa ekler (mükerrer kayıt önlenir)

DECLARE @CompanyId NVARCHAR(36) = 'd1e1b1a5-0000-0000-0000-000000000001';

-- admin@operax.com kullanıcısı
DECLARE @AdminId NVARCHAR(450) = (SELECT Id FROM AspNetUsers WHERE NormalizedEmail = 'ADMIN@OPERAX.COM');

IF @AdminId IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM AspNetUserClaims
    WHERE UserId = @AdminId AND ClaimType = 'company'
)
BEGIN
    INSERT INTO AspNetUserClaims (UserId, ClaimType, ClaimValue)
    VALUES (@AdminId, 'company', @CompanyId);
    PRINT 'Admin kullanicisina company claim eklendi.';
END
ELSE
    PRINT 'Admin kullanicisi zaten company claim iceriyor veya bulunamadi.';

-- test@operax.com kullanıcısı
DECLARE @TestId NVARCHAR(450) = (SELECT Id FROM AspNetUsers WHERE NormalizedEmail = 'TEST@OPERAX.COM');

IF @TestId IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM AspNetUserClaims
    WHERE UserId = @TestId AND ClaimType = 'company'
)
BEGIN
    INSERT INTO AspNetUserClaims (UserId, ClaimType, ClaimValue)
    VALUES (@TestId, 'company', @CompanyId);
    PRINT 'Test kullanicisina company claim eklendi.';
END
ELSE
    PRINT 'Test kullanicisi zaten company claim iceriyor veya bulunamadi.';
