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
/// POST handler'ları Details.Handlers.cs, DTO'lar Details.Dtos.cs partial dosyalarında (dosya boyutu).
/// </summary>
[Authorize]
public partial class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, INumberSeriesService numberSeries, ILogger<DetailsModel> logger) : PageModel
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

    // Belge zinciri sayaçları — smart button partial için
    public int ReceivingCount { get; set; }
    public DocFlowVm? DocFlow { get; set; }

    public bool IsNew => Header.Id == Guid.Empty;
    public decimal Subtotal => Lines.Sum(l => l.QtyOrdered * (l.Price ?? 0));
    // KDV satır-bazlı (her ürünün kendi TaxRate'i) — sabit %20 değil
    public decimal Vat      => Lines.Sum(l => l.LineTax);
    public decimal Grand    => Subtotal + Vat;

    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        using var conn = db.Open();

        // Form dropdown'ları (depo/tedarikçi/ürün + fiyat önerisi) — OnGet + OnPost hata reload ortak
        await LoadFormDropdownsAsync(conn, ct);

        if (id.HasValue)
        {
            await LoadHeaderAsync(conn, id.Value, ct);
            await LoadLinesAsync(conn, id.Value, ct);
            await LoadActivitiesAsync(conn, id.Value, ct);

            // Bağlı alt belge sayaçları (smart button)
            ReceivingCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM ReceivingHeader WHERE PurchaseOrderId = @Id AND IsDeleted = 0 AND Status <> @Cancelled",
                new { Id = id.Value, Cancelled = DocStatus.Cancelled }, cancellationToken: ct));

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

    // Form dropdown verilerini yükler (depo/tedarikçi/ürün + ürün→önerilen fiyat sözlüğü)
    private async Task LoadFormDropdownsAsync(System.Data.IDbConnection conn, CancellationToken ct)
    {
        var p = new { CompanyId = company.Id };

        Warehouses = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0", p, cancellationToken: ct));

        Vendors = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Partner WHERE CompanyId = @CompanyId AND Type IN ('VENDOR', 'BOTH') AND IsDeleted = 0", p, cancellationToken: ct));

        AvailableItems = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0", p, cancellationToken: ct));

        // Ürün → önerilen alış fiyatı (son hareketli ortalama maliyet). Satır eklerken fiyat otomatik gelir.
        // ItemCost ürün başına birden çok satır olabilir (depo bazlı) → GROUP BY ile tek değer (mükerrer key hatası önlenir).
        var itemPrices = await conn.QueryAsync<(Guid Id, decimal AvgCost)>(new CommandDefinition(
            @"SELECT i.Id, ISNULL(MAX(ic.AvgCost), 0) AS AvgCost
              FROM Item i
              LEFT JOIN ItemCost ic ON ic.ItemId = i.Id AND ic.CompanyId = @CompanyId
              WHERE i.CompanyId = @CompanyId AND i.IsActive = 1 AND i.IsDeleted = 0
              GROUP BY i.Id", p, cancellationToken: ct));
        ItemPriceJson = System.Text.Json.JsonSerializer.Serialize(
            itemPrices.ToDictionary(x => x.Id.ToString(), x => x.AvgCost));
    }

    // Header detayı + Partner ek alanları
    private async Task LoadHeaderAsync(System.Data.IDbConnection conn, Guid id, CancellationToken ct)
    {
        Header = await conn.QueryFirstOrDefaultAsync<PurchaseOrderHeaderDto>(new CommandDefinition(@"
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
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct)) ?? new();
    }

    // Satırlar + ürün kodu/adı + UOM kodu
    private async Task LoadLinesAsync(System.Data.IDbConnection conn, Guid id, CancellationToken ct)
    {
        Lines = await conn.QueryAsync<PurchaseOrderLineDto>(new CommandDefinition(@"
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
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct));
    }

    // Aktivite logu — bu evraka ait son 8 audit kaydı
    private async Task LoadActivitiesAsync(System.Data.IDbConnection conn, Guid id, CancellationToken ct)
    {
        Activities = await conn.QueryAsync<ActivityDto>(new CommandDefinition(@"
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
            new { Id = id }, cancellationToken: ct));
    }

    // Yeni veya mevcut PO'yu kaydeder; doğrulama başarısızsa formu yeniden gösterir
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        using var conn = db.Open();

        // Guard: form doğrulaması başarısızsa kaydetme — dropdown'ları yükleyip formu geri göster
        if (!ModelState.IsValid)
        {
            await LoadFormDropdownsAsync(conn, ct);
            if (!IsNew)
            {
                await LoadHeaderAsync(conn, Header.Id, ct);
                await LoadLinesAsync(conn, Header.Id, ct);
            }
            return Page();
        }

        if (IsNew)
        {
            Header.Id = Guid.NewGuid();
            // İş kuralı: evrak numarası belge seri yönetiminden (NumberSeries, ayardan) atanır
            Header.OrderNo = await numberSeries.NextAsync(company.Id, NumberSeriesType.PurchaseOrder);
            // İş kuralı: sipariş tarihi form'da yok; bind edilmediyse bugünün tarihi atanır (SqlDateTime taşması önlenir)
            if (Header.OrderDate == default) Header.OrderDate = DateTime.Today;

            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO PurchaseOrderHeader
                    (Id, CompanyId, WarehouseId, PartnerId, OrderNo, Status, OrderDate, Notes, CreatedBy)
                VALUES
                    (@Id, @CompanyId, @WarehouseId, @PartnerId, @OrderNo, @Status, @OrderDate, @Notes, @UserId)",
                new {
                    Header.Id, CompanyId = company.Id, Header.WarehouseId, Header.PartnerId,
                    Header.OrderNo, Status = DocStatus.Draft, Header.OrderDate, Header.Notes,
                    UserId = user.Id
                }, cancellationToken: ct));
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
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE PurchaseOrderHeader SET WarehouseId=@WarehouseId, PartnerId=@PartnerId, Notes=@Notes WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Header.WarehouseId, Header.PartnerId, Header.Notes, Header.Id, CompanyId = company.Id }, cancellationToken: ct));
            await audit.LogAsync("UPDATE", "PurchaseOrderHeader", Header.Id, $"OrderNo: {Header.OrderNo}");
        }

        return RedirectToPage(new { id = Header.Id });
    }
}
