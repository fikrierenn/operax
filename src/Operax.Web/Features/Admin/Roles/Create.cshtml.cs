using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Operax.Web.Features.Admin.Roles;

[Authorize(Roles = "Administrator")]
public class CreateModel(RoleManager<IdentityRole> roleManager) : PageModel
{
    [BindProperty]
    public string RoleName { get; set; } = "";

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(RoleName))
        {
            ModelState.AddModelError("", "Rol adı boş olamaz.");
            return Page();
        }

        var result = await roleManager.CreateAsync(new IdentityRole(RoleName.Trim()));
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError("", e.Description);
            return Page();
        }

        return RedirectToPage("./Index");
    }
}
