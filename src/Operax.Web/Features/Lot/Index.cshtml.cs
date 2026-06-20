using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Lot;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<LotListDto> Lots { get; set; } = [];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        // Lot listesi + sayfalama (OFFSET/FETCH); sayfa + toplam tek round-trip
        const string sql = @"
            SELECT l.*, i.Code as ItemCode, i.Name as ItemName,
                   (SELECT ISNULL(SUM(QtyBase), 0) FROM StockMovement
                    WHERE CompanyId = @CompanyId AND ItemId = l.ItemId AND LotNo = l.LotNo AND IsCancelled = 0) as QtyOnHand
            FROM ItemLot l
            JOIN Item i ON i.Id = l.ItemId
            WHERE l.CompanyId = @CompanyId
            ORDER BY l.ExpiryDate ASC
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM ItemLot WHERE CompanyId = @CompanyId;";

        using var grid = await conn.QueryMultipleAsync(sql, new { CompanyId = company.Id, Page = page, PageSize });
        Lots = (await grid.ReadAsync<LotListDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
    }

    public record LotListDto { 
        public Guid Id { get; set; } 
        public string LotNo { get; set; } = ""; 
        public string ItemCode { get; set; } = ""; 
        public string ItemName { get; set; } = ""; 
        public DateTime? ExpiryDate { get; set; } 
        public decimal QtyOnHand { get; set; } 
        public string Status { get; set; } = "";
    }
}
