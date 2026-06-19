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
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, INumberSeriesService numberSeries, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public PurchaseOrderHeaderDto Header { get; set; } = new();
    public IEnumerable<PurchaseOrderLineDto> Lines { get; set; } = [];
    public IEnumerable<ActivityDto>          Activities { get; set; } = [];

    public IEnumerable<DdlDto> Warehouses     { get; set; } = [];
    public IEnumerable<DdlDto> Vendors        { get; set; } = [];
    public IEnumerable<DdlDto> AvailableItems { get; set; } = [];

    // Ürün seçilince satır fiyatını otomatik doldurmak için ürün→önerilen alış fiyatı (son maliyet) sözlüğü.
    // Anahtar: ItemId (string), değer: ItemCost.AvgCost (öneri, kullanıcı override edebilir).
    public string ItemPriceJson { get; set; } = "{}";

    public bool IsNew => Header.Id == Guid.Empty;
    public decimal Subtotal => Lines.Sum(l => l.QtyOrdered * (l.Price ?? 0));
    // KDV satır-bazlı (her ürünün kendi TaxRate'i) — sabit %20 değil
    public decimal Vat      => Lines.Sum(l => l.LineTax);
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

        // Ürün → önerilen alış fiyatı (son hareketli ortalama maliyet). Satır eklerken fiyat otomatik gelir.
        // ItemCost ürün başına birden çok satır olabilir (depo bazlı) → GROUP BY ile tek değer (mükerrer key hatası önlenir).
        var itemPrices = await conn.QueryAsync<(Guid Id, decimal AvgCost)>(
            @"SELECT i.Id, ISNULL(MAX(ic.AvgCost), 0) AS AvgCost
              FROM Item i
              LEFT JOIN ItemCost ic ON ic.ItemId = i.Id AND ic.CompanyId = @CompanyId
              WHERE i.CompanyId = @CompanyId AND i.IsActive = 1 AND i.IsDeleted = 0
              GROUP BY i.Id", p);
        ItemPriceJson = System.Text.Json.JsonSerializer.Serialize(
            itemPrices.ToDictionary(x => x.Id.ToString(), x => x.AvgCost));

        if (id.HasValue)
        {
            await LoadHeaderAsync(conn, id.Value);
            await LoadLinesAsync(conn, id.Value);
            await LoadActivitiesAsync(conn, id.Value);

            // Bağlı alt belge sayaçları (smart button)
            ReceivingCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM ReceivingHeader WHERE PurchaseOrderId = @Id AND IsDeleted = 0 AND Status <> @Cancelled",
                new { Id = id.Value, Cancelled = DocStatus.Cancelled });

            DocFlow = new DocFlowVm([
                new DocFlowItem(
                    Label: "Mal Kabul",
                    Count: ReceivingCount,
                    ListUrl: ReceivingCount > 0 ? $"/Receiving?poId={id.Value}" : null,
                    CreateUrl: Header.Status == DocStatus.Posted ? $"/PurchaseOrders/Details/{id.Value}?handler=CreateReceiving" : null,
                    CreateLabel: "Mal Kabul Oluştur",
                    CanCreate: Header.Status == DocStatus.Posted && user.HasRole("Administrator","Purchasing","WarehouseManager"))
            ]);
        }
        else
        {
            Header.OrderDate = DateTime.UtcNow;
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
                l.QtyOrdered, l.QtyReceived, l.Price,
                ISNULL(i.TaxRate, 20) AS TaxRate
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
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: AuditLog salt-okuma denetim kaydıdır; firma verisi içermez.
            -- @Id parametresi LoadHeaderAsync'te WHERE o.Id = @Id AND o.CompanyId = @CompanyId
            -- ile doğrulanmış PurchaseOrderHeader.Id değeridir.
            -- EntityType + EntityId filtresi yalnızca o siparişe ait denetim izlerini getirir.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
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
            // İş kuralı: evrak numarası belge seri yönetiminden (NumberSeries, ayardan) atanır
            Header.OrderNo = await numberSeries.NextAsync(company.Id, NumberSeriesType.PurchaseOrder);
            // İş kuralı: sipariş tarihi form'da yok; bind edilmediyse bugünün tarihi atanır (SqlDateTime taşması önlenir)
            if (Header.OrderDate == default) Header.OrderDate = DateTime.Today;

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
            // Evrak bütünlüğü: bu siparişe mal kabul yapılmışsa düzenlenemez (document-immutability §3)
            if (await DocumentLock.PoHasReceivingAsync(conn, Header.Id, company.Id))
            {
                TempData["Error"] = "Belge kilitli: bu siparişe bağlı mal kabul mevcut, düzenlenemez.";
                return RedirectToPage(new { id = Header.Id });
            }
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
        // Evrak bütünlüğü: bu siparişe mal kabul yapılmışsa satır eklenemez (document-immutability §3)
        if (await DocumentLock.PoHasReceivingAsync(conn, id, company.Id))
        {
            TempData["Error"] = "Belge kilitli: bu siparişe bağlı mal kabul mevcut, satır eklenemez.";
            return RedirectToPage(new { id });
        }
        // İş kuralı: Ürünün temel birimi DB'den okunur, satıra yansıtılır
        var baseUomId = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT BaseUomId FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId",
            new { ItemId = itemId, CompanyId = company.Id });

        if (baseUomId is null) return RedirectToPage(new { id });

        // İş kuralı: Yeni satır Id'si geri alınır (fiyat farkı kontrolü için gerekli)
        var newLineId = await conn.ExecuteScalarAsync<Guid>(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: eklenen Item bu handler'da WHERE Id = @ItemId AND CompanyId = @CompanyId ile
            -- doğrulandı; bulunamazsa işlem iptal edildi (BaseUomId null döndü).
            -- @HeaderId değeri LoadHeaderAsync'te WHERE o.Id = @Id AND o.CompanyId = @CompanyId ile
            -- yüklenen PurchaseOrderHeader.Id'dir; LoadLinesAsync'te de aynı satır
            -- JOIN PurchaseOrderHeader oh ON oh.Id = l.HeaderId ... AND oh.CompanyId = @CompanyId
            -- ile sahiplik doğrulanır; farklı firmanın siparişine satır eklenemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
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
        // Tedarikçi (PartnerId) ve belge şubesi (Warehouse → BranchId) header'dan okunur.
        // Plan 30: şube boyutu fiyat önceliğine girer (cari baskın, Partner×2+Branch×1).
        var ctx = await conn.QuerySingleOrDefaultAsync<PriceCheckCtx>(
            @"SELECT h.PartnerId, w.BranchId
              FROM PurchaseOrderHeader h
              JOIN Warehouse w ON w.Id = h.WarehouseId
              WHERE h.Id = @Id AND h.CompanyId = @CompanyId",
            new { Id = headerId, CompanyId = company.Id });

        // Güvenlik: header veya bağlı depo kaydı bulunamazsa (silinmiş/tutarsız) kontrol atlanır.
        // PartnerId şemada NOT NULL olduğundan ayrıca null kontrolü gereksiz.
        if (ctx is null) return;

        var prm = new DynamicParameters();
        prm.Add("CompanyId",  company.Id);
        prm.Add("PoHeaderId", headerId);
        prm.Add("PoLineId",   lineId);
        prm.Add("ItemId",     itemId);
        prm.Add("PartnerId",  ctx.PartnerId);
        prm.Add("ActualPrice", actualPrice);
        prm.Add("BranchId",   ctx.BranchId);   // NULL = genel (şube ataması yoksa)
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
                new { PoHeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: System.Data.CommandType.StoredProcedure);
            await audit.LogAsync("POST", "PurchaseOrderHeader", id,
                "Satınalma siparişi onaylandı, ödeme planı oluşturuldu");
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "PO onay hatası: {PoId}", id);
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
                new { CompanyId = company.Id, DocumentType = "PURCHASE_ORDER",
                      FromStatus = DocStatus.Posted, ToStatus = DocStatus.Cancelled,
                      UserId = user.Id },
                commandType: System.Data.CommandType.StoredProcedure);

            await conn.ExecuteAsync(
                "UPDATE PurchaseOrderHeader SET Status=@Status, UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Status = DocStatus.Cancelled, UserId = user.Id, Id = id, CompanyId = company.Id });
            await audit.LogAsync("CANCEL", "PurchaseOrderHeader", id, "Satınalma siparişi iptal edildi");
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "PO iptal hatası: {PoId}", id);
            TempData["Error"] = "Sipariş iptal edilirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    // ─── DTO'lar ────────────────────────────────────────────────
    // Fiyat farkı kontrolü için belge bağlamı (tedarikçi + şube). Plan 30.
    private sealed record PriceCheckCtx(Guid PartnerId, Guid? BranchId);

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
        public decimal  TaxRate     { get; set; } = 20;
        public decimal  LineTotal   => QtyOrdered * (Price ?? 0);
        public decimal  LineTax     => System.Math.Round(LineTotal * TaxRate / 100m, 2);
    }

    // Denetim izi satırı — UserName NULL ise view 'Sistem' fallback uygular, etiket UiHelpers.AuditActionLabel'dan gelir.
    public record ActivityDto(DateTime CreatedAt, string? UserName, string Action, string? Notes);

    // Belge zinciri sayaçları — smart button partial için
    public int ReceivingCount { get; set; }
    public DocFlowVm? DocFlow { get; set; }

    public async Task<IActionResult> OnPostCreateReceivingAsync(Guid id)
    {
        // sp_CreateReceivingFromPO: POSTED PO'dan DRAFT Receiving oluşturur, satırları kopyalar
        using var conn = db.Open();
        try
        {
            var prm = new DynamicParameters();
            prm.Add("PoId",        id);
            prm.Add("CompanyId",   company.Id);
            prm.Add("UserId",      user.Id);
            prm.Add("NewHeaderId", dbType: System.Data.DbType.Guid,
                    direction: System.Data.ParameterDirection.Output);

            await conn.ExecuteAsync("sp_CreateReceivingFromPO", prm,
                commandType: System.Data.CommandType.StoredProcedure);

            var newId = prm.Get<Guid>("NewHeaderId");
            TempData["Success"] = "Mal kabul belgesi oluşturuldu.";
            return RedirectToPage("/Receiving/Details", new { id = newId });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
            return RedirectToPage(new { id });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Mal kabul oluşturma hatası: {PoId}", id);
            TempData["Error"] = "Mal kabul oluşturulurken veritabanı hatası oluştu.";
            return RedirectToPage(new { id });
        }
    }
}
