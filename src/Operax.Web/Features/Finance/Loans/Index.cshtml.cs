using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Loans;

/// <summary>
/// Kredi listesi: bekleyen taksit sayısı, kalan anapara, sıradaki vade.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "all";

    public List<LoanRowDto> Loans         { get; set; } = [];
    public LoanCountsDto    Counts        { get; set; } = new(0, 0, 0);
    public decimal          TotalOutstanding { get; set; }
    public decimal          NextMonthDue { get; set; }

    // Kredi listesini ve özet sayaçlarını yükler
    public async Task OnGetAsync()
    {
        try
        {
            using var conn = db.Open();
            var p = new { CompanyId = company.Id };

            Counts = await conn.QuerySingleAsync<LoanCountsDto>(@"
                SELECT
                    COUNT(*) AS Total,
                    SUM(CASE WHEN Status = 'ACTIVE' THEN 1 ELSE 0 END) AS Active,
                    SUM(CASE WHEN Status = 'CLOSED' THEN 1 ELSE 0 END) AS Closed
                FROM Loan
                WHERE CompanyId = @CompanyId AND IsDeleted = 0", p);

            TotalOutstanding = await conn.ExecuteScalarAsync<decimal>(@"
                SELECT ISNULL(SUM(OutstandingBalance), 0)
                FROM Loan WHERE CompanyId = @CompanyId AND Status = 'ACTIVE' AND IsDeleted = 0", p);

            NextMonthDue = await conn.ExecuteScalarAsync<decimal>(@"
                SELECT ISNULL(SUM(lp.TotalAmount), 0)
                FROM LoanPayment lp
                JOIN Loan l ON l.Id = lp.LoanId
                WHERE l.CompanyId = @CompanyId
                  AND lp.IsPaid = 0
                  AND lp.DueDate BETWEEN GETUTCDATE() AND DATEADD(DAY, 30, GETUTCDATE())", p);

            var sql = @"
                SELECT
                    l.Id, l.LoanNo, l.BankName, l.Principal, l.InterestRate,
                    l.TermMonths, l.StartDate, l.EndDate, l.MonthlyPayment,
                    l.OutstandingBalance, l.Currency, l.Status,
                    (SELECT COUNT(*) FROM LoanPayment WHERE LoanId = l.Id AND IsPaid = 0)        AS RemainingInstallments,
                    (SELECT MIN(DueDate) FROM LoanPayment WHERE LoanId = l.Id AND IsPaid = 0)    AS NextDueDate,
                    (SELECT TOP 1 TotalAmount FROM LoanPayment WHERE LoanId = l.Id AND IsPaid = 0 ORDER BY DueDate) AS NextDueAmount
                FROM Loan l
                WHERE l.CompanyId = @CompanyId AND l.IsDeleted = 0";

            var parms = new DynamicParameters();
            parms.Add("CompanyId", company.Id);

            if (Status != "all")
            {
                sql += " AND l.Status = @Status";
                parms.Add("Status", Status);
            }
            sql += " ORDER BY l.Status, l.EndDate";

            Loans = (await conn.QueryAsync<LoanRowDto>(sql, parms)).ToList();
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Kredi listesi veri yükleme hatası");
            TempData["Error"] = "Veriler yüklenirken bir hata oluştu.";
        }
    }

    public record LoanRowDto(
        Guid     Id,
        string   LoanNo,
        string   BankName,
        decimal  Principal,
        decimal  InterestRate,
        int      TermMonths,
        DateTime StartDate,
        DateTime EndDate,
        decimal  MonthlyPayment,
        decimal  OutstandingBalance,
        string   Currency,
        string   Status,
        int      RemainingInstallments,
        DateTime? NextDueDate,
        decimal? NextDueAmount);

    public record LoanCountsDto(int Total, int Active, int Closed);
}
