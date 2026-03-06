using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Transfer;

public class PutawayModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    public IEnumerable<StockAtReceivingDto> ItemsAtReceiving { get; set; } = [];
    public IEnumerable<DdlDto> StorageBins { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        
        // Mal kabul alanındaki stokları getir (LPN bazlı veya kalem bazlı)
        ItemsAtReceiving = await conn.QueryAsync<StockAtReceivingDto>(@"
            SELECT 
                inv.ItemId, i.Code as ItemCode, i.Name as ItemName,
                inv.BinId, b.Code as BinCode,
                inv.LpnId, l.Code as LpnCode,
                inv.QtyOnHand as Qty
            FROM vw_InventoryBalance inv
            JOIN Item i ON i.Id = inv.ItemId
            JOIN Bin b ON b.Id = inv.BinId
            LEFT JOIN LPN l ON l.Id = inv.LpnId
            WHERE b.IsReceivingArea = 1 AND inv.CompanyId = @CompanyId", new { CompanyId = company.Id });

        StorageBins = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Bin WHERE IsPickingArea = 1 OR IsStorageArea = 1", 
            new { CompanyId = company.Id });
    }

    public async Task<IActionResult> OnPostPutawayAsync(Guid itemId, Guid fromBinId, Guid? lpnId, Guid toBinId, decimal qty)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try 
        {
            var transferId = Guid.NewGuid();
            var docNo = $"PTW-{DateTime.Now:yyyyMMddHHmm}";

            // 1. Header
            await conn.ExecuteAsync(@"
                INSERT INTO StockTransfer (Id, CompanyId, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes, CreatedBy)
                SELECT @Id, CompanyId, @DocNo, 'POSTED', 'BIN_TO_BIN', WarehouseId, WarehouseId, 'ADRESLEME (PUTAWAY)', @UserId
                FROM Bin WHERE Id = @BinId", 
                new { Id = transferId, DocNo = docNo, BinId = fromBinId, UserId = user.Id }, trans);

            // 2. Movements
            // Issue
            await conn.ExecuteAsync(@"
                INSERT INTO StockMovement (CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy, LpnId)
                SELECT CompanyId, WarehouseId, BinId, @ItemId, 'TRANSFER', -@Qty, @UomId, @Qty, 'TRANSFER', @TransferId, @DocNo, @UserId, @LpnId
                FROM Bin WHERE Id = @BinId", 
                new { ItemId = itemId, Qty = qty, UomId = (await conn.QueryFirstOrDefaultAsync<Guid>("SELECT BaseUomId FROM Item WHERE Id = @Id", new { Id = itemId }, trans)), TransferId = transferId, DocNo = docNo, UserId = user.Id, BinId = fromBinId, LpnId = lpnId }, trans);

            // Receipt
            await conn.ExecuteAsync(@"
                INSERT INTO StockMovement (CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy, LpnId)
                SELECT CompanyId, WarehouseId, @ToBin, @ItemId, 'TRANSFER', @Qty, @UomId, @Qty, 'TRANSFER', @TransferId, @DocNo, @UserId, @LpnId
                FROM Bin WHERE Id = @BinId", 
                new { ItemId = itemId, Qty = qty, UomId = (await conn.QueryFirstOrDefaultAsync<Guid>("SELECT BaseUomId FROM Item WHERE Id = @Id", new { Id = itemId }, trans)), TransferId = transferId, DocNo = docNo, UserId = user.Id, BinId = fromBinId, ToBin = toBinId, LpnId = lpnId }, trans);

            trans.Commit();
        }
        catch { trans.Rollback(); throw; }

        return RedirectToPage();
    }

    public record StockAtReceivingDto { public Guid ItemId { get; set; } public string ItemCode { get; set; } = ""; public string ItemName { get; set; } = ""; public Guid BinId { get; set; } public string BinCode { get; set; } = ""; public Guid? LpnId { get; set; } public string? LpnCode { get; set; } public decimal Qty { get; set; } }
    public record DdlDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; }
}
