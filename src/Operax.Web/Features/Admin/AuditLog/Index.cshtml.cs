using Dapper;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Admin.AuditLog;

/// <summary>
/// Sistem denetim kaydı listesi.
/// Son 200 işlemi tarih sırasına göre gösterir.
/// Modül, kullanıcı ve aksiyon tipine göre filtreleme destekler.
/// </summary>
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<AuditLogDto> Entries { get; set; } = [];

    // Filtre parametreleri
    public string? FilterUser { get; set; }
    public string? FilterAction { get; set; }
    public string? FilterEntity { get; set; }

    public async Task OnGetAsync(string? user = null, string? action = null, string? entity = null)
    {
        // Filtre değerleri saklanır (form'da tekrar göstermek için)
        FilterUser = user;
        FilterAction = action;
        FilterEntity = entity;

        using var conn = db.Open();

        // Son 200 denetim kaydı getirilir; şirket ve filtre koşulları uygulanır
        Entries = await conn.QueryAsync<AuditLogDto>(@"
            SELECT TOP 200
                Id, CompanyId, UserId, UserName,
                Action, EntityType, EntityId,
                Details, IpAddress, CreatedAt
            FROM AuditLog
            WHERE CompanyId = @CompanyId
              AND (@User   IS NULL OR UserName   LIKE '%' + @User   + '%')
              AND (@Action IS NULL OR Action      LIKE '%' + @Action + '%')
              AND (@Entity IS NULL OR EntityType  LIKE '%' + @Entity + '%')
            ORDER BY CreatedAt DESC",
            new
            {
                CompanyId = company.Id,
                User = user,
                Action = action,
                Entity = entity
            });
    }

    /// <summary>Denetim kaydı satırı veri aktarım nesnesi.</summary>
    public record AuditLogDto(
        Guid Id,
        Guid CompanyId,
        Guid? UserId,
        string UserName,
        string Action,
        string EntityType,
        Guid? EntityId,
        string? Details,
        string? IpAddress,
        DateTime CreatedAt);
}
