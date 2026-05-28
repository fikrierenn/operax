using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Admin.Users;

[Authorize(Roles = "Administrator")]
public class CreateModel(
    UserManager<IdentityUser> userManager,
    ICurrentCompany company) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
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

        return RedirectToPage("./Index");
    }

    public class InputModel
    {
        public string Email    { get; set; } = "";
        public string Password { get; set; } = "";
        /// <summary>"Administrator" veya "" (rol yok = normal kullanıcı)</summary>
        public string Role     { get; set; } = "";
    }
}
