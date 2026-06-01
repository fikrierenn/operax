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

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        // Şube listesi — depo sayısıyla birlikte
        Branches = await conn.QueryAsync<BranchDto>(@"
            SELECT b.Id, b.Code, b.Name, b.City, b.BranchType, b.IsActive,
                   COUNT(w.Id) AS WarehouseCount
            FROM Branch b
            LEFT JOIN Warehouse w ON w.BranchId = b.Id AND w.IsDeleted = 0
            WHERE b.CompanyId = @CompanyId AND b.IsDeleted = 0
            GROUP BY b.Id, b.Code, b.Name, b.City, b.BranchType, b.IsActive
            ORDER BY b.Code",
            new { CompanyId = company.Id });

        ActiveCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Branch WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0",
            new { CompanyId = company.Id });

        TotalWarehouseCount = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM Warehouse w
            JOIN Branch b ON b.Id = w.BranchId
            WHERE b.CompanyId = @CompanyId AND w.IsDeleted = 0",
            new { CompanyId = company.Id });
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
