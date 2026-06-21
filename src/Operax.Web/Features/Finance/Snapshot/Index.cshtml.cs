using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Snapshot;

/// <summary>
/// Mali Durum Kapama Tablosu — belirtilen tarihte tüm hesap, kredi,
/// çek/senet, alacak/borç tek tabloda. Kullanıcı "toplam kredi borcum ne?"
/// sorduğunda krediler yükümlülük altında görünür; net pozisyon altta.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    // ?asof=2026-05-28 query parametresi; boşsa bugün
    [BindProperty(SupportsGet = true)] public DateTime? AsOf { get; set; }

    public DateTime EffectiveDate => AsOf ?? DateTime.Today;

    public List<PositionRowDto> Assets       { get; set; } = [];
    public List<PositionRowDto> Liabilities  { get; set; } = [];
    public List<LoanSummaryDto> LoanSummary  { get; set; } = [];

    public decimal AssetTotal      => Assets.Sum(r => r.Amount);
    public decimal LiabilityTotal  => Liabilities.Sum(r => r.Amount);
    public decimal NetPosition     => AssetTotal - LiabilityTotal;

    // Gruplara göre alt toplamlar (UI başlıkları için)
    public Dictionary<string, decimal> AssetGroupTotals      => Assets.GroupBy(r => r.GroupName).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
    public Dictionary<string, decimal> LiabilityGroupTotals  => Liabilities.GroupBy(r => r.GroupName).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

    public async Task OnGetAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id, AsOfDate = EffectiveDate.Date };

        // İş kuralı: Mali pozisyon tek TVF çağrısında, alttaki gruplama PageModel'de
        var rows = await conn.QueryAsync<PositionRowDto>(new CommandDefinition(@"
            SELECT Category, GroupName, ItemCode, ItemName, Currency, Amount, SortOrder
            FROM dbo.tvf_FinancialPosition(@CompanyId, @AsOfDate)
            ORDER BY SortOrder, GroupName, ItemCode", p, cancellationToken: ct));

        var list = rows.ToList();
        Assets      = list.Where(r => r.Category == "ASSET"    ).ToList();
        Liabilities = list.Where(r => r.Category == "LIABILITY").ToList();

        // Kredi detayı — kullanıcı "kapama tablosunda kredi borcum ne?" sorduğunda
        // tıkladığında her kredinin: ödenen anapara, kalan, geciken, sonraki ödeme
        LoanSummary = (await conn.QueryAsync<LoanSummaryDto>(new CommandDefinition(@"
            SELECT LoanId, LoanNo, BankName, CalcMethod, Status,
                   Principal, InterestRate, StartDate, EndDate, TermMonths,
                   DueInstallmentCount, PaidInstallmentCount, OverdueInstallmentCount,
                   TotalInstallmentCount,
                   PaidPrincipal, PaidInterest,
                   OutstandingPrincipal, RemainingInterest, TotalRemainingDebt,
                   OverdueAmount, NextDueDate, NextDueAmount
            FROM dbo.tvf_LoanSummaryAsOf(@CompanyId, @AsOfDate)
            ORDER BY CASE WHEN Status = 'ACTIVE' THEN 0 ELSE 1 END, BankName, LoanNo", p, cancellationToken: ct))).ToList();

        logger.LogInformation(
            "Mali durum: tarih={Date}, varlık={Assets}, yükümlülük={Liabilities}, kredi={Loans}",
            EffectiveDate.ToString("yyyy-MM-dd"), AssetTotal, LiabilityTotal,
            LoanSummary.Where(l => l.Status == "ACTIVE").Sum(l => l.TotalRemainingDebt));
    }

    // ─── DTO'lar ────────────────────────────────────────────────
    public record PositionRowDto(
        string  Category,
        string  GroupName,
        string  ItemCode,
        string  ItemName,
        string  Currency,
        decimal Amount,
        int     SortOrder);

    public record LoanSummaryDto(
        Guid     LoanId,
        string   LoanNo,
        string   BankName,
        string   CalcMethod,
        string   Status,
        decimal  Principal,
        decimal  InterestRate,
        DateTime StartDate,
        DateTime EndDate,
        int      TermMonths,
        int      DueInstallmentCount,
        int      PaidInstallmentCount,
        int      OverdueInstallmentCount,
        int      TotalInstallmentCount,
        decimal  PaidPrincipal,
        decimal  PaidInterest,
        decimal  OutstandingPrincipal,
        decimal  RemainingInterest,
        decimal  TotalRemainingDebt,
        decimal  OverdueAmount,
        DateTime? NextDueDate,
        decimal? NextDueAmount);
}
