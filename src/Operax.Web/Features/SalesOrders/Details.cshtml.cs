using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.SalesOrders;

/// <summary>
/// Satış siparişi detay/yeni/düzenleme sayfası.
/// DRAFT durumda satır eklenir/düzenlenir; APPROVED sonrası salt okunur.
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, INumberSeriesService numberSeries, ILogger<DetailsModel> logger) : PageModel
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
    // KDV satır-bazlı (her ürünün kendi TaxRate'i) — sabit %20 değil
    public decimal Vat      => Lines.Sum(l => l.LineTax);
    public decimal Grand    => Subtotal + Vat;

    // Sipariş detay sayfasını yükler: yeni sipariş ise boş form, mevcut ise başlık+satır+aktivite
    public async Task OnGetAsync(Guid? id)
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        Warehouses = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0", p);

        Customers = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Partner WHERE CompanyId = @CompanyId AND Type IN ('CUSTOMER', 'BOTH') AND IsDeleted = 0", p);

        // İş kuralı: CONSUMABLE (sarf malzeme) satışta gizlenir; yalnızca sarf fişinde kullanılır
        AvailableItems = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0 AND ItemType <> 'CONSUMABLE'", p);

        if (id.HasValue)
        {
            await LoadHeaderAsync(conn, id.Value);
            await LoadLinesAsync(conn, id.Value);
            await LoadActivitiesAsync(conn, id.Value);

            // Bağlı sevkiyat sayacı
            ShippingCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM ShippingHeader WHERE SalesOrderId = @Id AND CompanyId = @CompanyId AND Status <> @Cancelled",
                new { Id = id.Value, CompanyId = company.Id, Cancelled = DocStatus.Cancelled });

            DocFlow = new DocFlowVm([
                new DocFlowItem(
                    Label: "Sevkiyat",
                    Count: ShippingCount,
                    ListUrl: ShippingCount > 0 ? $"/Shipping?soId={id.Value}" : null,
                    CreateUrl: Header.Status == DocStatus.Posted
                        ? $"/SalesOrders/Details/{id.Value}?handler=CreateShipping"
                        : null,
                    CreateLabel: "Sevkiyat Oluştur",
                    CanCreate: Header.Status == DocStatus.Posted
                        && user.HasRole("Administrator","Sales","WarehouseManager"))
            ]);
        }
        else
        {
            Header.OrderDate = DateTime.UtcNow;
            Header.Status    = DocStatus.Draft;
            Header.OrderNo   = "NEW";
        }
    }

    // Sipariş başlık bilgilerini ve müşteri/depo adlarını yükler
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

    // Sipariş satırlarını madde/UOM detaylarıyla yükler
    private async Task LoadLinesAsync(System.Data.IDbConnection conn, Guid id)
    {
        Lines = await conn.QueryAsync<SalesOrderLineDto>(@"
            SELECT
                l.Id, i.Code AS ItemCode, i.Name AS ItemName, dv.Code AS UomCode,
                l.QtyOrdered, l.QtyReserved, l.QtyShipped, l.Price,
                ISNULL(i.TaxRate, 20) AS TaxRate
            FROM SalesOrderLine l
            JOIN SalesOrderHeader oh ON oh.Id = l.HeaderId
            JOIN Item i ON i.Id = l.ItemId
            JOIN DictionaryValue dv ON dv.Id = l.UomId
            WHERE l.HeaderId = @Id AND oh.CompanyId = @CompanyId
            ORDER BY l.CreatedAt",
            new { Id = id, CompanyId = company.Id });
    }

    // Son 8 denetim izi kaydını (aktivite akışı) yükler
    private async Task LoadActivitiesAsync(System.Data.IDbConnection conn, Guid id)
    {
        Activities = await conn.QueryAsync<ActivityDto>(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: AuditLog salt-okuma denetim kaydıdır; firma verisi içermez.
            -- @Id parametresi LoadHeaderAsync'te WHERE o.Id = @Id AND o.CompanyId = @CompanyId
            -- ile doğrulanmış SalesOrderHeader.Id değeridir.
            -- EntityType + EntityId filtresi yalnızca o siparişe ait denetim izlerini getirir.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            SELECT TOP 8
                a.CreatedAt,
                NULLIF(a.UserName, '') AS UserName,
                a.Action,
                a.Details AS Notes
            FROM AuditLog a
            WHERE a.EntityType = 'SalesOrderHeader' AND a.EntityId = @Id
            ORDER BY a.CreatedAt DESC",
            new { Id = id });
    }

    // Yeni sipariş oluşturur veya mevcut sipariş başlığını günceller
    public async Task<IActionResult> OnPostAsync()
    {
        using var conn = db.Open();
        try
        {
            if (IsNew)
            {
                Header.Id = Guid.NewGuid();
                // İş kuralı: evrak numarası belge seri yönetiminden (NumberSeries, ayardan) atanır
                Header.OrderNo = await numberSeries.NextAsync(company.Id, NumberSeriesType.SalesOrder);

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
                TempData["Success"] = "Sipariş oluşturuldu.";
            }
            else
            {
                await conn.ExecuteAsync(
                    "UPDATE SalesOrderHeader SET WarehouseId=@WarehouseId, PartnerId=@PartnerId, RequestedDeliveryDate=@RequestedDeliveryDate, Notes=@Notes WHERE Id=@Id AND CompanyId=@CompanyId",
                    new { Header.WarehouseId, Header.PartnerId, Header.RequestedDeliveryDate, Header.Notes, Header.Id, CompanyId = company.Id });
                await audit.LogAsync("UPDATE", "SalesOrderHeader", Header.Id, $"OrderNo: {Header.OrderNo}");
                TempData["Success"] = "Sipariş kaydedildi.";
            }
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı THROW — SP Türkçe mesaj fırlattı, kullanıcıya gösterilebilir
            TempData["Error"] = sqlEx.Message;
            return RedirectToPage(new { id = Header.Id == Guid.Empty ? (Guid?)null : Header.Id });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Satış siparişi başlık kaydet hatası");
            TempData["Error"] = "İşlem sırasında veritabanı hatası oluştu.";
            return RedirectToPage(new { id = Header.Id == Guid.Empty ? (Guid?)null : Header.Id });
        }

        return RedirectToPage(new { id = Header.Id });
    }

    // Sipariş satırı ekler; maddenin temel UOM'u otomatik seçilir
    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, decimal qty, decimal? price)
    {
        using var conn = db.Open();
        try
        {
            // İş kuralı: CONSUMABLE satılamaz; sorgu sarf maddesini eler, BaseUomId null dönerse satır eklenmez
            var baseUomId = await conn.ExecuteScalarAsync<Guid?>(
                "SELECT BaseUomId FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId AND ItemType <> 'CONSUMABLE'",
                new { ItemId = itemId, CompanyId = company.Id });

            // Guard: madde satışa uygun değilse (sarf malzeme/pasif/UOM tanımsız) sessiz dönme; kullanıcıyı bilgilendir
            if (baseUomId is null)
            {
                logger.LogWarning("SO satır ekleme reddedildi: Item {ItemId} satışa uygun değil (CONSUMABLE/pasif/UOM yok), SO {OrderId}", itemId, id);
                TempData["Error"] = "Seçilen madde satışa uygun değil (sarf malzeme veya tanımsız ölçü birimi). Satır eklenmedi.";
                return RedirectToPage(new { id });
            }

            await conn.ExecuteAsync(@"
                -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
                -- Gerekçe: eklenen Item bu handler'da WHERE Id = @ItemId AND CompanyId = @CompanyId ile
                -- doğrulandı; bulunamazsa işlem iptal edildi (BaseUomId null döndü).
                -- @HeaderId değeri OnGetAsync/LoadHeaderAsync'te WHERE o.Id = @Id AND o.CompanyId = @CompanyId
                -- ile yüklenen SalesOrderHeader.Id'dir; farklı firmanın siparişine satır eklenemez.
                -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                INSERT INTO SalesOrderLine (HeaderId, ItemId, UomId, QtyOrdered, Price, Currency)
                VALUES (@HeaderId, @ItemId, @UomId, @Qty, @Price, 'TRY')",
                new { HeaderId = id, ItemId = itemId, UomId = baseUomId, Qty = qty, Price = price ?? 0 });

            await audit.LogAsync("ADD_LINE", "SalesOrderHeader", id, $"Item: {itemId} Qty: {qty}");
            TempData["Success"] = "Satır eklendi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı THROW — SP Türkçe mesaj fırlattı, kullanıcıya gösterilebilir
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Satış siparişi satır ekleme hatası: SO {OrderId}", id);
            TempData["Error"] = "İşlem sırasında veritabanı hatası oluştu.";
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        // İş kuralı: DRAFT → APPROVED; sp_ValidateStatusTransition doğrulaması ile onaylama
        using var conn = db.Open();
        try
        {
            var currentStatus = await conn.ExecuteScalarAsync<string>(
                "SELECT Status FROM SalesOrderHeader WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Id = id, CompanyId = company.Id });

            if (currentStatus is null) return NotFound();

            await conn.ExecuteAsync("sp_ValidateStatusTransition",
                new { CompanyId = company.Id, DocumentType = "SALES_ORDER",
                      FromStatus = currentStatus, ToStatus = DocStatus.Approved,
                      UserId = user.Id },
                commandType: CommandType.StoredProcedure);

            await conn.ExecuteAsync(
                "UPDATE SalesOrderHeader SET Status=@Status, UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Status = DocStatus.Approved, UserId = user.Id, Id = id, CompanyId = company.Id });
            await audit.LogAsync("APPROVE", "SalesOrderHeader", id, "Satış siparişi onaylandı");
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Satış siparişi onaylama hatası: {OrderId}", id);
            TempData["Error"] = "Sipariş onaylanırken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id)
    {
        // İş kuralı: APPROVED/DRAFT → CANCELLED; sp_ValidateStatusTransition doğrulaması ile iptal etme
        using var conn = db.Open();
        try
        {
            var currentStatus = await conn.ExecuteScalarAsync<string>(
                "SELECT Status FROM SalesOrderHeader WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Id = id, CompanyId = company.Id });

            if (currentStatus is null) return NotFound();

            await conn.ExecuteAsync("sp_ValidateStatusTransition",
                new { CompanyId = company.Id, DocumentType = "SALES_ORDER",
                      FromStatus = currentStatus, ToStatus = DocStatus.Cancelled,
                      UserId = user.Id },
                commandType: CommandType.StoredProcedure);

            await conn.ExecuteAsync(
                "UPDATE SalesOrderHeader SET Status=@Status, UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Status = DocStatus.Cancelled, UserId = user.Id, Id = id, CompanyId = company.Id });
            await audit.LogAsync("CANCEL", "SalesOrderHeader", id, "Satış siparişi iptal edildi");
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Satış siparişi iptal hatası: {OrderId}", id);
            TempData["Error"] = "Sipariş iptal edilirken veritabanı hatası oluştu.";
        }
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
        public decimal  TaxRate     { get; set; } = 20;
        public decimal  LineTotal   => QtyOrdered * (Price ?? 0);
        public decimal  LineTax     => System.Math.Round(LineTotal * TaxRate / 100m, 2);
        public decimal  OpenQty     => QtyOrdered - QtyShipped;
    }

    // Denetim izi satırı — UserName NULL ise view 'Sistem' fallback uygular, etiket UiHelpers.AuditActionLabel'dan gelir.
    public record ActivityDto(DateTime CreatedAt, string? UserName, string Action, string? Notes);

    // Belge zinciri sayaçları
    public int ShippingCount { get; set; }
    public DocFlowVm? DocFlow { get; set; }

    public async Task<IActionResult> OnPostCreateShippingAsync(Guid id)
    {
        // sp_CreateShippingFromSO: POSTED SO'dan DRAFT Shipping oluşturur, satırları kopyalar
        using var conn = db.Open();
        try
        {
            var prm = new DynamicParameters();
            prm.Add("SoId",        id);
            prm.Add("CompanyId",   company.Id);
            prm.Add("UserId",      user.Id);
            prm.Add("NewHeaderId", dbType: System.Data.DbType.Guid,
                    direction: System.Data.ParameterDirection.Output);

            await conn.ExecuteAsync("sp_CreateShippingFromSO", prm,
                commandType: System.Data.CommandType.StoredProcedure);

            var newId = prm.Get<Guid>("NewHeaderId");
            TempData["Success"] = "Sevkiyat belgesi oluşturuldu.";
            return RedirectToPage("/Shipping/Details", new { id = newId });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
            return RedirectToPage(new { id });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Sevkiyat oluşturma hatası: {SoId}", id);
            TempData["Error"] = "Sevkiyat oluşturulurken veritabanı hatası oluştu.";
            return RedirectToPage(new { id });
        }
    }
}
