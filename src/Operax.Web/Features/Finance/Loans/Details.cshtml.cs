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
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, ILogger<DetailsModel> logger) : PageModel
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

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Id == Guid.Empty) return RedirectToPage("Index");

        using var conn = db.Open();
        var p = new { CompanyId = company.Id, Id };

        Loan = await conn.QuerySingleOrDefaultAsync<LoanInfoDto>(new CommandDefinition(@"
            SELECT Id, LoanNo, BankName, CalcMethod, Status,
                   Principal, InterestRate, TermMonths, StartDate, EndDate,
                   MonthlyPayment, OutstandingBalance, GracePeriodMonths, BalloonAmount, AccountId
            FROM Loan
            WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0", p, cancellationToken: ct));

        if (Loan == null) return NotFound();

        Payments = (await conn.QueryAsync<PaymentDto>(new CommandDefinition(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: üst belge Loan aynı handler içinde daha önce
            -- WHERE Id = @Id AND CompanyId = @CompanyId ile yüklendi; bulunamazsa NotFound döndü.
            -- Bu sorgu yalnızca o doğrulanmış Loan.Id üzerinden taksit planını okuyduğundan
            -- başka firmanın kredi taksitine erişilemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            SELECT Id, InstallmentNo, DueDate, PrincipalAmount, InterestAmount,
                   TotalAmount, PaidAmount, PaidAt, IsPaid
            FROM LoanPayment
            WHERE LoanId = @Id
            ORDER BY InstallmentNo", p, cancellationToken: ct))).ToList();

        BankAccounts = (await conn.QueryAsync<DdlDto>(new CommandDefinition(@"
            SELECT Id, Code, Name FROM FinancialAccount
            WHERE CompanyId = @CompanyId AND AccountType = 'BANK' AND IsDeleted = 0
            ORDER BY Name", new { CompanyId = company.Id }, cancellationToken: ct))).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostPayAsync(Guid paymentId, Guid fromAccountId, CancellationToken ct)
    {
        // İş kuralı: Taksit ödeme — sp_PayLoanInstallment banka hesabından gider yazar,
        // anapara bakiyesini düşürür, tüm taksitler bitince krediyi CLOSED yapar.
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_PayLoanInstallment",
                new { PaymentId = paymentId, CompanyId = company.Id, PayDate = (DateTime?)null, FromAccountId = fromAccountId, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
            // Audit izi (A-4): kredi taksit ödemesi — security-principles zorunlu kıldığı kayıt
            await audit.LogAsync("LOAN_INSTALLMENT_PAID", "LoanPayment", paymentId, $"Loan: {Id}");
            TempData["Success"] = "Taksit ödendi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            // İş kuralı hatası — SP Türkçe mesaj döndü, kullanıcıya göster
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Kredi taksit ödeme hatası: {PaymentId}", paymentId);
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
