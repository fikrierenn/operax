using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Budget;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<BudgetListDto> Budgets    { get; set; } = [];
    public IEnumerable<CashFlowDto>  CashFlow30  { get; set; } = [];
    public decimal NetCashFlow30                  { get; set; }

    public async Task OnGetAsync()
    {
        // Butce listesini ve 30 gunluk nakit akisi ozetini yukler
        using var conn = db.Open();

        Budgets = await conn.QueryAsync<BudgetListDto>(@"
            SELECT b.Id, b.Year, b.Code, b.Name, b.Type, b.Status,
                   (SELECT ISNULL(SUM(bl.AmountPlanned), 0) FROM BudgetLine bl WHERE bl.BudgetId = b.Id AND bl.Direction = 'OUT') AS TotalPlannedOut,
                   (SELECT ISNULL(SUM(bl.AmountActual), 0) FROM BudgetLine bl WHERE bl.BudgetId = b.Id AND bl.Direction = 'OUT') AS TotalActualOut
            FROM Budget b
            WHERE b.CompanyId = @CompanyId
            ORDER BY b.Year DESC, b.Code",
            new { CompanyId = company.Id });

        // 30 gunluk nakit akisi takvimi (gun bazli ozet)
        CashFlow30 = await conn.QueryAsync<CashFlowDto>(@"
            SELECT
                bl.CashDate,
                SUM(CASE WHEN bl.Direction = 'IN'  THEN bl.AmountPlanned ELSE 0 END) AS CashIn,
                SUM(CASE WHEN bl.Direction = 'OUT' THEN bl.AmountPlanned ELSE 0 END) AS CashOut
            FROM BudgetLine bl
            JOIN Budget b ON b.Id = bl.BudgetId
            WHERE b.CompanyId = @CompanyId
              AND bl.CashDate BETWEEN CAST(GETUTCDATE() AS DATE) AND DATEADD(DAY, 30, CAST(GETUTCDATE() AS DATE))
            GROUP BY bl.CashDate
            ORDER BY bl.CashDate",
            new { CompanyId = company.Id });

        NetCashFlow30 = CashFlow30.Sum(x => x.CashIn - x.CashOut);
    }

    public record BudgetListDto
    {
        public Guid    Id             { get; set; }
        public int     Year           { get; set; }
        public string  Code           { get; set; } = "";
        public string  Name           { get; set; } = "";
        public string  Type           { get; set; } = "";
        public string  Status         { get; set; } = "";
        public decimal TotalPlannedOut { get; set; }
        public decimal TotalActualOut  { get; set; }
        public decimal Variance        => TotalPlannedOut - TotalActualOut;
    }

    public record CashFlowDto
    {
        public DateTime CashDate { get; set; }
        public decimal  CashIn   { get; set; }
        public decimal  CashOut  { get; set; }
        public decimal  Net      => CashIn - CashOut;
    }
}
