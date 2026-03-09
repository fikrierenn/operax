using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Operax.Web.Features.Auth;

public class LogoutModel(SignInManager<IdentityUser> signInManager) : PageModel
{
    // GET: doğrudan çıkış yap ve Login'e yönlendir
    public async Task<IActionResult> OnGetAsync()
    {
        await signInManager.SignOutAsync();
        return RedirectToPage("/Auth/Login");
    }

    // POST (form submit): aynı işlem
    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();
        return RedirectToPage("/Auth/Login");
    }
}
