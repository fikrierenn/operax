using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.CreditCards;

/// <summary>
/// Kredi kartı listesi: limit, kalan limit, son ekstre.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public List<CardRowDto> Cards { get; set; } = [];
    public decimal TotalLimit { get; set; }
    public decimal TotalUsed  { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        var sql = @"
            SELECT
                c.Id, c.CardNoMasked, c.HolderName, c.BankName, c.CardType,
                c.CreditLimit, c.AvailableLimit, c.StatementDay, c.DueDay,
                c.InterestRate, c.Currency, c.ExpiresAt, c.IsActive,
                (SELECT TOP 1 PeriodEnd FROM CreditCardStatement WHERE CardId = c.Id ORDER BY PeriodEnd DESC) AS LastStatementDate,
                (SELECT TOP 1 ClosingBalance FROM CreditCardStatement WHERE CardId = c.Id ORDER BY PeriodEnd DESC) AS LastStatementBalance,
                (SELECT TOP 1 DueDate FROM CreditCardStatement WHERE CardId = c.Id AND IsClosed = 0 ORDER BY PeriodEnd DESC) AS NextStatementDue
            FROM CreditCard c
            WHERE c.CompanyId = @CompanyId AND c.IsDeleted = 0
            ORDER BY c.BankName, c.HolderName";

        Cards = (await conn.QueryAsync<CardRowDto>(sql, p)).ToList();
        TotalLimit = Cards.Sum(c => c.CreditLimit);
        TotalUsed  = TotalLimit - Cards.Sum(c => c.AvailableLimit);
    }

    public record CardRowDto(
        Guid     Id,
        string   CardNoMasked,
        string   HolderName,
        string   BankName,
        string   CardType,
        decimal  CreditLimit,
        decimal  AvailableLimit,
        int      StatementDay,
        int      DueDay,
        decimal? InterestRate,
        string   Currency,
        DateTime? ExpiresAt,
        bool     IsActive,
        DateTime? LastStatementDate,
        decimal? LastStatementBalance,
        DateTime? NextStatementDue);
}
