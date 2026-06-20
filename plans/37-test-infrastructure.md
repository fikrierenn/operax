# Plan 37 — Test Altyapısı (audit H-7)

> Tier 3 · Kaynak: `docs/AUDIT_2026-06-19.md` H-7 (en büyük sürdürülebilirlik açığı: 0 otomatik test) · auditor sırası #4.

## Problem

Solution'da test projesi YOK. `dotnet test` 0 testle "başarılı" döner. ERP/WMS doğruluğu (ledger, posting, izolasyon, dönem kilidi) hiçbir otomatik testle korunmuyor → her refactor sessiz regresyon riski.

## Scope (fazlı — küçükten büyüğe)

- **Faz 1 — Harness + ilk birim test (BU DİLİM):** `src/Operax.Tests/` xUnit projesi (net10.0), sln'e ekle, `InternalsVisibleTo` ile Web iç sembollerine eriş. İlk **regresyon testi**: P0-6 Help `RouteToSlug` path-traversal whitelist (yaptığım güvenlik fix'ini kilitler). `dotnet test` yeşil.
- **Faz 2 — Saf birim testleri:** UiHelpers.StatusBadge (DocStatus eşleme) · ResolveAdminPassword (env/fail-fast logic, env-bağımsız kısım) · L localization · Guard clause'lar. DB gerektirmez.
- **Faz 3a — DB integration harness ✅ (2026-06-21):** `DatabaseFixture` (ICollectionFixture) — env/Web-config'ten bağlantı çözer, katalog'u `Operax_Test`'e swap eder (aynı sunucu, izole katalog, gerçek DB'ye dokunmaz), drop+create → operax-cli migrate+seed (tek-kaynak sıra, drift yok). İlk 4 test yeşil: tablo varlığı + Plan 35 baseline regresyon (her şirket UOM≥12, Adet=C62) + tvf_InventoryBalance çağrılabilir. **24/24 test yeşil.**
- **Faz 3b — Posting/çift-post ✅ (2026-06-21):** `ReceivingPostingTests` — SYSTEM şirketinde izole (taze GUID) DRAFT mal kabul senaryosu (inline master) → sp_ReceivingPost → tvf_InventoryBalance bakiye=100 ✅; çift-post → SqlException 50000-59999 bandı + bakiye 50'de sabit ✅. **26/26 test yeşil.**
- **Faz 3c — Reversal + izolasyon ✅ (2026-06-21):** `ReceivingPostingTests` genişletildi:
  - **Reversal:** sp_ReceivingReverse → net bakiye 0 (flag-only IsCancelled, ters satır yok → çift-sayım yok) ✅.
  - **CompanyId izolasyon:** SYSTEM'de post edilen kalem DEMO şirketinin tvf_InventoryBalance'ında görünmüyor ✅.
  - **28/28 test yeşil.**
- **Faz 3d — Dönem/UOM (KALAN):**
  - **Dönem kilidi:** LOCKED AccountingPeriod → ledger SP THROW (önce sp_ReceivingPost'un sp_GuardPeriodOpen çağırıp çağırmadığı doğrulanmalı).
  - **UOM dönüşüm / fiyat-vergi:** fn_GetConversionRate + variance.
- **Faz 4 — Auth/role/company-switch:** DapperUserStore concurrency (H-4 regresyon), UserCompany IsActive guard (H-2), fallback authz (H-1).

## Alternatifler (reddedilen)
- **Mock IDbConnection ile PageModel unit test:** reddedildi — SQL-first mimaride iş mantığı SP'de; mock SP davranışını test etmez. Integration test (gerçek SQL) gerekli.
- **Testleri Web projesine gömme:** reddedildi — ayrı proje (bağımlılık izolasyonu + CI ayrı çalıştırma).

## Riskler
- 🟡 DB integration testleri `Operax_Test` ister (gerçek dev DB'ye dokunma — `OPERAX_CONN` env yönlendir). Faz 3 öncesi izole DB stratejisi netleşmeli.
- 🟡 `InternalsVisibleTo` Web iç API'sini test'e açar — yalnız test için, public yüzey büyütmez.

## Done Criteria
- Faz 1: `dotnet test` ≥1 gerçek test yeşil; RouteToSlug traversal regresyon testi (`..\`, `:`, normal slug) geçer.
- CI'da `dotnet test` exit 0 + test sayısı raporlanır (artık "0 test başarılı" yanılgısı yok).

## Rollback
Yeni proje + sln girişi; sorun olursa `dotnet sln remove` + klasör sil. Mevcut koda dokunmaz (yalnız RouteToSlug private→internal, davranış aynı).

## 5 Lens
- 🔴 Contrarian: Mock'lu PageModel testi yanlış güven verir (SP test edilmez) — integration şart.
- 🔵 First Principles: "0 test" en büyük açık; ilk gerçek test harness'ı kanıtlar, gerisi ekler.
- 🟢 Expansionist: Harness kurulunca her audit-fix (H-1/2/4, P0-3/6) regresyon testi alır.
- ⚪ Outsider: "ERP'de hiç test yok?" — ilk bakışta en şaşırtıcı.
- 🟡 Executor: Faz 1 — csproj + sln add + 1 test + dotnet test yeşil.
