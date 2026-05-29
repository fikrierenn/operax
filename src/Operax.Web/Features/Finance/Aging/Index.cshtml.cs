using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Aging;

/// <summary>
/// Yaşlandırma raporu — tvf_PaymentPlanAging(@CompanyId) iTVF'inden.
/// 0-30 / 31-60 / 61-90 / 90+ kovaları, alacak/borç ayrı sekmeli.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Direction { get; set; } = "RECEIVABLE";

    public List<AgingRowDto> Rows   { get; set; } = [];
    public AgingTotalsDto    Totals { get; set; } = new(0, 0, 0, 0, 0, 0, 0);

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id, Direction };

        Rows = (await conn.QueryAsync<AgingRowDto>(@"
            SELECT
                PartnerId, PartnerName,
                NotDue, Days1_30, Days31_60, Days61_90, Over90, TotalOpen,
                OpenOrderAmount
            FROM dbo.tvf_PaymentPlanAging(@CompanyId)
            WHERE Direction = @Direction
            ORDER BY TotalOpen DESC", p)).ToList();

        Totals = new AgingTotalsDto(
            Rows.Sum(r => r.NotDue),
            Rows.Sum(r => r.Days1_30),
            Rows.Sum(r => r.Days31_60),
            Rows.Sum(r => r.Days61_90),
            Rows.Sum(r => r.Over90),
            Rows.Sum(r => r.TotalOpen),
            Rows.Sum(r => r.OpenOrderAmount));
    }

    public record AgingRowDto(
        Guid    PartnerId,
        string  PartnerName,
        decimal NotDue,
        decimal Days1_30,
        decimal Days31_60,
        decimal Days61_90,
        decimal Over90,
        decimal TotalOpen,
        decimal OpenOrderAmount);

    public record AgingTotalsDto(
        decimal NotDue, decimal Days1_30, decimal Days31_60,
        decimal Days61_90, decimal Over90, decimal Total,
        decimal OpenOrder);
}
