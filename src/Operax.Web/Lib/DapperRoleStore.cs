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
        await conn.ExecuteAsync(@"
            INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
            VALUES (@Id, @Name, @NormalizedName, @ConcurrencyStamp)", role);
        return IdentityResult.Success;
    }

    /// <summary>Mevcut rolü günceller.</summary>
    public async Task<IdentityResult> UpdateAsync(IdentityRole role, CancellationToken ct)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(@"
            UPDATE AspNetRoles
            SET Name = @Name, NormalizedName = @NormalizedName, ConcurrencyStamp = @ConcurrencyStamp
            WHERE Id = @Id", role);
        return IdentityResult.Success;
    }

    /// <summary>Rolü siler.</summary>
    public async Task<IdentityResult> DeleteAsync(IdentityRole role, CancellationToken ct)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("DELETE FROM AspNetRoles WHERE Id = @Id", new { role.Id });
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
        return await conn.QueryFirstOrDefaultAsync<IdentityRole>(
            "SELECT * FROM AspNetRoles WHERE Id = @Id", new { Id = roleId });
    }

    /// <summary>Normalize edilmiş isimle rol bulur.</summary>
    public async Task<IdentityRole?> FindByNameAsync(string normalizedRoleName, CancellationToken ct)
    {
        using var conn = db.Open();
        return await conn.QueryFirstOrDefaultAsync<IdentityRole>(
            "SELECT * FROM AspNetRoles WHERE NormalizedName = @Name", new { Name = normalizedRoleName });
    }

    public void Dispose() { }
}
