using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.SalesOrders;

/// <summary>
/// Satış siparişi detay/yeni/düzenleme sayfası.
/// DRAFT durumda satır eklenir/düzenlenir; APPROVED sonrası salt okunur.
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit) : PageModel
{
    [BindProperty]
    public SalesOrderHeaderDto Header { get; set; } = new();
    public IEnumerable<SalesOrderLineDto> Lines { get; set; } = [];
    public IEnumerable<ActivityDto>       Activities { get; set; } = [];

    public IEnumerable<DdlDto> Warehouses     { get; set; } = [];
    public IEnumerable<DdlDto> Customers      { get; set; } = [];
    public IEnumerable<DdlDto> AvailableItems { get; set; } = [];

    public bool IsNew => Header.Id == Guid.Empty;
    public decimal Subtotal => Lines.Sum(l => l.QtyOrdered * (l.Price ?? 0));
    public decimal Vat      => System.Math.Round(Subtotal * 0.20m, 2);
    public decimal Grand    => Subtotal + Vat;

    public async Task OnGetAsync(Guid? id)
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        Warehouses = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0", p);

        Customers = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Partner WHERE CompanyId = @CompanyId AND Type IN ('CUSTOMER', 'BOTH') AND IsDeleted = 0", p);

        AvailableItems = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0", p);

        if (id.HasValue)
        {
            await LoadHeaderAsync(conn, id.Value);
            await LoadLinesAsync(conn, id.Value);
            await LoadActivitiesAsync(conn, id.Value);
        }
        else
        {
            Header.OrderDate = DateTime.Now;
            Header.Status    = DocStatus.Draft;
            Header.OrderNo   = "NEW";
        }
    }

    private async Task LoadHeaderAsync(System.Data.IDbConnection conn, Guid id)
    {
        Header = await conn.QueryFirstOrDefaultAsync<SalesOrderHeaderDto>(@"
            SELECT
                o.Id, o.WarehouseId, o.PartnerId, o.OrderNo, o.Status, o.Notes,
                o.OrderDate, o.RequestedDeliveryDate, o.CreatedAt, o.UpdatedAt,
                p.Name AS CustomerName,
                p.Code AS PartnerCode,
                p.TaxNumber AS PartnerTaxNumber,
                c.Name AS PartnerCity,
                w.Name AS WarehouseName
            FROM SalesOrderHeader o
            JOIN Partner p ON p.Id = o.PartnerId
            LEFT JOIN City c ON c.Id = p.CityId
            JOIN Warehouse w ON w.Id = o.WarehouseId
            WHERE o.Id = @Id AND o.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }) ?? new();
    }

    private async Task LoadLinesAsync(System.Data.IDbConnection conn, Guid id)
    {
        Lines = await conn.QueryAsync<SalesOrderLineDto>(@"
            SELECT
                l.Id, i.Code AS ItemCode, i.Name AS ItemName, dv.Code AS UomCode,
                l.QtyOrdered, l.QtyReserved, l.QtyShipped, l.Price
            FROM SalesOrderLine l
            JOIN SalesOrderHeader oh ON oh.Id = l.HeaderId
            JOIN Item i ON i.Id = l.ItemId
            JOIN DictionaryValue dv ON dv.Id = l.UomId
            WHERE l.HeaderId = @Id AND oh.CompanyId = @CompanyId
            ORDER BY l.CreatedAt",
            new { Id = id, CompanyId = company.Id });
    }

    private async Task LoadActivitiesAsync(System.Data.IDbConnection conn, Guid id)
    {
        Activities = await conn.QueryAsync<ActivityDto>(@"
            SELECT TOP 8
                a.CreatedAt,
                ISNULL(NULLIF(a.UserName, ''), 'Sistem') AS UserName,
                a.Action,
                a.Details AS Notes
            FROM AuditLog a
            WHERE a.EntityType = 'SalesOrderHeader' AND a.EntityId = @Id
            ORDER BY a.CreatedAt DESC",
            new { Id = id });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        using var conn = db.Open();

        if (IsNew)
        {
            Header.Id = Guid.NewGuid();
            var seq = conn.ExecuteScalar<int>(
                "SELECT COUNT(1) + 1 FROM SalesOrderHeader WHERE CompanyId = @CompanyId AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)",
                new { CompanyId = company.Id });
            Header.OrderNo = $"{DocPrefix.SalesOrder}-{DateTime.Now:yyyyMMdd}-{seq:D5}";

            await conn.ExecuteAsync(@"
                INSERT INTO SalesOrderHeader
                    (Id, CompanyId, WarehouseId, PartnerId, OrderNo, Status, OrderDate, RequestedDeliveryDate, Notes, CreatedBy)
                VALUES
                    (@Id, @CompanyId, @WarehouseId, @PartnerId, @OrderNo, @Status, @OrderDate, @RequestedDeliveryDate, @Notes, @UserId)",
                new {
                    Header.Id, CompanyId = company.Id, Header.WarehouseId, Header.PartnerId,
                    Header.OrderNo, Status = DocStatus.Draft, Header.OrderDate,
                    Header.RequestedDeliveryDate, Header.Notes, UserId = user.Id
                });
            await audit.LogAsync("CREATE", "SalesOrderHeader", Header.Id, $"OrderNo: {Header.OrderNo}");
        }
        else
        {
            await conn.ExecuteAsync(
                "UPDATE SalesOrderHeader SET WarehouseId=@WarehouseId, PartnerId=@PartnerId, RequestedDeliveryDate=@RequestedDeliveryDate, Notes=@Notes WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Header.WarehouseId, Header.PartnerId, Header.RequestedDeliveryDate, Header.Notes, Header.Id, CompanyId = company.Id });
            await audit.LogAsync("UPDATE", "SalesOrderHeader", Header.Id, $"OrderNo: {Header.OrderNo}");
        }

        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, decimal qty, decimal? price)
    {
        using var conn = db.Open();
        var baseUomId = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT BaseUomId FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId",
            new { ItemId = itemId, CompanyId = company.Id });

        if (baseUomId is null) return RedirectToPage(new { id });

        await conn.ExecuteAsync(@"
            INSERT INTO SalesOrderLine (HeaderId, ItemId, UomId, QtyOrdered, Price, Currency)
            VALUES (@HeaderId, @ItemId, @UomId, @Qty, @Price, 'TRY')",
            new { HeaderId = id, ItemId = itemId, UomId = baseUomId, Qty = qty, Price = price ?? 0 });

        await audit.LogAsync("ADD_LINE", "SalesOrderHeader", id, $"Item: {itemId} Qty: {qty}");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(
            "UPDATE SalesOrderHeader SET Status=@Status, UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId WHERE Id=@Id AND CompanyId=@CompanyId",
            new { Status = DocStatus.Approved, UserId = user.Id, Id = id, CompanyId = company.Id });
        await audit.LogAsync("APPROVE", "SalesOrderHeader", id, "Satış siparişi onaylandı");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(
            "UPDATE SalesOrderHeader SET Status=@Status, UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId WHERE Id=@Id AND CompanyId=@CompanyId",
            new { Status = DocStatus.Cancelled, UserId = user.Id, Id = id, CompanyId = company.Id });
        await audit.LogAsync("CANCEL", "SalesOrderHeader", id, "Satış siparişi iptal edildi");
        return RedirectToPage(new { id });
    }

    public record SalesOrderHeaderDto
    {
        public Guid      Id                      { get; set; }
        public Guid      WarehouseId             { get; set; }
        public Guid      PartnerId               { get; set; }
        public string    OrderNo                 { get; set; } = "";
        public string    Status                  { get; set; } = DocStatus.Draft;
        public DateTime  OrderDate               { get; set; }
        public DateTime? RequestedDeliveryDate   { get; set; }
        public DateTime? CreatedAt               { get; set; }
        public DateTime? UpdatedAt               { get; set; }
        public string?   Notes                   { get; set; }
        public string?   CustomerName            { get; set; }
        public string?   PartnerCode             { get; set; }
        public string?   PartnerTaxNumber        { get; set; }
        public string?   PartnerCity             { get; set; }
        public string?   WarehouseName           { get; set; }
    }

    public record SalesOrderLineDto
    {
        public Guid     Id          { get; set; }
        public string?  ItemCode    { get; set; }
        public string?  ItemName    { get; set; }
        public string?  UomCode     { get; set; }
        public decimal  QtyOrdered  { get; set; }
        public decimal  QtyReserved { get; set; }
        public decimal  QtyShipped  { get; set; }
        public decimal? Price       { get; set; }
        public decimal  LineTotal   => QtyOrdered * (Price ?? 0);
        public decimal  OpenQty     => QtyOrdered - QtyShipped;
    }

    public record ActivityDto(DateTime CreatedAt, string UserName, string Action, string? Notes)
    {
        public string ActionLabel => Action switch
        {
            "CREATE"    => "Taslak oluşturdu",
            "UPDATE"    => "Bilgileri güncelledi",
            "ADD_LINE"  => "Kalem ekledi",
            "POST"      => "Siparişi onayladı",
            "APPROVE"   => "Siparişi onayladı",
            "CANCEL"    => "Siparişi iptal etti",
            _           => Action
        };
    }
}
