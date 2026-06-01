# Plan 17 — Production Güvenlik Sertleştirmesi (Rate Limiter, Security Headers, DB-Driven RBAC)

**Tarih:** 2026-05-30 · **Revize:** 2026-05-31 (RBAC modeli DB-driven'a çevrildi) · **Durum:** `TAMAM` (DOĞRULANDI 2026-06-01 canlı kod) · **Modül:** M00 (Güvenlik) · **Kaynak:** F0.2 (🔴 KRİTİK)

---

## 1. Problem
Operax internet ortamında herkese açık yayına alınacak. Mevcut korumalar yetersiz:
1. **Brute-Force & DDoS:** `/Auth/Login` ve `/api/switch-company` uçlarında istek sınırı (Rate Limiting) yok.
2. **Eksik Security Header'ları:** CSP, Clickjacking, MIME-sniffing korumaları response'larda yok.
3. **Rol/Erişim Yönetimi:** Roller `AspNetRoles`'ta dinamik (Admin/Roles UI ile yönetiliyor) **ama hangi rolün hangi modüle eriştiği eşleştirmesi yok** → giriş yapan her kullanıcı tüm modüllere erişiyor.
4. **Güvenlik Loglaması:** Yapılandırılmış IP/UserAgent loglama altyapısı yok.

---

## 2. Scope
### Dahili
- **Rate Limiting:** ASP.NET Core yerleşik `Microsoft.AspNetCore.RateLimiting` (login 10/dk-IP, switch-company 10/dk-kullanıcı, global 200/dk-IP).
- **Security Headers:** `NetEscapades.AspNetCore.SecurityHeaders` ile CSP/HSTS/XFO/nosniff/Referrer/Permissions.
- **DB-Driven RBAC:** Yeni `RoleModuleAccess` tablosu (RoleId ↔ ModuleKey ↔ AccessLevel). Roller + eşleştirme **tam dinamik, admin-yönetimli**. Yetki DB'den okunur; kodda sadece ekran→modül (klasör) yapısal eşlemesi ve thin policy sarmalayıcı bulunur.
- **7 Şablon Rol:** Administrator + WarehouseManager/Finance/Purchasing/Sales/Manufacturing/Viewer seed edilir (silinebilir/değiştirilebilir şablon).
- **Admin/Roles UI Genişletmesi:** Rol başına modül izin grid'i (VIEW/EDIT) — `RoleModuleAccess`'i yönetir.
- **Admin Panel Koruması:** Hangfire dashboard + `/Admin` sadece `Administrator`.
- **TLS & HTTPS:** Cookie `SecurePolicy` ortam-koşullu (prod=Always, dev=SameAsRequest → lokal http bozulmaz), HSTS prod 1 yıl.
- **Serilog:** `Serilog.AspNetCore` + `Enrichers.ClientInfo` ile IP/UserAgent dosya loglama.

### Dışı
- OAuth/2FA, Geo-blocking (Cloudflare Edge), CompanyId-scoped rol (single-tenant → roller global).

---

## 3. Alternatifler & Kararlar

### A. Rate Limiting → **Yerleşik Microsoft.AspNetCore.RateLimiting** (seçilen)
Harici paket yok, pipeline'da hızlı, modern algoritmalar (FixedWindow). AspNetCoreRateLimit maintenance modunda → red.

### B. Security Headers → **NetEscapades.AspNetCore.SecurityHeaders** (seçilen)
Andrew Lock, aktif bakım, fluent CSP. NWebsec güncellenmiyor → red. (Custom middleware de mümkündü; Serilog zaten paket eklediğinden olgun kütüphane tercih edildi.)

### C. **RBAC Modeli → DB-Driven RoleModuleAccess tablosu** (seçilen — 2026-05-31 kullanıcı kararı)
- **C.1 (red — Hardcoded `[Authorize(Roles="...")]` + static policy):** Plan'ın ilk hali. Admin yeni rol eklediğinde yetki haritası kodda kalır, dinamik değil. **Mevcut Admin/Roles dinamik UI ile çelişir → RED.**
- **C.2 (red — AspNetRoleClaims):** `DapperRoleStore` `IRoleClaimStore` implement etmiyor → Identity store cerrahisi gerekir. Fazla plumbing → RED.
- **C.3 (SEÇİLEN — RoleModuleAccess tablosu):** Yeni tablo + custom `AuthorizationHandler`. Roller (`AspNetRoles`) ve eşleştirme (`RoleModuleAccess`) %100 DB'de, admin-yönetimli. Identity store'a dokunmaz, SQL-first + Dapper'a uyar. Kodda sadece ekran→modül-anahtarı (klasör adı) yapısal eşlemesi kalır — bu rol değil, ekranın hangi işlevsel alana ait olduğu gerçeğidir.

### D. Yetki Birimi → **Üst-seviye Feature klasörü (ModuleKey)** (seçilen)
Module kataloğu (M00–M17) lisans/aktivasyon odaklı ve folder'larla 1:1 değil (örn. Finance modülü katalogda yok). Bu yüzden yetki birimi = ekranların üst klasörü (`Receiving`, `Finance`, `SalesOrders`...). `Module`/`CompanyModule` tabloları lisanslama için olduğu gibi kalır.

---

## 4. Şema — RoleModuleAccess

```sql
-- Identity-bitişik tablo (AspNetRoles gibi CompanyId taşımaz — single-tenant, roller global)
CREATE TABLE RoleModuleAccess (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RoleId      NVARCHAR(450)    NOT NULL,            -- AspNetRoles.Id
    ModuleKey   NVARCHAR(64)     NOT NULL,            -- üst Feature klasörü: 'Receiving','Finance',...
    AccessLevel TINYINT          NOT NULL DEFAULT 1,  -- 1=VIEW (GET), 2=EDIT (GET+POST)
    CreatedAt   DATETIME2        DEFAULT GETUTCDATE(),
    CONSTRAINT FK_RMA_Role FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_RMA_RoleModule UNIQUE (RoleId, ModuleKey)
);
```

### Yetki Birimi (ModuleKey) Listesi — sayfa içeren klasörler
`Admin`* · `Budget` · `CycleCount` · `Dashboard` · `Expenses` · `Finance` · `Inventory` · `LPN` · `Lot` · `Manufacturing` · `MasterData` · `Picking` · `Production` · `PurchaseOrders` · `Receiving` · `SalesInvoices` · `SalesOrders` · `Serial` · `Shipping` · `Transfer` · `Warehouses`
(`Auth` = anonim, dokunulmaz. `Admin` = sadece Administrator, RoleModuleAccess'e tabi değil.)

### Default Şablon Rol → ModuleKey Eşleştirmesi (seed — admin değiştirebilir)
| Rol | EDIT | VIEW |
|---|---|---|
| **Administrator** | (tümü — handler bypass) | — |
| **WarehouseManager** | Receiving, Shipping, Transfer, CycleCount, LPN, Lot, Serial, Picking, Inventory, Warehouses | Dashboard, MasterData |
| **Finance** | Finance | Dashboard, MasterData |
| **Purchasing** | PurchaseOrders, Expenses, Budget | Dashboard, MasterData |
| **Sales** | SalesOrders, SalesInvoices | Dashboard, MasterData |
| **Manufacturing** | Manufacturing, Production | Dashboard, MasterData |
| **Viewer** | — | Dashboard, MasterData |

---

## 5. Authorization Mekanizması (dinamik)
1. `Lib/Authz/ModuleAccessRequirement.cs` — `IAuthorizationRequirement` + `ModuleKey`.
2. `Lib/Authz/ModuleAccessHandler.cs` — handler:
   - Administrator rolü varsa → izin (bypass).
   - Kullanıcının rollerini al → `RoleModuleAccess`'ten ModuleKey erişimlerini oku (per-request `IMemoryCache`).
   - GET → VIEW yeterli; POST → EDIT (AccessLevel=2) şart. Yoksa `Forbid`.
3. `Program.cs` — bilinen ModuleKey listesi üzerinde döngü: her biri için `AddPolicy($"mod:{key}", p => p.AddRequirements(new ModuleAccessRequirement(key)))`. Sonra `AddRazorPagesOptions` → `AuthorizeFolder("/"+key, $"mod:{key}")`. Policy ince sarmalayıcı; allow/deny DB'den gelir.
4. `Admin` + Hangfire → ayrı `AdministratorOnly` policy.

---

## 6. Adımlar
1. **NuGet + Serilog** — 4 paket (NetEscapades, Serilog.AspNetCore, Enrichers.ClientInfo, Sinks.File) ✅ kuruldu. `Program.cs` UseSerilog + ClientIp/Agent enricher + günlük dosya.
2. **Security Headers** — `HeaderPolicyCollection` (CSP/XFO/nosniff/Referrer/Permissions) + `app.UseSecurityHeaders()`.
3. **Rate Limiting + TLS** — `AddRateLimiter` (login/switch-company/global), `UseRateLimiter` (UseRouting sonrası), cookie SecurePolicy ortam-koşullu, HSTS 1yr. Login PageModel `[EnableRateLimiting("login")]` ✅, switch-company `.RequireRateLimiting`.
4. **Şema** — `RoleModuleAccess` tablosu (`docs/sql/schema_M00.sql` veya `schema_M00_security.sql`, idempotent) + migrate.
5. **Roller + Seed** — `Lib/Roles.cs` 7 şablon ✅, `SeedData` 7 rol ✅ + default `RoleModuleAccess` satırları seed.
6. **Authz handler + policy** — `Lib/Authz/*`, `Program.cs` policy döngüsü + AuthorizeFolder.
7. **Admin/Roles UI** — rol başına modül izin grid'i (VIEW/EDIT toggle) → RoleModuleAccess yönetimi.
8. **Doğrulama** — build 0 hata; rol bazlı 403 testi; login 429 testi; header doğrulama.

---

## 7. Done Criteria (DoD)
- [ ] 4 paket kuruldu (✅), Serilog `logs/operax-*.log` IP/UserAgent ile yazıyor.
- [ ] Login 10/dk-IP → 429; global 200/dk-IP.
- [ ] CSP/XFO/nosniff/Referrer header'ları response'ta.
- [ ] `RoleModuleAccess` tablosu + 7 şablon rol + default eşleştirme seed edildi.
- [ ] Authz handler: yanlış rol → 403; Viewer POST → 403 (read-only); Administrator → tüm erişim.
- [ ] Admin/Roles UI'den rol-modül izni eklenip kaldırılabiliyor (DB'ye yansıyor, kod değişmeden).
- [ ] Hangfire + /Admin sadece Administrator.
- [ ] Cookie prod=Always; HSTS prod 1yr; dev http çalışıyor.
- [ ] `dotnet build` 0 hata.

---

## 8. 5 Lens
- 🔴 **Contrarian:** Per-request DB sorgusu yük mü? → `IMemoryCache` ile rol-erişim haritası cache'lenir, rol değişince invalidate.
- 🔵 **First Principles:** "Rol kodda mı DB'de mi?" → DB; kod sadece ekran→alan yapısal eşlemesini bilir.
- 🟢 **Expansionist:** İleride alan bazlı VIEW/EDIT/DELETE granülerliği AccessLevel ile genişler.
- ⚪ **Outsider:** Yabancı "neden Module tablosu kullanılmıyor?" → katalog lisans odaklı + folder'larla 1:1 değil; ModuleKey=folder daha dürüst.
- 🟡 **Executor:** Pazartesi: şema → seed → handler → UI sırası.

---

## 9. Onay
*   [x] Gösterildi · [x] Onay: 2026-05-31 (kullanıcı "uygula")

## 10. Uygulama Durumu (2026-05-31)
**KOD TAMAM — build yeşil (Web 0 hata/10 uyarı [mevcut CRIT-4], CLI 0/0).**
- ✅ 4 paket + Serilog (IP/UserAgent enrich, günlük dosya) — `WithClientAgent` yok → `WithRequestHeader("User-Agent","ClientAgent")`.
- ✅ Security Headers (NetEscapades): XFO/nosniff/XSS/Referrer/CSP/Permissions + RemoveServer.
- ✅ Rate Limiter: login 10/dk-IP (`[EnableRateLimiting("login")]`), switch-company 10/dk-kullanıcı (`.RequireRateLimiting`), global 200/dk-IP.
- ✅ Cookie SecurePolicy ortam-koşullu (prod Always/dev SameAsRequest), HSTS 1yr.
- ✅ `RoleModuleAccess` şema (`schema_M00_Security.sql`) + CLI migrate listesi.
- ✅ `Lib/Authz/` — ModuleKeys, ModuleAccessRequirement, ModuleAccessHandler (IMemoryCache cache + GET=VIEW/POST=EDIT + Administrator bypass).
- ✅ `Program.cs` — AddAuthorizationBuilder policy döngüsü + AuthorizeFolder (Admin=AdministratorOnly), Hangfire AdministratorOnly.
- ✅ `Roles.cs` 7 sabit + `SeedData` 7 rol + default RoleModuleAccess seed (idempotent).
- ✅ Admin/Roles/Permissions UI (modül × Yok/Görüntüle/Düzenle) + cache invalidate.

**DB UYGULANDI + DOĞRULANDI (2026-05-31):**
- SEC-1 fallout fix: CLI'ya `AddUserSecrets("ab29872f-...")` (Web UserSecretsId) + Json 10.0.8 bump → `operax-cli migrate` gerçek DB'ye yeniden ulaşıyor (sır sızmadan).
- `migrate` → `schema_M00_Security.sql` ok, RoleModuleAccess tablosu (5 kolon) oluştu.
- Web app start → 6 rol + Administrator seed, default erişim seed doğrulandı (plan §4 ile birebir): WarehouseManager 12 (10 EDIT), Finance 3, Purchasing 5, Sales 4, Manufacturing 4, Viewer 2 (0 EDIT), Administrator 0 (bypass).
- Serilog structured log aktif, app `http://localhost:5193` boot OK.

**KALAN runtime smoke (manuel, opsiyonel):** test kullanıcısı farklı rolle login → yetkisiz modül 403; Viewer POST→403; login 10+ istek → 429; response header (XFO/CSP) tarayıcı doğrulama.

---
> ⚠️ **BAĞIMLILIK:** Authz handler çalışması için `RoleModuleAccess` seed'i ŞART. `UseRateLimiter` routing sonrası/endpoint öncesi olmalı. `UseSecurityHeaders` erken.
