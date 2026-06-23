using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Settings;

[Authorize(Roles = Operax.Web.Lib.Roles.Administrator)]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
