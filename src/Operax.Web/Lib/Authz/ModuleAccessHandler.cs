using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace Operax.Web.Lib.Authz;

/// <summary>
/// Rol-modül yetkisini DB'den (RoleModuleAccess) okuyarak değerlendiren handler.
/// Administrator tüm modüllere erişir (bypass). POST/PUT/DELETE → EDIT (2) şart; GET → VIEW (1) yeterli.
/// Rol erişim haritası IMemoryCache'te tutulur; admin izin değiştirince ilgili anahtar invalidate edilir.
/// </summary>
public sealed class ModuleAccessHandler(Db db, IHttpContextAccessor http, IMemoryCache cache)
    : AuthorizationHandler<ModuleAccessRequirement>
{
    // Rol yetki haritası cache anahtarı (Admin/Roles izin kaydında bu önek + rol adı ile invalidate edilir)
    public const string CacheKeyPrefix = "rma:";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ModuleAccessRequirement requirement)
    {
        // İş kuralı: kimlik doğrulanmamışsa yetki verilmez (challenge → login'e yönlenir)
        if (context.User.Identity?.IsAuthenticated != true) return;

        // İş kuralı: Administrator tüm modüllere tam erişir
        if (context.User.IsInRole(Roles.Administrator))
        {
            context.Succeed(requirement);
            return;
        }

        var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Count == 0) return;

        // İş kuralı: değiştiren HTTP metodu (POST/PUT/DELETE) EDIT seviyesi ister; GET/HEAD için VIEW yeterli
        var method = http.HttpContext?.Request.Method ?? HttpMethods.Get;
        byte required = HttpMethods.IsGet(method) || HttpMethods.IsHead(method)
            ? ModuleKeys.AccessView
            : ModuleKeys.AccessEdit;

        // Kullanıcının rollerinden herhangi biri bu modüle yeterli seviyede erişiyorsa izin ver
        foreach (var role in roles)
        {
            var access = await GetRoleAccessAsync(role);
            if (access.TryGetValue(requirement.ModuleKey, out var level) && level >= required)
            {
                context.Succeed(requirement);
                return;
            }
        }
    }

    // Tek rolün modül erişim haritasını (ModuleKey → AccessLevel) cache'li getirir
    private async Task<Dictionary<string, byte>> GetRoleAccessAsync(string roleName)
    {
        return (await cache.GetOrCreateAsync(CacheKeyPrefix + roleName, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);
            using var conn = db.Open();
            // İlgili rolün tüm modül erişimlerini oku (rol adı → AspNetRoles.Id join)
            var rows = await conn.QueryAsync<AccessRow>(@"
                SELECT rma.ModuleKey, rma.AccessLevel
                FROM RoleModuleAccess rma
                JOIN AspNetRoles r ON r.Id = rma.RoleId
                WHERE r.Name = @Role", new { Role = roleName });
            return rows.ToDictionary(x => x.ModuleKey, x => x.AccessLevel, StringComparer.OrdinalIgnoreCase);
        }))!;
    }

    private sealed record AccessRow(string ModuleKey, byte AccessLevel);
}
