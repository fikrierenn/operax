# Güvenlik İlkeleri (Operax)

Kapsam: Her değişiklikte uygulanacak defansif ilkeler. `paths:` yok — compact sonrası bile kalmalı.

## Mutlak Kurallar

1. **SQL Injection:** Her Dapper sorgusu parametreli. String concat veya `$"..."` SQL'e **yasak**. SP çağrısı `CommandType.StoredProcedure` + named parameter. Detay: `.claude/rules/sql-conventions.md`.

2. **XSS:** Razor view'larda `@Html.Raw(userInput)` **minimum**. Helper'lardan dönen HTML (UiHelpers.StatusBadge) güvenli — kullanıcı verisinden değil sabit template'ten gelir. Kullanıcı verisi her zaman `@variable` (auto-escape).

3. **CSRF:** Her POST handler `<form method="POST">` + `@Html.AntiForgeryToken()`. `DisableAntiforgery()` sadece `/api/switch-company` gibi external API'lere ve gerekçeli olarak.

4. **Open redirect:** `Url.IsLocalUrl(returnUrl)` yeterli değil. Ek kontrol: `returnUrl.StartsWith("/") && !returnUrl.StartsWith("//")`.

5. **Sır/şifre yönetimi:**
   - Connection string → **env var** veya User Secrets veya Azure Key Vault
   - `appsettings.json` içinde plain-text şifre **YASAK**
   - Hardcoded API key kod içinde **YASAK** — secrets.json + .gitignore

6. **Cookie güvenlik flag'leri:**
   ```csharp
   options.Cookie.HttpOnly       = true;
   options.Cookie.SecurePolicy   = CookieSecurePolicy.Always;
   options.Cookie.SameSite       = SameSiteMode.Strict;
   options.ExpireTimeSpan        = TimeSpan.FromHours(8);
   ```

7. **Exception handling:** User'a `ex.Message` GÖSTERME. SqlException → connection string sızabilir.
   ```csharp
   catch (SqlException sqlEx) {
       _logger.LogError(sqlEx, "DB error");
       TempData["Error"] = "Veritabanı işleminde hata oluştu.";
   }
   ```

8. **CompanyId disiplini (single-tenant kural rağmen):**
   - Tüm SELECT/UPDATE/DELETE'te `WHERE CompanyId = @CompanyId` filtresi
   - PageModel'de `ICurrentCompany.Id` kullan
   - SP girişlerinde `@CompanyId` parametre zorunlu
   - Multi-company test ortamında veri sızıntısı engellenir

9. **Password hashing:** ASP.NET Core Identity default (PBKDF2). Custom hash yazma.

10. **NCalc (DataTable.Compute YASAK):**
    Kullanıcı formülü değerlendirmek için `DataTable.Compute()` formula injection'a açıktır. NCalc kullan (`.claude/rules/coding-discipline.md` §4).

11. **Mass Assignment:**
    - PageModel'de `[BindNever]` kritik alanlarda (CompanyId, CreatedBy, IsDeleted, Status değişimi)
    - DTO record kullan (Entity bind etme)

12. **Evrak Bütünlüğü:** POSTED evrak child kayıt varsa düzenleme **engellenir**. `DocumentLock` helper (yazılacak) + SP-level guard + DB trigger. Detay: `.claude/rules/document-immutability.md`.

## Audit Log Kapsamı

Her kritik aksiyon `AuditLog` tablosuna yazılır:
- Login/logout (başarılı + başarısız)
- Password change
- User/Role create/update/delete
- Belge POSTED / CANCELLED (PO, Receiving, SO, Shipping, Invoice)
- Çek statü değişimi (DEPOSITED, COLLECTED, RETURNED)
- Kredi taksit ödemesi
- Export işlemi
- Şirket değiştirme (`/api/switch-company`)

Log'lanmazsa: bilmeyiz → bir şey olmuş gibi davranılır → audit gap.

## Security Review Ritüeli

1. **Yazım sırasında:** Bu dosyadaki kuralları uygula (proaktif)
2. **Commit öncesi:** `security-reviewer` agent çağır (`.claude/agents/security-reviewer.md`)
3. **Büyük değişiklik sonrası:** `code-reviewer` + `security-reviewer` paralel

## İlişkili

- `.claude/rules/sql-conventions.md` — Parametreli sorgu detay
- `.claude/rules/architecture.md` — Single-tenant + CompanyId
- `.claude/rules/document-immutability.md` — Evrak kilitleme
- `.claude/agents/security-reviewer.md` — Otomatik denetleyici
