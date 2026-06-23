# Plan 48 — Reconciliation Migrate-Gap (fresh-install kırılması)

**Durum:** Taslak · onay bekliyor
**Tier:** 3 (migrate sıra değişikliği + v_ExpenseDistribution çift-tanım çakışması + 4-6 SQL dosyası, geri-alması zor)
**Tarih:** 2026-06-23
**Tetik:** Plan 47 ölü-nesne taraması sırasında bulundu; kullanıcı "iş geldikçe SQL'i de çözelim" dedi.

---

## 1. Problem

Cari mutabakat feature'ı (Plan 18 Açık-Kalem Kapama + Plan 19 Cari Mutabakat Turu) **canlı ve wire'lı**:
- `Features/MasterData/Partners/Details.cshtml.cs` → `sp_CreateReconciliationStatement`, `sp_RespondReconciliation` çağırır.
- `Lib/Jobs/ReconciliationExpiryJob.cs` (Hangfire) → `sp_ExpireReconciliations` çağırır.

Ama feature'ın **6 SQL dosyası `operax-cli migrate` listesinde (`src/Operax.Cli/Program.cs`) YOK:**

| Dosya | Obje | Faz |
|---|---|---|
| `schema_M11_Reconciliation.sql` | tablo `AccountReconciliation` | schema |
| `schema_M11_PartnerReconciliation.sql` | tablo `PartnerReconciliationLog` | schema |
| `db_objects_reconciliation.sql` | `sp_ReconcileMovements`, `sp_UnreconcileMovements`, `tvf_OpenItems`, `tvf_OpenItemAging`, `v_ExpenseDistribution` | db_objects |
| `db_objects_partner_reconciliation.sql` | `sp_CreateReconciliationStatement`, `sp_RespondReconciliation`, `sp_GuardPartnerReconciled`, `tvf_ReconciliationPrep`, `sp_ExpireReconciliations` | db_objects |
| `migrate_backfill_accountmovement.sql` | (veri backfill, tek-seferlik) | backfill |
| `migrate_backfill_reconciliation.sql` | (veri backfill, tek-seferlik) | backfill |

**Sonuç:** Mevcut dev/prod DB'lerde objeler tarihsel olarak elle uygulanmış → **çalışıyor** (canlı doğrulandı: 10/10 obje mevcut). Ama **temiz kurulumda** (`migrate` ile fresh DB) bu tablolar/SP'ler/TVF'ler **oluşmaz** → mutabakat sekmesi + ExpiryJob **runtime 500** (`Invalid object name 'sp_CreateReconciliationStatement'`). Müşteri kurulumu = single-tenant fresh → **go-live kırılması**.

**Aciliyet:** Orta. Mevcut kurulumlar etkilenmez; yalnız yeni kurulum. Ama go-live öncesi kapanmalı.

## 2. Scope

**DAHİL — sadece migrate listesine kayıt + 1 çakışma temizliği. Obje davranışı DEĞİŞMEZ.**

