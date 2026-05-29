# Plan 14 — Ledger Clustered Anahtar + Immutability

**Tarih:** 2026-05-29 · **Durum:** `Onaylandı (2026-05-29)` · **Modül:** M02/M11 · **Kaynak:** AR-004 + AR-005 (🟠 YÜKSEK)

## 1. Problem
İki ilişkili ledger sorunu:
- **AR-004 (PK fragmentasyon):** `StockMovement` + `AccountMovement` clustered PK = `UNIQUEIDENTIFIER DEFAULT NEWID()` (rastgele). Sürekli büyüyen tablolarda page split + index fragmentasyonu → INSERT yavaşlar, "Dapper+SARGable hız" iddiasını baltalar.
- **AR-005 (immutability):** `AccountMovement.IsDeleted` taşıyor → append-only ledger silinebilir. VISION "ERP truth immutable" + VUK 359 bütünlük ile çelişir. Düzeltme = ters kayıt (contra-entry), silme değil. `StockMovement` `IsCancelled` kullanıyor (daha iyi ama yine cancel≠reversal netleşmeli).

## 2. Scope
### Dahili
- **ADR:** ledger clustered anahtar stratejisi. Aday: `BIGINT IDENTITY` clustered PK + mevcut `Id GUID` nonclustered unique (dış referanslar GUID kalsın). Alternatif: `NEWSEQUENTIALID()`.
- `AccountMovement`'tan `IsDeleted` kaldır → düzeltme `SourceDocType='REVERSAL'` ters kayıtla (zaten tasarımda var).
- StockMovement: cancel mekanizmasını reversal disiplinine bağla (ters StockMovement, IsCancelled yerine/yanında).
### Dışı
- Mevcut veri migrasyonu büyük tabloda — dikkatli down/up script (faz ayrı).

## 3. Alternatifler (clustered key)
- A: NEWID() korunsun — Reddedildi: fragmentasyon kanıtlı sorun.
- B: NEWSEQUENTIALID() — sıralı GUID, page split azalır; ama GUID 16 byte (geniş).
- C (öneri): BIGINT IDENTITY clustered + GUID nonclustered — en dar/sıralı clustered, GUID dış anahtar olarak kalır. ERP ledger standardı.

**5 lens:** 🔴 PK değişimi mevcut FK/sorguları etkiler → GUID'i nonclustered koruyarak kır. 🔵 Gerçek ihtiyaç: sıralı insert + immutable defter. 🟢 Aynı strateji tüm büyüyen tablolara (AuditLog vb.). ⚪ "GUID neden vardı?" → dağıtık üretim/merge; tek-DB'de gerek yok ama dış ref için tut. 🟡 ADR + yeni tablolarda uygula (mevcut migrate dikkatli).

## 4. Done
- [ ] ADR yazıldı (`docs/ADR/NN-ledger-clustered-key.md`)
- [ ] AccountMovement IsDeleted kaldırıldı + REVERSAL ile düzeltme akışı netleşti
- [ ] Clustered key stratejisi yeni ledger tablolarında uygulandı (mevcut için migrate planı)
- [ ] document-immutability.md ledger reversal kuralıyla güncellendi

## 5. Adımlar
1. [ ] ADR (clustered key kararı)
2. [ ] AccountMovement IsDeleted kaldır + reversal akış doğrula
3. [ ] StockMovement cancel→reversal disiplini
4. [ ] (Faz 2) mevcut tablolarda clustered key migrate (büyük — ayrı)

## 6. Onay
- [ ] Gösterildi · [ ] ADR onayı · [ ] Onay: <tarih>

> İlişkili: AR-004, AR-005, document-immutability.md, schema_M11_AccountMovement.sql, schema_all.sql (StockMovement)
