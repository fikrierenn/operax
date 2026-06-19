using Dapper;

namespace Operax.Web.Lib;

// ============================================================
// Audit Logging — AuditLog tablosuna yazma servisi
// ============================================================

/// <summary>Kullanıcı işlemlerini AuditLog tablosuna kaydeder.</summary>
public interface IAuditService
{
    Task LogAsync(string action, string entityType, Guid? entityId = null, string? details = null);
}

/// <summary>
/// Dapper tabanlı audit logging implementasyonu.
/// CompanyId, UserId, IP adresi otomatik alınır — sadece action + entity bilgisi verilir.
/// </summary>
public class AuditService(
    Db db,
    ICurrentCompany company,
    ICurrentUser user,
    IHttpContextAccessor http,
    ILogger<AuditService> logger) : IAuditService
{
    public async Task LogAsync(string action, string entityType, Guid? entityId = null, string? details = null)
    {
        try
        {
            using var conn = db.Open();
            await conn.ExecuteAsync(@"
                INSERT INTO AuditLog
                    (Id, CompanyId, UserId, UserName, Action, EntityType, EntityId, Details, IpAddress, CreatedAt)
                VALUES
                    (NEWID(), @CompanyId, @UserId, @UserName, @Action, @EntityType, @EntityId, @Details, @Ip, GETUTCDATE())",
                new
                {
                    CompanyId  = company.Id,
                    UserId     = user.Id == Guid.Empty ? (Guid?)null : user.Id,
                    UserName   = user.UserName,
                    Action     = action,
                    EntityType = entityType,
                    EntityId   = entityId,
                    Details    = details,
                    Ip         = http.HttpContext?.Connection.RemoteIpAddress?.ToString()
                });
        }
        catch (Exception ex)
        {
            // Audit log hatası asıl işlemi DURDURMAZ (izole — bilinçli). Ama sessiz yutulmaz: uyarı loglanır.
            logger.LogWarning(ex, "Audit log yazılamadı: {Action} {EntityType} {EntityId}", action, entityType, entityId);
        }
    }
}
