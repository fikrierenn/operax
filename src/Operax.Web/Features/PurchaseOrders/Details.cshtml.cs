using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.PurchaseOrders;

/// <summary>
/// Satınalma siparişi detay/yeni/düzenleme sayfası.
/// DRAFT durumda satır eklenir/düzenlenir; POSTED sonrası salt okunur.
/// Tüm header ek bilgileri (şehir, VKN, satır toplamı, aktivite) veritabanından gelir.
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit) : PageModel
{
    [BindProperty]
    public PurchaseOrderHeaderDto Header { get; set; } = new();
    public IEnumerable<PurchaseOrderLineDto> Lines { get; set; } = [];
    public IEnumerable<ActivityDto>          Activities { get; set; } = [];

    public IEnumerable<DdlDto> Warehouses     { get; set; } = [];
    public IEnumerable<DdlDto> Vendors        { get; set; } = [];
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

        Vendors = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Partner WHERE CompanyId = @CompanyId AND Type IN ('VENDOR', 'BOTH') AND IsDeleted = 0", p);

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

    // Header detayı + Partner ek alanları
    private async Task LoadHeaderAsync(System.Data.IDbConnection conn, Guid id)
    {
        Header = await conn.QueryFirstOrDefaultAsync<PurchaseOrderHeaderDto>(@"
            SELECT
                o.Id, o.WarehouseId, o.PartnerId, o.OrderNo, o.Status, o.Notes,
                o.OrderDate, o.CreatedAt, o.UpdatedAt,
                p.Name        AS PartnerName,
                p.Code        AS PartnerCode,
                p.TaxNumber   AS PartnerTaxNumber,
                c.Name        AS PartnerCity,
                w.Name        AS WarehouseName,
                DATEADD(DAY, 14, o.OrderDate) AS DueDate
            FROM PurchaseOrderHeader o
            JOIN Partner p ON p.Id = o.PartnerId
            LEFT JOIN City c ON c.Id = p.CityId
            JOIN Warehouse w ON w.Id = o.WarehouseId
            WHERE o.Id = @Id AND o.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }) ?? new();
    }

    // Satırlar + ürün kodu/adı + UOM kodu
    private async Task LoadLinesAsync(System.Data.IDbConnection conn, Guid id)
    {
        Lines = await conn.QueryAsync<PurchaseOrderLineDto>(@"
            SELECT
                l.Id, i.Code AS ItemCode, i.Name AS ItemName, dv.Code AS UomCode,
                l.QtyOrdered, l.QtyReceived, l.Price
            FROM PurchaseOrderLine l
            JOIN PurchaseOrderHeader oh ON oh.Id = l.HeaderId
            JOIN Item i ON i.Id = l.ItemId
            JOIN DictionaryValue dv ON dv.Id = l.UomId
            WHERE l.HeaderId = @Id AND oh.CompanyId = @CompanyId
            ORDER BY l.CreatedAt",
            new { Id = id, CompanyId = company.Id });
    }

    // Aktivite logu — bu evraka ait son 8 audit kaydı
    private async Task LoadActivitiesAsync(System.Data.IDbConnection conn, Guid id)
    {
        Activities = await conn.QueryAsync<ActivityDto>(@"
            SELECT TOP 8
                a.CreatedAt,
                ISNULL(NULLIF(a.UserName, ''), 'Sistem') AS UserName,
                a.Action,
                a.Details AS Notes
            FROM AuditLog a
            WHERE a.EntityType = 'PurchaseOrderHeader' AND a.EntityId = @Id
            ORDER BY a.CreatedAt DESC",
            new { Id = id });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        using var conn = db.Open();

        if (IsNew)
        {
            Header.Id = Guid.NewGuid();
            // İş kuralı: Günlük sıralı evrak numarası üretilir (PO-YYYYMMDD-NNNNN)
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

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, decimal qty, decimal? price)
    {
        using var conn = db.Open();
        // İş kuralı: Ürünün temel birimi DB'den okunur, satıra yansıtılır
        var baseUomId = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT BaseUomId FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId",
            new { ItemId = itemId, CompanyId = company.Id });

        if (baseUomId is null) return RedirectToPage(new { id });

        await conn.ExecuteAsync(@"
            INSERT INTO PurchaseOrderLine (HeaderId, ItemId, UomId, QtyOrdered, Price, Currency)
            VALUES (@HeaderId, @ItemId, @UomId, @Qty, @Price, 'TRY')",
            new { HeaderId = id, ItemId = itemId, UomId = baseUomId, Qty = qty, Price = price ?? 0 });

        await audit.LogAsync("ADD_LINE", "PurchaseOrderHeader", id, $"Item: {itemId} Qty: {qty}");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        // İş kuralı: DRAFT → POSTED geçişi. sp_PoPost StatusTransition doğrulamasını yapar,
        // sonrasında sp_GeneratePaymentPlanFromPO ile tedarikçi vade planını otomatik üretir.
        using var conn = db.Open();
        await conn.ExecuteAsync(
            "sp_PoPost",
            new { PoHeaderId = id, CompanyId = company.Id, UserId = (Guid?)null },
            commandType: System.Data.CommandType.StoredProcedure);

        await audit.LogAsync("POST", "PurchaseOrderHeader", id,
            "Satınalma siparişi onaylandı, ödeme planı oluşturuldu");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id)
    {
        // İş kuralı: POSTED durumdan CANCELLED'a geçiş; geri alma için audit kaydı zorunlu
        using var conn = db.Open();
        await conn.ExecuteAsync(
            "UPDATE PurchaseOrderHeader SET Status=@Status, UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId WHERE Id=@Id AND CompanyId=@CompanyId",
            new { Status = DocStatus.Cancelled, UserId = user.Id, Id = id, CompanyId = company.Id });
        await audit.LogAsync("CANCEL", "PurchaseOrderHeader", id, "Satınalma siparişi iptal edildi");
        return RedirectToPage(new { id });
    }

    // ─── DTO'lar ────────────────────────────────────────────────
    public record PurchaseOrderHeaderDto
    {
        public Guid     Id                 { get; set; }
        public Guid     WarehouseId        { get; set; }
        public Guid     PartnerId          { get; set; }
        public string   OrderNo            { get; set; } = "";
        public string   Status             { get; set; } = DocStatus.Draft;
        public DateTime OrderDate          { get; set; }
        public DateTime? CreatedAt         { get; set; }
        public DateTime? UpdatedAt         { get; set; }
        public DateTime? DueDate           { get; set; }
        public string?  Notes              { get; set; }
        public string?  PartnerName        { get; set; }
        public string?  PartnerCode        { get; set; }
        public string?  PartnerTaxNumber   { get; set; }
        public string?  PartnerCity        { get; set; }
        public string?  WarehouseName      { get; set; }
    }

    public record PurchaseOrderLineDto
    {
        public Guid     Id          { get; set; }
        public string?  ItemCode    { get; set; }
        public string?  ItemName    { get; set; }
        public string?  UomCode     { get; set; }
        public decimal  QtyOrdered  { get; set; }
        public decimal  QtyReceived { get; set; }
        public decimal? Price       { get; set; }
        public decimal  LineTotal   => QtyOrdered * (Price ?? 0);
    }

    public record ActivityDto(DateTime CreatedAt, string UserName, string Action, string? Notes)
    {
        // Görüntüleme için Türkçe aksiyon etiketi
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
