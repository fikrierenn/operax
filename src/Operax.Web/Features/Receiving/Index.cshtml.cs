using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Receiving;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ReceivingDto> Documents { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        const string sql = @"
            SELECT r.Id, r.DocNo, r.DocDate, r.Status, p.Name as PartnerName, wh.Code as WarehouseCode
            FROM ReceivingHeader r
            JOIN Partner p ON p.Id = r.PartnerId
            JOIN Warehouse wh ON wh.Id = r.WarehouseId
            WHERE r.CompanyId = @CompanyId AND r.IsDeleted = 0
            ORDER BY r.DocDate DESC, r.DocNo DESC";

        Documents = await conn.QueryAsync<ReceivingDto>(sql, new { CompanyId = company.Id });
    }

    public record ReceivingDto(Guid Id, string DocNo, DateTime DocDate, string Status, string PartnerName, string WarehouseCode);
}
