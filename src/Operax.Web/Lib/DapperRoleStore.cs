using Dapper;
using Microsoft.AspNetCore.Identity;

namespace Operax.Web.Lib;

/// <summary>
/// ASP.NET Core Identity rol deposu.
/// EF Core yerine Dapper kullanarak AspNetRoles tablosunu yönetir.
/// </summary>
public class DapperRoleStore(Db db) : IRoleStore<IdentityRole>
{
    /// <summary>Yeni rol oluşturur.</summary>
    public async Task<IdentityResult> CreateAsync(IdentityRole role, CancellationToken ct)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
            VALUES (@Id, @Name, @NormalizedName, @ConcurrencyStamp)", role, cancellationToken: ct));
        return IdentityResult.Success;
    }

    /// <summary>Mevcut rolü günceller.</summary>
    public async Task<IdentityResult> UpdateAsync(IdentityRole role, CancellationToken ct)
    {
        using var conn = db.Open();
        // İş kuralı (H-4): optimistic concurrency — eski damga WHERE'de doğrulanır, yeni damga yazılır.
        var oldStamp = role.ConcurrencyStamp;
        role.ConcurrencyStamp = Guid.NewGuid().ToString();
        var parms = new DynamicParameters(role);
        parms.Add("OldStamp", oldStamp);
        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE AspNetRoles
            SET Name = @Name, NormalizedName = @NormalizedName, ConcurrencyStamp = @ConcurrencyStamp
            WHERE Id = @Id AND (ConcurrencyStamp = @OldStamp OR @OldStamp IS NULL)",
            parms, cancellationToken: ct));
        // Etkilenen satır yoksa damga eskidi → eşzamanlı düzenleme çakışması
        if (affected == 0)
            return IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure());
        return IdentityResult.Success;
    }

    /// <summary>Rolü siler.</summary>
    public async Task<IdentityResult> DeleteAsync(IdentityRole role, CancellationToken ct)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM AspNetRoles WHERE Id = @Id", new { role.Id }, cancellationToken: ct));
        return IdentityResult.Success;
    }

    public Task<string> GetRoleIdAsync(IdentityRole role, CancellationToken ct) =>
        Task.FromResult(role.Id);

    public Task<string?> GetRoleNameAsync(IdentityRole role, CancellationToken ct) =>
        Task.FromResult(role.Name);

    public Task SetRoleNameAsync(IdentityRole role, string? roleName, CancellationToken ct)
    { role.Name = roleName; return Task.CompletedTask; }

    public Task<string?> GetNormalizedRoleNameAsync(IdentityRole role, CancellationToken ct) =>
        Task.FromResult(role.NormalizedName);

    public Task SetNormalizedRoleNameAsync(IdentityRole role, string? normalizedName, CancellationToken ct)
    { role.NormalizedName = normalizedName; return Task.CompletedTask; }

    /// <summary>Id ile rol bulur.</summary>
    public async Task<IdentityRole?> FindByIdAsync(string roleId, CancellationToken ct)
    {
        using var conn = db.Open();
        return await conn.QueryFirstOrDefaultAsync<IdentityRole>(new CommandDefinition(
            "SELECT * FROM AspNetRoles WHERE Id = @Id", new { Id = roleId }, cancellationToken: ct));
    }

    /// <summary>Normalize edilmiş isimle rol bulur.</summary>
    public async Task<IdentityRole?> FindByNameAsync(string normalizedRoleName, CancellationToken ct)
    {
        using var conn = db.Open();
        return await conn.QueryFirstOrDefaultAsync<IdentityRole>(new CommandDefinition(
            "SELECT * FROM AspNetRoles WHERE NormalizedName = @Name", new { Name = normalizedRoleName }, cancellationToken: ct));
    }

    public void Dispose() { }
}
