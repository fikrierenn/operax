using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Transfer;

public class ReplenishmentModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ReplenishmentSuggestionDto> Suggestions { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        
        // 1. Kritik seviyenin altına düşen toplama raflarını bul
        // 2. Bu rafları besleyebilecek (başka raflarda duran) stokları bul
        const string sql = @"
            SELECT 
                c.ItemId, i.Code as ItemCode, i.Name as ItemName,
                c.BinId as PickingBinId, pb.Code as PickingBinCode,
                c.MinQty, c.MaxQty,
                ISNULL(SUM(inv.QtyOnHand), 0) as CurrentQty,
                (c.MaxQty - ISNULL(SUM(inv.QtyOnHand), 0)) as NeededQty
            FROM ItemBinConfig c
            JOIN Item i ON i.Id = c.ItemId
            JOIN Bin pb ON pb.Id = c.BinId
            LEFT JOIN vw_InventoryBalance inv ON inv.ItemId = c.ItemId AND inv.BinId = c.BinId
            WHERE c.CompanyId = @CompanyId
            GROUP BY c.ItemId, i.Code, i.Name, c.BinId, pb.Code, c.MinQty, c.MaxQty
            HAVING ISNULL(SUM(inv.QtyOnHand), 0) < c.MinQty";

        Suggestions = await conn.QueryAsync<ReplenishmentSuggestionDto>(sql, new { CompanyId = company.Id });
    }

    public async Task<IActionResult> OnPostCreateTransferAsync(Guid itemId, Guid pickingBinId, decimal neededQty)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try 
        {
            // En uygun besleme kaynağını bul (Bulk/Overstock raflarından)
            var source = await conn.QueryFirstOrDefaultAsync(@"
                SELECT TOP 1 BinId, QtyOnHand 
                FROM vw_InventoryBalance 
                WHERE ItemId = @ItemId AND BinId <> @PickingBinId AND QtyOnHand > 0
                ORDER BY QtyOnHand DESC", new { ItemId = itemId, PickingBinId = pickingBinId }, trans);

            if (source != null)
            {
                var qtyToMove = Math.Min(neededQty, source.QtyOnHand);
                var transferId = Guid.NewGuid();
                
                // Transfer Header (Internal)
                await conn.ExecuteAsync(@"
                    INSERT INTO StockTransfer (Id, CompanyId, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes)
                    SELECT @Id, CompanyId, @DocNo, 'DRAFT', 'BIN_TO_BIN', WarehouseId, WarehouseId, 'AUTO REPLENISHMENT'
                    FROM Bin WHERE Id = @BinId", 
                    new { Id = transferId, DocNo = $"REP-{DateTime.Now:yyyyMMddHHmm}", BinId = pickingBinId }, trans);

                // Transfer Line
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

    public record ReplenishmentSuggestionDto { 
        public Guid ItemId { get; set; } 
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public Guid PickingBinId { get; set; }
        public string PickingBinCode { get; set; } = "";
        public decimal MinQty { get; set; }
        public decimal MaxQty { get; set; }
        public decimal CurrentQty { get; set; }
        public decimal NeededQty { get; set; }
    }
}
