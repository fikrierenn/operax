using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Operax.Web.Features.Auth;

// F0.2: Brute-force koruması — login uçunda IP başına 10 istek/dk (RL-1)
// Güvenlik (H-1): global FallbackPolicy authenticated istiyor → login sayfası açık olmalı
[AllowAnonymous]
[EnableRateLimiting("login")]
public class LoginModel(SignInManager<IdentityUser> signInManager, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "E-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçersiz e-posta adresi formatı.")]
        [Display(Name = "E-posta Adresi")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Beni Hatırla")]
        public bool RememberMe { get; set; }
    }

    // Giriş sayfasını açar; varsa returnUrl saklanır ve dış (external) oturum artığı temizlenir.
    public async Task OnGetAsync(string? returnUrl = null, CancellationToken ct = default)
    {
        ReturnUrl = returnUrl;
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    // E-posta/şifre doğrular; lockoutOnFailure ile brute-force kilidi aktif. Başarılıysa returnUrl'e döner.
    public async Task<IActionResult> OnPostAsync(string? returnUrl = null, CancellationToken ct = default)
    {
        returnUrl ??= Url.Content("~/");

        if (!ModelState.IsValid) return Page();

        var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        
        if (result.Succeeded)
        {
            logger.LogInformation("Kullanıcı başarıyla giriş yaptı.");
            return LocalRedirect(returnUrl);
        }
        
        ModelState.AddModelError(string.Empty, "E-posta adresi veya şifre hatalı.");
        return Page();
    }
}
