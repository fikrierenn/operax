using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Branches;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<BranchDto> Branches { get; set; } = [];
    public int ActiveCount { get; set; }
    public int TotalWarehouseCount { get; set; }

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        // Şube listesi (depo sayısıyla) + sayfalama; toplam şube sayısı tek round-trip
        const string sql = @"
            SELECT b.Id, b.Code, b.Name, b.City, b.BranchType, b.IsActive,
                   COUNT(w.Id) AS WarehouseCount
            FROM Branch b
            LEFT JOIN Warehouse w ON w.BranchId = b.Id AND w.IsDeleted = 0
            WHERE b.CompanyId = @CompanyId AND b.IsDeleted = 0
            GROUP BY b.Id, b.Code, b.Name, b.City, b.BranchType, b.IsActive
            ORDER BY b.Code
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM Branch WHERE CompanyId = @CompanyId AND IsDeleted = 0;";

        using (var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct)))
        {
            Branches = (await grid.ReadAsync<BranchDto>()).ToList();
            FilteredCount = await grid.ReadSingleAsync<int>();
        }

        ActiveCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Branch WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0",
            new { CompanyId = company.Id }, cancellationToken: ct));

        TotalWarehouseCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(1) FROM Warehouse w
            JOIN Branch b ON b.Id = w.BranchId
            WHERE b.CompanyId = @CompanyId AND w.IsDeleted = 0",
            new { CompanyId = company.Id }, cancellationToken: ct));
    }

    public record BranchDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? City { get; set; }
        public string BranchType { get; set; } = "SUBE";
        public bool IsActive { get; set; }
        public int WarehouseCount { get; set; }
    }
}
