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
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, ILogger<DetailsModel> logger) : PageModel
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
                DATEADD(DAY, ISNULL(o.PaymentTermDays, ISNULL(p.PaymentTermDays, 30)), o.OrderDate) AS DueDate,
                ISNULL(o.PaymentTermDays, ISNULL(p.PaymentTermDays, 30)) AS PaymentTermDays
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
                NULLIF(a.UserName, '') AS UserName,
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
            var seq = await conn.ExecuteScalarAsync<int>(
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

        // İş kuralı: Yeni satır Id'si geri alınır (fiyat farkı kontrolü için gerekli)
        var newLineId = await conn.ExecuteScalarAsync<Guid>(@"
            INSERT INTO PurchaseOrderLine (HeaderId, ItemId, UomId, QtyOrdered, Price, Currency)
            OUTPUT INSERTED.Id
            VALUES (@HeaderId, @ItemId, @UomId, @Qty, @Price, 'TRY')",
            new { HeaderId = id, ItemId = itemId, UomId = baseUomId, Qty = qty, Price = price ?? 0 });

        await audit.LogAsync("ADD_LINE", "PurchaseOrderHeader", id, $"Item: {itemId} Qty: {qty}");

        // İş kuralı: Fiyat farkı kontrolü — tedarikçi fiyat listesinden sapma eşik üstü ise PriceVariance kaydı
        await CheckPriceVarianceAsync(conn, id, newLineId, itemId, price ?? 0);

        return RedirectToPage(new { id });
    }

    // Satır fiyatını tedarikçi liste fiyatıyla karşılaştırır; sapma eşik üstü ise
    // sp_CheckPriceVariance bir PriceVariance (DRAFT) kaydı açar, kullanıcıya uyarı gösterilir.
    private async Task CheckPriceVarianceAsync(
        System.Data.IDbConnection conn, Guid headerId, Guid lineId, Guid itemId, decimal actualPrice)
    {
        // Tedarikçi (PartnerId) header'dan okunur
        var partnerId = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT PartnerId FROM PurchaseOrderHeader WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = headerId, CompanyId = company.Id });

        if (partnerId is null) return;

        var prm = new DynamicParameters();
        prm.Add("CompanyId",  company.Id);
        prm.Add("PoHeaderId", headerId);
        prm.Add("PoLineId",   lineId);
        prm.Add("ItemId",     itemId);
        prm.Add("PartnerId",  partnerId.Value);
        prm.Add("ActualPrice", actualPrice);
        prm.Add("UserId",     user.Id);
        prm.Add("VarianceId", dbType: System.Data.DbType.Guid, direction: System.Data.ParameterDirection.Output);

        await conn.ExecuteAsync("sp_CheckPriceVariance", prm,
            commandType: System.Data.CommandType.StoredProcedure);

        var varianceId = prm.Get<Guid?>("VarianceId");
        if (varianceId.HasValue)
        {
            // İş kuralı: Sapma tespit edildi — kullanıcı detayı PriceVariance ekranında görür
            TempData["PriceWarning"] =
                "Bu satırın fiyatı tedarikçi liste fiyatından saptı. Fiyat farkı onaya gönderildi.";
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        // İş kuralı: DRAFT → POSTED geçişi. sp_PoPost StatusTransition doğrulamasını yapar,
        // sonrasında sp_GeneratePaymentPlanFromPO ile tedarikçi vade planını otomatik üretir.
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(
                "sp_PoPost",
                new { PoHeaderId = id, CompanyId = company.Id, UserId = (Guid?)null },
                commandType: System.Data.CommandType.StoredProcedure);
            await audit.LogAsync("POST", "PurchaseOrderHeader", id,
                "Satınalma siparişi onaylandı, ödeme planı oluşturuldu");
        }
        catch (Microsoft.Data.SqlClient.SqlException sex) when (sex.Number is >= 50000 and < 60000)
        {
            TempData["Error"] = sex.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sex)
        {
            logger.LogError(sex, "PO onay hatası: {PoId}", id);
            TempData["Error"] = "Sipariş onaylanırken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id)
    {
        // İş kuralı: POSTED → CANCELLED; sp_ValidateStatusTransition bypass engeli
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_ValidateStatusTransition",
                new { CompanyId = company.Id, DocType = "PURCHASE_ORDER",
                      CurrentStatus = DocStatus.Posted, NewStatus = DocStatus.Cancelled,
                      UserId = user.Id },
                commandType: System.Data.CommandType.StoredProcedure);

            await conn.ExecuteAsync(
                "UPDATE PurchaseOrderHeader SET Status=@Status, UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Status = DocStatus.Cancelled, UserId = user.Id, Id = id, CompanyId = company.Id });
            await audit.LogAsync("CANCEL", "PurchaseOrderHeader", id, "Satınalma siparişi iptal edildi");
        }
        catch (Microsoft.Data.SqlClient.SqlException sex) when (sex.Number is >= 50000 and < 60000)
        {
            TempData["Error"] = sex.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sex)
        {
            logger.LogError(sex, "PO iptal hatası: {PoId}", id);
            TempData["Error"] = "Sipariş iptal edilirken veritabanı hatası oluştu.";
        }
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
        public int      PaymentTermDays    { get; set; } = 30;
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

    // Denetim izi satırı — UserName NULL ise view 'Sistem' fallback uygular, etiket UiHelpers.AuditActionLabel'dan gelir.
    public record ActivityDto(DateTime CreatedAt, string? UserName, string Action, string? Notes);
}
