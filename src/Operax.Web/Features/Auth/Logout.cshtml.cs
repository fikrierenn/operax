using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Operax.Web.Features.Auth;

// Güvenlik (H-1): süresi dolmuş oturum da çıkış yapabilmeli → global fallback'ten muaf
[AllowAnonymous]
public class LogoutModel(SignInManager<IdentityUser> signInManager) : PageModel
{
    // GET: doğrudan çıkış yap ve Login'e yönlendir
    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        await signInManager.SignOutAsync();
        return RedirectToPage("/Auth/Login");
    }

    // POST (form submit): aynı işlem
    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        await signInManager.SignOutAsync();
        return RedirectToPage("/Auth/Login");
    }
}
