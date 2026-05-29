using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Accounts;

/// <summary>
/// Finansal hesap listesi (kasa, banka, kredi kartı, kredi).
/// Bakiye tvf_AccountBalance(@CompanyId) üzerinden okunur — FinancialTransaction'lardan canlı hesaplanır.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    // Sekme filtresi: tüm tipler ya da belirli bir AccountType
    [BindProperty(SupportsGet = true)] public string Type { get; set; } = "all";

    public List<AccountRowDto> Accounts { get; set; } = [];
    public CountsDto           Counts   { get; set; } = new(0, 0, 0, 0, 0);
    public decimal             TotalBalance { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        // Tip bazlı sayım rozetleri için ayrı sorgu
        Counts = await conn.QuerySingleAsync<CountsDto>(@"
            SELECT
                COUNT(*) AS Total,
                SUM(CASE WHEN AccountType = 'CASH'        THEN 1 ELSE 0 END) AS Cash,
                SUM(CASE WHEN AccountType = 'BANK'        THEN 1 ELSE 0 END) AS Bank,
                SUM(CASE WHEN AccountType = 'CREDIT_CARD' THEN 1 ELSE 0 END) AS Card,
                SUM(CASE WHEN AccountType = 'LOAN'        THEN 1 ELSE 0 END) AS Loan
            FROM FinancialAccount
            WHERE CompanyId = @CompanyId AND IsDeleted = 0", p);

        // Hesap listesi + bakiye
        var sql = @"
            SELECT
                v.AccountId, v.Code, v.Name, v.AccountType, v.Currency,
                v.Balance, v.LastMovementDate, v.TransactionCount,
                a.BankName, a.IBAN, a.CreditLimit, a.InterestRate
            FROM dbo.tvf_AccountBalance(@CompanyId) v
            JOIN FinancialAccount a ON a.Id = v.AccountId
            WHERE 1 = 1";

        var parms = new DynamicParameters();
        parms.Add("CompanyId", company.Id);

        if (Type != "all")
        {
            sql += " AND v.AccountType = @Type";
            parms.Add("Type", Type);
        }
        sql += " ORDER BY v.AccountType, v.Name";

        Accounts = (await conn.QueryAsync<AccountRowDto>(sql, parms)).ToList();
        TotalBalance = Accounts.Sum(a => a.Balance);
    }

    public record AccountRowDto(
        Guid     AccountId,
        string   Code,
        string   Name,
        string   AccountType,
        string   Currency,
        decimal  Balance,
        DateTime? LastMovementDate,
        int      TransactionCount,
        string?  BankName,
        string?  IBAN,
        decimal? CreditLimit,
        decimal? InterestRate);

    public record CountsDto(int Total, int Cash, int Bank, int Card, int Loan);
}
