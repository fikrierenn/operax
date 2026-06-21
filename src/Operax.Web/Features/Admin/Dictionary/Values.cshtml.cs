using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Dictionary;

[Authorize(Roles = "Administrator")]
public class ValuesModel(Db db, ICurrentCompany company) : PageModel
{
    public DictionaryTypeDto? Type { get; set; }
    public IEnumerable<DictionaryValueDto> Values { get; set; } = [];

    public async Task OnGetAsync(Guid typeId, CancellationToken ct = default)
    {
        using var conn = db.Open();

        // Get Type Info
        Type = await conn.QueryFirstOrDefaultAsync<DictionaryTypeDto>(new CommandDefinition(@"
            SELECT Id, Code, NameTr, NameEn
            FROM DictionaryType
            WHERE Id = @Id AND (CompanyId = @CompanyId OR IsSystem = 1)",
            new { Id = typeId, CompanyId = company.Id }, cancellationToken: ct));

        if (Type == null) return;

        // Get Values
        Values = await conn.QueryAsync<DictionaryValueDto>(new CommandDefinition(@"
            SELECT Id, Code, NameTr, NameEn, OrderNo, IsActive
            FROM DictionaryValue
            WHERE TypeId = @TypeId AND CompanyId = @CompanyId AND IsDeleted = 0
            ORDER BY OrderNo, NameTr",
            new { TypeId = typeId, CompanyId = company.Id }, cancellationToken: ct));
    }

    public record DictionaryTypeDto(Guid Id, string Code, string NameTr, string NameEn);
    public record DictionaryValueDto(Guid Id, string Code, string NameTr, string NameEn, int OrderNo, bool IsActive);
}
