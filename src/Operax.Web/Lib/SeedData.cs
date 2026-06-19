using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Operax.Web.Lib.Authz;

namespace Operax.Web.Lib;

/// <summary>
/// Uygulama ilk ayağa kalktığında zorunlu başlangıç verilerini oluşturur.
/// Tablolar zaten dolduysa (IF NOT EXISTS mantığı) işlem atlanır.
/// Üretim ortamında şifreyi environment variable ile ezmek önerilir.
/// </summary>
public static class SeedData
{
    // Varsayılan admin şifresi — üretimde ADMIN_PASSWORD env variable ile değiştirilmeli
    private const string DefaultAdminEmail    = "admin@operax.com";
    private const string DefaultAdminPassword = "Admin123!";
    private const string DefaultCompanyName   = "Merkez Lojistik";

    /// <summary>
    /// Admin kullanıcısı, Admin rolü ve şirket kaydını oluşturur.
    /// Her çalışmada mevcut kayıt varsa atlar.
    /// </summary>
    public static async Task EnsureAdminAsync(IServiceProvider services)
    {
        // DI scope açılır — scoped servisler (UserManager vb.) ancak scope içinde çalışır
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db          = scope.ServiceProvider.GetRequiredService<Db>();
        var logger      = scope.ServiceProvider.GetRequiredService<ILogger<SeedDataMarker>>();
        var env         = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        // --- 1. F0.2: 7 temel rolü oluştur (RBAC) — mevcut olmayanlar eklenir ---
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                logger.LogInformation("Seed: {Role} rolü oluşturuluyor...", role);
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // --- 1b. F0.2: Şablon rollerin default modül erişimlerini seed et (idempotent) ---
        await SeedDefaultRoleAccessAsync(db, roleManager, logger);

        // --- 2. Admin kullanıcısını kontrol et ---
        var admin = await userManager.FindByEmailAsync(DefaultAdminEmail);
        if (admin != null)
        {
            // Development ortamında şifreyi bilinen değere sıfırla (test kolaylığı)
            if (env.IsDevelopment())
            {
                var resetToken  = await userManager.GeneratePasswordResetTokenAsync(admin);
                var resetResult = await userManager.ResetPasswordAsync(admin, resetToken, DefaultAdminPassword);
                if (resetResult.Succeeded)
                    logger.LogInformation("Seed: Admin şifresi Development modunda sıfırlandı.");
                else
                    logger.LogWarning("Seed: Admin şifre sıfırlama başarısız — {Errs}",
                        string.Join(", ", resetResult.Errors.Select(e => e.Description)));
            }

            // Kullanıcı zaten var — company claim + UserCompany erişim kaydı eksikse tamamla
            var claims = await userManager.GetClaimsAsync(admin);
            var companyClaim = claims.FirstOrDefault(c => c.Type == "company");
            if (companyClaim == null)
            {
                // Mevcut kullanıcının company claim'i yoksa ilk aktif şirkete bağla
                using var connFix = db.Open();
                var existingCompanyId = await connFix.QueryFirstOrDefaultAsync<Guid?>(
                    "SELECT Id FROM Company WHERE IsDeleted = 0 AND Id != '00000000-0000-0000-0000-000000000000'");
                if (existingCompanyId.HasValue)
                {
                    await userManager.AddClaimAsync(admin, new Claim("company", existingCompanyId.Value.ToString()));
                    await EnsureUserCompanyAdminAsync(db, admin.Id, existingCompanyId.Value);
                    logger.LogInformation("Seed: Mevcut admin kullanıcısına company claim + UserCompany erişimi eklendi.");
                }
            }
            else if (Guid.TryParse(companyClaim.Value, out var claimedCompanyId))
            {
                // İş kuralı (P0-2): claim var ama UserCompany erişim kaydı yoksa ekle — aksi halde
                // CompanyAwareClaimsPrincipalFactory rolü bulamaz, admin modüllerden kilitlenir
                await EnsureUserCompanyAdminAsync(db, admin.Id, claimedCompanyId);
                logger.LogInformation("Seed: Admin UserCompany erişimi doğrulandı.");
            }
            return;
        }

        logger.LogInformation("Seed: Admin kullanıcısı oluşturuluyor...");

        // --- 3. Admin kullanıcısını oluştur ---
        admin = new IdentityUser
        {
            Id            = Guid.NewGuid().ToString(),
            UserName      = DefaultAdminEmail,
            Email         = DefaultAdminEmail,
            EmailConfirmed = true
        };

        // İş kuralı (P0-1): Parola ADMIN_PASSWORD env'den okunur; üretimde yoksa fail-fast (aşağıda THROW)
        var adminPassword = ResolveAdminPassword(env);
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
        {
            // Kullanıcı oluşturulamazsa hata loglanır (şifre politikası ihlali vb.)
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError("Seed: Admin kullanıcısı oluşturulamadı — {Errors}", errors);
            // İş kuralı: Üretimde admin oluşmazsa sistem yetkisiz kalır → fail-fast
            if (!env.IsDevelopment())
                throw new InvalidOperationException($"Admin kullanıcısı oluşturulamadı: {errors}");
            return;
        }

        // --- 4. Administrator rolüne ata ---
        await userManager.AddToRoleAsync(admin, "Administrator");

        // --- 5. Company kaydı oluştur ---
        using var conn = db.Open();

        // Aynı isimde şirket varsa mevcut Id'yi kullan
        var companyId = await conn.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT Id FROM Company WHERE Name = @Name AND IsDeleted = 0",
            new { Name = DefaultCompanyName });

