using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.CreditCards;

/// <summary>
/// Kredi kartı detayı — kart bilgileri + ekstre listesi + son slip işlemleri.
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }

    public CardInfoDto?         Card         { get; set; }
    public List<StatementDto>   Statements   { get; set; } = [];
    public List<TxnDto>         Transactions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (Id == Guid.Empty) return RedirectToPage("Index");

        using var conn = db.Open();
        var p = new { CompanyId = company.Id, Id };

        Card = await conn.QuerySingleOrDefaultAsync<CardInfoDto>(@"
            SELECT cc.Id, cc.CardNoMasked, cc.HolderName, cc.BankName, cc.CardType,
                   cc.CreditLimit, cc.AvailableLimit, cc.StatementDay, cc.DueDay,
                   cc.InterestRate, cc.Currency, cc.ExpiresAt,
                   ba.Name AS LinkedBankAccountName
            FROM CreditCard cc
            LEFT JOIN FinancialAccount ba ON ba.Id = cc.LinkedBankAccountId
            WHERE cc.Id = @Id AND cc.CompanyId = @CompanyId AND cc.IsDeleted = 0", p);

        if (Card == null) return NotFound();

        Statements = (await conn.QueryAsync<StatementDto>(@"
            SELECT TOP 12 Id, PeriodStart, PeriodEnd, StatementDate, DueDate,
                   ClosingBalance, MinPayment, PaidAmount, IsClosed
            FROM CreditCardStatement
            WHERE CardId = @Id
            ORDER BY PeriodEnd DESC", new { Id })).ToList();

        Transactions = (await conn.QueryAsync<TxnDto>(@"
            SELECT TOP 30 Id, TransactionDate, MerchantName, Category,
                   Amount, Currency, InstallmentCount, InstallmentNo
            FROM CreditCardTransaction
            WHERE CardId = @Id
            ORDER BY TransactionDate DESC", new { Id })).ToList();

        logger.LogInformation("Kredi kartı detayı görüntülendi: {CardId}", Id);
        return Page();
    }

    public record CardInfoDto(
        Guid Id, string CardNoMasked, string HolderName, string BankName, string CardType,
        decimal CreditLimit, decimal AvailableLimit, int StatementDay, int DueDay,
        decimal? InterestRate, string Currency, DateTime? ExpiresAt,
        string? LinkedBankAccountName)
    {
        public decimal UsedLimit => CreditLimit - AvailableLimit;
        public decimal UsagePercent => CreditLimit > 0 ? System.Math.Round(UsedLimit / CreditLimit * 100, 1) : 0;
    }

    public record StatementDto(
        Guid Id, DateTime PeriodStart, DateTime PeriodEnd, DateTime StatementDate, DateTime DueDate,
        decimal ClosingBalance, decimal MinPayment, decimal PaidAmount, bool IsClosed);

    public record TxnDto(
        Guid Id, DateTime TransactionDate, string MerchantName, string? Category,
        decimal Amount, string Currency, int InstallmentCount, int InstallmentNo);
}
