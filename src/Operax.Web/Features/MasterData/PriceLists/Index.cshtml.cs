using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.PriceLists;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    public IEnumerable<PriceListRow> Rows { get; set; } = [];
    public int SalesCount { get; set; }
    public int PurchaseCount { get; set; }

    // Arama ve filtre parametreleri — GET query string'den bind edilir
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? DirectionFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        // Fiyat listeleri — arama (Q), yön ve aktiflik filtreleriyle + sayfalama.
        // Cari/Şube NULL ise "Genel"/"Tüm Şubeler" olarak gösterilir; satır sayısı LEFT JOIN ile.
        const string sql = @"
            SELECT pl.Id, pl.Code, pl.Name, pl.Direction, pl.Priority,
                   pl.Currency, pl.IsActive, pl.ValidFrom, pl.ValidTo,
                   p.Name AS PartnerName, b.Name AS BranchName,
                   (SELECT COUNT(1) FROM PriceListLine l
                    WHERE l.PriceListId = pl.Id AND l.IsDeleted = 0) AS LineCount
            FROM PriceList pl
            LEFT JOIN Partner p ON p.Id = pl.PartnerId
            LEFT JOIN Branch  b ON b.Id = pl.BranchId
            WHERE pl.CompanyId = @CompanyId AND pl.IsDeleted = 0
              AND (@Q IS NULL OR @Q = '' OR pl.Code LIKE '%' + @Q + '%' OR pl.Name LIKE '%' + @Q + '%')
              AND (@DirectionFilter IS NULL OR @DirectionFilter = '' OR pl.Direction = @DirectionFilter)
              AND (@StatusFilter IS NULL OR @StatusFilter = ''
                   OR (@StatusFilter = 'active'  AND pl.IsActive = 1)
                   OR (@StatusFilter = 'passive' AND pl.IsActive = 0))
            ORDER BY pl.Direction, pl.Priority DESC, pl.Code
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM PriceList pl
            WHERE pl.CompanyId = @CompanyId AND pl.IsDeleted = 0
              AND (@Q IS NULL OR @Q = '' OR pl.Code LIKE '%' + @Q + '%' OR pl.Name LIKE '%' + @Q + '%')
              AND (@DirectionFilter IS NULL OR @DirectionFilter = '' OR pl.Direction = @DirectionFilter)
              AND (@StatusFilter IS NULL OR @StatusFilter = ''
                   OR (@StatusFilter = 'active'  AND pl.IsActive = 1)
                   OR (@StatusFilter = 'passive' AND pl.IsActive = 0));";

        using (var grid = await conn.QueryMultipleAsync(sql, new
        {
            CompanyId = company.Id, Q, DirectionFilter, StatusFilter, Page = page, PageSize
        }))
        {
            Rows = (await grid.ReadAsync<PriceListRow>()).ToList();
            FilteredCount = await grid.ReadSingleAsync<int>();
        }

        // Yön bazlı sayaçlar (filtreden bağımsız toplamlar)
        var counts = await conn.QueryAsync<(string Direction, int Cnt)>(
            @"SELECT Direction, COUNT(1) AS Cnt FROM PriceList
              WHERE CompanyId = @CompanyId AND IsDeleted = 0 GROUP BY Direction",
            new { CompanyId = company.Id });
        foreach (var c in counts)
        {
            if (c.Direction == PriceDirection.Sales) SalesCount = c.Cnt;
            else if (c.Direction == PriceDirection.Purchase) PurchaseCount = c.Cnt;
        }
    }

    // Fiyat listesini soft-delete eder (IsDeleted=1). Liste başlığı + satırları gizlenir.
    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(
            @"UPDATE PriceList SET IsDeleted = 1, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
              WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id, UserId = user.Id });
        TempData["Success"] = "Fiyat listesi silindi.";
        return RedirectToPage("Index");
    }

    // Liste satırı görüntüleme DTO'su
    public record PriceListRow(
        Guid Id, string Code, string Name, string Direction, int Priority,
        string? Currency, bool IsActive, DateTime? ValidFrom, DateTime? ValidTo,
        string? PartnerName, string? BranchName, int LineCount);
}
