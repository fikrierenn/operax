using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Operax.Web.Features.Auth;

/// <summary>
/// Yetki reddi sayfası — kullanıcı oturum açmış ama ilgili modüle erişim rolü yoksa
/// (DB-driven RoleModuleAccess) cookie middleware buraya yönlendirir (AccessDeniedPath).
/// </summary>
[Authorize]
public class AccessDeniedModel : PageModel
{
    // İstenen yol (?returnUrl) bilgi amaçlı gösterilir
    public string? RequestedUrl { get; private set; }

    public void OnGet(string? returnUrl)
    {
        RequestedUrl = returnUrl;
    }
}
