using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.CreditCards;

/// <summary>
/// Kredi kartı listesi: limit, kalan limit, son ekstre.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    public List<CardRowDto> Cards { get; set; } = [];
    public decimal TotalLimit { get; set; }
    public decimal TotalUsed  { get; set; }

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);
    private record AggRow(int Cnt, decimal TotalLimit, decimal TotalAvailable);

    // Kredi kartı listesini ve limit özetini yükler
    public async Task OnGetAsync()
    {
        try
        {
            using var conn = db.Open();
            var page = Page < 1 ? 1 : Page;

            const string sql = @"
                SELECT
                    c.Id, c.CardNoMasked, c.HolderName, c.BankName, c.CardType,
                    c.CreditLimit, c.AvailableLimit, c.StatementDay, c.DueDay,
                    c.InterestRate, c.Currency, c.ExpiresAt, c.IsActive,
                    (SELECT TOP 1 PeriodEnd FROM CreditCardStatement WHERE CardId = c.Id ORDER BY PeriodEnd DESC) AS LastStatementDate,
                    (SELECT TOP 1 ClosingBalance FROM CreditCardStatement WHERE CardId = c.Id ORDER BY PeriodEnd DESC) AS LastStatementBalance,
                    (SELECT TOP 1 DueDate FROM CreditCardStatement WHERE CardId = c.Id AND IsClosed = 0 ORDER BY PeriodEnd DESC) AS NextStatementDue
                FROM CreditCard c
                WHERE c.CompanyId = @CompanyId AND c.IsDeleted = 0
                ORDER BY c.BankName, c.HolderName
                OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

                SELECT COUNT(*) AS Cnt, ISNULL(SUM(CreditLimit), 0) AS TotalLimit,
                       ISNULL(SUM(AvailableLimit), 0) AS TotalAvailable
                FROM CreditCard WHERE CompanyId = @CompanyId AND IsDeleted = 0;";

            using var grid = await conn.QueryMultipleAsync(sql, new { CompanyId = company.Id, Page = page, PageSize });
            Cards = (await grid.ReadAsync<CardRowDto>()).ToList();
            var agg = await grid.ReadSingleAsync<AggRow>();
            FilteredCount = agg.Cnt;
            TotalLimit = agg.TotalLimit;
            TotalUsed  = agg.TotalLimit - agg.TotalAvailable;
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Kredi kartı listesi veri yükleme hatası");
            TempData["Error"] = "Veriler yüklenirken bir hata oluştu.";
        }
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
