using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Operax.Web.Lib;
using Operax.Web.Lib.Authz;
using Xunit;

namespace Operax.Tests.Integration;

/// <summary>
/// Firma üyeliği iptalinin aktif oturuma yansıması (audit H-2): UserCompany.IsActive=0 ise
/// ModuleAccessHandler erişimi reddeder — cookie'deki eski rol claim'i (Administrator dahil) geçersiz.
/// Aktif üyelikte (IsActive=1) erişim sürer. Plan 37 Faz 4.
/// </summary>
[Collection("Database")]
public sealed class MembershipRevocationTests(DatabaseFixture fixture)
{
    private static readonly Guid SystemCompany = new("00000000-0000-0000-0000-000000000000");

    private Db TestDb() => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = fixture.ConnectionString
        }).Build());

    [Fact]
    public async Task RevokedMembership_Denies_Access_Even_For_Administrator()
    {
        var userId = SeedUserWithMembership(isActive: false);
        var context = BuildContext(userId, Roles.Administrator);

        await NewHandler().HandleAsync(context);

        // İş kuralı (H-2): üyelik iptal edilmişse Administrator bile reddedilir
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task ActiveMembership_Allows_Administrator_Access()
    {
        var userId = SeedUserWithMembership(isActive: true);
        var context = BuildContext(userId, Roles.Administrator);

        await NewHandler().HandleAsync(context);

        // İş kuralı: aktif üyelikte Administrator tüm modüllere erişir (bypass)
        Assert.True(context.HasSucceeded);
    }

    /// <summary>Her test taze cache ile yeni handler kurar (ucr: üyelik anahtarı testler arası sızmaz).</summary>
    private ModuleAccessHandler NewHandler() =>
        new(TestDb(), new HttpContextAccessor(), new MemoryCache(new MemoryCacheOptions()));

    /// <summary>SYSTEM şirketinde taze kullanıcı + UserCompany üyelik satırı kurar (verilen IsActive ile).</summary>
    private string SeedUserWithMembership(bool isActive)
    {
        var userId = Guid.NewGuid().ToString();
        var sfx = userId.Replace("-", "")[..8];
        using var conn = new SqlConnection(fixture.ConnectionString);
        conn.Open();
        conn.Execute(@"
            INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, EmailConfirmed,
                                     PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
                VALUES (@Id, 'mr-' + @sfx, 'MR-' + @sfx, 1, 0, 0, 1, 0);
            INSERT INTO UserCompany (UserId, CompanyId, Role, IsActive, CreatedAt)
                VALUES (@Id, @C, 'Administrator', @Active, GETUTCDATE());",
            new { Id = userId, C = SystemCompany, Active = isActive, sfx });
        return userId;
    }

    /// <summary>NameIdentifier + company + rol claim'leriyle kimliği doğrulanmış bir yetki bağlamı kurar.</summary>
    private static AuthorizationHandlerContext BuildContext(string userId, string role)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("company", SystemCompany.ToString()),
                new Claim(ClaimTypes.Role, role)
            },
            authenticationType: "TestAuth"); // non-null → IsAuthenticated = true
        var principal = new ClaimsPrincipal(identity);
        var requirement = new ModuleAccessRequirement("Inventory");
        return new AuthorizationHandlerContext(new[] { requirement }, principal, resource: null);
    }
}
