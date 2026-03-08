using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Dictionary;

[Authorize(Roles = "Admin")]
public class DetailsModel(Db db) : PageModel
{
    [BindProperty]
    public DictionaryTypeDto Type { get; set; } = new();
    public IEnumerable<DictionaryValueDto> Values { get; set; } = [];

    public async Task OnGetAsync(Guid id)
    {
        using var conn = db.Open();
        Type = await conn.QueryFirstOrDefaultAsync<DictionaryTypeDto>(
            "SELECT * FROM DictionaryType WHERE Id = @Id", new { Id = id }) ?? new();
        
        Values = await conn.QueryAsync<DictionaryValueDto>(
            "SELECT * FROM DictionaryValue WHERE DictionaryTypeId = @Id AND IsDeleted = 0 ORDER BY SortNo, NameTr",
            new { Id = id });
    }

    public async Task<IActionResult> OnPostAddValueAsync(Guid id, string code, string nameTr, string nameEn, int sortNo)
    {
        using var conn = db.Open();
        const string sql = @"
            INSERT INTO DictionaryValue (DictionaryTypeId, Code, NameTr, NameEn, SortNo)
            VALUES (@TypeId, @Code, @NameTr, @NameEn, @SortNo)";
        
        await conn.ExecuteAsync(sql, new { TypeId = id, Code = code, NameTr = nameTr, NameEn = nameEn, SortNo = sortNo });
        return RedirectToPage(new { id });
    }

    public record DictionaryTypeDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string NameTr { get; set; } = ""; public string NameEn { get; set; } = ""; }
    public record DictionaryValueDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string NameTr { get; set; } = ""; public string NameEn { get; set; } = ""; public int SortNo { get; set; } }
}
