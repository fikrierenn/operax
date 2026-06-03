using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.MasterData.Items;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, ParameterStore parameters, SupplierItemService supplierItems, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public ItemDto Item { get; set; } = new();

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

    public async Task OnGetAsync(Guid? id)
    {
        // Ürün formunu yükler: UOM listesi, kategori, vergi oranları ve ürün detayları
        using var conn = db.Open();

        var p = new { CompanyId = company.Id };

        Uoms = await conn.QueryAsync<DdlDto>(
            "SELECT dv.Id, dv.Code, dv.NameTr as Name FROM DictionaryValue dv JOIN DictionaryType dt ON dt.Id = dv.TypeId WHERE dt.Code = 'UOM' AND dt.CompanyId = @CompanyId AND dv.IsActive = 1 AND dv.IsDeleted = 0",
            p);

        // Kategori — şirkete ait + aktif
        Categories = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Category WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0",
            p);

        TaxRates = await conn.QueryAsync<DdlDto>(
            "SELECT dv.Id, dv.Code, dv.NameTr as Name FROM DictionaryValue dv JOIN DictionaryType dt ON dt.Id = dv.TypeId WHERE dt.Code = 'TAX_RATE' AND dt.CompanyId = @CompanyId AND dv.IsActive = 1 AND dv.IsDeleted = 0 ORDER BY dv.OrderNo",
            p);

        // Yeni ürün varsayılan KDV oranı parametreden (Plan 29) — Admin > Parametreler ile değişir
        if (!id.HasValue)
            Item.TaxRate = await parameters.GetDecimalAsync("DEFAULT_SALES_TAX_RATE", 20);

        if (id.HasValue)
        {
            // CompanyId filtresi — başka şirket ürünü görüntülenemez
            Item = await conn.QueryFirstOrDefaultAsync<ItemDto>(@"
                SELECT i.Id, i.Code, i.Name, i.Description, i.BaseUomId, i.CategoryId,
                       i.TaxRate, i.ItemType, i.IsLotTracked, i.IsSerialTracked, i.IsActive,
                       dv.Code as BaseUomCode, c.Name as CategoryName
                FROM Item i
                JOIN DictionaryValue dv ON dv.Id = i.BaseUomId
                LEFT JOIN Category c ON c.Id = i.CategoryId
                WHERE i.Id = @Id AND i.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }) ?? new();

            // UDF JSON Deserialization
            if (!string.IsNullOrEmpty(Item.Description) && Item.Description.TrimStart().StartsWith("{"))
            {
                try
                {
                    var udf = System.Text.Json.JsonSerializer.Deserialize<UdfDataDto>(Item.Description);
                    if (udf != null)
                    {
                        Item.ActualDescription = udf.ActualDescription;
                        Item.Volume = udf.Volume;
                        Item.Weight = udf.Weight;
                        Item.TempRange = udf.TempRange;
                        Item.MinQty = udf.MinQty;
                        Item.MaxQty = udf.MaxQty;
                    }
                }
                catch (Exception ex)
                {
                    // UDF JSON ayrıştırılamadı → açıklama ham metin olarak gösterilir (bozulmaz), uyarı loglanır
                    logger.LogWarning(ex, "Ürün UDF JSON ayrıştırma hatası: {ItemId}", id);
                    Item.ActualDescription = Item.Description;
                }
            }
            else
            {
                Item.ActualDescription = Item.Description;
            }

            // Odoo Smart Buttons için stok bilgilerini getir
            QtyOnHand = await conn.QueryFirstOrDefaultAsync<decimal>(
                "SELECT ISNULL(SUM(QtyBalance), 0) FROM tvf_InventoryBalance(@CompanyId) WHERE ItemId = @ItemId",
                new { ItemId = id, CompanyId = company.Id });

            MovementCount = await conn.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM StockMovement WHERE ItemId = @ItemId AND CompanyId = @CompanyId",
                new { ItemId = id, CompanyId = company.Id });

            UomConversions = await conn.QueryAsync<UomConversionDto>(@"
                SELECT u.Id, dv.Code as UomCode, dv.NameTr as UomName, u.ConversionRate
                FROM ItemUOM u
                JOIN DictionaryValue dv ON dv.Id = u.UomId
                JOIN Item i ON i.Id = u.ItemId
                WHERE u.ItemId = @Id AND i.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id });

            Barcodes = await conn.QueryAsync<BarcodeDto>(@"
                SELECT b.Id, b.Barcode, dv.Code as UomCode
                FROM ItemBarcode b
                JOIN DictionaryValue dv ON dv.Id = b.UomId
                JOIN Item i ON i.Id = b.ItemId
                WHERE b.ItemId = @Id AND i.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id });

            // Tedarikçiler tab (Plan 32): mevcut tedarikçi-ürün eşlemeleri + tedarikçi (cari) lookup
            var suppliers = await supplierItems.ListByItemAsync(company.Id, id.Value);
            SupplierLinesJson = System.Text.Json.JsonSerializer.Serialize(suppliers.Select(s => new
            {
                partnerId = s.PartnerId, supplierItemCode = s.SupplierItemCode, supplierItemName = s.SupplierItemName,
                leadTimeDays = s.LeadTimeDays, minOrderQty = s.MinOrderQty, lastPrice = s.LastPrice,
                currency = s.Currency, isPreferred = s.IsPreferred
            }));
            var vendors = await conn.QueryAsync<DdlDto>(
                "SELECT Id, Code, Name FROM Partner WHERE CompanyId=@CompanyId AND IsDeleted=0 AND Type IN ('VENDOR','BOTH') ORDER BY Name",
                new { CompanyId = company.Id });
            VendorsJson = System.Text.Json.JsonSerializer.Serialize(vendors.Select(v => new { id = v.Id, code = v.Code, name = v.Name }));
        }
        else
        {
            Item.IsActive = true;
            Item.TempRange = "Normal";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Ürünü kaydeder veya günceller
        using var conn = db.Open();

        var wasNew = IsNew; // redirect öncesinde durumu yakala (IsNew → Item.Id set sonrası değişir)

        // UDF JSON Serialization
        var udfData = new UdfDataDto
        {
            ActualDescription = Item.ActualDescription,
            Volume = Item.Volume,
            Weight = Item.Weight,
            TempRange = Item.TempRange ?? "Normal",
            MinQty = Item.MinQty,
            MaxQty = Item.MaxQty
        };
        Item.Description = System.Text.Json.JsonSerializer.Serialize(udfData);

        if (IsNew)
        {
            Item.Id = Guid.NewGuid();
            await conn.ExecuteAsync(@"
                INSERT INTO Item
                    (Id, CompanyId, Code, Name, Description, BaseUomId, CategoryId,
                     TaxRate, ItemType, IsLotTracked, IsSerialTracked, IsActive, CreatedBy)
                VALUES
                    (@Id, @CompanyId, @Code, @Name, @Description, @BaseUomId, @CategoryId,
                     @TaxRate, @ItemType, @IsLotTracked, @IsSerialTracked, @IsActive, @UserId)",
                new {
                    Item.Id, CompanyId = company.Id, Item.Code, Item.Name, Item.Description,
                    Item.BaseUomId, Item.CategoryId, Item.TaxRate, Item.ItemType,
                    Item.IsLotTracked, Item.IsSerialTracked, Item.IsActive, UserId = user.Id
                });
            await audit.LogAsync("CREATE", "Item", Item.Id, $"Kod: {Item.Code}, Ad: {Item.Name}");
        }
        else
        {
            await conn.ExecuteAsync(@"
                UPDATE Item
                SET Code=@Code, Name=@Name, Description=@Description,
                    BaseUomId=@BaseUomId, CategoryId=@CategoryId, TaxRate=@TaxRate,
                    ItemType=@ItemType, IsLotTracked=@IsLotTracked, IsSerialTracked=@IsSerialTracked,
                    IsActive=@IsActive, UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId
                WHERE Id=@Id AND CompanyId=@CompanyId",
                new {
                    Item.Code, Item.Name, Item.Description, Item.BaseUomId,
                    Item.CategoryId, Item.TaxRate, Item.ItemType, Item.IsLotTracked,
                    Item.IsSerialTracked, Item.IsActive, UserId = user.Id,
                    Item.Id, CompanyId = company.Id
                });
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

    public async Task<IActionResult> OnPostAddUomAsync(Guid id, Guid uomId, decimal rate)
    {
        // UOM dönüşüm oranı ekler — IDOR: ürün bu firmaya ait olmalı
        using var conn = db.Open();
        var owned = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Item WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id });
        if (owned == 0) return RedirectToPage(new { id, tab = "uom" });
        await conn.ExecuteAsync(
            "INSERT INTO ItemUOM (ItemId, UomId, ConversionRate) VALUES (@ItemId, @UomId, @Rate)",
            new { ItemId = id, UomId = uomId, Rate = rate });
        return RedirectToPage(new { id, tab = "uom" });
    }

    public async Task<IActionResult> OnPostDeleteUomAsync(Guid id, Guid uomConversionId)
    {
        // UOM dönüşüm satırını siler — IDOR: yalnızca bu firmanın ürününe ait satır
        using var conn = db.Open();
        await conn.ExecuteAsync(
            "DELETE FROM ItemUOM WHERE Id = @Id AND ItemId IN (SELECT Id FROM Item WHERE CompanyId = @CompanyId)",
            new { Id = uomConversionId, CompanyId = company.Id });
        return RedirectToPage(new { id, tab = "uom" });
    }

    public async Task<IActionResult> OnPostAddBarcodeAsync(Guid id, Guid uomId, string barcode)
    {
        // Barkod ekler — IDOR: ürün bu firmaya ait olmalı
        using var conn = db.Open();
        var owned = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Item WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id });
        if (owned == 0) return RedirectToPage(new { id, tab = "barcodes" });
        await conn.ExecuteAsync(
            "INSERT INTO ItemBarcode (ItemId, UomId, Barcode) VALUES (@ItemId, @UomId, @Barcode)",
            new { ItemId = id, UomId = uomId, Barcode = barcode });
        return RedirectToPage(new { id, tab = "barcodes" });
    }

    public async Task<IActionResult> OnPostDeleteBarcodeAsync(Guid id, Guid barcodeId)
    {
        // Barkod siler — IDOR: yalnızca bu firmanın ürününe ait barkod
        using var conn = db.Open();
        await conn.ExecuteAsync(
            "DELETE FROM ItemBarcode WHERE Id = @Id AND ItemId IN (SELECT Id FROM Item WHERE CompanyId = @CompanyId)",
            new { Id = barcodeId, CompanyId = company.Id });
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
        public string  ItemType         { get; set; } = "STOCK";  // Kalem türü: STOCK (stok), CONSUMABLE (sarf), SERVICE (hizmet), FIXED_ASSET (sabit kıymet)
        public bool    IsLotTracked     { get; set; }
        public bool    IsSerialTracked  { get; set; }
        public bool    IsActive         { get; set; }

        // JSON UDF Helper Fields
        public string? ActualDescription { get; set; }
        public decimal? Volume           { get; set; }
        public decimal? Weight           { get; set; }
        public string? TempRange         { get; set; } = "Normal";
        public decimal? MinQty           { get; set; }
        public decimal? MaxQty           { get; set; }
    }

    public class UdfDataDto
    {
        public string? ActualDescription { get; set; }
        public decimal? Volume           { get; set; }
        public decimal? Weight           { get; set; }
        public string? TempRange         { get; set; } = "Normal";
        public decimal? MinQty           { get; set; }
        public decimal? MaxQty           { get; set; }
    }

    public record UomConversionDto(Guid Id, string UomCode, string UomName, decimal ConversionRate);
    public record BarcodeDto(Guid Id, string Barcode, string UomCode);
}
