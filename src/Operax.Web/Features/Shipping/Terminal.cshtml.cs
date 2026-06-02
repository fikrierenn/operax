using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Shipping;

[Authorize]
public class TerminalModel(Db db, ICurrentCompany company, ILogger<TerminalModel> logger) : PageModel
{
    public ShipmentTermDto? ActiveDoc { get; set; }
    public IEnumerable<PendingShipDto> PendingDocs { get; set; } = [];

    public async Task OnGetAsync(Guid? docId)
    {
        // El terminali: sevkiyat belgesi seçilip barkod okutularak paketleme onaylanır
        using var conn = db.Open();

        PendingDocs = await conn.QueryAsync<PendingShipDto>(@"
            SELECT h.Id, h.DocNo,
                   (SELECT COUNT(*) FROM ShippingLine WHERE HeaderId = h.Id) AS LineCount
            FROM ShippingHeader h
            WHERE h.CompanyId = @CompanyId AND h.Status = @StDraft
            ORDER BY h.CreatedAt DESC",
            new { CompanyId = company.Id, StDraft = DocStatus.Draft });

        if (!docId.HasValue) return;

        ActiveDoc = await conn.QueryFirstOrDefaultAsync<ShipmentTermDto>(@"
            SELECT h.Id, h.DocNo, h.Status
            FROM ShippingHeader h
            WHERE h.Id = @DocId AND h.CompanyId = @CompanyId",
            new { DocId = docId, CompanyId = company.Id });

        if (ActiveDoc == null) return;

        ActiveDoc.Lines = (await conn.QueryAsync<ShipLineTermDto>(@"
            SELECT l.Id, i.Code AS ItemCode, i.Name AS ItemName,
                   dv.Code AS UomCode, l.QtyOriginal AS QtyToShip
            FROM ShippingLine l
            JOIN ShippingHeader h ON h.Id = l.HeaderId
            JOIN Item i ON i.Id = l.ItemId
            JOIN DictionaryValue dv ON dv.Id = l.UomId
            WHERE l.HeaderId = @DocId AND h.CompanyId = @CompanyId
            ORDER BY i.Code",
            new { DocId = docId, CompanyId = company.Id })).ToList();
    }

    public async Task<IActionResult> OnPostScanAsync(Guid docId, string barcode, decimal qty)
    {
        // Barkod okunan ürünü sevkiyat satırında doğrular
        using var conn = db.Open();

        try
        {
            var itemId = await conn.ExecuteScalarAsync<Guid?>(@"
                SELECT i.Id FROM ItemBarcode b
                JOIN Item i ON i.Id = b.ItemId
                WHERE b.Barcode = @Barcode AND i.CompanyId = @CompanyId
                UNION
                SELECT i.Id FROM Item i WHERE i.Code = @Barcode AND i.CompanyId = @CompanyId",
                new { Barcode = barcode, CompanyId = company.Id });

            if (itemId == null) { TempData["Error"] = $"Barkod bulunamadı: {barcode}"; return RedirectToPage(new { docId }); }

            var lineExists = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1) FROM ShippingLine l
                JOIN ShippingHeader h ON h.Id = l.HeaderId
                WHERE l.HeaderId = @DocId AND l.ItemId = @ItemId AND h.CompanyId = @CompanyId",
                new { DocId = docId, ItemId = itemId });

            if (lineExists == 0) { TempData["Error"] = "Bu ürün sevkiyat belgesinde yok."; return RedirectToPage(new { docId }); }

            TempData["Success"] = $"Doğrulandı: {barcode} × {qty}";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Terminal barkod tarama hatası: DocId={DocId}, Barkod={Barcode}", docId, barcode);
            TempData["Error"] = "Tarama sırasında veritabanı hatası oluştu.";
        }

        return RedirectToPage(new { docId });
    }

    public record ShipmentTermDto
    {
        public Guid Id { get; set; }
        public string DocNo { get; set; } = "";
        public string Status { get; set; } = "";
        public List<ShipLineTermDto> Lines { get; set; } = [];
    }

    public record ShipLineTermDto
    {
        public Guid    Id        { get; set; }
        public string  ItemCode  { get; set; } = "";
        public string  ItemName  { get; set; } = "";
        public string? UomCode   { get; set; }
        public decimal QtyToShip { get; set; }
    }

    public record PendingShipDto
    {
        public Guid   Id        { get; set; }
        public string DocNo     { get; set; } = "";
        public int    LineCount { get; set; }
    }
}
