using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Shipping;

[Authorize]
public class TerminalModel(Db db, ICurrentCompany company) : PageModel
{
    public ShipmentTermDto? ActiveDoc { get; set; }
    public IEnumerable<PendingShipDto> PendingDocs { get; set; } = [];

    public async Task OnGetAsync(Guid? docId)
    {
        // El terminali: sevkiyat belgesi seçilip barkod okutularak paketleme onaylanır
        using var conn = db.Open();

        PendingDocs = await conn.QueryAsync<PendingShipDto>(@"
            SELECT h.Id, h.DocNo, p.Name AS PartnerName,
                   (SELECT COUNT(*) FROM ShippingLine WHERE HeaderId = h.Id) AS LineCount
            FROM ShippingHeader h
            LEFT JOIN Partner p ON p.Id = h.PartnerId
            WHERE h.CompanyId = @CompanyId AND h.Status = 'DRAFT'
            ORDER BY h.CreatedAt DESC",
            new { CompanyId = company.Id });

        if (!docId.HasValue) return;

        ActiveDoc = await conn.QueryFirstOrDefaultAsync<ShipmentTermDto>(@"
            SELECT h.Id, h.DocNo, p.Name AS PartnerName, h.Status
            FROM ShippingHeader h
            LEFT JOIN Partner p ON p.Id = h.PartnerId
            WHERE h.Id = @DocId AND h.CompanyId = @CompanyId",
            new { DocId = docId, CompanyId = company.Id });

        if (ActiveDoc == null) return;

        ActiveDoc.Lines = (await conn.QueryAsync<ShipLineTermDto>(@"
            SELECT l.Id, i.Code AS ItemCode, i.Name AS ItemName,
                   l.QtyToShip, l.QtyShipped
            FROM ShippingLine l
            JOIN ShippingHeader h ON h.Id = l.HeaderId
            JOIN Item i ON i.Id = l.ItemId
            WHERE l.HeaderId = @DocId AND h.CompanyId = @CompanyId
            ORDER BY i.Code",
            new { DocId = docId, CompanyId = company.Id })).ToList();
    }

    public async Task<IActionResult> OnPostScanAsync(Guid docId, string barcode, decimal qty)
    {
        // Barkod okunan ürünü sevkiyat satırında doğrular ve sevk miktarını günceller
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            var itemId = await conn.ExecuteScalarAsync<Guid?>(@"
                SELECT i.Id FROM ItemBarcode b
                JOIN Item i ON i.Id = b.ItemId
                WHERE b.Barcode = @Barcode AND i.CompanyId = @CompanyId
                UNION
                SELECT i.Id FROM Item i WHERE i.Code = @Barcode AND i.CompanyId = @CompanyId",
                new { Barcode = barcode, CompanyId = company.Id }, trans);

            if (itemId == null) { TempData["Error"] = $"Barkod bulunamadı: {barcode}"; return RedirectToPage(new { docId }); }

            var lineId = await conn.ExecuteScalarAsync<Guid?>(@"
                SELECT l.Id FROM ShippingLine l
                JOIN ShippingHeader h ON h.Id = l.HeaderId
                WHERE l.HeaderId = @DocId AND l.ItemId = @ItemId AND h.CompanyId = @CompanyId",
                new { DocId = docId, ItemId = itemId }, trans);

            if (!lineId.HasValue) { TempData["Error"] = "Bu ürün sevkiyat belgesinde yok."; return RedirectToPage(new { docId }); }

            await conn.ExecuteAsync(
                "UPDATE ShippingLine SET QtyShipped = QtyShipped + @Qty WHERE Id = @LineId",
                new { Qty = qty, LineId = lineId }, trans);

            trans.Commit();
            TempData["Success"] = $"Paketlendi: {barcode} × {qty}";
        }
        catch { trans.Rollback(); TempData["Error"] = "Hata oluştu."; }
        return RedirectToPage(new { docId });
    }

    public record ShipmentTermDto
    {
        public Guid Id { get; set; }
        public string DocNo { get; set; } = "";
        public string? PartnerName { get; set; }
        public string Status { get; set; } = "";
        public List<ShipLineTermDto> Lines { get; set; } = [];
    }

    public record ShipLineTermDto
    {
        public Guid Id { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public decimal QtyToShip { get; set; }
        public decimal QtyShipped { get; set; }
        public bool IsComplete => QtyShipped >= QtyToShip;
    }

    public record PendingShipDto
    {
        public Guid Id { get; set; }
        public string DocNo { get; set; } = "";
        public string? PartnerName { get; set; }
        public int LineCount { get; set; }
    }
}
