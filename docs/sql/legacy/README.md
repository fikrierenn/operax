# legacy/ — Eskimiş (superseded) SQL dosyaları

Bu klasördeki dosyalar **migrate listesinde DEĞİL** (orphan) ve **çalıştırılmaz**. Tarihsel referans + kod kaybetmemek için saklanır (Plan 49, 2026-06-23).

## İçerik

- **Pre-consolidation per-module schema** (`schema_M00–M19`, `schema_StatusTransitions`): `docs/sql/schema_all.sql` v2.0 bunların hepsini tek konsolide script'te birleştirdi. Buradakiler eski modül-bazlı kırılım (yorumlar + tarihçe için değerli, ama canonical = schema_all).
- **Tek-seferlik fix scratch** (`fix_city_encoding`, `schema_fix_step1/2`): geçmişte uygulanmış düzeltmeler.
- **Eski test-seed** (`seed_dummy_data`, `seed_test_data`): `seed-demo` komutuyla süperseded.
- **`setup_identity`**: AspNet Identity tabloları artık `schema_all.sql`'de — redundant.
- **`seed_M01_Branch`**: demo İstanbul şubesi (IST-01); baseline yerine `seed_branch_default.sql` kullanılır.

## Kural

Buraya bir dosya geri gerekirse: önce **migrate/seed listesine wire et** (`src/Operax.Cli/Program.cs`), sonra `legacy/`'den çıkar. Doğrudan elle çalıştırma — fresh-install kanıtı için fresh-DB migrate ritüeli (`.claude/rules/phase-review-gate.md §3.5`).
