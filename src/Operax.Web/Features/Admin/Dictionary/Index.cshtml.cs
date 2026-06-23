using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Dictionary;

[Authorize(Roles = Operax.Web.Lib.Roles.Administrator)]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<DictionaryTypeDto> Types { get; set; } = [];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        const string sql = @"
            SELECT Id, Code, NameTr, NameEn
            FROM DictionaryType
            WHERE (CompanyId = @CompanyId OR IsSystem = 1) AND IsDeleted = 0
            ORDER BY NameTr
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM DictionaryType
            WHERE (CompanyId = @CompanyId OR IsSystem = 1) AND IsDeleted = 0;";

        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
        Types = (await grid.ReadAsync<DictionaryTypeDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
    }

    public record DictionaryTypeDto(Guid Id, string Code, string NameTr, string NameEn);
}
