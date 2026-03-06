using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.MasterData.Items;

public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ItemDto> Items { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        const string sql = @"
            SELECT i.Id, i.Code, i.Name, i.Barcode, i.TaxRate,
                   dv.NameTr as BaseUom, c.Name as CategoryName,
                   i.IsLotTracked, i.IsSerialTracked, i.IsActive
            FROM Item i
            JOIN DictionaryValue dv ON dv.Id = i.BaseUomId
            LEFT JOIN Category c ON c.Id = i.CategoryId
            WHERE i.CompanyId = @CompanyId AND i.IsDeleted = 0
            ORDER BY i.Code";

        Items = await conn.QueryAsync<ItemDto>(sql, new { CompanyId = company.Id });
    }

    public record ItemDto(Guid Id, string Code, string Name, string? Barcode, decimal TaxRate, string BaseUom, string? CategoryName, bool IsLotTracked, bool IsSerialTracked, bool IsActive);
}
