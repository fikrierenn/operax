using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Admin.Users;

[Authorize(Roles = Operax.Web.Lib.Roles.Administrator)]
public class CreateModel(
    UserManager<IdentityUser> userManager,
    ICurrentCompany company,
    Db db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return Page();

        var user = new IdentityUser
        {
            UserName       = Input.Email,
            Email          = Input.Email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        // Şirket claim'i — oluşturan admin'in şirketine otomatik bağla
        await userManager.AddClaimAsync(user, new Claim("company", company.Id.ToString()));

        // Rol ataması (opsiyonel)
        if (!string.IsNullOrEmpty(Input.Role))
            await userManager.AddToRoleAsync(user, Input.Role);

        // İş kuralı (Plan 13): login'de rol AspNetUserRoles'tan değil firma-bağlamlı UserCompany'den
        // çözülür (CompanyAwareClaimsPrincipalFactory). UserCompany'ye yazılmazsa yeni kullanıcı
        // (Administrator dahil) rolsüz kalır ve tüm yetki politikalarınca reddedilir. Boş rol → Viewer.
        await UpsertUserCompanyAsync(user.Id, string.IsNullOrEmpty(Input.Role) ? Operax.Web.Lib.Roles.Viewer : Input.Role, ct);

        return RedirectToPage("./Index");
    }

    /// <summary>Kullanıcının aktif firmadaki rolünü UserCompany'ye idempotent yazar (firma-bağlamlı rol).</summary>
    private async Task UpsertUserCompanyAsync(string userId, string role, CancellationToken ct = default)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(@"
            MERGE UserCompany AS t
            USING (SELECT @UserId AS UserId, @CompanyId AS CompanyId) AS s
              ON t.UserId = s.UserId AND t.CompanyId = s.CompanyId
            WHEN MATCHED THEN UPDATE SET Role = @Role, IsActive = 1
            WHEN NOT MATCHED THEN
              INSERT (UserId, CompanyId, Role, IsActive, CreatedAt)
              VALUES (@UserId, @CompanyId, @Role, 1, GETUTCDATE());",
            new { UserId = userId, CompanyId = company.Id, Role = role }, cancellationToken: ct));
    }

    public class InputModel
    {
        public string Email    { get; set; } = "";
        public string Password { get; set; } = "";
        /// <summary>"Administrator" veya "" (rol yok = normal kullanıcı)</summary>
        public string Role     { get; set; } = "";
    }
}
