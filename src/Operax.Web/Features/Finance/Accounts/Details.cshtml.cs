using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Accounts;

/// <summary>
/// Hesap ekstresi (FinancialTransaction listesi + üst kart bakiye).
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }

    public AccountInfoDto?      Account      { get; set; }
    public List<TxRowDto>       Transactions { get; set; } = [];
    public decimal              Balance      { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (Id == Guid.Empty) return RedirectToPage("Index");

        using var conn = db.Open();
        var p = new { CompanyId = company.Id, Id };

        Account = await conn.QuerySingleOrDefaultAsync<AccountInfoDto>(@"
            SELECT a.Id, a.Code, a.Name, a.AccountType, a.Currency,
                   a.BankName, a.BranchName, a.IBAN, a.AccountNumber,
                   a.CreditLimit, a.InterestRate, a.OpeningBalance, a.Notes,
                   v.Balance, v.LastMovementDate
            FROM FinancialAccount a
            LEFT JOIN v_AccountBalance v ON v.AccountId = a.Id
            WHERE a.Id = @Id AND a.CompanyId = @CompanyId AND a.IsDeleted = 0", p);

        if (Account == null) return NotFound();
        Balance = Account.Balance;

        Transactions = (await conn.QueryAsync<TxRowDto>(@"
            SELECT TOP 100
                t.Id, t.TransactionDate, t.TransactionType,
                t.Amount, t.Currency, t.AmountTRY,
                t.Description, t.InstrumentType,
                t.SourceDocType, t.SourceDocNo,
                p.Name AS PartnerName
            FROM FinancialTransaction t
            LEFT JOIN Partner p ON p.Id = t.PartnerId
            WHERE t.AccountId = @Id AND t.IsDeleted = 0
            ORDER BY t.TransactionDate DESC, t.CreatedAt DESC", p)).ToList();

        return Page();
    }

    public record AccountInfoDto(
        Guid Id, string Code, string Name, string AccountType, string Currency,
        string? BankName, string? BranchName, string? IBAN, string? AccountNumber,
        decimal? CreditLimit, decimal? InterestRate, decimal OpeningBalance, string? Notes,
        decimal Balance, DateTime? LastMovementDate);

    public record TxRowDto(
        Guid Id, DateTime TransactionDate, string TransactionType,
        decimal Amount, string Currency, decimal AmountTRY,
        string? Description, string? InstrumentType,
        string? SourceDocType, string? SourceDocNo,
        string? PartnerName);
}
