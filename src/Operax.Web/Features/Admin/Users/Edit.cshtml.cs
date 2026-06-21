using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Admin.Users;

[Authorize(Roles = "Administrator")]
public class EditModel(
    UserManager<IdentityUser> userManager,
    ICurrentCompany company,
    Db db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var roles = await userManager.GetRolesAsync(user);

        Input = new InputModel
        {
            Id          = user.Id,
            UserName    = user.UserName ?? "",
            Email       = user.Email ?? "",
            CurrentRole = roles.FirstOrDefault() ?? "",
            Role        = roles.FirstOrDefault() ?? ""
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return Page();

        var user = await userManager.FindByIdAsync(Input.Id);
        if (user == null) return NotFound();

        // E-posta güncelle
        user.Email    = Input.Email;
        user.UserName = Input.Email;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var e in updateResult.Errors) ModelState.AddModelError("", e.Description);
            return Page();
        }

        // Şifre değişikliği (opsiyonel)
        if (!string.IsNullOrEmpty(Input.NewPassword))
        {
            var token      = await userManager.GeneratePasswordResetTokenAsync(user);
            var passResult = await userManager.ResetPasswordAsync(user, token, Input.NewPassword);
            if (!passResult.Succeeded)
            {
                foreach (var e in passResult.Errors) ModelState.AddModelError("", e.Description);
                return Page();
            }
        }

        // Rol güncelleme — önce mevcut rolleri kaldır, sonra yenisini ata
        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Any())
            await userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!string.IsNullOrEmpty(Input.Role))
            await userManager.AddToRoleAsync(user, Input.Role);

        // Company claim — yoksa oluşturan admin'in şirketiyle ekle
        var claims       = await userManager.GetClaimsAsync(user);
        var companyClaim = claims.FirstOrDefault(c => c.Type == "company");
        if (companyClaim == null)
            await userManager.AddClaimAsync(user, new Claim("company", company.Id.ToString()));

        // İş kuralı (Plan 13): rol AspNetUserRoles'tan değil UserCompany'den çözülür
        // (CompanyAwareClaimsPrincipalFactory). Rol değişikliği UserCompany'ye yansımazsa login'de
        // eski/boş rol döner. Aktif firma kaydını güncel rolle hizala (boş rol → Viewer).
        await UpsertUserCompanyAsync(user.Id, string.IsNullOrEmpty(Input.Role) ? Operax.Web.Lib.Roles.Viewer : Input.Role, ct);

        return RedirectToPage("./Index");
    }

    /// <summary>Kullanıcının aktif firmadaki rolünü UserCompany'ye idempotent hizalar (firma-bağlamlı rol).</summary>
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
        public string Id          { get; set; } = "";
        public string UserName    { get; set; } = "";
        public string Email       { get; set; } = "";
        public string NewPassword { get; set; } = "";
        public string CurrentRole { get; set; } = "";
        /// <summary>"Administrator" veya "" (normal kullanıcı)</summary>
        public string Role        { get; set; } = "";
    }
}
