using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Admin.CompanyProfile;

/// <summary>
/// Şirket profili — aktif şirketin temel kimlik bilgileri (ünvan, VKN).
/// e-Belge ve yasal evrak başlıklarında kullanılır; yalnız Administrator düzenler.
/// </summary>
[Authorize(Roles = "Administrator")]
public class IndexModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<IndexModel> logger) : PageModel
{
    public ProfileDto Profile { get; set; } = new("", null);

    // Aktif şirketin ünvan + VKN bilgisini yükler
    public async Task OnGetAsync(CancellationToken ct = default)
    {
        using var conn = db.Open();
        Profile = await conn.QuerySingleAsync<ProfileDto>(new CommandDefinition(
            "SELECT Name, TaxNumber FROM Company WHERE Id = @Id",
            new { Id = company.Id }, cancellationToken: ct));
    }

    // Şirket ünvanı ve VKN'sini günceller (aktif şirket)
    public async Task<IActionResult> OnPostAsync(string name, string? taxNumber, CancellationToken ct = default)
    {
        // İş kuralı: ünvan zorunlu (sidebar ve evrak başlıkları buradan beslenir)
        if (string.IsNullOrWhiteSpace(name)) { TempData["Error"] = "Şirket ünvanı zorunludur."; return RedirectToPage(); }

        // İş kuralı: VKN girilmişse 10 (VKN) veya 11 (TCKN) haneli rakam olmalı
        var tax = string.IsNullOrWhiteSpace(taxNumber) ? null : taxNumber.Trim();
        if (tax is not null && (tax.Length is not (10 or 11) || !tax.All(char.IsDigit)))
        { TempData["Error"] = "VKN/TCKN 10 (VKN) veya 11 (TCKN) haneli rakam olmalıdır."; return RedirectToPage(); }

        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE Company SET Name = @Name, TaxNumber = @TaxNumber, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
             WHERE Id = @Id",
            new { Name = name.Trim(), TaxNumber = tax, Id = company.Id, UserId = user.Id }, cancellationToken: ct));

        logger.LogInformation("Şirket profili güncellendi: {Id}", company.Id);
        TempData["Success"] = "Şirket profili güncellendi.";
        return RedirectToPage();
    }

    public record ProfileDto(string Name, string? TaxNumber);
}
