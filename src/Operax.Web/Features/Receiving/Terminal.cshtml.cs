using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Receiving;

[Authorize]
public class TerminalModel(Db db, ICurrentCompany company) : PageModel
{
    public ReceivingTerminalDto? ActiveDoc { get; set; }
    public IEnumerable<PendingDocDto> PendingDocs { get; set; } = [];

    public async Task OnGetAsync(Guid? docId)
    {
        // El terminali: mal kabul belgesi seçilip barkod okutularak satır girilir
        using var conn = db.Open();

        // Bekleyen (DRAFT) mal kabul belgeleri
        PendingDocs = await conn.QueryAsync<PendingDocDto>(@"
            SELECT h.Id, h.DocNo, p.Name AS PartnerName,
                   (SELECT COUNT(*) FROM ReceivingLine WHERE HeaderId = h.Id) AS LineCount
            FROM ReceivingHeader h
            LEFT JOIN Partner p ON p.Id = h.PartnerId
            WHERE h.CompanyId = @CompanyId AND h.Status = 'DRAFT'
            ORDER BY h.CreatedAt DESC",
            new { CompanyId = company.Id });

        if (!docId.HasValue) return;

        // Seçili belge ve satırları
        ActiveDoc = await conn.QueryFirstOrDefaultAsync<ReceivingTerminalDto>(@"
            SELECT h.Id, h.DocNo, p.Name AS PartnerName, h.Status
            FROM ReceivingHeader h
            LEFT JOIN Partner p ON p.Id = h.PartnerId
            WHERE h.Id = @DocId AND h.CompanyId = @CompanyId",
            new { DocId = docId, CompanyId = company.Id });

        if (ActiveDoc == null) return;

        ActiveDoc.Lines = (await conn.QueryAsync<ReceivingLineTermDto>(@"
            SELECT l.Id, i.Code AS ItemCode, i.Name AS ItemName,
                   l.QtyExpected, l.QtyReceived, l.LotNo
            FROM ReceivingLine l
            JOIN Item i ON i.Id = l.ItemId
            JOIN ReceivingHeader h ON h.Id = l.HeaderId
            WHERE l.HeaderId = @DocId AND h.CompanyId = @CompanyId
            ORDER BY i.Code",
            new { DocId = docId, CompanyId = company.Id })).ToList();
    }

    public async Task<IActionResult> OnPostScanAsync(Guid docId, string barcode, string? lotNo, decimal qty)
    {
        // Barkod okunan ürünü belgede arar ve miktarı günceller
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            // Barkoddan ItemId bul (şirkete ait ürün)
            var itemId = await conn.ExecuteScalarAsync<Guid?>(@"
                SELECT i.Id FROM ItemBarcode b
                JOIN Item i ON i.Id = b.ItemId
                WHERE b.Barcode = @Barcode AND i.CompanyId = @CompanyId",
                new { Barcode = barcode, CompanyId = company.Id }, trans);

            if (itemId == null) { TempData["Error"] = $"Barkod bulunamadı: {barcode}"; return RedirectToPage(new { docId }); }

            // İş kuralı: belgede bu ürün varsa miktarı artır, yoksa yeni satır ekle
            var lineId = await conn.ExecuteScalarAsync<Guid?>(@"
                SELECT l.Id FROM ReceivingLine l
                JOIN ReceivingHeader h ON h.Id = l.HeaderId
                WHERE l.HeaderId = @DocId AND l.ItemId = @ItemId AND h.CompanyId = @CompanyId",
                new { DocId = docId, ItemId = itemId }, trans);

            if (lineId.HasValue)
            {
                await conn.ExecuteAsync(
                    "UPDATE ReceivingLine SET QtyReceived = QtyReceived + @Qty WHERE Id = @LineId",
                    new { Qty = qty, LineId = lineId }, trans);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO ReceivingLine (Id, HeaderId, ItemId, LotNo, QtyExpected, QtyReceived, UomId)
                    SELECT NEWID(), @DocId, @ItemId, @LotNo, @Qty, @Qty, i.BaseUomId
                    FROM Item i WHERE i.Id = @ItemId",
                    new { DocId = docId, ItemId = itemId, LotNo = lotNo, Qty = qty }, trans);
            }

            trans.Commit();
            TempData["Success"] = $"Eklendi: {barcode} × {qty}";
        }
        catch { trans.Rollback(); TempData["Error"] = "Hata oluştu."; }
        return RedirectToPage(new { docId });
    }

    public record ReceivingTerminalDto
    {
        public Guid Id { get; set; }
        public string DocNo { get; set; } = "";
        public string? PartnerName { get; set; }
        public string Status { get; set; } = "";
        public List<ReceivingLineTermDto> Lines { get; set; } = [];
    }

    public record ReceivingLineTermDto
    {
        public Guid Id { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public decimal QtyExpected { get; set; }
        public decimal QtyReceived { get; set; }
        public string? LotNo { get; set; }
        public bool IsComplete => QtyReceived >= QtyExpected && QtyExpected > 0;
    }

    public record PendingDocDto
    {
        public Guid Id { get; set; }
        public string DocNo { get; set; } = "";
        public string? PartnerName { get; set; }
        public int LineCount { get; set; }
    }
}
