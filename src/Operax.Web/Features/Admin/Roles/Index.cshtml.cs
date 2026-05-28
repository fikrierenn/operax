using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Admin.Roles;

[Authorize(Roles = "Administrator")]
public class IndexModel(Db db, RoleManager<IdentityRole> roleManager) : PageModel
{
    public IEnumerable<IdentityRole> Roles { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        Roles = await conn.QueryAsync<IdentityRole>(
            "SELECT Id, Name, NormalizedName, ConcurrencyStamp FROM AspNetRoles ORDER BY Name");
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role == null) return NotFound();

        // Yerleşik roller silinemez
        if (role.Name == "Administrator")
        {
            ModelState.AddModelError("", "Administrator rolü silinemez.");
            await OnGetAsync();
            return Page();
        }

        await roleManager.DeleteAsync(role);
        return RedirectToPage();
    }
}
