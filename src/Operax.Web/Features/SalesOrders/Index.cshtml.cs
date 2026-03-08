using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.SalesOrders;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<OrderDto> Orders { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        const string sql = @"
            SELECT o.Id, o.OrderNo, o.OrderDate, o.Status, p.Name as CustomerName,
                   (SELECT ISNULL(SUM(QtyShipped) / NULLIF(SUM(QtyOrdered), 0) * 100, 0) FROM SalesOrderLine WHERE HeaderId = o.Id) as FulfillmentRate
            FROM SalesOrderHeader o
            JOIN Partner p ON p.Id = o.PartnerId
            WHERE o.CompanyId = @CompanyId AND o.IsDeleted = 0
            ORDER BY o.OrderDate DESC, o.OrderNo DESC";

        Orders = await conn.QueryAsync<OrderDto>(sql, new { CompanyId = company.Id });
    }

    public record OrderDto(Guid Id, string OrderNo, DateTime OrderDate, string Status, string CustomerName, decimal FulfillmentRate);
}
