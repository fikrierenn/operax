using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Lib.Authz;

/// <summary>
/// Belirli bir modül (üst Feature klasörü) için erişim gereksinimi.
/// Allow/deny kararı ModuleAccessHandler tarafından RoleModuleAccess tablosundan okunur.
/// </summary>
public sealed class ModuleAccessRequirement(string moduleKey) : IAuthorizationRequirement
{
    public string ModuleKey { get; } = moduleKey;
}
