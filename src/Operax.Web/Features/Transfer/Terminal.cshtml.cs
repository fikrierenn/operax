using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Transfer;

[Authorize]
public class TerminalModel(Db db, ICurrentCompany company) : PageModel
{
    public TransferTermDto? ActiveTransfer { get; set; }
    public IEnumerable<PendingTransferDto> PendingTransfers { get; set; } = [];

    public async Task OnGetAsync(Guid? transferId)
    {
        // El terminali: transfer belgesi seçilip barkod ile satırlar onaylanır
        using var conn = db.Open();

        PendingTransfers = await conn.QueryAsync<PendingTransferDto>(@"
            SELECT t.Id, t.DocNo,
                   (SELECT COUNT(*) FROM StockTransferLine WHERE TransferId = t.Id) AS LineCount
            FROM StockTransfer t
            WHERE t.CompanyId = @CompanyId AND t.Status = 'DRAFT'
            ORDER BY t.CreatedAt DESC",
            new { CompanyId = company.Id });

        if (!transferId.HasValue) return;

        ActiveTransfer = await conn.QueryFirstOrDefaultAsync<TransferTermDto>(@"
            SELECT t.Id, t.DocNo, t.Status,
                   wf.Code AS FromWhCode, wt.Code AS ToWhCode
            FROM StockTransfer t
            LEFT JOIN Warehouse wf ON wf.Id = t.FromWarehouseId
            LEFT JOIN Warehouse wt ON wt.Id = t.ToWarehouseId
            WHERE t.Id = @TransferId AND t.CompanyId = @CompanyId",
            new { TransferId = transferId, CompanyId = company.Id });

        if (ActiveTransfer == null) return;

        ActiveTransfer.Lines = (await conn.QueryAsync<TransferLineTermDto>(@"
            SELECT l.Id, l.QtyBase, l.IsConfirmed,
                   i.Code AS ItemCode, i.Name AS ItemName,
                   bf.Code AS FromBinCode, bt.Code AS ToBinCode, l.LotNo
            FROM StockTransferLine l
            JOIN StockTransfer t ON t.Id = l.TransferId
            JOIN Item i ON i.Id = l.ItemId
            LEFT JOIN Bin bf ON bf.Id = l.FromBinId
            LEFT JOIN Bin bt ON bt.Id = l.ToBinId
            WHERE l.TransferId = @TransferId AND t.CompanyId = @CompanyId
            ORDER BY (CASE WHEN l.IsConfirmed = 0 THEN 0 ELSE 1 END), i.Code",
            new { TransferId = transferId, CompanyId = company.Id })).ToList();
    }

    public async Task<IActionResult> OnPostConfirmLineAsync(Guid transferId, Guid lineId, string barcode)
    {
        // Barkod doğrulamasıyla transfer satırını onaylar
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            var itemId = await conn.ExecuteScalarAsync<Guid?>(@"
                SELECT i.Id FROM ItemBarcode b JOIN Item i ON i.Id = b.ItemId
                WHERE b.Barcode = @Barcode AND i.CompanyId = @CompanyId
                UNION
                SELECT i.Id FROM Item i WHERE i.Code = @Barcode AND i.CompanyId = @CompanyId",
                new { Barcode = barcode, CompanyId = company.Id }, trans);

            var lineItemId = await conn.ExecuteScalarAsync<Guid?>(@"
                SELECT l.ItemId FROM StockTransferLine l
                JOIN StockTransfer t ON t.Id = l.TransferId
                WHERE l.Id = @LineId AND l.TransferId = @TransferId AND t.CompanyId = @CompanyId",
                new { LineId = lineId, TransferId = transferId, CompanyId = company.Id }, trans);

            if (itemId == null || itemId != lineItemId)
            {
                TempData["Error"] = "Barkod bu satırla eşleşmiyor!";
                return RedirectToPage(new { transferId });
            }

            await conn.ExecuteAsync(@"
                UPDATE l SET l.IsConfirmed = 1
                FROM StockTransferLine l
                JOIN StockTransfer t ON t.Id = l.TransferId
                WHERE l.Id = @LineId AND t.CompanyId = @CompanyId",
                new { LineId = lineId, CompanyId = company.Id }, trans);

            trans.Commit();
            TempData["Success"] = "Satır onaylandı!";
        }
        catch { trans.Rollback(); TempData["Error"] = "Hata oluştu."; }
        return RedirectToPage(new { transferId });
    }

    public record TransferTermDto
    {
        public Guid Id { get; set; }
        public string DocNo { get; set; } = "";
        public string Status { get; set; } = "";
        public string? FromWhCode { get; set; }
        public string? ToWhCode { get; set; }
        public List<TransferLineTermDto> Lines { get; set; } = [];
    }

    public record TransferLineTermDto
    {
        public Guid Id { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public decimal QtyBase { get; set; }
        public bool IsConfirmed { get; set; }
        public string? FromBinCode { get; set; }
        public string? ToBinCode { get; set; }
        public string? LotNo { get; set; }
    }

    public record PendingTransferDto
    {
        public Guid Id { get; set; }
        public string DocNo { get; set; } = "";
        public int LineCount { get; set; }
    }
}
