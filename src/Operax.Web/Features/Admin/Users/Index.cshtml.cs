using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Users;

[Authorize(Roles = Operax.Web.Lib.Roles.Administrator)]
public class IndexModel(Db db, UserManager<IdentityUser> userManager) : PageModel
{
    public IEnumerable<UserDto> Users { get; set; } = [];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        // Kullanıcıları doğrudan AspNetUsers tablosundan Dapper ile çek (EF bağımlılığı yok)
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;
        const string sql = @"
            SELECT Id, UserName, Email, EmailConfirmed, LockoutEnabled, AccessFailedCount
            FROM AspNetUsers ORDER BY UserName
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM AspNetUsers;";
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { Page = page, PageSize }, cancellationToken: ct));
        var users = (await grid.ReadAsync<IdentityUser>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();

        var userDtos = new List<UserDto>();
        foreach (var user in users)
        {
            // Her kullanıcının rollerini Identity üzerinden al
            var roles = await userManager.GetRolesAsync(user);
            userDtos.Add(new UserDto(user.Id, user.UserName ?? "", user.Email ?? "", roles));
        }

        Users = userDtos;
    }

    public record UserDto(string Id, string UserName, string Email, IList<string> Roles);
}
