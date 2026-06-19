# ADR-01 — Ledger Clustered Anahtar Stratejisi

**Tarih:** 2026-05-31 · **Durum:** Kabul edildi · **Bağlam:** Plan 14 (AR-004) · **Karar verici:** Kullanıcı (2026-05-31)

## Bağlam

`StockMovement`, `AccountMovement`, `FinancialTransaction` sürekli büyüyen ledger (append-only) tablolarıdır. Canlı VT (`sys.indexes`, 2026-05-31) ile doğrulandı:

- Üçünün de **PK = CLUSTERED, `UNIQUEIDENTIFIER DEFAULT NEWID()`** (rastgele GUID).
- Rastgele GUID clustered anahtar → her INSERT rastgele bir sayfaya düşer → **page split + index fragmentasyonu** → INSERT yavaşlar, "Dapper + SARGable hız" hedefini baltalar.
- Mevcut nonclustered index'ler (`IX_StockMovement_Item/Bin/Source`, `IX_AccountMovement_Partner_Date`, `UX_AccountMovement_Source`, `IX_FinTx_*`) basılı ve bakiye SUM'unun dayanağı (K6).

## Karar

**Yeni ledger tabloları için: `BIGINT IDENTITY` clustered PK + mevcut `Id UNIQUEIDENTIFIER` nonclustered UNIQUE.**

- `Seq BIGINT IDENTITY(1,1)` → **clustered PK** (dar 8 byte, monoton artan → page split yok, doğal kronolojik sıra).
- `Id UNIQUEIDENTIFIER` → **nonclustered UNIQUE** korunur; tüm dış FK referansları ve C# `Guid` üretimi değişmeden kalır.

### Reddedilenler
- **A — NEWID() korunsun:** Reddedildi; fragmentasyon kanıtlı (canlı VT'de clustered random GUID).
- **B — NEWSEQUENTIALID():** Sıralı GUID page split'i azaltır ama 16 byte geniş clustered kalır; ayrıca GUID üretimini DB'ye taşır (C#-tarafı `Guid.NewGuid()` deseniyle çelişir). Kısmi çözüm.
- **C (seçilen) — BIGINT IDENTITY clustered + GUID nonclustered:** En dar/sıralı clustered, GUID dış anahtar olarak korunur. Klasik ERP ledger deseni.

## Sonuç

- **Yeni** ledger/büyüyen tablolar (örn. `PeriodOverrideLog`) bu desenle açılır: `Seq BIGINT IDENTITY PK CLUSTERED` + iş anahtarı GUID nonclustered.
- **Mevcut** `StockMovement` / `AccountMovement` / `FinancialTransaction` clustered-key dönüşümü **Faz 2** (ayrı, dikkatli migration — clustered index drop/rebuild, FK etkisi yok çünkü FK'lar `Id` GUID'e bağlı, o nonclustered unique olarak kalır). Bu ADR kararı sabitler; migration ayrı plan/oturumda.
- Aynı strateji ileride diğer yüksek-hacimli append tablolarına (AuditLog vb.) uygulanabilir.

## İlişkili
- Plan 14 (ledger immutability + dönem kontrolü) · AR-004 · `document-immutability.md`
