using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.StatusTransitions;

[Authorize(Roles = "Administrator")]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<TransitionDto> Transitions { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        Transitions = await conn.QueryAsync<TransitionDto>(@"
            SELECT Id, DocumentType, FromStatusCode, ToStatusCode, ActionNameTr 
            FROM StatusTransition 
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 
            ORDER BY DocumentType, OrderNo", 
            new { CompanyId = company.Id });
    }

    public record TransitionDto(Guid Id, string DocumentType, string FromStatusCode, string ToStatusCode, string ActionNameTr);
}
