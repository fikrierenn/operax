using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Identity;

namespace Operax.Web.Lib;

/// <summary>
/// ASP.NET Core Identity kullanıcı deposu.
/// EF Core yerine Dapper kullanarak AspNetUsers tablosunu yönetir.
/// SignInManager ve UserManager'ın tüm gerekli arayüzlerini karşılar.
/// IUserClaimStore sayesinde "company" claim'i login sırasında cookie'ye yazılır.
/// </summary>
public class DapperUserStore(Db db) :
    IUserStore<IdentityUser>,
    IUserPasswordStore<IdentityUser>,
    IUserEmailStore<IdentityUser>,
    IUserSecurityStampStore<IdentityUser>,
    IUserLockoutStore<IdentityUser>,
    IUserTwoFactorStore<IdentityUser>,
    IUserRoleStore<IdentityUser>,
    IUserClaimStore<IdentityUser>
{
    // --- IUserStore ---

    /// <summary>Kullanıcının benzersiz Id'sini döndürür.</summary>
    public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.Id);

    /// <summary>Kullanıcı adını döndürür.</summary>
    public Task<string?> GetUserNameAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.UserName);

    public Task SetUserNameAsync(IdentityUser user, string? userName, CancellationToken ct)
    { user.UserName = userName; return Task.CompletedTask; }

    public Task<string?> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(IdentityUser user, string? normalizedName, CancellationToken ct)
    { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }

    /// <summary>Yeni kullanıcı kaydı oluşturur.</summary>
    public async Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken ct)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO AspNetUsers (
                Id, UserName, NormalizedUserName, Email, NormalizedEmail,
                EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
                PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
            VALUES (
                @Id, @UserName, @NormalizedUserName, @Email, @NormalizedEmail,
                @EmailConfirmed, @PasswordHash, @SecurityStamp, @ConcurrencyStamp,
                0, 0, 1, 0)", user, cancellationToken: ct));
        return IdentityResult.Success;
    }

    /// <summary>Kullanıcı bilgilerini günceller (şifre, güvenlik damgası, kilitleme vb.).</summary>
    public async Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken ct)
    {
        using var conn = db.Open();
        // İş kuralı (H-4): optimistic concurrency — eski damga WHERE'de doğrulanır, yeni damga yazılır.
        // Başka oturum aradan güncellediyse 0 satır etkilenir → ConcurrencyFailure (kayıp güncelleme engellenir).
        var oldStamp = user.ConcurrencyStamp;
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        var parms = new DynamicParameters(user);
        parms.Add("OldStamp", oldStamp);
        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE AspNetUsers SET
                UserName           = @UserName,
                NormalizedUserName = @NormalizedUserName,
                Email              = @Email,
                NormalizedEmail    = @NormalizedEmail,
                EmailConfirmed     = @EmailConfirmed,
                PasswordHash       = @PasswordHash,
                SecurityStamp      = @SecurityStamp,
                ConcurrencyStamp   = @ConcurrencyStamp,
                LockoutEnd         = @LockoutEnd,
                LockoutEnabled     = @LockoutEnabled,
                AccessFailedCount  = @AccessFailedCount,
                TwoFactorEnabled   = @TwoFactorEnabled
            WHERE Id = @Id AND (ConcurrencyStamp = @OldStamp OR @OldStamp IS NULL)",
            parms, cancellationToken: ct));
        // Etkilenen satır yoksa damga eskidi → eşzamanlı düzenleme çakışması
        if (affected == 0)
            return IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure());
        return IdentityResult.Success;
    }

    /// <summary>Kullanıcı kaydını siler.</summary>
    public async Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken ct)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AspNetUsers WHERE Id = @Id", new { user.Id }, cancellationToken: ct));
        return IdentityResult.Success;
    }

    /// <summary>Id ile kullanıcı bulur.</summary>
    public async Task<IdentityUser?> FindByIdAsync(string userId, CancellationToken ct)
    {
        using var conn = db.Open();
        return await conn.QueryFirstOrDefaultAsync<IdentityUser>(new CommandDefinition(
            "SELECT * FROM AspNetUsers WHERE Id = @Id", new { Id = userId }, cancellationToken: ct));
    }

    /// <summary>Normalize edilmiş kullanıcı adına göre bulur (oturum açma için).</summary>
    public async Task<IdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct)
    {
        using var conn = db.Open();
        return await conn.QueryFirstOrDefaultAsync<IdentityUser>(new CommandDefinition(
            "SELECT * FROM AspNetUsers WHERE NormalizedUserName = @Name", new { Name = normalizedUserName }, cancellationToken: ct));
    }

    // --- IUserPasswordStore ---

    public Task SetPasswordHashAsync(IdentityUser user, string? passwordHash, CancellationToken ct)
    { user.PasswordHash = passwordHash; return Task.CompletedTask; }

    public Task<string?> GetPasswordHashAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.PasswordHash != null);

    // --- IUserEmailStore ---

    public Task SetEmailAsync(IdentityUser user, string? email, CancellationToken ct)
    { user.Email = email; return Task.CompletedTask; }

    public Task<string?> GetEmailAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(IdentityUser user, bool confirmed, CancellationToken ct)
    { user.EmailConfirmed = confirmed; return Task.CompletedTask; }

    /// <summary>E-posta adresine göre kullanıcı bulur.</summary>
    public async Task<IdentityUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        using var conn = db.Open();
        return await conn.QueryFirstOrDefaultAsync<IdentityUser>(new CommandDefinition(
            "SELECT * FROM AspNetUsers WHERE NormalizedEmail = @Email", new { Email = normalizedEmail }, cancellationToken: ct));
    }

    public Task<string?> GetNormalizedEmailAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(IdentityUser user, string? normalizedEmail, CancellationToken ct)
    { user.NormalizedEmail = normalizedEmail; return Task.CompletedTask; }

    // --- IUserSecurityStampStore ---
    // Güvenlik damgası: şifre değiştiğinde veya oturum geçersiz kılındığında güncellenir

    public Task SetSecurityStampAsync(IdentityUser user, string stamp, CancellationToken ct)
    { user.SecurityStamp = stamp; return Task.CompletedTask; }

    public Task<string?> GetSecurityStampAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.SecurityStamp);

    // --- IUserLockoutStore ---
    // Yanlış şifre denemelerinde hesap kilitleme yönetimi

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(IdentityUser user, DateTimeOffset? lockoutEnd, CancellationToken ct)
    { user.LockoutEnd = lockoutEnd; return Task.CompletedTask; }

    public Task<int> IncrementAccessFailedCountAsync(IdentityUser user, CancellationToken ct)
    { user.AccessFailedCount++; return Task.FromResult(user.AccessFailedCount); }

    public Task ResetAccessFailedCountAsync(IdentityUser user, CancellationToken ct)
    { user.AccessFailedCount = 0; return Task.CompletedTask; }

    public Task<int> GetAccessFailedCountAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(IdentityUser user, bool enabled, CancellationToken ct)
    { user.LockoutEnabled = enabled; return Task.CompletedTask; }

    // --- IUserTwoFactorStore ---
    // 2FA desteği — şimdilik devre dışı, ilerleyen sprintlerde açılabilir

    public Task SetTwoFactorEnabledAsync(IdentityUser user, bool enabled, CancellationToken ct)
    { user.TwoFactorEnabled = enabled; return Task.CompletedTask; }

    public Task<bool> GetTwoFactorEnabledAsync(IdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.TwoFactorEnabled);

    // --- IUserRoleStore ---
    // Kullanıcı-rol ilişkisi yönetimi (AspNetUserRoles tablosu)

    /// <summary>Kullanıcıyı belirtilen role ekler.</summary>
    public async Task AddToRoleAsync(IdentityUser user, string roleName, CancellationToken ct)
    {
        using var conn = db.Open();
        // Rol Id'sini bul
        var roleId = await conn.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            "SELECT Id FROM AspNetRoles WHERE NormalizedName = @Name",
            new { Name = roleName.ToUpperInvariant() }, cancellationToken: ct));

        if (roleId is null) return;

        // Mükerrer kayıt kontrolü yaparak ekle
        await conn.ExecuteAsync(new CommandDefinition(@"
            IF NOT EXISTS (SELECT 1 FROM AspNetUserRoles WHERE UserId = @UserId AND RoleId = @RoleId)
                INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@UserId, @RoleId)",
            new { UserId = user.Id, RoleId = roleId }, cancellationToken: ct));
    }

    /// <summary>Kullanıcıyı rolden çıkarır.</summary>
    public async Task RemoveFromRoleAsync(IdentityUser user, string roleName, CancellationToken ct)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(@"
            DELETE ur FROM AspNetUserRoles ur
            JOIN AspNetRoles r ON r.Id = ur.RoleId
            WHERE ur.UserId = @UserId AND r.NormalizedName = @Name",
            new { UserId = user.Id, Name = roleName.ToUpperInvariant() }, cancellationToken: ct));
    }

    /// <summary>Kullanıcının tüm rollerini listeler.</summary>
    public async Task<IList<string>> GetRolesAsync(IdentityUser user, CancellationToken ct)
    {
        using var conn = db.Open();
        var roles = await conn.QueryAsync<string>(new CommandDefinition(@"
            SELECT r.Name FROM AspNetRoles r
            JOIN AspNetUserRoles ur ON ur.RoleId = r.Id
            WHERE ur.UserId = @UserId", new { UserId = user.Id }, cancellationToken: ct));
        return roles.ToList();
    }

    /// <summary>Kullanıcının belirli bir rolde olup olmadığını kontrol eder.</summary>
    public async Task<bool> IsInRoleAsync(IdentityUser user, string roleName, CancellationToken ct)
    {
        using var conn = db.Open();
        var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(1) FROM AspNetUserRoles ur
            JOIN AspNetRoles r ON r.Id = ur.RoleId
            WHERE ur.UserId = @UserId AND r.NormalizedName = @Name",
            new { UserId = user.Id, Name = roleName.ToUpperInvariant() }, cancellationToken: ct));
        return count > 0;
    }

    /// <summary>Belirli bir roldeki tüm kullanıcıları listeler.</summary>
    public async Task<IList<IdentityUser>> GetUsersInRoleAsync(string roleName, CancellationToken ct)
    {
        using var conn = db.Open();
        var users = await conn.QueryAsync<IdentityUser>(new CommandDefinition(@"
            SELECT u.* FROM AspNetUsers u
            JOIN AspNetUserRoles ur ON ur.UserId = u.Id
            JOIN AspNetRoles r ON r.Id = ur.RoleId
            WHERE r.NormalizedName = @Name",
            new { Name = roleName.ToUpperInvariant() }, cancellationToken: ct));
        return users.ToList();
    }

    // --- IUserClaimStore ---
    // AspNetUserClaims tablosu üzerinden kullanıcı claim'lerini yönetir.
    // "company" claim'i buradan okunarak SignInManager tarafından cookie'ye yazılır.

    /// <summary>Kullanıcıya ait tüm claim'leri getirir (company, özel yetkiler vb.).</summary>
    public async Task<IList<Claim>> GetClaimsAsync(IdentityUser user, CancellationToken ct)
    {
        using var conn = db.Open();
        // AspNetUserClaims tablosundan kullanıcının tüm ek claim'leri çekilir
        var rows = await conn.QueryAsync<(string Type, string Value)>(new CommandDefinition(
            "SELECT ClaimType, ClaimValue FROM AspNetUserClaims WHERE UserId = @UserId",
            new { UserId = user.Id }, cancellationToken: ct));
        return rows.Select(r => new Claim(r.Type, r.Value)).ToList();
    }

    /// <summary>Kullanıcıya yeni claim'ler ekler.</summary>
    public async Task AddClaimsAsync(IdentityUser user, IEnumerable<Claim> claims, CancellationToken ct)
    {
        using var conn = db.Open();
        // Her claim için yeni bir AspNetUserClaims satırı oluşturulur
        foreach (var claim in claims)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO AspNetUserClaims (UserId, ClaimType, ClaimValue) VALUES (@UserId, @Type, @Value)",
                new { UserId = user.Id, Type = claim.Type, Value = claim.Value }, cancellationToken: ct));
        }
    }

    /// <summary>Kullanıcının mevcut bir claim'ini yenisiyle değiştirir.</summary>
    public async Task ReplaceClaimAsync(IdentityUser user, Claim claim, Claim newClaim, CancellationToken ct)
    {
        using var conn = db.Open();
        // Eski claim bulunarak yeni değerle güncellenir
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE AspNetUserClaims
            SET ClaimType = @NewType, ClaimValue = @NewValue
            WHERE UserId = @UserId AND ClaimType = @OldType AND ClaimValue = @OldValue",
            new { UserId = user.Id, OldType = claim.Type, OldValue = claim.Value, NewType = newClaim.Type, NewValue = newClaim.Value },
            cancellationToken: ct));
    }

    /// <summary>Kullanıcıdan belirtilen claim'leri kaldırır.</summary>
    public async Task RemoveClaimsAsync(IdentityUser user, IEnumerable<Claim> claims, CancellationToken ct)
    {
        using var conn = db.Open();
        // Her claim için eşleşen satır silinir
        foreach (var claim in claims)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "DELETE FROM AspNetUserClaims WHERE UserId = @UserId AND ClaimType = @Type AND ClaimValue = @Value",
                new { UserId = user.Id, Type = claim.Type, Value = claim.Value }, cancellationToken: ct));
        }
    }

    /// <summary>Belirli bir claim'e sahip tüm kullanıcıları listeler.</summary>
    public async Task<IList<IdentityUser>> GetUsersForClaimAsync(Claim claim, CancellationToken ct)
    {
        using var conn = db.Open();
        // Claim değerine göre kullanıcılar çekilir (örneğin belirli bir şirketteki tüm kullanıcılar)
        var users = await conn.QueryAsync<IdentityUser>(new CommandDefinition(@"
            SELECT u.* FROM AspNetUsers u
            JOIN AspNetUserClaims c ON c.UserId = u.Id
            WHERE c.ClaimType = @Type AND c.ClaimValue = @Value",
            new { Type = claim.Type, Value = claim.Value }, cancellationToken: ct));
        return users.ToList();
    }

    public void Dispose() { }
}
