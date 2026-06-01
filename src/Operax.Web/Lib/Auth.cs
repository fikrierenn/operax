using System.Security.Claims;
using Dapper;

namespace Operax.Web.Lib;

/// <summary>
/// Oturum açmış kullanıcıya ait anlık bilgileri sunar.
/// Cookie'deki claim'lerden okunur, DB sorgusu yapılmaz.
/// </summary>
public interface ICurrentUser
{
    Guid Id { get; }
    string UserName { get; }
    string[] Roles { get; }
    bool IsAuthenticated { get; }
}

/// <summary>
/// Aktif şirket bilgisini sunar.
/// Id → cookie'deki "company" claim'inden, Name → Company tablosundan çekilir.
/// </summary>
public interface ICurrentCompany
{
    Guid Id { get; }
    string Name { get; }
}

/// <summary>
/// Cookie claim'lerinden kullanıcı kimliğini ve rollerini okur.
/// </summary>
public class CurrentUser(IHttpContextAccessor ctx) : ICurrentUser
{
    // Kimlik doğrulama durumu kontrol edilir
    public bool IsAuthenticated => ctx.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    // NameIdentifier claim'i GUID'e çevrilir
    public Guid Id => IsAuthenticated && Guid.TryParse(ctx.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : Guid.Empty;

    // Görüntü adı için ClaimTypes.Name okunur (Identity tarafından UserName olarak set edilir)
    public string UserName => IsAuthenticated
        ? ctx.HttpContext!.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty
        : string.Empty;

    // Tüm rol claim'leri dizi olarak döndürülür
    public string[] Roles => IsAuthenticated
        ? ctx.HttpContext!.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
        : Array.Empty<string>();
}

/// <summary>
/// Aktif şirket Id'sini cookie'deki "company" claim'inden okur.
/// Şirket adını ise Company tablosundan tek seferlik (lazy) çeker.
/// </summary>
public class CurrentCompany(IHttpContextAccessor ctx, Db db) : ICurrentCompany
{
    // Şirket adı önbelleği — request başına bir kez DB'ye gidilir
    private string? _name;

    // "company" claim'i GUID'e çevrilir; yoksa Guid.Empty döner
    public Guid Id => Guid.TryParse(ctx.HttpContext?.User?.FindFirstValue("company"), out var id)
        ? id
        : Guid.Empty;

    /// <summary>
    /// Şirket adını Company tablosundan getirir.
    /// İlk erişimde DB sorgusu yapılır, sonraki erişimlerde önbellekten döner.
    /// </summary>
    public string Name
    {
        get
        {
            // Önbellekte varsa DB'ye gitme
            if (_name != null) return _name;
            // Şirket claim'i yoksa boş döndür
            if (Id == Guid.Empty) return _name = string.Empty;

            // Şirket adını tek sorguda çek
            using var conn = db.Open();
            _name = conn.QueryFirstOrDefault<string>(
                "SELECT Name FROM Company WHERE Id = @Id AND IsDeleted = 0",
                new { Id }) ?? string.Empty;
            return _name;
        }
    }
}

/// <summary>
/// ICurrentUser yetki kontrol extension'ları.
/// </summary>
public static class CurrentUserExtensions
{
    /// <summary>
    /// Kullanıcının belirtilen rollerden en az birine sahip olup olmadığını kontrol eder.
    /// </summary>
    public static bool HasRole(this ICurrentUser user, params string[] roles)
        => roles.Any(r => user.Roles.Contains(r, StringComparer.OrdinalIgnoreCase));
}
