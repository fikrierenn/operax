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

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;
        const string sql = @"
            SELECT Id, Name, NormalizedName, ConcurrencyStamp FROM AspNetRoles ORDER BY Name
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM AspNetRoles;";
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { Page = page, PageSize }, cancellationToken: ct));
        Roles = (await grid.ReadAsync<IdentityRole>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id, CancellationToken ct = default)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role == null) return NotFound();

        // Yerleşik roller silinemez
        if (role.Name == "Administrator")
        {
            ModelState.AddModelError("", "Administrator rolü silinemez.");
            await OnGetAsync(ct);
            return Page();
        }

        await roleManager.DeleteAsync(role);
        return RedirectToPage();
    }
}
