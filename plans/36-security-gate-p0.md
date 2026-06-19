# Plan 36 — Üretim Güvenlik Kapısı (Audit P0/HIGH)

> Tier 3 · Kaynak: `docs/AUDIT_2026-06-19.md` (3-agent doğrulandı) · denetçi sırası #1.
> Kapsam: SALT kod-seviyesi P0/HIGH güvenlik. Config/deployment (P0-4 proxy), migration (P0-3), test altyapısı (H-7) AYRI gate.

## Problem

Doğrulanan üretim bloklayıcılar (hepsi ✅AÇIK, conf≥80): varsayılan prod parola, fresh-install admin kilidi, 2 CompanyId izolasyon açığı, Help path traversal, fail-open authz.

## Scope (fazlar — her faz kapanış kapısı)

- **F1 — Admin bootstrap (P0-1, conf98):** `SeedData.cs` — ADMIN_PASSWORD env oku; **üretimde env yoksa fail-fast** (uygulama açılmasın); parola **loglanmasın** (:131,:58 temizle); dev'de sabit fallback OK ama log'suz. `Program.cs:44-47` parola politikası sertleştir (RequireUppercase=true, RequiredLength≥10, RequireNonAlphanumeric=true). (İlk-giriş-değiştir zorunluluğu = opsiyon, ayrı.)
- **F2 — Fresh-install admin yetki (P0-2, conf95):** `SeedData.cs` admin company claim'iyle ATOMİK `UserCompany(UserId, CompanyId, Role='Administrator', IsActive=1)` INSERT (idempotent). Factory global-rol-silme ↔ UserCompany çelişkisi kapanır.
- **F3 — Help path traversal (P0-6, conf80):** `Help/Index.cshtml.cs` RouteToSlug çıkışına whitelist `^[a-z0-9-]+$` guard + `Path.GetFullPath` ile kök-dizin (`App_Data/manual/screens`) içinde kalma kontrolü. Dışarı → null/NotFound.
- **F4 — Fallback authz (H-1, conf88):** `Program.cs` global `FallbackPolicy = RequireAuthenticatedUser`; Login/Logout/Error/AccessDenied'a açık `[AllowAnonymous]`. Modül/Admin policy üstüne ek kısıt.
- **F5 — 2 izolasyon açığı (P0-5, conf95):** Branches/Details.cshtml.cs:41 Warehouse sorgusuna `AND CompanyId=@CompanyId` · PurchaseInvoices/Details.cshtml.cs:71 PaymentPlan sorgusuna CompanyId predikatı (veya parent header CompanyId ile doğrulanmış → `isolation-guard:ignore` gerekçe). `operax-cli scan-isolation` 0 ihlal doğrula.

**Kapsam dışı (not):** P0-4 proxy (nginx IP config — ops kararı, kod değil) · P0-3 migration fail-closed · H-5 dup SP · H-2 firma-iptali security-stamp · H-3 Error.cshtml · H-4 concurrency. Her biri ayrı faz/plan, AUDIT doc'ta izli.

## Alternatifler (reddedilen)
- **Hepsini tek commit:** reddedildi — F4 fallback authz tüm sayfaları etkiler, izole faz+smoke şart.
- **P0-4'ü de koda almak:** reddedildi — proxy trust deployment kararı (nginx IP), kodda hardcode etmek yanlış katman.

## Riskler
- 🔴 F4 fallback authz **regresyon**: [AllowAnonymous] unutulan açık sayfa (login/static/health) kilitlenir → login öncesi smoke şart.
- 🟡 F1 fail-fast: dev/test ortamında env yoksa fallback ayrımı net olmalı (IsDevelopment guard).
- 🟡 F5: PurchaseInvoices parent zaten CompanyId-doğrulanmışsa ignore-gerekçe; körü körüne predikat eklemek çift-filtre.

## Done Criteria
- F1: prod'da ADMIN_PASSWORD yoksa açılış THROW + log'da parola YOK + politika sert. Dev'de çalışır.
- F2: fresh migrate→seed→ilk login admin TÜM modüllere erişir (UserCompany Administrator var).
- F3: `/Help?ret=..\..\foo` → NotFound; normal slug çalışır.
- F4: anonim `/Dashboard` → login redirect; `/login` açık.
- F5: scan-isolation 0 ihlal.
- Build Web+Cli 0/0. security-reviewer F1/F3/F4 onay. Fresh-install smoke.

## Rollback
Her faz tek commit, `git revert`. F4 en riskli → ayrı commit, smoke sonrası.

## 5 Lens
- 🔴 Contrarian: F4 yanlış yapılırsa login'i de kilitler — fatal; mitigasyon AllowAnonymous + smoke.
- 🔵 First Principles: Asıl açık "varsayılan kimlik üretimde" — F1+F2 birlikte gerçek kapı.
- 🟢 Expansionist: FallbackPolicy + audit kapsam genişletme aynı damar (sonraki).
- ⚪ Outsider: "Prod parola repo'da sabit?" — dışarıdan ilk görülen kırmızı bayrak.
- 🟡 Executor: F1 SeedData env+fail-fast (1 dosya), hemen başlanır.
