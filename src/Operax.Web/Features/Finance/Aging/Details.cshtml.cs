using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Aging;

/// <summary>
/// Cari bazlı yaşlandırma detayı — hangi ödeme planı hangi vade kovasında.
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid   PartnerId { get; set; }
    [BindProperty(SupportsGet = true)] public string Direction { get; set; } = "RECEIVABLE";

    public PartnerSummaryDto?   Partner  { get; set; }
    public List<PaymentLineDto> Lines    { get; set; } = [];
    public decimal              TotalOpen  { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (PartnerId == Guid.Empty) return RedirectToPage("Index");

        using var conn = db.Open();
        var p = new { CompanyId = company.Id, PartnerId, Direction };

        Partner = await conn.QueryFirstOrDefaultAsync<PartnerSummaryDto>(
            "SELECT Id, Code, Name, RiskCategory FROM Partner WHERE Id = @PartnerId AND CompanyId = @CompanyId",
            new { PartnerId, CompanyId = company.Id });

        if (Partner == null) return NotFound();

        Lines = (await conn.QueryAsync<PaymentLineDto>(@"
            SELECT
                pp.Id,
                pp.DueDate,
                pp.Amount,
                pp.PaidAmount,
                pp.Amount - pp.PaidAmount AS OpenAmount,
                pp.Status,
                pp.SourceDocType,
                pp.SourceDocNo,
                DATEDIFF(DAY, pp.DueDate, CAST(GETUTCDATE() AS DATE)) AS DaysOverdue
            FROM PaymentPlan pp
            WHERE pp.CompanyId    = @CompanyId
              AND pp.PartnerId    = @PartnerId
              AND pp.Direction    = @Direction
              AND pp.Status      <> 'PAID'
              AND pp.IsDeleted    = 0
            ORDER BY pp.DueDate ASC", p)).ToList();

        TotalOpen = Lines.Sum(l => l.OpenAmount);
        return Page();
    }

    public record PartnerSummaryDto(Guid Id, string Code, string Name, string? RiskCategory);

    public record PaymentLineDto(
        Guid    Id,
        DateTime DueDate,
        decimal  Amount,
        decimal  PaidAmount,
        decimal  OpenAmount,
        string   Status,
        string?  SourceDocType,
        string?  SourceDocNo,
        int      DaysOverdue);
}
