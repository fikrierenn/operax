using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.MasterData.Items;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, ParameterStore parameters, SupplierItemService supplierItems, UdfService udfSvc, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public ItemDto Item { get; set; } = new();

    // Dinamik kullanıcı tanımlı alanlar paneli (Plan 34)
    public CustomFieldsVm UdfPanel { get; set; } = new("Item", [], new());

    public IEnumerable<UomConversionDto> UomConversions { get; set; } = [];
    public IEnumerable<BarcodeDto>       Barcodes       { get; set; } = [];
    public IEnumerable<DdlDto>           Uoms           { get; set; } = [];
    public IEnumerable<DdlDto>           Categories     { get; set; } = [];
    public IEnumerable<DdlDto>           TaxRates       { get; set; } = [];

    public string SupplierLinesJson { get; set; } = "[]";   // Tedarikçiler tab grid
    public string VendorsJson       { get; set; } = "[]";   // tedarikçi (cari) lookup

    public decimal QtyOnHand { get; set; } = 0;
    public int MovementCount { get; set; } = 0;

    public bool IsNew => Item.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        // Ürün formunu yükler: UOM listesi, kategori, vergi oranları ve ürün detayları
        using var conn = db.Open();

        var p = new { CompanyId = company.Id };

        Uoms = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT dv.Id, dv.Code, dv.NameTr as Name FROM DictionaryValue dv JOIN DictionaryType dt ON dt.Id = dv.TypeId WHERE dt.Code = 'UOM' AND dt.CompanyId = @CompanyId AND dv.IsActive = 1 AND dv.IsDeleted = 0",
            p, cancellationToken: ct));

        // Kategori — şirkete ait + aktif
        Categories = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Category WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0",
            p, cancellationToken: ct));

        TaxRates = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT dv.Id, dv.Code, dv.NameTr as Name FROM DictionaryValue dv JOIN DictionaryType dt ON dt.Id = dv.TypeId WHERE dt.Code = 'TAX_RATE' AND dt.CompanyId = @CompanyId AND dv.IsActive = 1 AND dv.IsDeleted = 0 ORDER BY dv.OrderNo",
            p, cancellationToken: ct));

        // Yeni ürün varsayılan KDV oranı parametreden (Plan 29) — Admin > Parametreler ile değişir
        if (!id.HasValue)
            Item.TaxRate = await parameters.GetDecimalAsync("DEFAULT_SALES_TAX_RATE", 20);

        if (id.HasValue)
        {
            // CompanyId filtresi — başka şirket ürünü görüntülenemez
            Item = await conn.QueryFirstOrDefaultAsync<ItemDto>(new CommandDefinition(@"
                SELECT i.Id, i.Code, i.Name, i.Description, i.BaseUomId, i.CategoryId,
                       i.TaxRate, i.ItemType, i.IsLotTracked, i.IsSerialTracked, i.IsActive,
                       i.MinStockLevel, i.MaxStockLevel, i.AdditionalFields,
                       dv.Code as BaseUomCode, c.Name as CategoryName
                FROM Item i
                JOIN DictionaryValue dv ON dv.Id = i.BaseUomId
                LEFT JOIN Category c ON c.Id = i.CategoryId
                WHERE i.Id = @Id AND i.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct)) ?? new();

            // Odoo Smart Buttons için stok bilgilerini getir
            QtyOnHand = await conn.QueryFirstOrDefaultAsync<decimal>(new CommandDefinition(
                "SELECT ISNULL(SUM(QtyBalance), 0) FROM tvf_InventoryBalance(@CompanyId) WHERE ItemId = @ItemId",
                new { ItemId = id, CompanyId = company.Id }, cancellationToken: ct));

            MovementCount = await conn.QueryFirstOrDefaultAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM StockMovement WHERE ItemId = @ItemId AND CompanyId = @CompanyId",
                new { ItemId = id, CompanyId = company.Id }, cancellationToken: ct));

            UomConversions = await conn.QueryAsync<UomConversionDto>(new CommandDefinition(@"
                SELECT u.Id, dv.Code as UomCode, dv.NameTr as UomName, u.ConversionRate
                FROM ItemUOM u
                JOIN DictionaryValue dv ON dv.Id = u.UomId
                JOIN Item i ON i.Id = u.ItemId
                WHERE u.ItemId = @Id AND i.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct));

            Barcodes = await conn.QueryAsync<BarcodeDto>(new CommandDefinition(@"
                SELECT b.Id, b.Barcode, dv.Code as UomCode
                FROM ItemBarcode b
                JOIN DictionaryValue dv ON dv.Id = b.UomId
                JOIN Item i ON i.Id = b.ItemId
                WHERE b.ItemId = @Id AND i.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct));

            // Tedarikçiler tab (Plan 32) + dinamik UDF paneli (Plan 34) yüklenir
            await LoadSupplierTabAsync(conn, id.Value, ct);

            var udfDefs = await udfSvc.LoadDefinitionsAsync("Item");
            var udfOpts = await udfSvc.ResolveAllAsync(udfDefs);
            UdfPanel = new CustomFieldsVm("Item", udfDefs, udfSvc.ReadValues(Item.AdditionalFields), Options: udfOpts);
        }
        else
        {
            Item.IsActive = true;
        }
    }

    // Tedarikçiler tab verisini hazırlar: tedarikçi-ürün eşlemeleri + tedarikçi (cari) lookup JSON'ları
    private async Task LoadSupplierTabAsync(System.Data.IDbConnection conn, Guid id, CancellationToken ct)
    {
        var suppliers = await supplierItems.ListByItemAsync(company.Id, id);
        SupplierLinesJson = System.Text.Json.JsonSerializer.Serialize(suppliers.Select(s => new
        {
            partnerId = s.PartnerId, supplierItemCode = s.SupplierItemCode, supplierItemName = s.SupplierItemName,
            leadTimeDays = s.LeadTimeDays, minOrderQty = s.MinOrderQty, lastPrice = s.LastPrice,
            currency = s.Currency, isPreferred = s.IsPreferred
        }));
        var vendors = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Partner WHERE CompanyId=@CompanyId AND IsDeleted=0 AND Type IN ('VENDOR','BOTH') ORDER BY Name",
            new { CompanyId = company.Id }, cancellationToken: ct));
        VendorsJson = System.Text.Json.JsonSerializer.Serialize(vendors.Select(v => new { id = v.Id, code = v.Code, name = v.Name }));
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Ürünü kaydeder veya günceller
        using var conn = db.Open();

        var wasNew = IsNew; // redirect öncesinde durumu yakala (IsNew → Item.Id set sonrası değişir)

        // Dinamik UDF: form gönderimini tanımlara göre doğrula + güvenli JSON üret (Plan 34)
        var udfDefs = await udfSvc.LoadDefinitionsAsync("Item");
        var (udfJson, udfErrors) = await udfSvc.BuildValidatedJsonAsync(Request.Form, udfDefs);
        if (udfErrors.Count > 0)
        {
            // İş kuralı: zorunlu/geçersiz UDF varsa kayıt yapılmaz, kullanıcı uyarılır
            TempData["Error"] = string.Join(" ", udfErrors);
            // UDF panelini tekrar kur (form yeniden gösterilecek)
            UdfPanel = new CustomFieldsVm("Item", udfDefs, udfSvc.ReadValues(udfJson), Options: await udfSvc.ResolveAllAsync(udfDefs));
            return Page();
        }
        Item.AdditionalFields = udfJson;

        if (IsNew)
        {
            Item.Id = Guid.NewGuid();
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO Item
                    (Id, CompanyId, Code, Name, Description, BaseUomId, CategoryId,
                     TaxRate, ItemType, IsLotTracked, IsSerialTracked, IsActive,
                     MinStockLevel, MaxStockLevel, AdditionalFields, CreatedBy)
                VALUES
                    (@Id, @CompanyId, @Code, @Name, @Description, @BaseUomId, @CategoryId,
                     @TaxRate, @ItemType, @IsLotTracked, @IsSerialTracked, @IsActive,
                     @MinStockLevel, @MaxStockLevel, @AdditionalFields, @UserId)",
                new {
                    Item.Id, CompanyId = company.Id, Item.Code, Item.Name, Item.Description,
                    Item.BaseUomId, Item.CategoryId, Item.TaxRate, Item.ItemType,
                    Item.IsLotTracked, Item.IsSerialTracked, Item.IsActive,
                    Item.MinStockLevel, Item.MaxStockLevel, Item.AdditionalFields, UserId = user.Id
                }, cancellationToken: ct));
            await audit.LogAsync("CREATE", "Item", Item.Id, $"Kod: {Item.Code}, Ad: {Item.Name}");
        }
        else
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE Item
                SET Code=@Code, Name=@Name, Description=@Description,
                    BaseUomId=@BaseUomId, CategoryId=@CategoryId, TaxRate=@TaxRate,
                    ItemType=@ItemType, IsLotTracked=@IsLotTracked, IsSerialTracked=@IsSerialTracked,
                    IsActive=@IsActive, MinStockLevel=@MinStockLevel, MaxStockLevel=@MaxStockLevel,
                    AdditionalFields=@AdditionalFields, UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId
                WHERE Id=@Id AND CompanyId=@CompanyId",
                new {
                    Item.Code, Item.Name, Item.Description, Item.BaseUomId,
                    Item.CategoryId, Item.TaxRate, Item.ItemType, Item.IsLotTracked,
                    Item.IsSerialTracked, Item.IsActive, Item.MinStockLevel, Item.MaxStockLevel,
                    Item.AdditionalFields, UserId = user.Id,
                    Item.Id, CompanyId = company.Id
                }, cancellationToken: ct));
            await audit.LogAsync("UPDATE", "Item", Item.Id, $"Kod: {Item.Code}, Ad: {Item.Name}");
        }

        TempData["Success"] = wasNew ? "Ürün oluşturuldu." : "Ürün güncellendi.";
        return RedirectToPage(new { id = Item.Id });
    }

    // Tedarikçiler grid "Kaydet" (Plan 32): JSON satır dizisini bulk-upsert (SyncDelete=1).
    public async Task<IActionResult> OnPostSupplierBulkSaveAsync(Guid id, CancellationToken ct = default)
    {
        // İş kuralı: bozuk JSON sistem hatası gibi 500'e gitmemeli
        List<SupplierItemService.SupplierRow> rows;
        try
        {
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync(ct);
            rows = System.Text.Json.JsonSerializer.Deserialize<List<SupplierItemService.SupplierRow>>(
                json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (System.Text.Json.JsonException jsonEx)
        {
            logger.LogWarning(jsonEx, "Tedarikçi toplu kaydet: geçersiz JSON. Ürün {ItemId}", id);
            return new JsonResult(new { ok = false, error = "Gönderilen veri okunamadı. Sayfayı yenileyip tekrar deneyin." });
        }

        var lines = rows.Where(r => r.PartnerId != Guid.Empty);
        try
        {
            var n = await supplierItems.BulkSaveAsync(company.Id, id, user.Id, lines, ct);
            return new JsonResult(new { ok = true, written = n });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP Türkçe yazdı (geçersiz tedarikçi / çoklu tercih)
            return new JsonResult(new { ok = false, error = sqlEx.Message });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Tedarikçi toplu kaydet DB hatası. Ürün {ItemId}", id);
            return new JsonResult(new { ok = false, error = "Veritabanı hatası oluştu." });
        }
    }

    // Tercih edilen tedarikçiyi tekil ayarla (exclusive).
    public async Task<IActionResult> OnPostSetPreferredAsync(Guid id, Guid partnerId, CancellationToken ct = default)
    {
        try
        {
            await supplierItems.SetPreferredAsync(company.Id, id, partnerId, user.Id, ct);
            TempData["Success"] = "Tercih edilen tedarikçi ayarlandı.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Tercih tedarikçi ayarlama DB hatası. Ürün {ItemId}", id);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id, tab = "suppliers" });
    }

    // Ürünü pasife alır (IsActive=0) — silmez (soft-delete ayrı). IDOR: yalnızca bu firmanın ürünü.
    public async Task<IActionResult> OnPostDeactivateAsync(Guid id, CancellationToken ct)
    {
        try
        {
            using var conn = db.Open();
            // İş kuralı: yalnızca bu firmaya ait ve hâlâ aktif ürün pasife alınabilir
            var affected = await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE Item SET IsActive = 0, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId WHERE Id = @Id AND CompanyId = @CompanyId AND IsActive = 1",
                new { Id = id, CompanyId = company.Id, UserId = user.Id }, cancellationToken: ct));

            // Guard: etki yoksa (başka firma / zaten pasif / bulunamadı) işlem yapılmaz
            if (affected == 0)
            {
                TempData["Error"] = "Ürün pasife alınamadı (bulunamadı veya zaten pasif).";
                return RedirectToPage(new { id });
            }

            await audit.LogAsync("DEACTIVATE", "Item", id, "Ürün pasife alındı.");
            TempData["Success"] = "Ürün pasife alındı.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Sistem hatası — ham mesaj kullanıcıya gösterilmez, log'a detay
            logger.LogError(sqlEx, "Ürün pasife alma DB hatası. Ürün {ItemId}", id);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddUomAsync(Guid id, Guid uomId, decimal rate, CancellationToken ct)
    {
        // UOM dönüşüm oranı ekler — IDOR: ürün bu firmaya ait olmalı
        using var conn = db.Open();
        var owned = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Item WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct));
        if (owned == 0) return RedirectToPage(new { id, tab = "uom" });
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO ItemUOM (ItemId, UomId, ConversionRate) VALUES (@ItemId, @UomId, @Rate)",
            new { ItemId = id, UomId = uomId, Rate = rate }, cancellationToken: ct));
        return RedirectToPage(new { id, tab = "uom" });
    }

    public async Task<IActionResult> OnPostDeleteUomAsync(Guid id, Guid uomConversionId, CancellationToken ct)
    {
        // UOM dönüşüm satırını siler — IDOR: yalnızca bu firmanın ürününe ait satır
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM ItemUOM WHERE Id = @Id AND ItemId IN (SELECT Id FROM Item WHERE CompanyId = @CompanyId)",
            new { Id = uomConversionId, CompanyId = company.Id }, cancellationToken: ct));
        return RedirectToPage(new { id, tab = "uom" });
    }

    public async Task<IActionResult> OnPostAddBarcodeAsync(Guid id, Guid uomId, string barcode, CancellationToken ct)
    {
        // Barkod ekler — IDOR: ürün bu firmaya ait olmalı
        using var conn = db.Open();
        var owned = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Item WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct));
        if (owned == 0) return RedirectToPage(new { id, tab = "barcodes" });
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO ItemBarcode (ItemId, UomId, Barcode) VALUES (@ItemId, @UomId, @Barcode)",
            new { ItemId = id, UomId = uomId, Barcode = barcode }, cancellationToken: ct));
        return RedirectToPage(new { id, tab = "barcodes" });
    }

    public async Task<IActionResult> OnPostDeleteBarcodeAsync(Guid id, Guid barcodeId, CancellationToken ct)
    {
        // Barkod siler — IDOR: yalnızca bu firmanın ürününe ait barkod
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM ItemBarcode WHERE Id = @Id AND ItemId IN (SELECT Id FROM Item WHERE CompanyId = @CompanyId)",
            new { Id = barcodeId, CompanyId = company.Id }, cancellationToken: ct));
        return RedirectToPage(new { id, tab = "barcodes" });
    }

    public record ItemDto
    {
        public Guid    Id               { get; set; }
        public string  Code             { get; set; } = "";
        public string  Name             { get; set; } = "";
        public string? Description      { get; set; }
        public Guid    BaseUomId        { get; set; }
        public string? BaseUomCode      { get; set; }
        public Guid?   CategoryId       { get; set; }
        public string? CategoryName     { get; set; }
        public decimal TaxRate          { get; set; } = 20;
        public string  ItemType         { get; set; } = Operax.Web.Lib.ItemType.Stock;  // Ürün doğası: STOCK (fiziksel) | SERVICE (hizmet) | FIXED_ASSET (demirbaş) — Plan 52: CONSUMABLE kaldırıldı
        public bool    IsLotTracked     { get; set; }
        public bool    IsSerialTracked  { get; set; }
        public bool    IsActive         { get; set; }

        // Ürün-seviyesi emniyet stok limitleri (Plan 34 — eski Description-JSON MinQty/MaxQty'den terfi)
        public decimal? MinStockLevel    { get; set; }
        public decimal? MaxStockLevel    { get; set; }
        // Dinamik UDF JSON çantası — servisle doldurulur, kullanıcı bind etmez
        public string? AdditionalFields  { get; set; }
    }

    public record UomConversionDto(Guid Id, string UomCode, string UomName, decimal ConversionRate);
    public record BarcodeDto(Guid Id, string Barcode, string UomCode);
}
