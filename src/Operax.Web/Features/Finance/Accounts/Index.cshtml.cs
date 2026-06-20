using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Accounts;

/// <summary>
/// Finansal hesap listesi (kasa, banka, kredi kartı, kredi).
/// Bakiye tvf_AccountBalance(@CompanyId) üzerinden okunur — FinancialTransaction'lardan canlı hesaplanır.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    // Sekme filtresi: tüm tipler ya da belirli bir AccountType
    [BindProperty(SupportsGet = true)] public string Type { get; set; } = "all";

    public List<AccountRowDto> Accounts { get; set; } = [];
    public CountsDto           Counts   { get; set; } = new(0, 0, 0, 0, 0);
    public decimal             TotalBalance { get; set; }

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);
    private record AggRow(int Cnt, decimal TotalBalance);

    // Finansal hesap listesini ve tip bazlı sayım rozetlerini yükler.
    public async Task OnGetAsync()
    {
        try
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

            // Hesap listesi + bakiye (sayfalanmış + toplam bakiye SQL aggregate)
            var page = Page < 1 ? 1 : Page;
            var parms = new DynamicParameters();
            parms.Add("CompanyId", company.Id);
            parms.Add("Page", page);
            parms.Add("PageSize", PageSize);

            var filter = "";
            if (Type != "all") { filter = " AND v.AccountType = @Type"; parms.Add("Type", Type); }

            var sql = $@"
                SELECT
                    v.AccountId, v.Code, v.Name, v.AccountType, v.Currency,
                    v.Balance, v.LastMovementDate, v.TransactionCount,
                    a.BankName, a.IBAN, a.CreditLimit, a.InterestRate
                FROM dbo.tvf_AccountBalance(@CompanyId) v
                JOIN FinancialAccount a ON a.Id = v.AccountId
                WHERE 1 = 1{filter}
                ORDER BY v.AccountType, v.Name
                OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

                SELECT COUNT(*) AS Cnt, ISNULL(SUM(v.Balance), 0) AS TotalBalance
                FROM dbo.tvf_AccountBalance(@CompanyId) v
                WHERE 1 = 1{filter};";

            using var grid = await conn.QueryMultipleAsync(sql, parms);
            Accounts = (await grid.ReadAsync<AccountRowDto>()).ToList();
            var agg = await grid.ReadSingleAsync<AggRow>();
            FilteredCount = agg.Cnt;
            TotalBalance  = agg.TotalBalance;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Finansal hesap listesi veri yükleme hatası");
            TempData["Error"] = "Veriler yüklenirken bir hata oluştu.";
        }
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
