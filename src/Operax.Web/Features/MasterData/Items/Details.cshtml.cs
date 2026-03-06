using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.MasterData.Items;

public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty]
    public ItemDto Item { get; set; } = new();
    
    public IEnumerable<UomConversionDto> UomConversions { get; set; } = [];
    public IEnumerable<BarcodeDto> Barcodes { get; set; } = [];
    public IEnumerable<DdlDto> Uoms { get; set; } = [];
    public IEnumerable<DdlDto> Categories { get; set; } = [];
    public IEnumerable<DdlDto> TaxRates { get; set; } = [];

    public bool IsNew => Item.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        using var conn = db.Open();
        
        Uoms = await conn.QueryAsync<DdlDto>("SELECT Id, Code, NameTr as Name FROM DictionaryValue WHERE TypeId = (SELECT Id FROM DictionaryType WHERE Code = 'UOM') AND IsActive = 1");
        Categories = await conn.QueryAsync<DdlDto>("SELECT Id, Code, Name FROM Category WHERE IsActive = 1 AND IsDeleted = 0");
        TaxRates = await conn.QueryAsync<DdlDto>("SELECT Id, Code, NameTr as Name FROM DictionaryValue WHERE TypeId = (SELECT Id FROM DictionaryType WHERE Code = 'TAX_RATE') AND IsActive = 1 ORDER BY OrderNo");

        if (id.HasValue)
        {
            Item = await conn.QueryFirstOrDefaultAsync<ItemDto>(@"
                SELECT i.Id, i.Code, i.Name, i.Description, i.BaseUomId, i.CategoryId, i.TaxRate, i.IsLotTracked, i.IsSerialTracked, i.IsActive, dv.Code as BaseUomCode, c.Name as CategoryName
                FROM Item i
                JOIN DictionaryValue dv ON dv.Id = i.BaseUomId
                LEFT JOIN Category c ON c.Id = i.CategoryId
                WHERE i.Id = @Id", new { Id = id }) ?? new();

            UomConversions = await conn.QueryAsync<UomConversionDto>(@"
                SELECT u.Id, dv.Code as UomCode, dv.NameTr as UomName, u.ConversionRate
                FROM ItemUOM u
                JOIN DictionaryValue dv ON dv.Id = u.UomId
                WHERE u.ItemId = @Id", new { Id = id });

            Barcodes = await conn.QueryAsync<BarcodeDto>(@"
                SELECT b.Id, b.Barcode, dv.Code as UomCode
                FROM ItemBarcode b
                JOIN DictionaryValue dv ON dv.Id = b.UomId
                WHERE b.ItemId = @Id", new { Id = id });
        }
        else
        {
            Item.IsActive = true;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        using var conn = db.Open();

        if (IsNew)
        {
            Item.Id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO Item (Id, CompanyId, Code, Name, Description, BaseUomId, CategoryId, TaxRate, IsLotTracked, IsSerialTracked, IsActive)
                VALUES (@Id, @CompanyId, @Code, @Name, @Description, @BaseUomId, @CategoryId, @TaxRate, @IsLotTracked, @IsSerialTracked, @IsActive)";
            
            await conn.ExecuteAsync(sql, new { 
                Item.Id, CompanyId = company.Id, Item.Code, Item.Name, Item.Description, 
                Item.BaseUomId, Item.CategoryId, Item.TaxRate, Item.IsLotTracked, Item.IsSerialTracked, Item.IsActive 
            });
        }
        else
        {
            const string sql = @"
                UPDATE Item SET Code = @Code, Name = @Name, Description = @Description, 
                                BaseUomId = @BaseUomId, CategoryId = @CategoryId, TaxRate = @TaxRate,
                                IsLotTracked = @IsLotTracked, IsSerialTracked = @IsSerialTracked, IsActive = @IsActive 
                WHERE Id = @Id";
            await conn.ExecuteAsync(sql, new { 
                Item.Code, Item.Name, Item.Description, Item.BaseUomId, Item.CategoryId, 
                Item.TaxRate, Item.IsLotTracked, Item.IsSerialTracked, Item.IsActive, Item.Id 
            });
        }

        return RedirectToPage(new { id = Item.Id });
    }

    public async Task<IActionResult> OnPostAddUomAsync(Guid id, Guid uomId, decimal rate)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("INSERT INTO ItemUOM (ItemId, UomId, ConversionRate) VALUES (@ItemId, @UomId, @Rate)", new { ItemId = id, UomId = uomId, Rate = rate });
        return RedirectToPage(new { id, tab = "uom" });
    }

    public async Task<IActionResult> OnPostDeleteUomAsync(Guid id, Guid uomConversionId)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("DELETE FROM ItemUOM WHERE Id = @Id", new { Id = uomConversionId });
        return RedirectToPage(new { id, tab = "uom" });
    }

    public async Task<IActionResult> OnPostAddBarcodeAsync(Guid id, Guid uomId, string barcode)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("INSERT INTO ItemBarcode (ItemId, UomId, Barcode) VALUES (@ItemId, @UomId, @Barcode)", new { ItemId = id, UomId = uomId, Barcode = barcode });
        return RedirectToPage(new { id, tab = "barcodes" });
    }

    public async Task<IActionResult> OnPostDeleteBarcodeAsync(Guid id, Guid barcodeId)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("DELETE FROM ItemBarcode WHERE Id = @Id", new { Id = barcodeId });
        return RedirectToPage(new { id, tab = "barcodes" });
    }

    public record ItemDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string? Description { get; set; } public Guid BaseUomId { get; set; } public string? BaseUomCode { get; set; } public Guid? CategoryId { get; set; } public string? CategoryName { get; set; } public decimal TaxRate { get; set; } = 20; public bool IsLotTracked { get; set; } public bool IsSerialTracked { get; set; } public bool IsActive { get; set; } }
    public record UomConversionDto(Guid Id, string UomCode, string UomName, decimal ConversionRate);
    public record BarcodeDto(Guid Id, string Barcode, string UomCode);
    public record DdlDto(Guid Id, string Code, string Name);
}
