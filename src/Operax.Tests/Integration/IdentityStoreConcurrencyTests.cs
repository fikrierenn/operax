using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Operax.Web.Lib;
using Xunit;

namespace Operax.Tests.Integration;

/// <summary>
/// Identity depolarının (DapperUserStore / DapperRoleStore) optimistic concurrency doğruluğu (audit H-4):
/// eski ConcurrencyStamp WHERE'de doğrulanır; aradan başka oturum güncellediyse 0 satır → ConcurrencyFailure
/// (kayıp güncelleme engellenir). Plan 37 Faz 4.
/// </summary>
[Collection("Database")]
public sealed class IdentityStoreConcurrencyTests(DatabaseFixture fixture)
{
    private Db TestDb() => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = fixture.ConnectionString
        }).Build());

    [Fact]
    public async Task UserStore_Update_With_Stale_Stamp_Returns_ConcurrencyFailure()
    {
        var store = new DapperUserStore(TestDb());
        var sfx = Guid.NewGuid().ToString("N")[..8];
        var user = new IdentityUser
        {
            Id                 = Guid.NewGuid().ToString(),
            UserName           = "ccu-" + sfx,
            NormalizedUserName = "CCU-" + sfx,
            Email              = sfx + "@test.local",
            NormalizedEmail    = (sfx + "@TEST.LOCAL").ToUpperInvariant(),
            PasswordHash       = "x",
            SecurityStamp      = Guid.NewGuid().ToString(),
            ConcurrencyStamp   = Guid.NewGuid().ToString()
        };
        Assert.True((await store.CreateAsync(user, CancellationToken.None)).Succeeded);

        // İki bağımsız oturum aynı (eski) damgayla kullanıcıyı okur
        var session1 = await store.FindByIdAsync(user.Id, CancellationToken.None);
        var session2 = await store.FindByIdAsync(user.Id, CancellationToken.None);
        Assert.NotNull(session1);
        Assert.NotNull(session2);

        // İlk oturumun güncellemesi geçer ve damgayı döndürür
        var first = await store.UpdateAsync(session1!, CancellationToken.None);
        Assert.True(first.Succeeded);

        // İkinci oturum hâlâ eski damgayı taşır → WHERE eşleşmez → ConcurrencyFailure
        var second = await store.UpdateAsync(session2!, CancellationToken.None);
        Assert.False(second.Succeeded);
        Assert.Contains(second.Errors, e => e.Code == new IdentityErrorDescriber().ConcurrencyFailure().Code);
    }

    [Fact]
    public async Task UserStore_Update_With_Current_Stamp_Succeeds()
    {
        var store = new DapperUserStore(TestDb());
        var sfx = Guid.NewGuid().ToString("N")[..8];
        var user = new IdentityUser
        {
            Id                 = Guid.NewGuid().ToString(),
            UserName           = "ccu-ok-" + sfx,
            NormalizedUserName = "CCU-OK-" + sfx,
            Email              = sfx + "@test.local",
            NormalizedEmail    = (sfx + "@TEST.LOCAL").ToUpperInvariant(),
            PasswordHash       = "x",
            SecurityStamp      = Guid.NewGuid().ToString(),
            ConcurrencyStamp   = Guid.NewGuid().ToString()
        };
        await store.CreateAsync(user, CancellationToken.None);

        var loaded = await store.FindByIdAsync(user.Id, CancellationToken.None);
        var result = await store.UpdateAsync(loaded!, CancellationToken.None);

        // Güncel damgayla güncelleme başarılı olur ve DB damgayı rotasyona uğratır
        Assert.True(result.Succeeded);
        using var conn = new SqlConnection(fixture.ConnectionString);
        var dbStamp = conn.ExecuteScalar<string>(
            "SELECT ConcurrencyStamp FROM AspNetUsers WHERE Id = @Id", new { user.Id });
        Assert.NotEqual(user.ConcurrencyStamp, dbStamp); // ctor'daki ilk damga değil, yeni damga
    }

    [Fact]
    public async Task RoleStore_Update_With_Stale_Stamp_Returns_ConcurrencyFailure()
    {
        var store = new DapperRoleStore(TestDb());
        var sfx = Guid.NewGuid().ToString("N")[..8];
        var role = new IdentityRole
        {
            Id               = Guid.NewGuid().ToString(),
            Name             = "ccrole-" + sfx,
            NormalizedName   = "CCROLE-" + sfx,
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
        Assert.True((await store.CreateAsync(role, CancellationToken.None)).Succeeded);

        var session1 = await store.FindByIdAsync(role.Id, CancellationToken.None);
        var session2 = await store.FindByIdAsync(role.Id, CancellationToken.None);

        var first = await store.UpdateAsync(session1!, CancellationToken.None);
        Assert.True(first.Succeeded);

        // İkinci oturum eski damga ile → ConcurrencyFailure
        var second = await store.UpdateAsync(session2!, CancellationToken.None);
        Assert.False(second.Succeeded);
        Assert.Contains(second.Errors, e => e.Code == new IdentityErrorDescriber().ConcurrencyFailure().Code);
    }
}
