using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Receiving;

[Authorize]
public class TerminalModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<TerminalModel> logger) : PageModel
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
            WHERE h.CompanyId = @CompanyId AND h.Status = @StDraft
            ORDER BY h.CreatedAt DESC",
            new { CompanyId = company.Id, StDraft = DocStatus.Draft });

        if (!docId.HasValue) return;

        // Seçili belge ve satırları
        ActiveDoc = await conn.QueryFirstOrDefaultAsync<ReceivingTerminalDto>(@"
            SELECT h.Id, h.DocNo, p.Name AS PartnerName, h.Status
            FROM ReceivingHeader h
            LEFT JOIN Partner p ON p.Id = h.PartnerId
            WHERE h.Id = @DocId AND h.CompanyId = @CompanyId",
            new { DocId = docId, CompanyId = company.Id });

        if (ActiveDoc == null) return;

        // İlerleme sayacı: beklenen miktar tek-sipariş modunda PO satırından (QtyOrdered) gelir;
        // toplu/serbest modda PO satırı olmadığından okutulan orijinal (QtyOriginal) beklenen sayılır.
        ActiveDoc.Lines = (await conn.QueryAsync<ReceivingLineTermDto>(@"
            SELECT l.Id, i.Code AS ItemCode, i.Name AS ItemName,
                   COALESCE(pol.QtyOrdered, l.QtyOriginal) AS QtyExpected,
                   l.QtyBase AS QtyReceived, l.ReturnQty AS ReturnPending, l.LotNo
            FROM ReceivingLine l
            JOIN Item i ON i.Id = l.ItemId
            JOIN ReceivingHeader h ON h.Id = l.HeaderId
            LEFT JOIN PurchaseOrderLine pol ON pol.Id = l.PurchaseOrderLineId
            WHERE l.HeaderId = @DocId AND h.CompanyId = @CompanyId
            ORDER BY i.Code",
            new { DocId = docId, CompanyId = company.Id })).ToList();
    }

    public async Task<IActionResult> OnPostScanAsync(Guid docId, string barcode, string? lotNo, decimal qty)
    {
        // Sipariş kontrollü tarama (Plan 28): mod + miktar doğrulaması SP'de; fazla → iade alanı.
        using var conn = db.Open();

        // Barkoddan ItemId bul (şirkete ait ürün)
        var itemId = await conn.ExecuteScalarAsync<Guid?>(@"
            SELECT i.Id FROM ItemBarcode b
            JOIN Item i ON i.Id = b.ItemId
            WHERE b.Barcode = @Barcode AND i.CompanyId = @CompanyId",
            new { Barcode = barcode, CompanyId = company.Id });
        if (itemId == null) { TempData["Error"] = $"Barkod bulunamadı: {barcode}"; return RedirectToPage(new { docId }); }

        // İş kuralı: serbest (FREE) mod yalnızca yetkili rollerde
        var mode = await conn.ExecuteScalarAsync<string?>(
            "SELECT ReceivingMode FROM ReceivingHeader WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = docId, CompanyId = company.Id });
        if (mode == "FREE" && !user.HasRole(Roles.Administrator, Roles.WarehouseManager, Roles.Purchasing))
        {
            TempData["Error"] = "Siparişsiz (serbest) mal kabul yetkiniz yok.";
            return RedirectToPage(new { docId });
        }

        try
        {
            var r = await conn.QueryFirstOrDefaultAsync<(decimal Accepted, decimal Excess, string ItemName)>(
                "sp_ReceivingTerminalScan",
                new { HeaderId = docId, ItemId = itemId, Qty = qty, LotNo = lotNo, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure);

            // İş kuralı: fazla okutma → iade alanına ayrıldı, kullanıcıyı UYAR
            if (r.Excess > 0)
                TempData["Error"] = $"{r.ItemName}: {r.Accepted:N0} kabul, {r.Excess:N0} sipariş fazlası → iade alanına ayrıldı.";
            else
                TempData["Success"] = $"{r.ItemName}: {r.Accepted:N0} kabul edildi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Mal kabul tarama hatası: {DocId}", docId);
            TempData["Error"] = "Tarama sırasında veritabanı hatası oluştu.";
        }
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
        public decimal ReturnPending { get; set; }
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