        if (!companyId.HasValue)
        {
            // Yeni şirket kaydı oluşturulur
            companyId = Guid.NewGuid();
            await conn.ExecuteAsync(@"
                INSERT INTO Company (Id, Name, IsActive, CreatedAt, IsDeleted)
                VALUES (@Id, @Name, 1, GETUTCDATE(), 0)",
                new { Id = companyId.Value, Name = DefaultCompanyName });
            logger.LogInformation("Seed: '{CompanyName}' şirketi oluşturuldu.", DefaultCompanyName);
        }

        // --- 6. Admin kullanıcısına company claim ekle ---
        // Bu claim cookie'ye yazılarak CurrentCompany.Id'nin doğru çalışmasını sağlar
        await userManager.AddClaimAsync(admin, new Claim("company", companyId.Value.ToString()));

        // İş kuralı (P0-2): company claim ile ATOMİK Administrator UserCompany erişimi
        await EnsureUserCompanyAdminAsync(db, admin.Id, companyId.Value);

        logger.LogInformation("Seed: Tamamlandı — admin@operax.com / şirket: {CompanyName}", DefaultCompanyName);
    }

    /// <summary>
    /// Admin parolasını çözer: ADMIN_PASSWORD env önceliklidir; Development'ta sabit fallback'e izin verilir,
    /// üretimde env yoksa fail-fast (varsayılan parola ile başlatma engellenir — audit P0-1).
    /// </summary>
    private static string ResolveAdminPassword(IWebHostEnvironment env)
    {
        var envPwd = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        if (!string.IsNullOrWhiteSpace(envPwd)) return envPwd;

        // İş kuralı: Üretimde varsayılan parola ile başlatma güvenlik açığıdır → fail-fast
        if (!env.IsDevelopment())
            throw new InvalidOperationException(
                "ADMIN_PASSWORD ortam değişkeni tanımlı değil — üretimde varsayılan parola ile başlatma engellendi.");

        return DefaultAdminPassword;
    }

    /// <summary>
    /// Admin'e firma-bağlamlı Administrator erişimini idempotent yazar (audit P0-2).
    /// company claim tek başına yetmez; CompanyAwareClaimsPrincipalFactory rolü UserCompany'den okur.
    /// </summary>
    private static async Task EnsureUserCompanyAdminAsync(Db db, string userId, Guid companyId)
    {
        using var conn = db.Open();
        // İş kuralı: Mevcut erişim varsa dokunma (idempotent) — admin'in sonradan değişen rolünü ezme
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM UserCompany WHERE UserId = @UserId AND CompanyId = @CompanyId)
                INSERT INTO UserCompany (UserId, CompanyId, Role, IsActive, CreatedAt)
                VALUES (@UserId, @CompanyId, @Role, 1, GETUTCDATE());",
            new { UserId = userId, CompanyId = companyId, Role = Roles.Administrator });
    }

    /// <summary>
    /// Şablon rollerin default RoleModuleAccess satırlarını idempotent ekler.
    /// Administrator hariç tutulur (handler tüm modülleri bypass eder).
    /// Mevcut satıra dokunmaz → admin'in UI'den yaptığı değişiklik ezilmez.
    /// </summary>
    private static async Task SeedDefaultRoleAccessAsync(Db db, RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        // Rol → (EDIT modülleri, VIEW modülleri) default şablonu (plan 17 §4)
        var defaults = new (string Role, string[] Edit, string[] View)[]
        {
            (Roles.WarehouseManager,
                ["Receiving", "Shipping", "Transfer", "CycleCount", "LPN", "Lot", "Serial", "Picking", "Inventory", "Warehouses", "MaterialIssue"],
                ["Dashboard", "MasterData"]),
            (Roles.Finance,       ["Finance", "PurchaseInvoices"],                          ["Dashboard", "MasterData"]),
            (Roles.Purchasing,    ["PurchaseOrders", "PurchaseInvoices", "Expenses", "Budget", "MaterialIssue"], ["Dashboard", "MasterData"]),
            (Roles.Sales,         ["SalesOrders", "SalesInvoices"],         ["Dashboard", "MasterData"]),
            (Roles.Manufacturing, ["Manufacturing", "Production"],          ["Dashboard", "MasterData"]),
            (Roles.Viewer,        [],                                       ["Dashboard", "MasterData"]),
        };

        using var conn = db.Open();
        foreach (var (roleName, editKeys, viewKeys) in defaults)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null) continue;

            foreach (var key in editKeys)
                await UpsertAccessAsync(conn, role.Id, key, ModuleKeys.AccessEdit);
            foreach (var key in viewKeys)
                await UpsertAccessAsync(conn, role.Id, key, ModuleKeys.AccessView);
        }
        logger.LogInformation("Seed: Şablon rol-modül erişimleri kontrol edildi.");
    }

    // Tek RoleModuleAccess satırını idempotent ekler (varsa dokunmaz)
    private static async Task UpsertAccessAsync(System.Data.IDbConnection conn, string roleId, string moduleKey, byte level)
    {
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM RoleModuleAccess WHERE RoleId = @RoleId AND ModuleKey = @ModuleKey)
                INSERT INTO RoleModuleAccess (Id, RoleId, ModuleKey, AccessLevel)
                VALUES (NEWID(), @RoleId, @ModuleKey, @Level)",
            new { RoleId = roleId, ModuleKey = moduleKey, Level = level });
    }
}

/// <summary>Logger kategori adı için işaretçi sınıf.</summary>
internal class SeedDataMarker { }
