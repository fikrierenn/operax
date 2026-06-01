# OPERAX Platform — Modül & Ekran Bazlı TODO

---

## 🧾 REVIEW BORÇLARI (2026-06-01 — /review-pr, plan 21/25 sonrası — pre-existing, kapsam-dışı)

> 3 paralel agent. Plan 21 (N:1 fatura) + CONSUMABLE filtre kodu TEMİZ; aşağıdakiler dokunmadığım
> pre-existing kod (drive-by yasağı → ayrı borç). 2 CRITICAL silent-failure bu oturumda kapatıldı (`8...`).

- [ ] **MED · SP catch üst-sınır:** `SalesOrders/Details.cshtml.cs:220,255,327` + `Shipping/Details.cshtml.cs:199,222,246` — `sqlEx.Number >= 50000` üst-sınırsız; 60000+ teknik THROW kullanıcıya `ex.Message` sızar. `< 60000` ekle (referans: `SalesInvoices/Create.cshtml.cs:91`).
- [ ] **HIGH · hata yönetimi yok:** `SalesOrders/Details.cshtml.cs` `OnPostAsync` (kaydet, NumberSeries+INSERT korumasız) + `OnPostAddLineAsync` (INSERT+audit try/catch yok). Approve/Cancel pattern'ini uygula.
- [ ] **HIGH · audit izolasyonu DOĞRULANMADI:** `IAuditService.LogAsync` caller'ı patlatıyor mu? İzole değilse audit hatası satır eklemeyi geçersiz kılar. Kaynak okunmalı.
- [ ] **HIGH · inline style:** `SalesInvoices/Index.cshtml:96` `style="color:@dueColor;"` Razor expression — class'a çevir (`text-danger`/`text-2`). "Touch ettikçe azalt".
- [ ] **MED · magic string:** `SalesOrders/Details.cshtml.cs:42` `'CUSTOMER'`/`'BOTH'` — `Dtos.cs`'e `PartnerType` sabiti ekle.
- [ ] **MED · CancellationToken:** handler'lar `ct` almıyor, `OperationCanceledException` rethrow yok (`error-handling.md §4-5`). Disiplin borcu.

---

## 🚨 F0.1 CODE REVIEW SONUÇLARI (2026-05-30 — sprint öncesi, canlı koddan doğrulandı)

> code-reviewer (sonnet) + security-reviewer (opus) paralel. file:line kanıtlı. Stale maddeler kapalı.

### 🔴 YENİ KRİTİK — GÜVENLİK
- [x] **SEC-1 · appsettings DB şifresi** ✅ KAPALI 2026-05-31 — appsettings.json artık `(localdb)` Trusted_Connection (plain şifre yok); gerçek bağlantı User Secrets'ta; git history scrub + force-push yapıldı. (sa rotate düşük-risk borç olarak duruyor — lokal+private)
- [x] **SEC-2 · switch-company yetki** ✅ KAPALI 2026-05-31 — Program.cs UserCompany erişim kontrolü + `.RequireAuthorization()` + (F0.2) `.RequireRateLimiting`.
- [x] **SEC-3 · Cookie flag** ✅ KAPALI 2026-05-31 — HttpOnly + SameSite=Strict + SecurePolicy ortam-koşullu (prod Always).

### 🔴 AÇIK — CRIT/HIGH
- [x] **HIGH-1 · SP THROW kod aralığı** ✅ KAPALI 2026-05-31 (canlı kod doğrulandı) — 12 SP-çağıran handler'ın hepsi `catch when (sex.Number >= 50000)` (üst sınır YOK) → 60001-72001 SP hataları kullanıcıya ULAŞIYOR. `< 60000` bounded catch app kodunda yok. Kalıntı: 3 Create handler (Accounts/Cheques/CreditCards Create) generic SqlException catch → SP Türkçe mesajını yutar ama basit INSERT, business-THROW yok, düşük etki.
- [x] **HIGH-2 · SO Approve/Cancel direct UPDATE** ✅ KAPALI 2026-05-31 — Approve(171-175)+Cancel(206-210) UPDATE'ten ÖNCE `sp_ValidateStatusTransition` çağırıyor, `>= 50000` catch SP mesajını gösteriyor. Bypass yok.
- [x] **CRIT-4 · ILogger kullanılmıyordu** ✅ KAPALI 2026-05-31 — 10 dosyada enjekte ama boşta olan logger, veri-yükleme metotları `try/catch(SqlException sqlEx)` ile sarılıp `logger.LogError` + TempData ile bağlandı (silent failure yok). SalesOrders/Index'te logger gerçekten yoktu, eklendi. Build 10→0 uyarı.
- [x] **CRIT-3 · magic string** ✅ KAPALI 2026-05-31 — 38 SQL-gömülü status literali (14 dosya) DocStatus parametresine çevrildi (`@StDraft/@StPosted/@StCancelled/@StApproved`). Bonus: SalesOrders/Index + PurchaseOrders/Index `$"...{DocStatus.X}..."` string-concat'leri de parametrik yapıldı. Build yeşil, davranış korundu. KALAN (yeni borç): Picking/Terminal `'ASSIGNED'`/`'IN_PROGRESS'` — DocStatus'ta yok, ayrı `PickTaskStatus` sabit sınıfı gerek.
- [x] **CRIT-3b · Picking status literalleri** ✅ KAPALI 2026-05-31 — DocStatus zaten `Assigned/InProgress/Completed/Draft` içeriyormuş (yeni sınıf gerekmedi). Picking/Terminal:27,94,103 `@StDraft/@StAssigned/@StInProgress/@StCompleted` parametreleştirildi.
- [x] **IMP-2 · sync ExecuteScalar** ✅ KAPALI 2026-05-31 — Receiving/Details:87, Shipping/Details:87 zaten `ExecuteScalarAsync` (async).
- [x] **Türkçe yorum eksiği** ✅ KAPALI 2026-05-31 — SalesOrders/Details(6 metot)+Index, Finance/Accounts/Aging/PaymentPlan Index, SalesInvoices Index+Details metot başlarına Türkçe açıklama eklendi.

### ✅ KAPALI (stale)
CRIT-1 (SP catch) · CRIT-2 (XSS SubHtml+HtmlEncode) · IMP-1 (Cheques→sabit blok) · IMP-3 (vade→PaymentTermDays) · AR-001 (CompanyId filtre var) · DataTable.Compute(0) · IDOR(filtreli) · mass assignment(override güvenli).

### F0.1 ÖNCELİK: 1) SEC-1 şifre 2) HIGH-1 SP kod 3) HIGH-2 SO bypass 4) CRIT-4 ILogger 5) SEC-2/3 6) CRIT-3+IMP-2+yorum
> ⚠️ code-reviewer agent PowerShell deny'i Bash'le aştı (harness uyarısı) — çıktı salt-okuma zararsız.

> ✅ F0.1 DURUMU (2026-05-30): SEC-1 ✅ HIGH-1 ✅ HIGH-2 ✅ CRIT-4 ✅ SEC-2 ✅ SEC-3 ✅ CRIT-3 ✅ IMP-2 ✅

## 🔒 PLAN 12 — MULTI-COMPANY İZOLASYON GUARD (2026-05-31 — uygulandı)
- [x] **scan-isolation guard** — `operax-cli scan-isolation` (`src/Operax.Cli/IsolationScanner.cs`); 52 doğrudan + 27 dolaylı company-scoped tablo envanteri. CompanyId predikatsız ham sorgu → exit 1.
- [x] **İhlal sweep** — 55 aday → 2 gerçek fix (AutoTraceability, Transfer/Replenishment) + 33 suppress (defense-in-depth, kendini-açıklayan yorum) + Production/Terminal gerçek fix (order firma guard).
- [x] **Production/Terminal izolasyon açığı** ✅ KAPALI — OnPostStart firma-doğrulama guard'ı + ProductionOrder UPDATE CompanyId + bare-catch→logger.
- [ ] **DEAD/WIP production servisleri** (ProductionReceiptService, ProductionActivityService, DynamicBomService) — caller/DI yok, kırık StockMovement INSERT'leri (canlı VT'de `Qty`/`ReferenceId` kolonu yok, NOT NULL CompanyId/UomId/QtyOriginal eksik). Şimdilik guard'da `isolation-guard:ignore` (dead/WIP gerekçesi) ile bastırıldı. **Karar ertelendi:** WIP atölye terminali feature'ı planlanırsa SP'ye taşınarak (`sp_ProductionConsume`/`sp_ProductionInspect`) yeniden yazılmalı; planlanmazsa silinmeli. Architecture §4: ham C# stok insert yasak, SP zorunlu.
- [ ] **Sprint-kapanış (plan 12 §4):** dead servisler çözülünce guard 0 → blocking hook'a bağla (pre-commit/session-start).

---

## 🔐 F0.2 PRODUCTION GÜVENLİK SERTLEŞTİRMESİ (2026-05-30 — internet yayına hazırlık)

> ✅ **TAMAMLANDI 2026-05-31** (plan 17) — RL-1, SH-1, RB-1/2/3 (DB-driven RoleModuleAccess + Admin/Roles UI), AP-1/2, TLS-1/3 uygulandı, build yeşil, migrate+seed DB'de doğrulandı.
> ⚠️ **NOT — RBAC modeli değişti:** RB-3 hardcoded `[Authorize(Roles=)]` yerine **DB-driven** `RoleModuleAccess` tablosu + custom handler + AuthorizeFolder oldu (roller dinamik kalsın diye). TLS-2 (dev https) reddedildi — dev http korundu. Runtime smoke testi opsiyonel kaldı.
>
> Sistem internet ortamında herkese açık hale gelecek. Aşağıdaki katmanlar eksik/yetersiz.
> Kaynak: bu oturum güvenlik analizi (2026-05-30 22:19).

### Öncelik Sırası

**1. 🔴 Rate Limiting — Brute-Force Koruması**
- [ ] **RL-1** `Program.cs` — ASP.NET Core `RateLimiter` middleware ekle
  - `/Auth/Login` POST: IP başına 10 istek/dakika (5 dk bekleme)
  - `/api/switch-company`: Kullanıcı başına 10 istek/dakika
  - Global fallback: IP başına 200 istek/dakika
  - Identity'nin 5-deneme kilidi IP değiştirilince bypass ediliyor → bu onu kapatır

**2. 🔴 HTTP Security Headers**
- [ ] **SH-1** `Lib/SecurityHeadersMiddleware.cs` — Yeni middleware oluştur, `app.Use()` ile ekle:
  - `X-Content-Type-Options: nosniff` — MIME sniffing koruması
  - `X-Frame-Options: SAMEORIGIN` — Clickjacking koruması
  - `X-XSS-Protection: 1; mode=block` — Eski tarayıcı XSS filtresi
  - `Referrer-Policy: strict-origin-when-cross-origin` — URL sızıntısı önleme
  - `Permissions-Policy: camera=(), microphone=(), geolocation=()` — İzin kısıtlaması
  - `Content-Security-Policy` — script/style kaynaklarını whitelist'le

