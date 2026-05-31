-- =============================================================================
-- schema_M11_Reconciliation.sql — Plan 18: Açık-Kalem Kapama (AccountReconciliation)
-- =============================================================================
-- AM Debit (fatura/borç) ↔ AM Credit (tahsilat/alacak) eşleştirme defteri.
-- Endüstri deseni: Mikro CARI_HAREKET_BORC_ALACAK_ESLEME + ERPNext PLE + Odoo
-- account.partial.reconcile — üçü de DebitMovementId↔CreditMovementId+Amount.
-- Append-only (Plan 14): silme yok → IsReversal=1 ters satır.
-- YASAL DAYANAK: TTK md.82 (10 yıl saklama, silme = belge ziyaı) + VUK md.280
--   (dövizli açık-kalem dönem sonu değerleme). Bu yüzden IsDeleted EKLENMEZ.
-- Açık kalem = AM.Debit - SUM(reconciliation.Amount WHERE IsReversal=0) (snapshot tutulmaz).
-- NOT: Realize kur farkı bu tabloya GÖMÜLMEZ — ayrı AM satırı (SourceDocType='FX_DIFF',
--   ayrı mini-plan, K1 öncesi). Amount her zaman TRY eşleştirme tutarı.
-- =============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccountReconciliation')
BEGIN
    CREATE TABLE dbo.AccountReconciliation (
        Id               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        CompanyId        UNIQUEIDENTIFIER NOT NULL,
        PartnerId        UNIQUEIDENTIFIER NOT NULL,
        -- Eşleştirilen iki AM hareketi (Mikro chk_Borc_uid ↔ chk_Alc_uid deseni)
        DebitMovementId  UNIQUEIDENTIFIER NOT NULL,   -- borç hareketi (fatura)
        CreditMovementId UNIQUEIDENTIFIER NOT NULL,   -- alacak hareketi (tahsilat)
        Amount           DECIMAL(18,2)    NOT NULL,   -- eşleştirilen tutar (kısmi kapama)
        MovementDate     DATETIME2        NOT NULL,   -- eşleştirme anı (dönem kilidi için)
        -- Append-only iptal: silme yok, IsReversal=1 ters satır (ReversalOfId = orijinal)
        IsReversal       BIT              NOT NULL DEFAULT 0,
        ReversalOfId     UNIQUEIDENTIFIER NULL,
        CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy        NVARCHAR(450)    NULL,
        CONSTRAINT PK_AccountReconciliation PRIMARY KEY (Id),
        CONSTRAINT CK_AccountReconciliation_Amount CHECK (Amount > 0),
        CONSTRAINT FK_AccountReconciliation_Partner FOREIGN KEY (PartnerId) REFERENCES Partner(Id),
        CONSTRAINT FK_AccountReconciliation_Debit FOREIGN KEY (DebitMovementId) REFERENCES AccountMovement(Id),
        CONSTRAINT FK_AccountReconciliation_Credit FOREIGN KEY (CreditMovementId) REFERENCES AccountMovement(Id)
    );
    -- İki yönlü index ZORUNLU (Mikro+Odoo kanıtı): "bu faturayı ne kapattı" + "bu tahsilat neyi kapattı"
    CREATE NONCLUSTERED INDEX IX_Recon_Debit  ON dbo.AccountReconciliation (CompanyId, DebitMovementId)  INCLUDE (Amount, IsReversal);
    CREATE NONCLUSTERED INDEX IX_Recon_Credit ON dbo.AccountReconciliation (CompanyId, CreditMovementId) INCLUDE (Amount, IsReversal);
    PRINT 'AccountReconciliation tablosu olusturuldu.';
END
GO
