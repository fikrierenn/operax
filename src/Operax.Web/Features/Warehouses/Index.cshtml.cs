using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Warehouses;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    // Metin araması — kod veya ad
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    // Durum filtresi — "1" aktif, "0" pasif, null = tümü
    [BindProperty(SupportsGet = true)]
    public string? IsActive { get; set; }

    public IEnumerable<WarehouseDto> Warehouses { get; set; } = [];

    public int ActiveWarehouses { get; set; }
    public int TotalBins { get; set; }
    public decimal AverageCapacity { get; set; }

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        // İş kuralı: arama terimi varsa başına/sonuna % eklenerek LIKE filtresi uygulanır
        var search = string.IsNullOrWhiteSpace(Q) ? null : $"%{Q.Trim()}%";

        // İş kuralı: durum filtresi "1" ya da "0" değilse tümü gösterilir
        bool? activeFilter = IsActive switch { "1" => true, "0" => false, _ => null };

        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        const string sql = @"
            SELECT Id, Code, Name, IsActive
            FROM Warehouse
            WHERE CompanyId = @CompanyId AND IsDeleted = 0
              AND (@Search IS NULL OR Code LIKE @Search OR Name LIKE @Search)
              AND (@ActiveFilter IS NULL OR IsActive = @ActiveFilter)
            ORDER BY Code
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM Warehouse
            WHERE CompanyId = @CompanyId AND IsDeleted = 0
              AND (@Search IS NULL OR Code LIKE @Search OR Name LIKE @Search)
              AND (@ActiveFilter IS NULL OR IsActive = @ActiveFilter);";

        using (var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql,
            new { CompanyId = company.Id, Search = search, ActiveFilter = activeFilter, Page = page, PageSize },
            cancellationToken: ct)))
        {
            Warehouses = (await grid.ReadAsync<WarehouseDto>()).ToList();
            FilteredCount = await grid.ReadSingleAsync<int>();
        }

        ActiveWarehouses = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Warehouse WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0",
            new { CompanyId = company.Id },
            cancellationToken: ct));

        TotalBins = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(1)
            FROM Bin b
            JOIN Warehouse w ON w.Id = b.WarehouseId
            WHERE w.CompanyId = @CompanyId AND b.IsActive = 1 AND b.IsDeleted = 0",
            new { CompanyId = company.Id },
            cancellationToken: ct));

        int occupiedBins = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(DISTINCT BinId)
            FROM tvf_InventoryBalance(@CompanyId)
            WHERE BinId IS NOT NULL",
            new { CompanyId = company.Id },
            cancellationToken: ct));

        if (TotalBins > 0)
        {
            AverageCapacity = ((decimal)occupiedBins / TotalBins) * 100;
        }
        else
        {
            AverageCapacity = 0;
        }
    }

    public record WarehouseDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public bool IsActive { get; set; } }
}
