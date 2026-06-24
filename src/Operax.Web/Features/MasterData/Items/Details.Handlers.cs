using Microsoft.AspNetCore.Mvc;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.MasterData.Items;

// Ürün detayı UOM dönüşümü + barkod alt-handler'ları (Details.cshtml.cs'ten ayrıldı — dosya boyutu).
// partial class: primary constructor parametreleri (db/company) erişilebilir.
public partial class DetailsModel
{
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
}
