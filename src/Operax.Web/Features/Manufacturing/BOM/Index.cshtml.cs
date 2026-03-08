using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Manufacturing.BOM;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ProductModelDto> Models { get; set; } = [];

    public async Task OnGetAsync()
    {
        // Şirkete ait tüm ürün modellerini (dinamik BOM şablonları) listeler
        using var conn = db.Open();
        Models = await conn.QueryAsync<ProductModelDto>(@"
            SELECT
                m.Id, m.Code, m.Name, m.IsActive,
                (SELECT COUNT(*) FROM ProductModelParameter WHERE ProductModelId = m.Id) AS ParamCount,
                (SELECT COUNT(*) FROM ProductModelBOM WHERE ProductModelId = m.Id) AS BomLineCount,
                i.Code AS BaseItemCode
            FROM ProductModel m
            LEFT JOIN Item i ON i.Id = m.BaseItemId
            WHERE m.CompanyId = @CompanyId
            ORDER BY m.Code",
            new { CompanyId = company.Id });
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