**3. 🟡 Rol Tabanlı Yetkilendirme**
- [ ] **RB-1** `Lib/Roles.cs` — Rol sabitleri static class (magic string yasak kuralı gereği)
- [ ] **RB-2** `Lib/SeedData.cs` — 7 rol seed et: Administrator, WarehouseManager, Finance, Purchasing, Sales, Manufacturing, Viewer
- [ ] **RB-3** Modül → Rol eşleştirmesi (tüm `[Authorize]` attr'ları güncelle):
  - `Administrator` → Her şey
  - `WarehouseManager` → Receiving, Shipping, Transfer, Inventory, CycleCount, LPN, Lot, Picking
  - `Finance` → Payments, Loans, CreditCards, Cheques, Accounts, Aging, PaymentPlan, Snapshot
  - `Purchasing` → PurchaseOrders, Expenses, Budget
  - `Sales` → SalesOrders, SalesInvoices, Incentives, Sales
  - `Manufacturing` → Manufacturing, Production, BOM, WorkOrders, WorkCenters
  - `Viewer` → Dashboard + MasterData (Items, Partners) — salt okuma
  - `Admin` modülü → sadece `Administrator`

**4. 🟡 Admin Panel Sıkılaştırması**
- [ ] **AP-1** `Program.cs` — Hangfire dashboard `RequireAuthorization()` → `RequireAuthorization("AdministratorOnly")` policy ekle
- [ ] **AP-2** `Features/Admin/**` — `[Authorize]` → `[Authorize(Roles = Roles.Administrator)]`

**5. 🟢 HTTPS & TLS**
- [ ] **TLS-1** `Program.cs` — `CookieSecurePolicy.SameAsRequest` → `CookieSecurePolicy.Always`
- [ ] **TLS-2** `Program.cs` — HTTPS redirection tüm ortamlarda aktif (dev dahil)
- [ ] **TLS-3** `appsettings.json` HSTS süresi 30 gün → 1 yıl (production)

### Tasarım Kararları (onay bekliyor)
- Rol eşleştirme tablosu yukarıdaki gibi mi? Değişiklik var mı?
- Bir kullanıcı birden fazla rol alabilir (örn. hem Finance hem Purchasing)
- CSP whitelist: Google Fonts + kendi JS/CSS → onaylı

### Tahmini Süre
RL-1 + SH-1: ~45 dk | RB-1/2/3: ~60 dk | AP + TLS: ~15 dk | **Toplam: ~2 saat**

---

## 📌 KARARLAR & GELECEK-İŞ (2026-05-30 — defter/muhasebe stratejisi)

> Kaynak: `docs/VISION.md` §7.7 + `docs/reference/REFERENCE_STUDY.md` §7 (K1–K7) + `docs/BUGS.md` AR-005/006/008/009.

**Öncelik sırası (onaylı kararlar):**
1. [ ] **B1 — plan 12** Multi-company izolasyon (TVF `@CompanyId`-sargı + analyzer guard). Existential, ucuz.
2. [ ] **plan 14 paketi (aynı omurga):** immutability (B2: AccountMovement IsDeleted→reversal) + **dönem kontrolü (B12, K4: AccountingPeriod firma-bazlı + sp_GuardPeriodOpen + trigger + OPEN/CLOSED/LOCKED — sadece mekanizma)** + clustered PK (B4, R4). ⚠️ Ön koşul: sys.indexes ile clustered+NEWID + IX_StockMovement teyidi.
3. [ ] **B3 — plan 16** Hafif cari besleme (onay SP'leri AccountMovement'a atomik borç/alacak). **KEBİR/COGS/SRBNB/GL YOK** (K3).
4. [ ] **B5 — FIFO** "İleri"→**Gerekli** (K7). Snapshot'sız SP içi kuyruk (ERPNext stock_queue deseni); ayrı CostLayer tablosu yok. Roadmap.
5. [ ] Dolgu: B6 (Available vs Allocated stok), B8 (lokasyon IsReceivable/IsPickable), B9 (slice audit).
6. [ ] **Ertele:** B10 (ASN), B11 (Decision/routing katmanı), **B13 sayım freeze (K5 — M08/S7, satır bazlı; spec yazıldı `docs/MODULE_SPECS/M08_CycleCount_Freeze.md`)**, **Periyodik GL muhasebeleştirme modülü.**

**GELECEK-İŞ (plan AÇILMADI — sadece kayıt):**
- [ ] **Periyodik GL muhasebeleştirme modülü** — subledger→GL aylık/seçimli posting (K1). **Ön koşul: muhasebe-mevzuat skill'i** (VUK / e-Defter tebliğleri / hesap planı standardı / berat / GİB formatları). e-Defter ÜRETİMİ kapsam dışı (K5 — Operax sadece LOCKED döneme saygı gösterir). **Posting-rule deseni netleşti** (Mikro §3.5, `docs/reference/MIKRO_V16_ANALYSIS.md`): 3 yapı taşı = HesapPlani + PostingRule(grup+hareket tipi→hesap kodu, normalize) + masraf merkezi boyutu; muhasebeleştirme SP subledger hareketini grup+yön→hesap eşleyip işaretli meblağla fişe yazar, `fis_ticari_uid` ile geri-bağlar.

**EKSİK EVRAK TİPLERİ (B17 — Mikro karşılaştırma, `docs/reference/MIKRO_V16_ANALYSIS.md` §12):**
- [ ] **En yüksek 4 (üretilmeli):** E1 irsaliye↔fatura ayrımı+dönüşüm (VUK) · E2 alış/satış iade (ayrı belge+ters-kayıt) · E4 fire/zayi/imha (maliyet+vergi) · E11 virman kasa↔kasa/cari↔cari (Plan 11 başlamadı).
- [ ] **Orta:** E5 sayım fazla/eksik · E7 stok açılış/devir · E12 vade farkı/borç-alacak dekontu.
- [ ] **Çek statü gap:** TEMİNAT (sck_sonpoz=3) + KISMİ ÖDEME (=9) → Cheque statü makinesine + document-immutability §2.4.
- [ ] **Çözüm deseni (§0.5 uyumlu — yeni ledger tablosu AÇMA):** SourceDocType kataloğu genişlet (RETURN_IN/OUT, WASTE, OPENING_STOCK…) + ADJUST sebep kodu (AdjustReason) + belge zinciri (Header/Line: irsaliye/iade/dekont/virman).
- [ ] Mikro tam enum (`sth_cins` 14/15, `cha_evrak_tip` 51-137) DOĞRULANMADI — gerekirse resmi DDL'den kesinleştir.

**İPTAL (K6):** B7 SLE-snapshot kolonları (QtyAfterTransaction/ValuationRate/StockValue) — eklenmeyecek. `SUM(QtyBase) WHERE IsCancelled=0` kalıcı.

**⚡ PERFORMANS BORCU (K6):** snapshot yok → bakiye SUM'unun tek dayanağı index. `IX_StockMovement_Company_Item_Date` basılı mı + `vw_InventoryBalance` bunu kullanıyor mu (exec plan) → plan 14 ön koşulunda teyit. Bu index'ler gevşetilemez/silinemez.

---

## 🚨 KOD REVIEW BULGULARI (2026-05-28 — 3 paralel agent: code-reviewer + security-reviewer + silent-failure-hunter)

> ⛔ **SUPERSEDED 2026-05-31** — Bu blok F0.1 (2026-05-30, satır 5-30) + bu oturum çalışmasıyla MÜKERRER. Canlı kod doğrulandı: CRIT-1 (PO/Details:218,247 SP catch+sp_ValidateStatusTransition ✅), CRIT-2 (_PageHeader Raw yok ✅), CRIT-3 (38 literal→param ✅), CRIT-4 (logger bağlandı ✅), HIGH-1 (catch >=50000 açık ✅), HIGH-2 (SP validate var ✅), IMP-1 (Cheques interpolation yok ✅), IMP-2 (async ✅), IMP-3 (PaymentTermDays ✅). Aşağıdaki maddeler TARİHSEL — yeni iş için F0.1/üst bölümlere bak.

Kapsam: HEAD~10..HEAD + uncommitted. Tüm bulgular `.claude/rules/todo-verification.md` kuralı gereği fix öncesi canlı koddan doğrulanmalı.

### CRITICAL — Plan 02 adayı (Fix Sprint)

- [ ] **CRIT-1 · SP THROW handler catch eksik** — PO/Details.cshtml.cs:166-179 (Approve), Receiving/Details.cshtml.cs:142-151 (Post), Shipping/Details.cshtml.cs:175-194 (CreatePickTask + Post), PO/Details.cshtml.cs:181-190 (Cancel — SP bypass).
  - **Etki:** SP Türkçe iş kuralı mesajları (stok yetersiz, durum geçişi, vade plan) user'a 500 sayfası olarak iletiliyor, mesaj kayboluyor.
  - **Fix:** `catch (SqlException sex) when (sex.Number is >= 50000 and < 60000) { TempData["Error"] = sex.Message; return RedirectToPage(new { id }); }`
  - **Kural:** `.claude/rules/error-handling.md` "SP'lerden Gelen THROW"

- [ ] **CRIT-2 · XSS — `_PageHeader.Sub` `@Html.Raw` + ham PartnerName interpolation** — Shared/_PageHeader.cshtml:33,38 + SalesInvoices/Details.cshtml:13, PO/Details.cshtml:46, SO/Details.cshtml:44.
  - **Etki:** Partner.Name'de `<script>` → stored XSS tüm detay sayfalarında.
  - **Fix:** PageHeaderVm'e `SubHtml` (raw) ayrı property + `Sub` encode-safe; veya partial'da `Html.Encode(Model.Sub)` + `Html.Raw(Model.SubHtml)` ayrımı. PartnerName her zaman encode'lu.
  - **Kural:** `.claude/rules/security-principles.md` §2 XSS

- [ ] **CRIT-3 · Magic string `"APPROVED"` + SQL `IN ('POSTED','APPROVED')`** — SO/Details.cshtml.cs:164, PO/Index.cshtml.cs:41,80, SO/Index.cshtml.cs paralel.
  - **Fix:** `DocStatus.Posted` sabit kullan; SQL'de IN listesi `Dtos.cs`'te `DocStatus.PostedAliases = ["POSTED","APPROVED"]` array'i + parametre.
  - **Kural:** `.claude/rules/architecture.md` §3 Magic String Yasağı

- [ ] **CRIT-4 · `ILogger<T>` DI eksik** — Tüm yeni PageModel'ler: Finance/Accounts/Index+Details, Finance/Aging/Index, Finance/Cheques/Index, Finance/CreditCards/Index, Finance/Loans/Index, Finance/PaymentPlan/Index, SalesInvoices/Index+Details, PurchaseOrders/Index+Details, SalesOrders/Index+Details.
  - **Etki:** SqlException sızar, log yok, production debug imkansız.
  - **Fix:** Primary ctor `(Db db, ICurrentCompany company, ILogger<XModel> log)` + catch'lerde `log.LogError(sex, "...")`.
  - **Kural:** `.claude/rules/csharp-conventions.md` Exception Handling

### HIGH — sonraki sprint

- [ ] **HIGH-1 · SP THROW kod aralığı kural dışı** — db_objects_starter.sql 60001-72001 aralığı kullanmış; kural 50000-59999.
  - **Fix:** Modül slot tahsisi: M02: 51000-51099, M03: 51100-51199, M04: 51200-51299, M11: 51300-51399. `Lib/Errors.cs`'e kayıt sözleşmesi tablo.

- [ ] **HIGH-2 · PO/SO Cancel direct UPDATE — `sp_ValidateStatusTransition` bypass** — PO/Details.cshtml.cs:181-190, SO/Details Cancel.
  - **Etki:** yetkisiz geçişe açık (POSTED → DRAFT vb.).
  - **Fix:** `sp_PoCancel`, `sp_SoCancel` SP'leri yaz, içlerinde `sp_ValidateStatusTransition` çağır + ters stok hareketi.

### IMPORTANT — biriktir

- [ ] **IMP-1 · Tablo adı SQL string interpolation** — Cheques/Index.cshtml.cs:37-52 `$"... FROM {table} ..."`. Whitelist'li injection yok ama kural ihlali. İki ayrı sabit SQL bloğu yaz.

- [ ] **IMP-2 · Sync `ExecuteScalar<int>` async handler** — PO/Details.cshtml.cs:119, SO/Details.cshtml.cs:113. → `ExecuteScalarAsync<int>`.

- [ ] **IMP-3 · Hardcoded 14-gün vade** — PO/Index.cshtml.cs:66 `DATEADD(DAY, 14, h.OrderDate)`, PO/Details.cshtml:114 Razor sabit. `Partner.PaymentTermDays` kolonu mevcut (`schema_M01_M04_StarterFields.sql`) — SQL ve UI'da bunu kullan.

- [x] ✅ KAPALI 2026-05-29 — **IMP-4 · `Guid.ToString()[..8]` substring** — `UiHelpers.ShortGuid(g)` eklendi, 4 kullanım birleşti (SalesInvoices/Details, Payments/Create). Commit 480e511.

- [x] ✅ KAPALI 2026-05-29 — **IMP-5 · View'larda CompanyId filtresi eksik** — `v_AccountBalance`/`v_PaymentPlanAging` → `tvf_AccountBalance(@CompanyId)`/`tvf_PaymentPlanAging(@CompanyId)` inline TVF'e dönüştü. 4 çağıran güncellendi, migrate ok.

- [x] ✅ KAPALI 2026-05-29 — **IMP-6 · `ActionLabel` switch DRY ihlali** — `UiHelpers.AuditActionLabel(string)` ortak helper'a taşındı, PO/SO Details kullanıyor.

- [x] ✅ KAPALI 2026-05-29 — **IMP-7 · `'Sistem'` magic SQL içinde** — SQL `NULLIF(a.UserName,'')` NULL döndürüyor, view `?? L.T("Sistem","System")` fallback uyguluyor.

### FEATURE — Cari Ekstre Raporu (Tier 3 — plan gerekli)

- [ ] **FEAT-EKSTRE · Cari Ekstre raporu** — `Partners/Statement/{id}` yazdırılabilir + tarih filtreli ekran (Plan 02'de Faz 6 sayılmıştı ama plan dosyasında yok — ayrı Tier 3 plan yazılacak).
  - **İçerik:** FinancialTransaction tabanlı hareket dökümü, açılış bakiye + yürüyen bakiye, tarih aralığı filtresi, yazdırma.
  - **Toggle: "Açık siparişleri dahil et"** — varsayılan KAPALI. Sipariş ledger'da olmaz, bakiyeyi etkilemez ([[open-orders-not-in-ledger]]). Seçilirse rapora **bilgi amaçlı ayrı blok/satır** olarak açık SO/PO eklenir (yürüyen bakiyeye karışmaz, projeksiyon olarak ayrı gösterilir). Kapalıyken tamamen dışarıda.
  - **Hesap kaynağı:** açık sipariş = `(QtyOrdered − QtySevk/Kabul) × Price`, Status IN (APPROVED, POSTED) — Aging/Partners ile aynı formül (tek yerden helper/TVF).

### FEATURE — Cari Kart Tablı Yapı (Plan 08, onaylı 2026-05-29)

- [ ] **TAB-0 · Tab kabuğu + Genel + sorumlu temsilci** — `?tab=` lazy-load, `ALTER Partner ADD SalesRepUserId/PurchaseRepUserId` (FK AspNetUsers), Genel form + Ekstre tabı partial'a taşı.
- [ ] **TAB-1 · Okuma tabları** — Siparişler (SO/PO), Fiyatlar (PriceList), Faturalar (Sales/ExpenseInvoice), Çek/Senet — hepsi PartnerId.
- [ ] **TAB-2 · Yeni tablo + CRUD** — PartnerContact, PartnerAddress, PartnerBankAccount, PartnerActivity (CRM notu) → İlgili Kişiler/Adresler/Banka/Görüşme tabları.
- [ ] **TAB-3 · İstatistik** — aylık ciro grafiği (Chart.js), top alınan/satılan ürün, risk & limit doluluk barı.
- [ ] **TAB-4 · Cleanup** — 500-satır split, journal/TODO, plan arşivle.
- _İleride:_ temsilci bazlı satır-yetki (kendi carisi). _Kapsam dışı:_ Belgeler/Ekler (ayrı mini-plan).

### POSITIVE — koruyalım

- ✅ Tek CSS katmanı (`parts/` parçalı) — ui-standard.md uygulanmış
- ✅ `L.T("tr","en")` tüm yeni sayfalarda tutarlı
- ✅ `<div class="page" data-screen-label>` + `_PageHeader` her sayfa
- ✅ SP `SET XACT_ABORT ON` + BEGIN TRY/CATCH + Türkçe THROW (db_objects_starter)
- ✅ CompanyId filtresi Dapper sorgularında tutarlı
- ✅ AntiForgery temiz (Razor Pages default), `ex.Message` user'a sızıntı YOK

### Inline Style Refactor (ayrı kategori — Plan 03 adayı)

- [x] **STYLE-1 · Inline style flood** ✅ KAPALI 2026-05-31 — `parts/_docdetail.css` açıldı (.doc-val/.doc-sub/.doc-total/.doc-grand/.doc-foot-note/.activity-*/.btn-danger-outline + uuid/empty/sub-item). PO/Details 44→23, SalesInvoices/Details 30→~8, Cheques/Index L93. Kalan inline'lar layout/size/data-driven (guard-izinli). Tüm görsel (color/font/border) inline kaldırıldı. Tailwind rebuild + preview'da PO + SalesInvoices görsel doğrulandı (regresyon yok). Bonus: 2 cshtml magic string (`"POSTED"/"APPROVED"`→DocStatus).
  - **Not (gelecek):** proje geneli kalan ~330 inline (diğer ekranlar) — aynı `.doc-*` pattern ile aşamalı temizlenebilir.

---

> Kaynak: OPERAX_Platform_Master_Document_v2_2_TR.docx  
> Sürüm: v2.2-TR | Güncelleme: Mart 2026  
> Format: `[ ]` yapılacak · `[/]` devam ediyor · `[x]` tamamlandı

---

## Genel Notlar

- Her belge DRAFT → POSTED → CANCELLED yaşam döngüsünü izler.
- Tüm stok etkileri StockMovement (Inventory Ledger) üzerine yazılır.
- Hard-code yasağı: durum/tip/birim tanımları DictionaryValue.Code üzerinden gelir.
- Base UOM her zaman EACH'tir; barkod PACK/CASE okunsa bile QtyBase EACH olarak hesaplanır.
- Schema: İngilizce PascalCase | Code: İngilizce sabit | UI: Türkçe NameTr/NameEn

---

## M00 — Platform Core

> Bağımlılık: Yok — Tüm modüllerin temeli

---

### Ekran: Şirket Yönetimi (`/admin/companies`)

**Amaç:** Multi-company yapısını yönetir.

**Liste Görünümü**
- [x] Sütunlar: Code, Name, IsActive, CreatedAt
- [x] Filtreler: IsActive
- [x] Aksiyonlar: Yeni Ekle, Düzenle, Pasif Et

**Detay / Form**
- [x] Alan: `Code` — zorunlu, unique, max 20 karakter
- [x] Alan: `Name` — zorunlu, max 200 karakter
- [x] Alan: `IsActive` — toggle, default true
- [x] Alan: `Address`, `Phone`, `TaxNo` — opsiyonel
- [x] Kaydet / İptal aksiyonları

---

### Ekran: Kullanıcı Yönetimi (`/admin/users`)

**Amaç:** Sistem kullanıcılarını ve şirkete atamalarını yönetir.

**Liste Görünümü**
- [x] Sütunlar: Username, FullName, Email, IsActive, Roller
- [x] Filtreler: IsActive, Rol, Şirket
- [x] Aksiyonlar: Yeni Kullanıcı, Düzenle, Şifre Sıfırla, Pasif Et

**Detay / Form**
- [x] Alan: `Username` — zorunlu, unique
- [x] Alan: `FullName` — zorunlu
- [x] Alan: `Email` — zorunlu, unique, email formatı
- [x] Alan: `Password` — ilk oluşturmada zorunlu (hash'li saklanır)
- [x] Alan: `IsActive` — toggle
- [x] Bölüm: Rol Ataması — CompanyId + RoleId çoklu seçim
- [x] Validasyon: En az 1 şirket + rol ataması zorunlu

---

### Ekran: Rol Yönetimi (`/admin/roles`)

**Amaç:** Roller ve izin gruplarını tanımlar.

**Liste Görünümü**
- [x] Sütunlar: Code, NameTr, NameEn, IsActive
- [x] Aksiyonlar: Yeni Rol, Düzenle, Pasif Et

**Detay / Form**
- [x] Alan: `Code` — zorunlu, unique
- [x] Alan: `NameTr`, `NameEn` — zorunlu
- [x] Bölüm: İzinler (Permission) — modül + ekran + okuma/yazma/silme matrisi
- [x] Validasyon: Code değiştirilemez (seed roller korunmalı)

---

### Ekran: Sözlük Yönetimi (`/admin/dictionary`)

**Amaç:** Hard-code yasağının merkezi — tüm tip/durum/birim tanımları.

**DictionaryType Listesi**
- [x] Sütunlar: Code, NameTr, NameEn, IsActive, Değer Sayısı
- [x] Aksiyonlar: Yeni Tip, Düzenle

**DictionaryValue Listesi (Tip seçiliyken)**
- [x] Sütunlar: Code, NameTr, NameEn, SortNo, IsActive
- [x] Aksiyonlar: Yeni Değer, Düzenle, Sıra Değiştir
- [x] Validasyon: Seed değerler (DRAFT, POSTED, EACH vb.) silinemez / Code değiştirilemez

---

### Ekran: Parametre Yönetimi (`/admin/parameters`)

**Amaç:** Modül davranışlarını şirket bazlı ayarlar.

**Liste Görünümü**
- [x] Sütunlar: Şirket, Modül, Key, Value, Açıklama
- [x] Filtreler: Şirket, Modül

**Detay / Form**
- [x] Alan: `CompanyId` — zorunlu, dropdown
- [x] Alan: `ModuleCode` — zorunlu, dropdown (aktif modüller)
- [x] Alan: `Key` — zorunlu (örn: InvoiceMode, AllocationStrategy)
- [x] Alan: `Value` — zorunlu, tip bazlı input (text/number/bool/enum)
- [x] Alan: `Description` — opsiyonel açıklama

**Kritik Parametreler (seed olarak gelir)**
- [x] `InvoiceMode`: INSTANT / EOD_FROM_SHIPMENT
- [x] `AllocationStrategy`: FIFO / FEFO / NEAREST_BIN
- [x] `CountTolerance`: sayısal (sayım tolerans yüzdesi)
- [x] `RequireBinScan`: true/false
- [x] `FEFOEnabled`: true/false
- [x] `SLAHoursDefault`: sayısal
- [x] `CostVisibility`: true/false (portal)
- [x] `LicenseMode`: trial/active/expired

---

### Ekran: Modül Aktivasyonu (`/admin/modules`)

**Amaç:** Şirket bazlı modül aktif/pasif etme.

**Liste Görünümü**
- [x] Sütunlar: ModuleCode, ModuleName, Bağımlılıklar, Aktif mi?
- [x] Aksiyonlar: Aktif Et, Pasif Et

**Aktif Etme Kuralı**
- [x] Bağımlı modüller aktif değilse hata göster (örn: M06 için M05 aktif olmalı)
- [x] Bağımlılıkları otomatik aktif etme teklifi (onay dialogu)

---

### Ekran: Durum Geçişleri (`/admin/status-transitions`)

**Amaç:** Belge bazlı durum makinesi kuralları.

**Liste Görünümü**
- [x] Sütunlar: DocumentType, FromStatus, ToStatus, Rol, RuleCode
- [x] Filtreler: DocumentType

**Detay / Form**
- [x] Alan: `DocumentTypeCode` — dropdown (RECEIVING, SALES_ORDER, SHIPMENT…)
- [x] Alan: `FromStatusCode` — dropdown (DictionaryValue)
- [x] Alan: `ToStatusCode` — dropdown (DictionaryValue)
- [x] Alan: `RoleId` — hangi rol bu geçişi yapabilir
- [x] Alan: `RuleCode` — opsiyonel ek kural kodu

---

### Ekran: Denetim İzi (`/admin/audit-log`)

**Amaç:** Kim, ne zaman, neyi değiştirdi?

**Liste Görünümü**
- [x] Sütunlar: Tarih, Kullanıcı, İşlem (INSERT/UPDATE/DELETE), Tablo, RecordId, Özet
- [x] Filtreler: Tarih aralığı, Kullanıcı, Tablo
- [x] Aksiyonlar: Detay Görüntüle (before/after JSON karşılaştırması)
- [x] Not: Sadece okuma; düzenleme/silme yok

---

## M01 — Master Data

> Bağımlılık: M00

---

### Ekran: Ürün (Item) Listesi & Detayı (`/master/items`)

**Liste Görünümü**
- [x] Sütunlar: SKU, NameTr, BaseUOM, IsActive, Stok (anlık bakiye özeti)
- [x] Filtreler: IsActive, Kategori
- [x] Aksiyonlar: Yeni Ürün, Düzenle, Pasif Et
- [x] Arama: SKU veya Ad ile

**Detay / Form — Genel**
- [x] Alan: `SKU` — zorunlu, unique, sistem otomatik veya manuel
- [x] Alan: `NameTr`, `NameEn` — zorunlu
- [x] Alan: `BaseUOM` — sabit: EACH (değiştirilemez, tooltip ile açıklanır)
- [x] Alan: `Category` — opsiyonel, DictionaryValue
- [x] Alan: `IsActive` — toggle

**Detay / Form — UOM Dönüşümleri (ItemUOM)**
- [x] Alt liste: Birim (PACK/CASE), Çarpan (1 PACK = X EACH)
- [x] Aksiyonlar: Dönüşüm Ekle, Sil
- [x] Validasyon: Aynı UOM ile birden fazla dönüşüm olmamalı

**Detay / Form — Barkodlar (ItemBarcode)**
- [x] Alt liste: Barkod, UOM, Çarpan
- [x] Aksiyonlar: Barkod Ekle, Sil
- [x] Validasyon: Barkod sisteme unique olmalı

---

### Ekran: Müşteri/Tedarikçi (Account) Listesi & Detayı (`/master/accounts`)

**Liste Görünümü**
- [x] Sütunlar: Code, Name, Tür (Müşteri/Tedarikçi/Her ikisi), Telefon, IsActive
- [x] Filtreler: AccountType, IsActive
- [x] Aksiyonlar: Yeni, Düzenle, Pasif Et

**Detay / Form**
- [x] Alan: `Code` — zorunlu, unique
- [x] Alan: `Name` — zorunlu
- [x] Alan: `AccountType` — Müşteri / Tedarikçi / Her İkisi (DictionaryValue)
- [x] Alan: `Phone`, `Email`, `Address` — opsiyonel
- [x] Alan: `TaxNo` — opsiyonel
- [x] Alan: `IsActive` — toggle

---

### Ekran: Depo (Warehouse) Yönetimi (`/master/warehouses`)

**Liste Görünümü**
- [x] Sütunlar: Code, NameTr, IsActive, Lokasyon Sayısı

**Detay / Form**
- [x] Alan: `Code` — zorunlu, unique
- [x] Alan: `NameTr`, `NameEn` — zorunlu
- [x] Alan: `Address` — opsiyonel
- [x] Alan: `IsActive` — toggle

---

### Ekran: Lokasyon/Bin Yönetimi (`/master/locations`)

**Amaç:** Depodaki hücre/raf/zemin tanımları (Phase 2+)

**Liste Görünümü**
- [x] Sütunlar: Code, WarehouseCode, LocationType, IsActive
- [x] Filtreler: Warehouse, LocationType
- [x] Aksiyonlar: Yeni Lokasyon, Düzenle, Pasif Et, Toplu İçe Aktar

**Detay / Form**
- [x] Alan: `Code` — zorunlu, unique (örn: A-01-03)
- [x] Alan: `WarehouseId` — zorunlu
- [x] Alan: `LocationType` — RACK / FLOOR / QUARANTINE / SHIPMENT_STAGING (DictionaryValue)
- [x] Alan: `IsActive` — toggle
- [x] Alan: `Description` — opsiyonel

---

## M02 — Inventory Ledger

> Bağımlılık: M00, M01

---

### Ekran: Stok Bakiye Görünümü (`/inventory/balance`)

**Amaç:** Anlık stok durumu — okunur, yazılmaz.

**Filtreler**
- [x] Warehouse, Location (bin), Item/SKU, Lot, LPN
- [x] IsReserved (ayrılmış mı?)

**Sütunlar**
- [x] SKU, NameTr, Warehouse, Location, Lot (varsa), LPN (varsa), QtyOnHand, QtyReserved, QtyAvailable
- [x] QtyAvailable = QtyOnHand - QtyReserved

**Aksiyonlar**
- [x] Harekete Git (ilgili StockMovement listesi)
- [x] Dışa Aktar (Excel)

---

### Ekran: Stok Hareketleri (`/inventory/movements`)

**Amaç:** Defterin tam geçmişi — okunur, yazılmaz.

**Filtreler**
- [x] Tarih aralığı, MovementType, Item, Warehouse, Location, Lot, SourceDocumentType, SourceDocNo

**Sütunlar**
- [x] Tarih, MovementType (NameTr), Item SKU, Warehouse, Location, QtyBase (±), SourceDocNo, CreatedBy

**Aksiyonlar**
- [x] Kaynak belgeye git (Receiving/Shipment/WorkOrder vb.)

---

### Ekran: Rezervasyonlar (`/inventory/reservations`)

**Amaç:** Sipariş için ayrılmış stok.

**Filtreler**
- [x] Item, SalesOrderNo, Status

**Sütunlar**
- [x] Item SKU, SalesOrderLine, Qty, Location, ReservedAt, Status

---

### Ekran: Stok Snapshot (`/inventory/snapshots`)

**Amaç:** Mutabakat için anlık görüntü.

**Aksiyonlar**
- [x] Snapshot Al (tarih parametre ile)
- [x] Snapshot Listesi — Tarih, Item Sayısı, Oluşturan
- [x] Snapshot Karşılaştır (iki snapshot arası fark)

---

## M03 — Purchase Order (Alış Siparişi)

> Bağımlılık: M00, M01

---

### Ekran: Sipariş Listesi (`/purchase/orders`)

- [x] Sütunlar: DocNo, Tarih, Tedarikçi, Status (NameTr), Toplam Tutar, OrderLineStatus özeti
- [x] Filtreler: Status, Tarih aralığı, Tedarikçi
- [x] Aksiyonlar: Yeni Sipariş, Görüntüle, İptal Et

---

### Ekran: Yeni / Düzenle Sipariş (`/purchase/orders/new`, `/purchase/orders/:id`)

**Başlık Bilgileri**
- [x] Alan: `DocNo` — otomatik (PO-YYYY-XXXXX)
- [x] Alan: `OrderDate` — zorunlu, default bugün
- [x] Alan: `SupplierId` (AccountId) — zorunlu, lookup
- [x] Alan: `WarehouseId` — zorunlu
- [x] Alan: `Notes` — opsiyonel
- [x] Alan: `Status` — DRAFT (salt okunur)

**Sipariş Satırları (PurchaseOrderLine)**
- [x] Alan: `ItemId` — lookup
- [x] Alan: `RequestedQty` — zorunlu, pozitif
- [x] Alan: `UOM` — EACH/PACK/CASE, QtyBase otomatik hesap

**Aksiyonlar**
- [x] Kaydet (DRAFT)
- [x] Onayla (DRAFT→APPROVED)
- [x] Mal Kabul Oluştur (POSTED siparişten → M04'e bağlı)

---

## M04 — Receiving (Mal Kabul)

> Bağımlılık: M00, M01, M02

---

### Ekran: Mal Kabul Listesi (`/receiving`)

- [x] Sütunlar: DocNo, Tarih, Tedarikçi, Status (NameTr), Kalem Sayısı
- [x] Filtreler: Status, Tarih aralığı, Tedarikçi
- [x] Aksiyonlar: Yeni Mal Kabul, Görüntüle, İptal Et

---

### Ekran: Yeni / Düzenle Mal Kabul (`/receiving/new`, `/receiving/:id`)

**Başlık Bilgileri**
- [x] Alan: `DocNo` — otomatik (RCV-YYYY-XXXXX), salt okunur
- [x] Alan: `ReceivingDate` — zorunlu, default bugün
- [x] Alan: `SupplierId` (AccountId) — zorunlu, lookup
- [x] Alan: `WarehouseId` — zorunlu
- [x] Alan: `Notes` — opsiyonel
- [x] Alan: `Status` — DRAFT (salt okunur, durum makinesi yönetir)

**Satır Ekleme (ReceivingLine)**
- [x] Barkod okutma alanı (önce ItemBarcode tablosunda ara)
- [x] Alan: `ItemId` — lookup
- [x] Alan: `ScannedUOM` — EACH/PACK/CASE (barkoddan otomatik veya manuel)
- [x] Alan: `ScannedQty` — zorunlu, pozitif sayı
- [x] Alan: `QtyBase` — otomatik hesap: ScannedQty × UOM çarpanı (salt okunur)
- [x] Alan: `LotNo` — Phase 3: opsiyonel (M09 aktifse)
- [x] Alan: `ExpiryDate` — Phase 3: opsiyonel (FEFOEnabled ise)
- [x] Alan: `LocationId` — Phase 2: opsiyonel (Putaway yoksa direkt bin)

**Aksiyonlar**
- [x] Satır Ekle, Satır Sil
- [x] Kaydet (DRAFT)
- [x] Onayla (DRAFT→POSTED) — StockMovement RECEIPT oluşturur
- [x] İptal Et (POSTED→CANCELLED) — ters hareket oluşturur
- [x] Validasyon: POSTED sonrası satır düzenlenemez

---

### Ekran: Yerine Koyma Görevi — Putaway (`/receiving/:id/putaway`)

**Amaç:** Phase 2+ — Staging alandan bin'e yerleştirme.

**Görev Listesi (PutawayTask)**
- [x] Sütunlar: Item SKU, QtyBase, From (STAGING), To (hedef bin), Status
- [x] Aksiyonlar: Görevi Başlat, Tamamla

**Görev Yürütme (PutawayTaskLine)**
- [x] Barkod oku (Item)
- [x] Barkod oku (Hedef LocationId — `RequireBinScan=true` ise zorunlu)
- [x] Qty gir
- [x] Onayla → StockMovement TRANSFER (STAGING → bin)

---

## M05 — Sales Order (Satış Siparişi)

> Bağımlılık: M00, M01

---

### Ekran: Sipariş Listesi (`/sales/orders`)

- [x] Sütunlar: DocNo, Tarih, Müşteri, Status (NameTr), Toplam Tutar, OrderLineStatus özeti
- [x] Filtreler: Status, Tarih aralığı, Müşteri, OrderLineStatus
- [x] Aksiyonlar: Yeni Sipariş, Görüntüle, İptal Et

---

### Ekran: Yeni / Düzenle Sipariş (`/sales/orders/new`, `/sales/orders/:id`)

**Başlık Bilgileri**
- [x] Alan: `DocNo` — otomatik (SO-YYYY-XXXXX)
- [x] Alan: `OrderDate` — zorunlu, default bugün
- [x] Alan: `CustomerId` (AccountId) — zorunlu, lookup
- [x] Alan: `WarehouseId` — zorunlu
- [x] Alan: `PromiseDate` — Phase 2: opsiyonel
- [x] Alan: `Priority` — Phase 2: Normal/Yüksek/Kritik (DictionaryValue)
- [x] Alan: `Notes` — opsiyonel

**Sipariş Satırları (SalesOrderLine)**
- [x] Alan: `ItemId` — lookup
- [x] Alan: `RequestedQty` — zorunlu, pozitif
- [x] Alan: `UOM` — EACH/PACK/CASE, QtyBase otomatik hesap
- [x] Alan: `UnitPrice` — opsiyonel
- [x] Alan: `OrderLineStatus` — OPEN / PARTIAL / CLOSED (salt okunur, sistem yönetir)
- [x] Görüntü: `ShippedQty`, `OpenQty` (RequestedQty - ShippedQty)

**Aksiyonlar**
- [x] Kaydet (DRAFT)
- [x] Onayla (DRAFT→POSTED)
- [x] Sevkiyat Oluştur (POSTED siparişten → M05'e bağlı)
- [x] İptal Et
- [x] Validasyon: POSTED sonrası RequestedQty azaltılamaz

---

## M06 — Shipping (Sevkiyat)

> Bağımlılık: M00, M01, M02, M04

---

### Ekran: Sevkiyat Listesi (`/shipping/shipments`)

- [x] Sütunlar: DocNo, Tarih, Müşteri, Status, Toplam QtyBase
- [x] Filtreler: Status, Tarih aralığı, Müşteri
- [x] Aksiyonlar: Yeni Sevkiyat, Görüntüle, İptal Et

---

### Ekran: Yeni / Düzenle Sevkiyat (`/shipping/shipments/new`, `/shipping/shipments/:id`)

**Başlık Bilgileri**
- [x] Alan: `DocNo` — otomatik (SHP-YYYY-XXXXX)
- [x] Alan: `ShipmentDate` — zorunlu
- [x] Alan: `CustomerId` — zorunlu
- [x] Alan: `WarehouseId` — zorunlu
- [x] Alan: `CarrierInfo` — Phase 3: opsiyonel
- [x] Alan: `Notes` — opsiyonel

**Sevkiyat Satırları (ShipmentLine)**
- [x] Alan: `ItemId` — lookup
- [x] Alan: `ScannedUOM`, `ScannedQty`, `QtyBase` (otomatik hesap)
- [x] Alan: `LocationId` — Phase 2: stok çekilen bin

**Sipariş Dağıtımı (ShipmentAllocation)**
- [x] Her ShipmentLine için SalesOrderLine eşlemesi
- [x] Görüntü: Hangi siparişin hangi satırına ne kadar sayıldı
- [x] Validasyon: Toplam dağıtılan qty > line qty ise hata

**Aksiyonlar**
- [x] Kaydet (DRAFT)
- [x] Onayla (POSTED) → StockMovement ISSUE + SalesOrderLine güncelle (PARTIAL/CLOSED)
- [x] Fatura Oluştur: InvoiceMode=INSTANT ise otomatik
- [x] İptal Et → ters StockMovement, OrderLine'ları geri aç

---

## M06 — Picking (Toplama)

> Bağımlılık: M05 + M01 Location

---

### Ekran: Toplama Görevi Listesi (`/picking/tasks`)

- [x] Sütunlar: TaskNo, Shipment DocNo, Durum, Depocu, Atanan, Başlangıç, Bitiş
- [x] Filtreler: Status, Tarih, Depocu
- [x] Aksiyonlar: Göreve Başla, Görevi Ata, Görüntüle

---

### Ekran: Toplama Görevi Yürütme (`/picking/tasks/:id/execute`)

**Görev Detayı (PickTaskLine)**
- [x] Sıra no, FromBin kodu, Item SKU, NameTr, QtyBase istenilen
- [x] Barkod okutma alanı (Item barkodu)
- [x] Barkod okutma alanı (FromBin — RequireBinScan=true)
- [x] Alan: Toplanan Qty girişi
- [x] Her satır: Onayla / Atla
- [x] Kısmi toplama: Toplanan < İstenen → sonraki turda devam

**Wave Yönetimi (Phase 3)**
- [x] Wave kaydı oluştur (birden fazla PickTask grubu)
- [x] Wave Listesi: WaveNo, Görev Sayısı, Status
- [x] Wave başlat → tüm görevleri RELEASED duruma geçir

---

## M07 — Transfer

> Bağımlılık: M00, M01, M02

---

### Ekran: Transfer Listesi (`/inventory/transfers`)

- [x] Sütunlar: DocNo, Tarih, From, To, Status, Toplam QtyBase
- [x] Filtreler: Status, Tarih, Warehouse

---

### Ekran: Yeni / Düzenle Transfer (`/inventory/transfers/new`, `/inventory/transfers/:id`)

**Başlık Bilgileri**
- [x] Alan: `DocNo` — otomatik (TRF-YYYY-XXXXX)
- [x] Alan: `TransferDate` — zorunlu
- [x] Alan: `FromWarehouseId` — zorunlu
- [x] Alan: `ToWarehouseId` — zorunlu (Phase 1: farklı depo)
- [x] Alan: `FromLocationId` — Phase 2: bin zorunlu
- [x] Alan: `ToLocationId` — Phase 2: bin zorunlu
- [x] Alan: `Notes` — opsiyonel

**Transfer Satırları (TransferLine)**
- [x] Alan: `ItemId` — lookup / barkod okutma
- [x] Alan: `ScannedUOM`, `ScannedQty`, `QtyBase`
- [x] Validasyon: FromLocation bakiyesi yeterliyse izin ver

**Aksiyonlar**
- [x] Kaydet (DRAFT)
- [x] Onayla (POSTED) → StockMovement TRANSFER (ISSUE from + RECEIPT to)
- [x] İptal Et → ters hareketler

---

## M08 — Cycle Count (Sayım)

> Bağımlılık: M00, M01, M02

---

### Ekran: Sayım Listesi (`/inventory/cycle-counts`)

- [x] Sütunlar: DocNo, Tarih, Tip, Depo, Bin, Status, Fark var mı?
- [x] Filtreler: Status, Tarih, Warehouse, CountType

---

### Ekran: Yeni Sayım (`/inventory/cycle-counts/new`)

- [x] Alan: `DocNo` — otomatik (CC-YYYY-XXXXX)
- [x] Alan: `CountDate` — zorunlu
- [x] Alan: `CountType` — BLIND / OPEN / RECOUNT (DictionaryValue)
- [x] Alan: `WarehouseId` — zorunlu
- [x] Alan: `LocationId` — opsiyonel (tüm depo veya tek bin)
- [x] Sistem: Sayım başlatılınca beklenen stok snapshot'ı alınır (BLIND'da gizlenir)

---

### Ekran: Sayım Yürütme (`/inventory/cycle-counts/:id/execute`)

**Satır Girişi (CycleCountLine)**
- [x] Barkod okutma (Item)
- [x] Alan: CountedQty — depocu girer
- [x] OPEN modda: ExpectedQty görünür; BLIND modda gizlenir
- [x] Her satır onayla

**Fark Ekranı**
- [x] Sütunlar: Item, Bin, ExpectedQty, CountedQty, Fark (±)
- [x] Tolerans kontrolü: |Fark| > CountTolerance (%) → uyarı
- [x] Aksiyonlar: Yeniden Say (RECOUNT), Onayla

**Aksiyonlar**
- [x] Onayla (POSTED) → StockMovement ADJUSTMENT (fark için)
- [x] İptal Et

---

## M09 — Traceability (İzlenebilirlik)

> Bağımlılık: M01, M02 (+M05 packing)  
> Phase: 3-4

---

### Ekran: Lot Yönetimi (`/traceability/lots`)

- [x] Sütunlar: LotNo, Item SKU, SupplierLot, ExpiryDate, Status
- [x] Filtreler: Item, Status, ExpiryDate aralığı
- [x] Aksiyonlar: Yeni Lot, Görüntüle, Karantinaya Al

**Detay**
- [x] Alan: `LotNo` — zorunlu, unique
- [x] Alan: `ItemId` — zorunlu
- [x] Alan: `SupplierLotNo`, `ProductionDate`, `ExpiryDate` — opsiyonel
- [x] Sekme: Stok Hareketleri (bu lot ile ilgili tüm StockMovement)

---

### Ekran: Seri No Yönetimi (`/traceability/serials`)

- [x] Sütunlar: SerialNo, Item SKU, Status, Son Lokasyon
- [x] Filtreler: Item, Status
- [x] Aksiyonlar: Görüntüle

---

### Ekran: LPN Yönetimi (`/traceability/lpns`)

**Amaç:** Palet/kasa handling unit takibi.

- [x] Sütunlar: LpnNo, LPNType, Location, İçerik Sayısı, Status
- [x] Detay: LPNLine listesi (Item, Lot, Qty)
- [x] Aksiyonlar: LPN Oluştur, İçerik Ekle/Çıkar, LPN Böl, LPN Birleştir

---

### Ekran: Koli (Carton) Yönetimi (`/traceability/cartons`)

**Amaç:** Sevkiyat koli izlenebilirliği.

- [x] Sütunlar: CartonNo, Shipment DocNo, İçerik Sayısı, Status
- [x] Detay: CartonLine listesi (Item, Lot, Serial, Qty)

---

### Ekran: İzlenebilirlik Sorgusu (`/traceability/trace`)

**Amaç:** Bir lot veya seriyi başından sonuna izle.

- [x] Giriş: LotNo / SerialNo / LpnNo
- [x] Sonuç zinciri: Receiving → Transfer → Picking → Shipment (zaman çizelgesi)

---

## M10 — Manufacturing (Üretim) [COMPLETED]

> Bağımlılık: M00, M01, M02 (+M09 lot)  
> Phase: 4-5

---

### Ekran: BOM & Rota Yönetimi (`/manufacturing/boms`) [x]
- [x] **Dinamik Reçete (Parametrik BOM):** Ölçülere göre miktar formülü (`QtyFormula`) ve fire oranı (`WastePercentage`).
- [x] **Çok Aşamalı Rota (Routing):** Operasyon adımları, standart süreler ve standart maliyetler.
- [x] **Tahmini Maliyet:** Reçete fiyatları ve rota standartları üzerinden `PlannedMaterialCost` ve `PlannedResourceCost` hesabı.

### Ekran: İş Emri & Terminal (`/manufacturing/terminal`) [x]
- [x] **Start/Stop Terminali:** Operatör bazlı saniye hassasiyetli iş takibi.
- [x] **WIP (İşlemdeki Ürün):** İstasyonlar arası ara stok ve bağımlılık kontrolü (Önceki iş bitmeden sonraki başlayamaz).
- [x] **Kümülatif Maliyet:** Elektrik, işçilik ve makine maliyetlerinin saniye saniye "fiili" (Actual) birikmesi.

### Ekran: Sarfiyat & Giriş (`/manufacturing/receipt`) [x]
- [x] **Consumption (Sarfiyat):** Hammaddelerin stoktan düşülmesi ve `ActualMaterialCost` güncellemesi.
- [x] **Production Receipt:** Üretilen mamülün stoka girmesi ve iş emrinin sonlanması.
- [x] **Kalite Kontrol (QC):** PASS/FAIL kararları ve hata kodları.
- [x] **Esnek Rework (Yeniden İşlem):** Parça değişimi (ayak, ayna vb.) ve ek maliyet yönetimi.

### Analoji & Raporlama [x]
- [x] **Varyans Analizi:** Planned vs Actual karşılaştırması.
- [x] **Mola/Duruş:** Üretim aralarının maliyetten hariç tutulması.

---

## M11 — B2B Portal

> Bağımlılık: M04 (+M05 görüntüleme)

---

### Ekran: Portal Kullanıcı Yönetimi (`/admin/portal-users`)

**Admin tarafı**
- [ ] Sütunlar: Email, Müşteri (Account), Rol, IsActive
- [ ] Aksiyonlar: Davet Gönder, Düzenle, Pasif Et

**Detay / Form**
- [ ] Alan: `Email` — zorunlu
- [ ] Alan: `AccountId` — zorunlu (hangi müşteriye bağlı)
- [ ] Alan: `PortalRole` — CUSTOMER_ADMIN / CUSTOMER_USER (DictionaryValue)

---

### Ekran: Portal — Sipariş Girişi (Müşteri tarafı) (`/portal/orders/new`)

- [ ] Alan: Item barkod/SKU arama
- [ ] Alan: Qty (müşteri kendi birimi ile girer)
- [ ] Alan: Teslimat tarihi isteği
- [ ] Alan: Notlar
- [ ] Aksiyon: Taslak Kaydet (PortalOrderDraft — JSON payload)
- [ ] Aksiyon: Gönder → Onay bekleniyor

---

### Ekran: Portal — Sipariş Takibi (Müşteri tarafı) (`/portal/orders`)

- [ ] Sütunlar: DocNo, Tarih, Status, Satır Sayısı
- [ ] Satır detayı: RequestedQty, ShippedQty, OpenQty, OrderLineStatus

---

### Ekran: Taslak Sipariş Onayı — Admin (`/sales/portal-drafts`)

- [ ] Gelen taslakları listele
- [ ] Görüntüle / Düzenle / SalesOrder'a Çevir / Reddet

---

## M12 — Service (Servis)

> Bağımlılık: M00, M01 (+M11 portal entegrasyonu)

---

### Ekran: Servis Talebi Listesi (`/service/tickets`)

- [ ] Sütunlar: TicketNo, Müşteri, Kanal, Kategori, Status, SLA Kalan, Atanan
- [ ] Filtreler: Status, Kategori, Müşteri, SLA aşımı
- [ ] Aksiyonlar: Yeni Talep, Görüntüle, Ata

---

### Ekran: Yeni / Düzenle Servis Talebi (`/service/tickets/new`, `/:id`)

**Başlık**
- [ ] Alan: `TicketNo` — otomatik (SRV-XXXXX)
- [ ] Alan: `CustomerId` — zorunlu
- [ ] Alan: `Channel` — PHONE / EMAIL / PORTAL / WHATSAPP (DictionaryValue)
- [ ] Alan: `Category` — INSTALLATION / MAINTENANCE / COMPLAINT vb. (DictionaryValue)
- [ ] Alan: `Description` — zorunlu
- [ ] Alan: `Priority` — Normal/Yüksek/Kritik
- [ ] Alan: `AssignedUserId` — opsiyonel
- [ ] Alan: `SLADeadline` — otomatik (şimdiki zaman + SLAHoursDefault)

**RMA (İade) Bağlantısı — Phase 2**
- [ ] RMA Oluştur → SalesOrder/Shipment referansı ile
- [ ] RMA Durumu: OPEN/APPROVED/RECEIVED/CLOSED

**Durum Logu (ServiceStatusLog)**
- [ ] Otomatik log: Her status değişikliğinde zaman + kullanıcı + not

**Dosya Ekleri (ServiceAttachment)**
- [ ] Dosya yükle (resim, video, belge)

**Aksiyonlar**
- [ ] Kaydet (OPEN)
- [ ] Atama Yap
- [ ] Çözüme Al (IN_PROGRESS → RESOLVED)
- [ ] Kapat (RESOLVED → CLOSED)
- [ ] SLA aşımı: Renk uyarısı (sarı→kırmızı)

---

## M13 — Project (Proje)

> Bağımlılık: M00, M01 (+M04/M10/M12 opsiyonel)

---

### Ekran: Proje Listesi (`/projects`)

- [ ] Sütunlar: ProjectNo, Ad, Müşteri, Tip, Status, Güncel Revizyon, Tarih
- [ ] Filtreler: Status, Tip, Müşteri

---

### Ekran: Proje Detayı (`/projects/:id`)

**Başlık**
- [ ] Alan: `ProjectNo` — otomatik (PRJ-YYYY-XXXXX)
- [ ] Alan: `Name` — zorunlu
- [ ] Alan: `CustomerId` — zorunlu
- [ ] Alan: `ProjectType` — DictionaryValue
- [ ] Alan: `Status` — DRAFT/APPROVED/IN_PROGRESS/COMPLETED/CANCELLED

**Revizyonlar (ProjectRevision)**
- [ ] Revizyon No (otomatik artış), Tarih, Açıklama, Onaylayan
- [ ] Aktif revizyon işaretleme

**Ölçümler (ProjectMeasurement)**
- [ ] Alan: `MeasurementKey` (ör: KitchenWidth), `Value`, `Unit`
- [ ] Serbest form key-value yapısı

**Maliyet Satırları (ProjectCost)**
- [ ] Alan: `CostType` (INSTALL, MATERIAL, LABOR vb.), `Amount`, `Notes`

**Aksiyonlar**
- [ ] Teklif Oluştur (SalesOrder'a bağla)
- [ ] İş Emri Bağlantısı (M10)
- [ ] Servis Talebi Bağlantısı (M12)

---

## M14 — Incentives (Prim Yönetimi)

> Bağımlılık: M00 (EventQueue)

---

### Ekran: Prim Kuralları (`/incentives/rules`)

- [ ] Sütunlar: KuralAdı, TriggerType, Hesaplama (%), IsActive
- [ ] Aksiyonlar: Yeni Kural, Düzenle, Pasif Et

**Kural Detayı**
- [ ] Alan: `TriggerType` — ORDER_POSTED / SHIPMENT_POSTED / CASH_RECEIVED (DictionaryValue)
- [ ] Alan: `Direction` — + (prim) / - (kesinti)
- [ ] Alan: `Rate` — yüzde veya sabit tutar
- [ ] Alan: `TargetType` — Personel / Müşteri Personeli
- [ ] Alan: `Caps`: Dönem maksimum (Phase 3)

---

### Ekran: Prim Hareketleri (`/incentives/transactions`)

- [ ] Sütunlar: Tarih, Personel, TriggerType, Kaynak Belge, Tutar, Yön
- [ ] Filtreler: Personel, Dönem, TriggerType

---

### Ekran: Dönem Özetleri (`/incentives/summary`)

- [ ] Filtreler: Personel, Dönem (ay/çeyrek/yıl)
- [ ] Sütunlar: Personel, TotalPlus, TotalMinus, NetAmount
- [ ] Aksiyonlar: Onayla (muhasebe aktarımı), Dışa Aktar

---

## M15 — Dashboards (Panolar)

> Bağımlılık: M00 + hedef modüller

---

### Ekran: Pano Yönetimi (`/admin/dashboards`)

**Admin tarafı**
- [ ] Pano Oluştur: Ad, Rol (kimin göreceği), Kart listesi
- [ ] Kart ekle: KPIType, DataSource, Görselleştirme tipi (sayısal/grafik/liste)
- [ ] Cache TTL ayarı (saniye)

---

### Ekran: Operasyon Panosu (Phase 1) (`/dashboard/operations`)

- [ ] Kart: Açık Siparişler (OpenOrders count + trend)
- [ ] Kart: Bugünkü Sevkiyatlar
- [ ] Kart: Bekleyen Mal Kabul
- [ ] Kart: Düşük Stok Uyarısı (eşik altı item listesi)
- [ ] Kart: Bugünkü Toplama Görevleri

---

### Ekran: Varyans Panosu (Phase 2) (`/dashboard/variance`)

- [ ] Kart: Sayım Farkları (son 30 gün)
- [ ] Kart: ADJUSTMENT hareketleri toplam ± değer

---

### Ekran: Servis Panosu (Phase 3) (`/dashboard/service`)

- [ ] Kart: Açık Biletler
- [ ] Kart: SLA Aşımı
- [ ] Kart: Ortalama Çözüm Süresi

---

### Ekran: Üretim Panosu (Phase 4) (`/dashboard/manufacturing`)

- [ ] Kart: Açık İş Emirleri
- [ ] Kart: Bugün Tamamlanan Üretim (FG qty)
- [ ] Kart: Komponent Stok Kritik Noktası

---

## M16 — Integration Bridge (ERP Entegrasyon Köprüsü)

> Bağımlılık: M00 + belge modülleri

---

### Ekran: Olay Kuyruğu İzleme (`/integration/event-queue`)

- [ ] Sütunlar: EventType, Payload özeti, Status (PENDING/PROCESSING/DONE/FAILED), Deneme Sayısı, Son Hata
- [ ] Filtreler: Status, EventType, Tarih
- [ ] Aksiyonlar: Manuel Yeniden Dene, İptal Et

---

### Ekran: Hata Kuyruğu (`/integration/error-queue`)

- [ ] Sütunlar: EventId, Hata Mesajı, Hata Zamanı, Kaynak
- [ ] Aksiyonlar: Hata Detayı, Yeniden Kuyruğa Al, Sil

---

### Ekran: Dış Belge Eşlemesi (`/integration/document-map`)

**Amaç:** OPERAX belt numarasını ERP referansıyla eşler.

- [ ] Sütunlar: ExternalSystem, ExternalRef, SourceDocType, SourceDocNo
- [ ] Filtreler: ExternalSystem, SourceDocType
- [ ] Aksiyonlar: Manuel Eşleme Ekle, Sil

---

### Ekran: Entegrasyon Ayarları (`/admin/integration-settings`)

- [ ] Hangi doküman tipleri event üretsin? (checkbox listesi)
- [ ] RetryPolicy: MaxRetries, RetryDelaySeconds
- [ ] ExternalSystem tanımları (MIKRO/LOGO/NETSIS vb.)

---

## M17 — Packaging & Licensing (Paketleme & Lisanslama)

> Bağımlılık: M00

---

### Ekran: Paket Kataloğu (`/admin/packages`)

- [ ] Sütunlar: PackageCode, NameTr, İçerdiği Modüller, LicenseDuration
- [ ] Aksiyonlar: Yeni Paket, Düzenle

**Detay / Form**
- [ ] Alan: `Code` — zorunlu (STARTER_WAREHOUSE, WMS_PRO…)
- [ ] Alan: `NameTr`, `NameEn`
- [ ] Alan: Modül seçimi (PackageModule — çoklu)
- [ ] Alan: `DefaultLicenseMonths` — varsayılan lisans süresi
- [ ] Alan: `TrialDays` — deneme süresi (0 = deneme yok)

---

### Ekran: Şirket Lisansları (`/admin/company-licenses`)

- [ ] Sütunlar: Şirket, Paket, Başlangıç, Bitiş, Kalan Gün, Status
- [ ] Filtreler: Status (active/trial/expired)
- [ ] Aksiyonlar: Lisans Ata, Yenile, İptal Et
- [ ] Uyarı: 30 gün kala + 7 gün kala otomatik bildirim

---

### Ekran: Kurulum Logu (`/admin/install-log`)

- [ ] Sütunlar: Tarih, Şirket, İşlem, Modül/Paket, Kullanıcı, Sonuç
- [ ] Salt okunur
- [ ] Dışa Aktar: Teknik destek için

---

## Teknik / Altyapı TODO (Tüm Modüller İçin)

---

### DDL & Seed

- [ ] Her modül için ayrı `schema_M00.sql` … `schema_M17.sql`
- [ ] `seed_dictionary.sql` — DictionaryType + DictionaryValue (tüm modüller)
- [ ] `seed_parameter.sql` — varsayılan parametre değerleri
- [ ] `seed_status_transitions.sql` — tüm belge tipleri için geçiş kuralları
- [ ] `seed_master_data_sample.sql` — demo şirket, kullanıcı, ürün, depo

### API (OpenAPI / Swagger)

- [ ] Her modül için REST endpoint listesi
- [ ] Request/Response şemaları
- [ ] Yetki gereksinimi (hangi rol hangi endpoint)

### Test / Acceptance Criteria

- [ ] Her modül için temel senaryo (happy path)
- [ ] Kısmi sevk senaryosu (M04+M05)
- [ ] UOM dönüşüm senaryosu (barkod CASE → EACH hesap)
- [ ] StockMovement doğruluk testi (ISSUED + RECEIVED = 0 net for transfer)
- [ ] SLA aşım senaryosu (M12)
- [ ] EventQueue retry senaryosu (M16)

### Güvenlik & Yetki

- [ ] Rol-ekran-izin matrisi dökümanı
- [ ] Portal kullanıcısı sadece kendi müşteri verilerini görür (row-level security)
- [ ] AuditLog kritik alanlar için trigger veya interceptor

### Performans

- [ ] StockMovement tablosu için index stratejisi (ItemId, WarehouseId, MovementDate)
- [ ] InventoryBalance tablosu partition veya clustered index tasarımı
- [ ] EventQueue polling yerine push (SignalR / Service Bus)
- [ ] DashboardCache TTL yönetimi

---

## EKSİK / TAMAMLAYICI BÖLÜMLER

> Aşağıdaki başlıklar master dökümanda yer almayan ancak platform için zorunlu olan konulardır.

---

## AUTH — Kimlik Doğrulama & Session

> M00'un parçası ama ayrı ekranlar gerektirir.

### Ekran: Giriş (`/login`)

- [ ] Alan: `Username` veya `Email`
- [ ] Alan: `Password`
- [ ] Alan: Beni Hatırla (RememberMe token)
- [ ] Multi-company: Giriş sonrası şirket seçim ekranı (birden fazla şirkete üyeyse)
- [ ] Hatalı girişte: 5 denemede hesap kilitleme + AuditLog kaydı
- [ ] Başarılı girişte: Son aktif şirkete yönlendir

### Ekran: Şifre Sıfırlama (`/forgot-password`, `/reset-password`)

- [ ] E-posta ile token gönder
- [ ] Token süresi: 30 dakika, tek kullanım
- [ ] Yeni şifre: min 8 karakter, büyük harf + rakam zorunlu

### Ekran: Profil & Şifre Değiştirme (`/profile`)

- [ ] Kendi bilgilerini güncelle (FullName, Email)
- [ ] Eski şifre doğrulayarak yeni şifre belirle
- [ ] Aktif session'ları listele (cihaz, IP, son giriş) — opsiyonel

### Session Yönetimi

- [ ] JWT + Refresh Token (veya server-side session) kararı
- [ ] Token süresi: Access=15dk, Refresh=7gün
- [ ] Session zaman aşımı uyarısı (2 dk kala popup)
- [ ] Şirket değiştirme (context switch) — token yenile, sayfayı sıfırla

---

## SATIN ALMA / PURCHASE ORDER (Eksik Modül — M03 Öncesi)

> Receiving tek başına çalışabilir ama PO varsa referans kontrolü gerekir.  
> Döküman bunu atlamış — isteğe bağlı eklenti olarak tasarlanmalı.

### Ekran: Satınalma Siparişi Listesi (`/purchasing/orders`)

- [ ] Sütunlar: DocNo, Tarih, Tedarikçi, Status, Toplam Tutar, Teslim Durumu
- [ ] Filtreler: Status, Tedarikçi, Tarih

### Ekran: Yeni / Düzenle Satınalma Siparişi (`/purchasing/orders/new`, `/:id`)

- [ ] Alan: `DocNo` — otomatik (PO-YYYY-XXXXX)
- [ ] Alan: `SupplierId` — zorunlu
- [ ] Alan: `ExpectedDate` — beklenen teslim tarihi
- [ ] PO Satırları: Item, OrderedQty, UnitPrice
- [ ] Status: DRAFT → SENT → PARTIALLY_RECEIVED → CLOSED / CANCELLED
- [ ] Mal Kabul (Receiving) ile bağlantı: ReceivingLine.POLineId
- [ ] Validasyon: Teslim alınan qty > sipariş qty ise uyarı (over-receipt)

---

## FATURA YÖNETİMİ (Master Dökümanda Eksik)

> InvoiceMode parametresi var ama fatura ekranları tanımlanmamış.

### Ekran: Fatura Listesi (`/finance/invoices`)

- [ ] Sütunlar: InvoiceNo, Tarih, Müşteri, Tutar, Status (DRAFT/SENT/PAID/CANCELLED)
- [ ] Filtreler: Status, Müşteri, Tarih

### Ekran: Fatura Detayı (`/finance/invoices/:id`)

- [ ] Alan: `InvoiceNo` — otomatik (INV-YYYY-XXXXX)
- [ ] Alan: `InvoiceDate`
- [ ] Alan: `CustomerId`
- [ ] Alan: `Shipment` referansı (ShipmentId — hangi sevkiyattan oluştu)
- [ ] Satırlar: Item, Qty, UnitPrice, LineTotal, KDV%
- [ ] Alan: `TotalAmount`, `VATAmount`, `GrandTotal`
- [ ] Aksiyon: İndir (PDF), E-posta Gönder, İptal Et
- [ ] InvoiceMode=INSTANT: Shipment POSTED olunca otomatik oluşur
- [ ] InvoiceMode=EOD: Gün sonu toplu fatura oluşturma komutu

### Ekran: Toplu Fatura Oluşturma (`/finance/invoices/batch`)

- [ ] Filtre: Tarih, Müşteri, Faturalanmamış Sevkiyatları Listele
- [ ] Seç + Toplu INV Oluştur

---

## BİLDİRİM & UYARI SİSTEMİ (Master Dökümanda Eksik)

> M15 Dashboard'da eşik uyarısı var ama bildirim altyapısı tanımlanmamış.

### Ekran: Bildirim Merkezi (`/notifications`)

- [ ] Okunmamış bildirim sayısı: navbar'da rozet
- [ ] Liste: Tarih, Tip, İlgili belge linki, Okundu mu?
- [ ] Aksiyonlar: Tümünü Okundu İşaretle, Sil

### Bildirim Türleri (Tetikleyiciler)

- [ ] SLA aşımı → Servis sorumlusuna
- [ ] Stok eşiği altı → Depo sorumlusuna
- [ ] EventQueue FAILED → Teknik ekibe
- [ ] Lisans sona erecek (30 gün / 7 gün) → Admin'e
- [ ] Cycle Count farkı toleransı aştı → Depo sorumlusuna
- [ ] B2B Portal taslak sipariş geldi → Satış ekibine
- [ ] İş emri tamamlandı → Planlama ekibine

### Bildirim Kanalları

- [ ] In-app (anlık bildirim / bildirim merkezi)
- [ ] E-posta (şablon bazlı, `NotificationTemplate` tablosu)
- [ ] Opsiyonel Phase 3+: SMS / WhatsApp webhook

### Ekran: Bildirim Şablonları (`/admin/notification-templates`)

- [ ] Şablon listesi: TriggerType, Kanal, Konu, İçerik
- [ ] Değişken desteği: `{{DocNo}}`, `{{CustomerName}}`, `{{SLADeadline}}` vb.

---

## RAPORLAR (Master Dökümanda Eksik)

> Dashboard KPI gösterir; ayrı rapor sayfaları veri analizi için gerekli.

### Ekran: Rapor Merkezi (`/reports`)

**Stok Raporları**
- [ ] Stok Durumu Raporu (tarih bazlı, item/warehouse/lot filtreli)
- [ ] Stok Hareket Raporu (MovementType filtreli)
- [ ] Stok Yaşlandırma Raporu (FEFO — son kullanma tarihi analizi)
- [ ] Rezervasyon vs. Mevcut Raporu

**Sevkiyat / Satış Raporları**
- [ ] Sipariş Dolum Oranı Raporu (RequestedQty vs. ShippedQty)
- [ ] Kısmi Sevkiyat Raporu (OpenQty > 0 olan siparişler)
- [ ] Müşteri Bazlı Satış Raporu
- [ ] Geciken Siparişler (PromiseDate aşılmış)

**Operasyon Raporları**
- [ ] Putaway Performansı (süre analizi)
- [ ] Toplama (Picking) Performansı (depocu bazlı)
- [ ] Sayım Farkı Raporu (ADJUSTMENT hareketleri özeti)

**Üretim Raporları (Phase 4)**
- [ ] İş Emri Gerçekleşme Raporu (PlannedQty vs. ProducedQty)
- [ ] Hammadde Sarf Raporu
- [ ] Fire/Hurda Analizi

**Filtre ve Dışa Aktarım**
- [ ] Her rapor: tarih aralığı, şirket, warehouse filtresi
- [ ] Dışa Aktar: Excel, CSV, PDF
- [ ] Zamanlanmış rapor: e-posta ile otomatik gönder (cron)

---

## MOBİL / BARKOD TARAYICI UX (Master Dökümanda Eksik)

> `RequireBinScan`, barkod okutma referansları var ama mobil UX tasarlanmamış.

### Genel Mobil Prensipler

- [ ] Responsive web (PWA) veya dedicated mobile app kararı
- [ ] Büyük dokunmatik alanlar, minimum yazı girişi
- [ ] Barkod okutma: kamera (PWA) veya USB/Bluetooth scanner desteği
- [ ] Çevrimdışı mod: kısmi cache + sync (Phase 3+)

### Mobil Ekranlar (Depocu)

- [ ] **Görev Seç:** Açık PickTask / PutawayTask listesi (basit liste)
- [ ] **Barkod Tara:** Büyük kamera/tarayıcı alanı, anlık geri bildirim (yeşil/kırmızı)
- [ ] **Qty Gir:** Sayısal klavye odaklı, önceki + sonraki navigasyon
- [ ] **Bin Onayla:** Bin barkodu tara → konum doğrulama
- [ ] **Hata Ekranı:** "Ürün bulunamadı", "Stok yetersiz", "Yanlış bin" gibi açık hata mesajları
- [ ] **Tamamlandı Ekranı:** Görev tamamlandı onay animasyonu + sonraki göreve git

### Barkod Okutma Mantığı

- [ ] Önce `ItemBarcode` tablosunda ara → Item + UOM + çarpan döner
- [ ] Bulunamazsa: `Item.SKU` ile direkt eşleştir
- [ ] Hâlâ bulunamazsa: "Tanımsız barkod" uyarısı + manuel giriş seçeneği
- [ ] `RequireBinScan=true` ise: barkod okunmadan konum geçişi engellenir

---

## TOPLU VERİ AKTARIMI / IMPORT-EXPORT (Eksik)

### Ekran: İçe Aktar (`/admin/import`)

- [ ] Modül seçimi: Item, Account, Location, BOM, PO vb.
- [ ] Şablon İndir (CSV/Excel örnek dosya)
- [ ] Dosya Yükle → Önizleme (ilk 10 satır)
- [ ] Validasyon raporu: hatalı satırlar gösterilir, düzeltme için tekrar yükle
- [ ] Onayla → Toplu kayıt oluştur (background job)
- [ ] İşlem logu: başarılı / başarısız satır sayısı

**Desteklenecek İçe Aktarımlar**
- [ ] Item master (SKU, NameTr, UOM dönüşümleri, barkodlar)
- [ ] Account (Müşteri/Tedarikçi)
- [ ] Location (bin listesi — toplu depo kurulumu)
- [ ] BOM satırları
- [ ] Açılış stok sayımı (ilk kurulum için)
- [ ] SalesOrder toplu girişi (ERP göçü senaryosu)

---

## BASKI & ETİKET (Eksik)

> Sevkiyat, LPN, barkod etiketleri tanımlanmamış.

### Ekran: Etiket Şablonları (`/admin/label-templates`)

- [ ] Şablon listesi: Tip, Boyut, Önizleme
- [ ] Şablon Tipleri:
  - [ ] Item barkod etiketi (SKU + İsim + Barkod)
  - [ ] LPN etiketi (LpnNo, İçerik özeti, Lokasyon)
  - [ ] Koli etiketi (CartonNo, Shipment, Müşteri)
  - [ ] Lot etiketi (LotNo, ExpiryDate, Item)
- [ ] Baskı: Zebra/TSPL uyumlu çıktı veya PDF

### Baskı Tetikleyicileri

- [ ] Receiving POSTED → Item etiketi bas (opsiyonel)
- [ ] LPN oluşturuldu → LPN etiketi bas
- [ ] Shipment POSTED → Koli etiketi bas
- [ ] Cycle Count başlatıldı → Bin etiketi bas (lokasyon kodu QR)

---

## GLOBAL UX / UYGULAMA KABUKLARI (Eksik)

> Ekranlar tanımlandı ama navigasyon altyapısı, ortak bileşenler belirtilmedi.

### Navigasyon & Düzen

- [ ] Sol sidebar: Modül bazlı menü (sadece aktif modüller görünür)
- [ ] Üst navbar: Şirket adı, aktif kullanıcı, bildirim rozeti, şirket değiştir
- [ ] Breadcrumb: Her ekranda konum gösterimi
- [ ] Dil değiştirici: TR / EN (UI dili — Code değişmez, NameTr/NameEn toggle)
- [ ] Global Arama (`/search`): DocNo, SKU, Müşteri adı, LotNo ile tüm modüllerde ara

### Ortak Liste Bileşeni

- [ ] Sayfalama: 20/50/100 kayıt seçimi
- [ ] Sütun sıralaması (ascending/descending)
- [ ] Filtre paneli açılır/kapanır
- [ ] Sütun görünürlük seçimi (kullanıcı bazlı tercih)
- [ ] Seçili satırlara toplu aksiyon (ör: toplu iptal, toplu etiket bas)

### Ortak Form Bileşeni

- [ ] Kaydet / İptal her formda standart konumda
- [ ] Unsaved changes uyarısı (sayfadan çıkarken)
- [ ] Form validasyon: inline hata mesajları, alan odaklanma
- [ ] Lookup alanları: debounced arama (300ms), sonuçta Enter ile seç
- [ ] Tarih alanları: takvim picker, klavye girişi destekli

### Hata & Boş Durum Ekranları

- [ ] 404 — Sayfa bulunamadı (anasayfaya dön butonu)
- [ ] 403 — Yetkisiz erişim (rol açıklaması ile)
- [ ] 500 — Sunucu hatası (hata referans kodu göster)
- [ ] Bağlantı hatası: Offline uyarısı, yeniden bağlanıyor göstergesi
- [ ] Boş liste: "Henüz kayıt yok" mesajı + ilgili oluşturma aksiyonu

---

## SİSTEM SAĞLIK & İZLEME (Eksik)

> M16 ERP kuyruğunu izliyor ama platform sağlık izlemesi eksik.

### Ekran: Sistem Durumu (`/admin/system-health`)

- [ ] Servis durumları: DB bağlantısı, EventQueue, Dosya sistemi, Harici sistemler
- [ ] Son 24 saat hata oranı grafiği
- [ ] Aktif kullanıcı sayısı (anlık session)
- [ ] EventQueue birikim grafiği (PENDING sayısı trendi)
- [ ] Yavaş sorgu uyarısı (>1s response time)

### Ekran: Bakım Modu (`/admin/maintenance`)

- [ ] Bakım modunu aç/kapat (kullanıcılara özel mesaj gösterir)
- [ ] Planlı bakım bildirimi: tarih+saat + etkilenecek modüller

---

## UYGULAMA MİMARİSİ KARARLARI (Teknik TODO)

> Platform kararları — geliştirmeye başlamadan verilmeli.

### Backend

- [ ] Framework: .NET 10 Web API
- [ ] Pattern: CQRS + MediatR (Command/Query ayrımı)
- [ ] ORM: EF Core 10 (Code First migration)
- [ ] Background Jobs: Hangfire (EventQueue işleme, zamanlanmış raporlar)
- [ ] Cache: Redis (DashboardCache, barkod lookup cache)
- [ ] Realtime: SignalR (bildirim, olay kuyruğu monitörü)

### Frontend

- [ ] Framework kararı: Next.js (SSR) veya Vite+React (SPA) — karar verilmeli
- [ ] State: Zustand veya Redux Toolkit
- [ ] Form: React Hook Form + Zod validasyon
- [ ] Tablo: TanStack Table (server-side pagination)
- [ ] Barkod: `@ericblade/quagga2` (kamera) veya native scanner event listener

### Veritabanı

- [ ] SQL Server (primary)
- [ ] Çoklu şema mı tek şema mı? — Öneri: tek şema, CompanyId ile row-level isolation
- [ ] Soft delete standardı: `IsDeleted + DeletedAt + DeletedBy` (tüm tablolarda)
- [ ] Timestamp standardı: `CreatedAt, CreatedBy, UpdatedAt, UpdatedBy` (tüm tablolarda)

### DevOps / Deploy

- [ ] Docker Compose (geliştirme ortamı)
- [ ] CI/CD: GitHub Actions veya Azure DevOps pipeline
- [ ] Ortamlar: dev → staging → production
- [ ] Secrets: Azure Key Vault veya .env + Vault (k8s)
- [ ] DB Migration: EF Migrations, otomatik seed kontrolü (idempotent)

### Test Stratejisi

- [ ] Unit: xUnit + Moq (servis katmanı)
- [ ] Integration: WebApplicationFactory (API endpoint)
- [ ] E2E: Playwright (kritik akışlar: login, receiving-post, shipment-post)
- [ ] Performans: k6 (EventQueue, StockMovement yükleme testi)


## 🎨 UI BORÇ — Tailwind Utility Salatası → Semantic Class (sistemik)
- [ ] **Fatura/rapor view'larında Tailwind utility salatası** (ui-standard.md §2 ihlali) — SalesInvoices, PurchaseInvoices, Expenses, Expenses/Report Index/Details: `text-slate-600`, `bg-slate-50`, `text-indigo-600` (token değil!) gibi utility'ler markup'ta. Semantic class'a (`_tables.css` türevi) toplu taşınmalı. `text-indigo-600` → `var(--brand-500)` bağlı class. Sistemik — tek view değil, toplu refactor planı gerek.
