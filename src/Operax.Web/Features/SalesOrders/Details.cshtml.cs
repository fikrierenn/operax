using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.SalesOrders;

public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    [BindProperty]
    public SalesOrderHeaderDto Header { get; set; } = new();
    public IEnumerable<SalesOrderLineDto> Lines { get; set; } = [];
    
    public IEnumerable<DdlDto> Warehouses { get; set; } = [];
    public IEnumerable<DdlDto> Customers { get; set; } = [];
    public IEnumerable<DdlDto> AvailableItems { get; set; } = [];

    public bool IsNew => Header.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        using var conn = db.Open();

        Warehouses = await conn.QueryAsync<DdlDto>("SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0", new { CompanyId = company.Id });
        Customers = await conn.QueryAsync<DdlDto>("SELECT Id, Code, Name FROM Partner WHERE CompanyId = @CompanyId AND Type IN ('CUSTOMER', 'BOTH') AND IsDeleted = 0", new { CompanyId = company.Id });
        AvailableItems = await conn.QueryAsync<DdlDto>("SELECT Id, Code, Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0", new { CompanyId = company.Id });

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<SalesOrderHeaderDto>(@"
                SELECT o.Id, o.WarehouseId, o.PartnerId, o.OrderNo, o.Status, o.Notes, o.OrderDate, o.RequestedDeliveryDate, p.Name as CustomerName
                FROM SalesOrderHeader o
                JOIN Partner p ON p.Id = o.PartnerId
                WHERE o.Id = @Id", new { Id = id }) ?? new();

            Lines = await conn.QueryAsync<SalesOrderLineDto>(@"
                SELECT l.Id, i.Code as ItemCode, i.Name as ItemName, dv.Code as UomCode, 
                       l.QtyOrdered, l.QtyReserved, l.QtyShipped
                FROM SalesOrderLine l
                JOIN Item i ON i.Id = l.ItemId
                JOIN DictionaryValue dv ON dv.Id = l.UomId
                WHERE l.HeaderId = @Id", new { Id = id });
        }
        else
        {
            Header.OrderDate = DateTime.Now;
            Header.Status = "DRAFT";
            Header.OrderNo = "NEW";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        using var conn = db.Open();

        if (IsNew)
        {
            Header.Id = Guid.NewGuid();
            Header.OrderNo = $"SO-{DateTime.Now:yyyyMMdd}-{conn.ExecuteScalar<int>("SELECT COUNT(1) + 1 FROM SalesOrderHeader WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)"):D5}";
            
            const string sql = @"
                INSERT INTO SalesOrderHeader (Id, CompanyId, WarehouseId, PartnerId, OrderNo, Status, OrderDate, RequestedDeliveryDate, Notes, CreatedBy)
                VALUES (@Id, @CompanyId, @WarehouseId, @PartnerId, @OrderNo, 'DRAFT', @OrderDate, @RequestedDeliveryDate, @Notes, @UserId)";
            
            await conn.ExecuteAsync(sql, new { 
                Header.Id, CompanyId = company.Id, Header.WarehouseId, Header.PartnerId, 
                Header.OrderNo, Header.OrderDate, Header.RequestedDeliveryDate, Header.Notes, UserId = user.Id 
            });
        }
        else
        {
            const string sql = "UPDATE SalesOrderHeader SET WarehouseId = @WarehouseId, PartnerId = @PartnerId, RequestedDeliveryDate = @RequestedDeliveryDate, Notes = @Notes WHERE Id = @Id";
            await conn.ExecuteAsync(sql, Header);
        }

        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, decimal qty)
    {
        using var conn = db.Open();
        var item = await conn.QueryFirstOrDefaultAsync("SELECT BaseUomId FROM Item WHERE Id = @ItemId", new { ItemId = itemId });
        // Ürün bulunamadıysa yönlendir
        if (item is null) return RedirectToPage(new { id });

        const string sql = "INSERT INTO SalesOrderLine (HeaderId, ItemId, UomId, QtyOrdered) VALUES (@HeaderId, @ItemId, @UomId, @Qty)";
        await conn.ExecuteAsync(sql, new { HeaderId = id, ItemId = itemId, UomId = item.BaseUomId, Qty = qty });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("UPDATE SalesOrderHeader SET Status = 'APPROVED', UpdatedAt = GETUTCDATE() WHERE Id = @Id", new { Id = id });
        return RedirectToPage(new { id });
    }

    public record SalesOrderHeaderDto { public Guid Id { get; set; } public Guid WarehouseId { get; set; } public Guid PartnerId { get; set; } public string OrderNo { get; set; } = ""; public string Status { get; set; } = "DRAFT"; public DateTime OrderDate { get; set; } public DateTime? RequestedDeliveryDate { get; set; } public string? Notes { get; set; } public string? CustomerName { get; set; } }
    public record SalesOrderLineDto { public Guid Id { get; set; } public string? ItemCode { get; set; } public string? ItemName { get; set; } public string? UomCode { get; set; } public decimal QtyOrdered { get; set; } public decimal QtyReserved { get; set; } public decimal QtyShipped { get; set; } }
    public record DdlDto { public Guid Id { get; set; } public string? Code { get; set; } public string? Name { get; set; } }
}
