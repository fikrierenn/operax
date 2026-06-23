# Plan 49 — SQL Ölü-Nesne Süpürmesi (orphan .sql temizliği)

**Durum:** ✅ TAMAMLANDI (2026-06-23, commit e3db7bd + 3dee12b) — 48 orphan legacy/+smoke/'a taşındı (silme YOK), D baseline'a wire; fresh-DB ritüeli geçti (migrate 0 fail + seed 9/9 + MRK/CostCenter ✓, demo IST-01 YOK).
**Tier:** 3 (çok dosya silme, geri-alması git ile kolay ama yanlış silme migrate'i/feature'ı bozar)
**Tarih:** 2026-06-23
**Tetik:** Plan 47/48 sırasında ~48 orphan .sql tespit edildi; kullanıcı "iş geldikçe SQL'i de çözelim".

---

## 1. Problem

`docs/sql/` altında **~48 .sql dosyası migrate listesinde DEĞİL** (orphan). Çoğu pre-consolidation kalıntı: `schema_all.sql` v2.0 "tüm modüllerin tek konsolide scripti" (başlıkta "çift tanımlar düzeltildi" notu) — eski per-module `schema_M00–M19` dosyaları onunla **redundant**. Ek olarak tek-seferlik fix/smoke/test-seed scratch'leri.

**Zarar:** (1) Hangi dosya canonical karışıklığı (Plan 48 `v_ExpenseDistribution` çift-tanımı bunun maliyetiydi). (2) Yanlış dosyayı düzenleme riski (before-major-change). (3) Repo gürültüsü. **Acil değil** ama hijyen + gelecek-hata önleme.

**Güvenlik dayanağı:** Bu dosyalar migrate'te OLMADIĞI için silmek `migrate`'i kıramaz. **Nihai kanıt = fresh-DB migrate ritüeli (`phase-review-gate.md §3.5`):** sil → boş test DB'ye migrate → 0 fail + tüm beklenen obje mevcut.

## 2. Kategoriler + Tasarruf

**İLKE (kullanıcı 2026-06-23): KOD KAYBOLMAZ.** Silme değil → değerliyse TAŞI (`docs/sql/legacy/` veya `docs/sql/smoke/`), gap ise (eksik) migrate/seed'e EKLE. Yalnız git history değil, bulunabilir klasörde korunur.

| # | Kategori | Dosyalar | Karar |
|---|---|---|---|
| **A** | Pre-consolidation per-module schema | `schema_M00*`, `M01*`, `M02*`, `M03*`, `M04`, `M05`, `M06*`, `M07*`, `M08*`, `M09*`, `M10*`, `M15_Dashboards`, `M18_Expenses`, `M19_Budgeting`, `StatusTransitions` (~34) | **TAŞI → `docs/sql/legacy/`** — schema_all'da konsolide; tarihsel modül-kırılımı + yorumlar korunur. Örnek doğrulandı: ItemSerial/ItemLot/ProductRoute/StatusTransition hepsi schema_all'da. |
| **B** | Tek-seferlik fix scratch | `fix_city_encoding`, `schema_fix_step1`, `schema_fix_step2` | **TAŞI → `legacy/`** — uygulanmış; referans için korunur. |
| **C** | Eski test-seed | `seed_dummy_data`, `seed_test_data` | **TAŞI → `legacy/`** — seed-demo süperseded; örnek-veri deseni korunur. |
| **D** | Orphan seed | `seed_M01_Branch`, `seed_M18_ExpenseReporting` | **DOĞRULA** — içerik başka seed'e taşınmış mı yoksa EKSİK mi? Eksikse seed listesine **EKLE** (Plan-48-tarzı gap, silme); süperseded ise `legacy/`'e taşı. |
| **E** | Eski identity setup | `setup_identity` | **DOĞRULA** — SeedData.cs süperseded mi? Süperseded ise `legacy/`, değilse migrate/seed'e EKLE. |
| **F** | Manuel smoke script | `smoke_plan22_e(_restore)`, `smoke_plan24(_cleanup)`, `smoke_plan25`, `smoke_plan26(_cleanup)`, `smoke_plan30` (8) | **TAŞI → `docs/sql/smoke/`** — test dokümantasyonu, korunur; kök sql/ temizlenir. |
| **G** | Backfill | `migrate_backfill_accountmovement`, `migrate_backfill_reconciliation` | **KAL** (yerinde) — Plan 48 kararı (tek-seferlik veri, elle script). |

## 3. Alternatifler (reddedilen)

