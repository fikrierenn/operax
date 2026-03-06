using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.MasterData.Partners;

public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<PartnerDto> Partners { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        Partners = await conn.QueryAsync<PartnerDto>(@"
            SELECT Id, Code, Name, Type, TaxNumber, Email 
            FROM Partner 
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 
            ORDER BY Name", 
            new { CompanyId = company.Id });
    }

    public record PartnerDto(Guid Id, string Code, string Name, string Type, string TaxNumber, string Email);
}
