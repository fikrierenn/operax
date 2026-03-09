using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Users;

[Authorize(Roles = "Administrator")]
public class IndexModel(Db db, UserManager<IdentityUser> userManager) : PageModel
{
    public IEnumerable<UserDto> Users { get; set; } = [];

    public async Task OnGetAsync()
    {
        // Kullanıcıları doğrudan AspNetUsers tablosundan Dapper ile çek (EF bağımlılığı yok)
        using var conn = db.Open();
        var users = await conn.QueryAsync<IdentityUser>(
            "SELECT TOP 50 Id, UserName, Email, EmailConfirmed, LockoutEnabled, AccessFailedCount FROM AspNetUsers ORDER BY UserName");

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
