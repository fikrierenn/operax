-- ============================================================
-- Plan 50 Faz 2 (H3) — BranchType CHECK constraint
-- BranchType yalnız NVARCHAR(20) DEFAULT 'SUBE' idi; geçersiz tipo (örn. 'SUB')
-- fn_DefaultBranchId'de sessizce NULL üretiyordu. CHECK ile geçerli küme zorlanır.
-- Geçerli küme: Dtos.cs BranchType sabitleri (SUBE/MERKEZ/FABRIKA/MAGAZA/OFIS).
-- Idempotent.
-- ============================================================
SET XACT_ABORT ON;
SET NOCOUNT ON;

-- Önce olası geçersiz değerleri varsayılana çek (CHECK eklenmeden temizlik)
UPDATE Branch SET BranchType = 'SUBE'
WHERE BranchType NOT IN ('SUBE', 'MERKEZ', 'FABRIKA', 'MAGAZA', 'OFIS');
GO

-- CHECK constraint — yalnızca yoksa ekle (idempotent)
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Branch_BranchType')
    ALTER TABLE Branch ADD CONSTRAINT CK_Branch_BranchType
        CHECK (BranchType IN ('SUBE', 'MERKEZ', 'FABRIKA', 'MAGAZA', 'OFIS'));
GO
