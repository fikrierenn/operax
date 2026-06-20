using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Operax.Web.Features;

/// <summary>
/// Üretim hata sayfası (audit H-3) — UseExceptionHandler("/Error") buraya yönlenir.
/// Global fallback policy'den muaf: hata anında auth context olmayabilir.
/// </summary>
[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    // İstek izleme kimliği — kullanıcı destek talebinde bu referansı paylaşır
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}