- **Faz A — schema kaydı:** `schema_M11_Reconciliation.sql` + `schema_M11_PartnerReconciliation.sql` → migrate schema listesine, **`schema_M11_AccountMovement.sql`'den SONRA** (FK dep: AccountReconciliation→AccountMovement; PartnerReconciliationLog→Partner/Company zaten schema_all'da).
- **Faz B — db_objects kaydı:** `db_objects_reconciliation.sql` + `db_objects_partner_reconciliation.sql` → db_objects fazına, **`db_objects_expensereport.sql`'den ÖNCE** (v_ExpenseDistribution çakışması — aşağıda).
- **Faz C — v_ExpenseDistribution çakışma temizliği (ANA TEHLİKE):** `v_ExpenseDistribution` İKİ dosyada tanımlı, **farklı** (reconciliation 17 satır vs expensereport 24 satır). Canonical = expensereport (zaten migrate'te, "view son kazanır" notuyla step 8). **Karar: stale tanımı `db_objects_reconciliation.sql`'den ÇIKAR** (canonical tek kaynak expensereport kalır) → sıra-kırılganlığı tümden biter. Alternatif (sadece sıraya güven) reddedildi: gelecekte biri sırayı bozarsa sessiz view-regresyonu.

**HARİÇ (gerekçeli):**
- **Backfill (2 dosya) migrate'e EKLENMEZ.** Bunlar tek-seferlik tarihsel veri dönüşümü (mevcut fatura/ödeme → AccountMovement/AccountReconciliation), idempotent. Fresh install'da kaynak veri YOK → no-op. Yapısal migrate yoluna veri-backfill koymak yanlış katmanlama. Gerekirse elle `operax-cli script <dosya>`. Dosya başlıklarında zaten "TEK SEFERLİK, Çalıştır: operax-cli script ..." yazıyor.
- **AccountMovement post-SP besleme drift'i** (memory R0: "AccountMovement onay SP'leriyle beslenmiyor") — AYRI bilinen sorun, bu planın konusu değil (bu plan yalnız migrate-gap'i kapatır).

## 3. Alternatifler (reddedilen)

- **Sadece sıraya güven (view'ı çıkarma):** reddedildi — sıra-kırılganlığı kalır, sessiz regresyon riski.
- **Backfill'i migrate'e koy:** reddedildi — yanlış katman; idempotent ama her migrate'te veri-mutasyon yolu açar.
- **Hiç dokunma (dev çalışıyor):** reddedildi — fresh install kırık, go-live patlar.

## 4. Riskler

| Risk | Etki | Mitigasyon |
|---|---|---|
| v_ExpenseDistribution yanlış sürüm canlı kalır | orta | Faz C: stale tanım çıkar → tek kaynak; fresh test migrate sonrası view tanımı = expensereport doğrula |
| schema sırası FK kırar (AccountMovement yoksa) | orta | AccountMovement'tan sonra ekle; fresh test DB'de migrate 0-hata doğrula |
| db_objects_reconciliation SP'leri başka migrate objesine bağımlı | düşük | fresh test migrate koşumu kanıt; sql-sp-reviewer |
| Mevcut dev DB'de tekrar migrate çift-etki | düşük | hepsi CREATE OR ALTER / IF NOT EXISTS idempotent; mevcut dev'de re-migrate 0-hata |
| Backfill'siz fresh install'da AccountMovement boş | düşük (kapsam dışı) | dokümante; ayrı drift sorunu |

## 5. Done Criteria

- [ ] Faz A: 2 schema dosyası migrate listesinde, AccountMovement'tan sonra
- [ ] Faz B: 2 db_objects dosyası migrate'te, expensereport'tan önce
- [ ] Faz C: v_ExpenseDistribution `db_objects_reconciliation.sql`'den çıkarıldı; canonical=expensereport
- [ ] **Fresh test DB** (drop+create veya Operax_Test) → `operax-cli migrate` 0-hata + 10/10 reconciliation obje mevcut + v_ExpenseDistribution = expensereport sürümü (satır/tanım doğrula)
- [ ] Mevcut dev DB re-migrate 0-hata (idempotent kanıt)
- [ ] sql-sp-reviewer geçti (sıra + view tekilliği)
- [ ] Plan arşive + journal

## 6. Faz sırası / bağımlılık

1. **Faz C önce** (view çıkar) — db_objects_reconciliation'ı migrate'e eklemeden temizle, çakışma hiç doğmasın.
2. **Faz A** (schema kaydı) — yapısal taban.
3. **Faz B** (db_objects kaydı) — SP/TVF, schema'dan sonra.
4. **Doğrulama** — fresh test DB migrate (en kritik kanıt).

## 7. Rollback

- Program.cs migrate satırlarını geri al (git revert) → eski davranış (objeler migrate'siz, dev elle-uygulanmış halde kalır).
- v_ExpenseDistribution çıkarımı: git ile geri yükle.
- Hiçbir veri silinmez (yalnız migrate listesi + view tekilliği).

## 8. 5 Lens

- 🔴 **Contrarian:** Fatal flaw? v_ExpenseDistribution'ı yanlış dosyadan çıkarırsam canonical kaybolur → fresh test migrate sonrası view tanımını expensereport ile byte-karşılaştır (kanıt).
- 🔵 **First Principles:** Doğru soru "fresh install bu feature'ı kurabiliyor mu?" — şu an HAYIR. Migrate = kurulumun tek gerçeği; feature canlıysa migrate'te olmalı.
- 🟢 **Expansionist:** Daha büyük fırsat? Tüm migrate-gap'leri tara (başka canlı-ama-migratede-yok feature var mı?) — bu planda reconciliation'a odak, genel tarama ayrı (db-schema-checker bir sonraki tur).
- ⚪ **Outsider:** Yabancı ne garip bulur? "Feature C#'tan çağrılıyor ama kurulum scriptinde yok" — klasik elle-uygulanmış-unutulmuş borç.
- 🟡 **Executor:** Pazartesi sabahı? Faz C view çıkar → A/B Program.cs 4 satır → fresh test DB migrate → 10/10 obje doğrula.

## 9. İlişkili
- `src/Operax.Cli/Program.cs` (migrate listesi)
- `docs/sql/db_objects_reconciliation.sql` · `db_objects_partner_reconciliation.sql` · `db_objects_expensereport.sql` (v_ExpenseDistribution canonical)
- `docs/sql/schema_M11_Reconciliation.sql` · `schema_M11_PartnerReconciliation.sql`
- Plan 18 (open-item-reconciliation) · Plan 19 (partner-reconciliation-statement)
- `.claude/rules/phase-review-gate.md` · `before-major-change.md` (şema/migration)
