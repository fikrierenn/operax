using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Operax.Web.Lib;

/// <summary>
/// Plan 13 (Model 3) — Firma-bağlamlı rol çözümleyici.
/// Identity oturum principal'i üretilirken (login + RefreshSignInAsync) global AspNetUserRoles
/// yerine, aktif "company" claim'ine karşılık gelen UserCompany.Role rol claim'i olarak yazılır.
/// Böylece kullanıcı A firmasında 'Finance', B firmasında 'Sales' olabilir; rol kişiye değil
/// kişi+firma çiftine aittir. switch-company company claim'ini değiştirip RefreshSignInAsync
/// çağırdığında bu factory yeniden çalışır ve rol otomatik aktif firmaya göre güncellenir.
/// </summary>
public sealed class CompanyAwareClaimsPrincipalFactory(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options,
    Db db)
    : UserClaimsPrincipalFactory<IdentityUser, IdentityRole>(userManager, roleManager, options)
{
    public override async Task<ClaimsPrincipal> CreateAsync(IdentityUser user)
    {
        // Önce Identity'nin standart principal'ini üret (UserName, stored claim'ler — "company" dahil — ve global roller)
        var principal = await base.CreateAsync(user);
        if (principal.Identity is not ClaimsIdentity identity) return principal;

        // İş kuralı: aktif firma yoksa firma-bağlamlı rol çözülemez; global roller olduğu gibi kalır
        var companyIdStr = identity.FindFirst("company")?.Value;
        if (string.IsNullOrEmpty(companyIdStr) || !Guid.TryParse(companyIdStr, out var companyId))
            return principal;

        // Aktif firmadaki rolü UserCompany'den oku (yalnızca aktif erişim)
        using var conn = db.Open();
        var companyRole = await conn.ExecuteScalarAsync<string?>(
            "SELECT Role FROM UserCompany WHERE UserId = @UserId AND CompanyId = @CompanyId AND IsActive = 1",
            new { UserId = user.Id, CompanyId = companyId });

        // İş kuralı: global rolleri firma-bağlamlı rolle değiştir — A'nın rolüyle B'de dolaşma engellenir
        foreach (var stale in identity.FindAll(identity.RoleClaimType).ToList())
            identity.RemoveClaim(stale);

        // İş kuralı: bu firmada tanımlı rol varsa rol claim'i olarak yaz; yoksa hiçbir rol verilmez (en az ayrıcalık)
        if (!string.IsNullOrWhiteSpace(companyRole))
            identity.AddClaim(new Claim(identity.RoleClaimType, companyRole));

        return principal;
    }
}
