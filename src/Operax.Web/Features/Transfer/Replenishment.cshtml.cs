using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Transfer;

[Authorize]
public class ReplenishmentModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ReplenishmentSuggestionDto> Suggestions { get; set; } = [];

    public async Task OnGetAsync()
    {
        // tvf_ReplenishmentSuggestions: MinQty altına düşmüş rafları CompanyId ile yalıtılmış döner
        using var conn = db.Open();
        Suggestions = await conn.QueryAsync<ReplenishmentSuggestionDto>(
            "SELECT * FROM tvf_ReplenishmentSuggestions(@CompanyId)",
            new { CompanyId = company.Id });
    }

    public async Task<IActionResult> OnPostCreateTransferAsync(Guid itemId, Guid pickingBinId, decimal neededQty)
    {
        // En uygun besleme kaynağını (bulk/overstock raf) bularak yenileme transferi oluşturur
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            // tvf_InventoryBalance: en dolu kaynak rafı bul (CompanyId yalıtılmış)
            var source = await conn.QueryFirstOrDefaultAsync(@"
                SELECT TOP 1 BinId, QtyBalance
                FROM tvf_InventoryBalance(@CompanyId)
                WHERE ItemId   = @ItemId
                  AND BinId   <> @PickingBinId
                  AND QtyBalance > 0
                ORDER BY QtyBalance DESC",
                new { ItemId = itemId, CompanyId = company.Id, PickingBinId = pickingBinId }, trans);

            if (source != null)
            {
                var qtyToMove  = Math.Min(neededQty, (decimal)source.QtyBalance);
                var transferId = Guid.NewGuid();
                var docNo      = $"{DocPrefix.Replenishment}-{DateTime.Now:yyyyMMddHHmm}";

                // Transfer başlığı — aynı depo içinde bin-to-bin
                await conn.ExecuteAsync(@"
                    INSERT INTO StockTransfer
                        (Id, CompanyId, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes)
                    SELECT @Id, w.CompanyId, @DocNo, @Status, 'BIN_TO_BIN', w.Id, w.Id, 'OTO YENİLEME'
                    FROM Bin b JOIN Warehouse w ON w.Id = b.WarehouseId WHERE b.Id = @BinId",
                    new { Id = transferId, DocNo = docNo, Status = DocStatus.Draft, BinId = pickingBinId }, trans);

                // Transfer satırı
                await conn.ExecuteAsync(@"
                    INSERT INTO StockTransferLine (TransferId, ItemId, UomId, FromBinId, ToBinId, Qty, QtyBase)
                    SELECT @TransferId, @ItemId, BaseUomId, @FromBin, @ToBin, @Qty, @Qty
                    FROM Item WHERE Id = @ItemId",
                    new { TransferId = transferId, ItemId = itemId, FromBin = source.BinId, ToBin = pickingBinId, Qty = qtyToMove }, trans);
            }

            trans.Commit();
        }
        catch { trans.Rollback(); throw; }

        return RedirectToPage();
    }

    public record ReplenishmentSuggestionDto
    {
        public Guid    ItemId        { get; set; }
        public string  ItemCode      { get; set; } = "";
        public string  ItemName      { get; set; } = "";
        public Guid    PickingBinId  { get; set; }
        public string  PickingBinCode { get; set; } = "";
        public decimal MinQty        { get; set; }
        public decimal MaxQty        { get; set; }
        public decimal CurrentQty    { get; set; }
        public decimal NeededQty     { get; set; }
    }
}
