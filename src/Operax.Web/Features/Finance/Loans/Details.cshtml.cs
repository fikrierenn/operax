using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Loans;

/// <summary>
/// Kredi detayı — başlık bilgileri + taksit takvimi + taksit ödeme.
/// Ödeme sp_PayLoanInstallment ile banka hesabından gider hareketi yazar.
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }

    public LoanInfoDto?        Loan         { get; set; }
    public List<PaymentDto>    Payments     { get; set; } = [];
    public List<DdlDto>        BankAccounts { get; set; } = [];

    public decimal PaidPrincipal     => Payments.Where(p => p.IsPaid).Sum(p => p.PrincipalAmount);
    public decimal PaidInterest      => Payments.Where(p => p.IsPaid).Sum(p => p.InterestAmount);
    public decimal RemainingTotal    => Payments.Where(p => !p.IsPaid).Sum(p => p.TotalAmount);
    public int     PaidCount         => Payments.Count(p => p.IsPaid);
    public int     OverdueCount      => Payments.Count(p => !p.IsPaid && p.DueDate < DateTime.Today);

    public async Task<IActionResult> OnGetAsync()
    {
        if (Id == Guid.Empty) return RedirectToPage("Index");

        using var conn = db.Open();
        var p = new { CompanyId = company.Id, Id };

        Loan = await conn.QuerySingleOrDefaultAsync<LoanInfoDto>(@"
            SELECT Id, LoanNo, BankName, CalcMethod, Status,
                   Principal, InterestRate, TermMonths, StartDate, EndDate,
                   MonthlyPayment, OutstandingBalance, GracePeriodMonths, BalloonAmount, AccountId
            FROM Loan
            WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0", p);

        if (Loan == null) return NotFound();

        Payments = (await conn.QueryAsync<PaymentDto>(@"
            SELECT Id, InstallmentNo, DueDate, PrincipalAmount, InterestAmount,
                   TotalAmount, PaidAmount, PaidAt, IsPaid
            FROM LoanPayment
            WHERE LoanId = @Id
            ORDER BY InstallmentNo", p)).ToList();

        BankAccounts = (await conn.QueryAsync<DdlDto>(@"
            SELECT Id, Code, Name FROM FinancialAccount
            WHERE CompanyId = @CompanyId AND AccountType = 'BANK' AND IsDeleted = 0
            ORDER BY Name", new { CompanyId = company.Id })).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostPayAsync(Guid paymentId, Guid fromAccountId)
    {
        // İş kuralı: Taksit ödeme — sp_PayLoanInstallment banka hesabından gider yazar,
        // anapara bakiyesini düşürür, tüm taksitler bitince krediyi CLOSED yapar.
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_PayLoanInstallment",
                new { PaymentId = paymentId, PayDate = (DateTime?)null, FromAccountId = fromAccountId, UserId = user.Id },
                commandType: CommandType.StoredProcedure);
            TempData["Success"] = "Taksit ödendi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sex) when (sex.Number is >= 50000 and < 60000)
        {
            // İş kuralı hatası — SP Türkçe mesaj döndü, kullanıcıya göster
            TempData["Error"] = sex.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sex)
        {
            logger.LogError(sex, "Kredi taksit ödeme hatası: {PaymentId}", paymentId);
            TempData["Error"] = "Taksit ödenirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id = Id });
    }

    public record LoanInfoDto(
        Guid Id, string LoanNo, string BankName, string CalcMethod, string Status,
        decimal Principal, decimal InterestRate, int TermMonths, DateTime StartDate, DateTime EndDate,
        decimal MonthlyPayment, decimal OutstandingBalance, int GracePeriodMonths, decimal? BalloonAmount,
        Guid? AccountId);

    public record PaymentDto(
        Guid Id, int InstallmentNo, DateTime DueDate, decimal PrincipalAmount,
        decimal InterestAmount, decimal TotalAmount, decimal PaidAmount, DateTime? PaidAt, bool IsPaid)
    {
        public bool IsOverdue => !IsPaid && DueDate < DateTime.Today;
    }
}
