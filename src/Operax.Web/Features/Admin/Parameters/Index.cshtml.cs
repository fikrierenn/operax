using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Parameters;

[Authorize(Roles = "Administrator")]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ParameterDto> Parameters { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        Parameters = await conn.QueryAsync<ParameterDto>(@"
            SELECT Id, ModuleCode, Code, Value, Description 
            FROM Parameter 
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 
            ORDER BY ModuleCode, Code", 
            new { CompanyId = company.Id });
    }

    public record ParameterDto(Guid Id, string ModuleCode, string Code, string Value, string Description);
}
