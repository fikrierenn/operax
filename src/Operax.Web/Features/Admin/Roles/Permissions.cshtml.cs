using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Operax.Web.Lib;
using Operax.Web.Lib.Authz;

namespace Operax.Web.Features.Admin.Roles;

// Rol başına modül erişim (RoleModuleAccess) yönetim ekranı — sadece Administrator.
[Authorize(Roles = Operax.Web.Lib.Roles.Administrator)]
public class PermissionsModel(Db db, RoleManager<IdentityRole> roleManager, IMemoryCache cache) : PageModel
{
    public string RoleName { get; private set; } = "";

    // Her modül için mevcut erişim seviyesi (0=Yok, 1=Görüntüle, 2=Düzenle)
    public Dictionary<string, int> Current { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public string[] ModuleKeys => Lib.Authz.ModuleKeys.All;

    [BindProperty] public string RoleId { get; set; } = "";
    [BindProperty] public Dictionary<string, int> Access { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id, CancellationToken ct = default)
    {
        // Guard: rol yoksa liste sayfasına dön
        var role = await roleManager.FindByIdAsync(id);
        if (role == null) return NotFound();

        RoleId = role.Id;
        RoleName = role.Name ?? "";

        using var conn = db.Open();
        var rows = await conn.QueryAsync<AccessRow>(new CommandDefinition(
            "SELECT ModuleKey, AccessLevel FROM RoleModuleAccess WHERE RoleId = @RoleId",
            new { RoleId = role.Id }, cancellationToken: ct));
        foreach (var row in rows)
            Current[row.ModuleKey] = row.AccessLevel;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        // Guard: rol yoksa işlem yapılmaz
        var role = await roleManager.FindByIdAsync(RoleId);
        if (role == null) return NotFound();

        using var conn = db.Open();
        // İş kuralı: her modül için seçilen seviyeye göre satırı ekle/güncelle/sil
        foreach (var key in Lib.Authz.ModuleKeys.All)
        {
            var level = Access.TryGetValue(key, out var v) ? v : 0;
            if (level <= 0)
                await conn.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM RoleModuleAccess WHERE RoleId = @RoleId AND ModuleKey = @Key",
                    new { RoleId = role.Id, Key = key }, cancellationToken: ct));
            else
                await conn.ExecuteAsync(new CommandDefinition(@"
                    IF EXISTS (SELECT 1 FROM RoleModuleAccess WHERE RoleId = @RoleId AND ModuleKey = @Key)
                        UPDATE RoleModuleAccess SET AccessLevel = @Level WHERE RoleId = @RoleId AND ModuleKey = @Key
                    ELSE
                        INSERT INTO RoleModuleAccess (Id, RoleId, ModuleKey, AccessLevel)
                        VALUES (NEWID(), @RoleId, @Key, @Level)",
                    new { RoleId = role.Id, Key = key, Level = (byte)Math.Min(level, 2) }, cancellationToken: ct));
        }

        // Cache invalidate: rolün yetki haritası yeniden okunsun
        cache.Remove(ModuleAccessHandler.CacheKeyPrefix + role.Name);

        TempData["Success"] = $"{role.Name} rolünün modül izinleri güncellendi.";
        return RedirectToPage(new { id = RoleId });
    }

    private sealed record AccessRow(string ModuleKey, byte AccessLevel);
}
