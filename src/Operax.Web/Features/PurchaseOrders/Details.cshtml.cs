using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.PurchaseOrders;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit) : PageModel
{
    [BindProperty]
    public PurchaseOrderHeaderDto Header { get; set; } = new();
    public IEnumerable<PurchaseOrderLineDto> Lines { get; set; } = [];

    public IEnumerable<DdlDto> Warehouses     { get; set; } = [];
    public IEnumerable<DdlDto> Vendors        { get; set; } = [];
    public IEnumerable<DdlDto> AvailableItems { get; set; } = [];

    public bool IsNew => Header.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        // Satınalma siparişi formunu yükler
        using var conn = db.Open();

        Warehouses = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0",
            new { CompanyId = company.Id });

        Vendors = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Partner WHERE CompanyId = @CompanyId AND Type IN ('VENDOR', 'BOTH') AND IsDeleted = 0",
            new { CompanyId = company.Id });

        AvailableItems = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0",
            new { CompanyId = company.Id });

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<PurchaseOrderHeaderDto>(@"
                SELECT o.Id, o.WarehouseId, o.PartnerId, o.OrderNo, o.Status, o.Notes, o.OrderDate,
                       p.Name as PartnerName
                FROM PurchaseOrderHeader o
                JOIN Partner p ON p.Id = o.PartnerId
                WHERE o.Id = @Id AND o.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }) ?? new();

            Lines = await conn.QueryAsync<PurchaseOrderLineDto>(@"
                SELECT l.Id, i.Code as ItemCode, i.Name as ItemName, dv.Code as UomCode,
                       l.QtyOrdered, l.QtyReceived, l.Price
                FROM PurchaseOrderLine l
                JOIN PurchaseOrderHeader oh ON oh.Id = l.HeaderId
                JOIN Item i ON i.Id = l.ItemId
                JOIN DictionaryValue dv ON dv.Id = l.UomId
                WHERE l.HeaderId = @Id AND oh.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id });
        }
        else
        {
            Header.OrderDate = DateTime.Now;
            Header.Status    = DocStatus.Draft;
            Header.OrderNo   = "NEW";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Satınalma siparişi başlığını kaydeder veya günceller (DRAFT)
        using var conn = db.Open();

        if (IsNew)
        {
            Header.Id = Guid.NewGuid();
            var seq = conn.ExecuteScalar<int>(
                "SELECT COUNT(1) + 1 FROM PurchaseOrderHeader WHERE CompanyId = @CompanyId AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)",
                new { CompanyId = company.Id });
            Header.OrderNo = $"{DocPrefix.PurchaseOrder}-{DateTime.Now:yyyyMMdd}-{seq:D5}";

            await conn.ExecuteAsync(@"
                INSERT INTO PurchaseOrderHeader
                    (Id, CompanyId, WarehouseId, PartnerId, OrderNo, Status, OrderDate, Notes, CreatedBy)
                VALUES
                    (@Id, @CompanyId, @WarehouseId, @PartnerId, @OrderNo, @Status, @OrderDate, @Notes, @UserId)",
                new {
                    Header.Id, CompanyId = company.Id, Header.WarehouseId, Header.PartnerId,
                    Header.OrderNo, Status = DocStatus.Draft, Header.OrderDate, Header.Notes,
                    UserId = user.Id
                });
            await audit.LogAsync("CREATE", "PurchaseOrderHeader", Header.Id, $"OrderNo: {Header.OrderNo}");
        }
        else
        {
            await conn.ExecuteAsync(
                "UPDATE PurchaseOrderHeader SET WarehouseId=@WarehouseId, PartnerId=@PartnerId, Notes=@Notes WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Header.WarehouseId, Header.PartnerId, Header.Notes, Header.Id, CompanyId = company.Id });
            await audit.LogAsync("UPDATE", "PurchaseOrderHeader", Header.Id, $"OrderNo: {Header.OrderNo}");
        }

        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, decimal qty)
    {
        // Satır ekler; temel birimi DB'den okur
        using var conn = db.Open();

        var baseUomId = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT BaseUomId FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId",
            new { ItemId = itemId, CompanyId = company.Id });

        // Ürün şirkete ait değilse veya bulunamazsa yönlendir
        if (baseUomId is null) return RedirectToPage(new { id });

        await conn.ExecuteAsync(
            "INSERT INTO PurchaseOrderLine (HeaderId, ItemId, UomId, QtyOrdered) VALUES (@HeaderId, @ItemId, @UomId, @Qty)",
            new { HeaderId = id, ItemId = itemId, UomId = baseUomId, Qty = qty });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        // Siparişi onaylar: DRAFT → APPROVED
        using var conn = db.Open();
        await conn.ExecuteAsync(
            "UPDATE PurchaseOrderHeader SET Status=@Status, UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId WHERE Id=@Id AND CompanyId=@CompanyId",
            new { Status = DocStatus.Approved, UserId = user.Id, Id = id, CompanyId = company.Id });
        await audit.LogAsync("APPROVE", "PurchaseOrderHeader", id, "Satınalma siparişi onaylandı");
        return RedirectToPage(new { id });
    }

    public record PurchaseOrderHeaderDto
    {
        public Guid     Id          { get; set; }
        public Guid     WarehouseId { get; set; }
        public Guid     PartnerId   { get; set; }
        public string   OrderNo     { get; set; } = "";
        public string   Status      { get; set; } = DocStatus.Draft;
        public DateTime OrderDate   { get; set; }
        public string?  Notes       { get; set; }
        public string?  PartnerName { get; set; }
    }

    public record PurchaseOrderLineDto
    {
        public Guid    Id          { get; set; }
        public string? ItemCode    { get; set; }
        public string? ItemName    { get; set; }
        public string? UomCode     { get; set; }
        public decimal QtyOrdered  { get; set; }
        public decimal QtyReceived { get; set; }
        public decimal? Price      { get; set; }
    }
}