- **Hiç dokunma:** reddedilen — gürültü + yanlış-dosya riski sürer (v_ExpenseDistribution dersi).
- **Hepsini körlemesine sil:** reddedilen — D/E (orphan seed/identity) gap olabilir; doğrulama şart.
- **Tek tek (iş geldikçe):** kısmen — sweep tek fresh-DB doğrulamasıyla daha verimli; ama D/E/F per-dosya karar.

## 4. Riskler

| Risk | Etki | Mitigasyon |
|---|---|---|
| Redundant sanılan dosyada schema_all'da OLMAYAN obje | orta | **fresh-DB migrate ritüeli** (sil sonrası 0 fail + obje sayımı); ek: silmeden önce her A-dosyası objelerini schema_all'a karşı statik tara |
| D/E aslında gerekli (gap) | orta | Silmeden ÖNCE: C# referans taraması + canlı DB'de obje var mı; eksikse migrate/seed'e EKLE, silme |
| Smoke script ileride gerekir | düşük | Sil yerine `smoke/` klasörüne taşı (geçmiş kanıt korunur) |
| Yanlış silme geri-alma | düşük | git mv/rm → revert kolay; commit kategorisi başına ayrı |

## 5. Done Criteria

- [x] A: 34 per-module schema → objeleri schema_all'da doğrulandı (gap yok; ItemUnit+vw_EnvanterDurumu ölü) → `legacy/`
- [x] B/C: fix + test-seed scratch → `legacy/`
- [x] D: seed_M18 (jenerik) baseline'a EKLENDİ; seed_branch_default (temiz MRK) baseline'a; demo seed_M01_Branch (IST-01) → `legacy/`
- [x] E: setup_identity redundant (AspNet zaten schema_all'da) → `legacy/`
- [x] F: 8 smoke script → `docs/sql/smoke/`
- [x] **Fresh-DB ritüeli:** boş DB → migrate 0 fail + seed 9/9 + MRK şube/5 CostCenter ✓, demo IST-01 YOK (§3.5)
- [x] legacy/README.md (ne olduğu + geri-wire kuralı) + kategori başına commit; plan arşive

## 6. Faz sırası

1. **Faz 1 — Statik doğrulama:** A dosyalarının objelerini schema_all'a karşı tara (eksik var mı?). D/E/F karar için içerik + C# referans.
2. **Faz 2 — Güvenli silme (A/B/C):** git rm + dev build (CLI etkilenmez, sql dosyaları derlemeye girmez).
3. **Faz 3 — D/E/F kararları:** gap olanı migrate/seed'e ekle; gerçekten ölü olanı sil; smoke taşı.
4. **Faz 4 — Fresh-DB ritüeli:** boş test DB migrate → 0 fail + obje sayımı = kanıt.
5. **Faz 5 — Commit (kategori başına) + arşiv + journal.**

## 7. Rollback
- Tüm silmeler git rm → `git revert` ile geri. Hiçbir canlı obje silinmez (yalnız migrate-dışı dosyalar). Migrate listesi A/B/C için DEĞİŞMEZ (zaten yoklar).

## 8. 5 Lens
- 🔴 **Contrarian:** Fatal flaw? Bir "redundant" dosyada schema_all'da olmayan tek obje → fresh-DB test yakalar (obje sayımı eksik çıkar) + statik tarama ön-filtre.
- 🔵 **First Principles:** Doğru soru "bu dosya migrate'te mi, değilse fresh install'ı etkiler mi?" — etkilemiyorsa silme güvenli; etkiliyorsa (D/E gap) silme değil EKLE.
- 🟢 **Expansionist:** Daha büyük fırsat? Tüm orphan'ları tarayınca başka Plan-48-tarzı gap (canlı-ama-migratede-yok) çıkabilir → D/E tam o tarama.
- ⚪ **Outsider:** Yabancı ne garip bulur? "schema_M01.sql VE schema_all.sql ikisi de var, hangisi gerçek?" → tekilleştir.
- 🟡 **Executor:** Pazartesi? Faz 1 statik tara → A/B/C git rm → fresh-DB migrate → D/E/F karar.

## 9. İlişkili
- `.claude/rules/phase-review-gate.md §3.5` (fresh-DB ritüeli — bu planın kanıt motoru)
- `.claude/rules/before-major-change.md` (silme öncesi referans tarama)
- `docs/sql/schema_all.sql` (canonical konsolide şema)
- Plan 48 (reconciliation migrate-gap — orphan'ın canlı-feature versiyonu, karşı örnek)
