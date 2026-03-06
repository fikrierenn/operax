using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Admin.Users;

public class EditModel(UserManager<IdentityUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        Input = new InputModel
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            Email = user.Email ?? ""
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await userManager.FindByIdAsync(Input.Id);
        if (user == null) return NotFound();

        user.Email = Input.Email;
        user.UserName = Input.Email; // UserName ile Email'i senkron tutuyoruz
        
        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return Page();
        }

        if (!string.IsNullOrEmpty(Input.NewPassword))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var passResult = await userManager.ResetPasswordAsync(user, token, Input.NewPassword);
            if (!passResult.Succeeded)
            {
                foreach (var error in passResult.Errors) ModelState.AddModelError("", error.Description);
                return Page();
            }
        }

        return RedirectToPage("./Index");
    }

    public class InputModel
    {
        public string Id { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }
}
