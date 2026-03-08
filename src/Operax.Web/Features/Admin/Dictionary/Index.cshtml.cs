using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Dictionary;

[Authorize(Roles = "Admin")]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<DictionaryTypeDto> Types { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        
        const string sql = @"
            SELECT Id, Code, NameTr, NameEn 
            FROM DictionaryType 
            WHERE (CompanyId = @CompanyId OR IsSystem = 1) 
              AND IsDeleted = 0 
            ORDER BY NameTr";

        Types = await conn.QueryAsync<DictionaryTypeDto>(sql, new { CompanyId = company.Id });
    }

    public record DictionaryTypeDto(Guid Id, string Code, string NameTr, string NameEn);
}
