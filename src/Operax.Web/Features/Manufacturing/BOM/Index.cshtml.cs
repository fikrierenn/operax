using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Manufacturing.BOM;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ProductModelDto> Models { get; set; } = [];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Şirkete ait tüm ürün modellerini (dinamik BOM şablonları) listeler + sayfalama
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        const string sql = @"
            SELECT
                m.Id, m.Code, m.Name, m.IsActive,
                (SELECT COUNT(*) FROM ProductModelParameter WHERE ProductModelId = m.Id) AS ParamCount,
                (SELECT COUNT(*) FROM ProductModelBOM WHERE ProductModelId = m.Id) AS BomLineCount,
                i.Code AS BaseItemCode
            FROM ProductModel m
            LEFT JOIN Item i ON i.Id = m.BaseItemId
            WHERE m.CompanyId = @CompanyId
            ORDER BY m.Code
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM ProductModel WHERE CompanyId = @CompanyId;";

        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
        Models = (await grid.ReadAsync<ProductModelDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
    }

    public record ProductModelDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public int ParamCount { get; set; }
        public int BomLineCount { get; set; }
        public string? BaseItemCode { get; set; }
    }
}
