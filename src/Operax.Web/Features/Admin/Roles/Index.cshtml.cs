using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Admin.Roles;

public class IndexModel(Db db) : PageModel
{
    public IEnumerable<IdentityRole> Roles { get; set; } = [];

    public async Task OnGetAsync()
    {
        // Tüm rolleri doğrudan AspNetRoles tablosundan Dapper ile çek
        using var conn = db.Open();
        Roles = await conn.QueryAsync<IdentityRole>(
            "SELECT Id, Name, NormalizedName, ConcurrencyStamp FROM AspNetRoles ORDER BY Name");
    }
}
