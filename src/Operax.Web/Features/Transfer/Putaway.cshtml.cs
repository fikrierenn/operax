using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Transfer;

[Authorize]
public class PutawayModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    public IEnumerable<StockAtReceivingDto> ItemsAtReceiving { get; set; } = [];
    public IEnumerable<DdlDto>             StorageBins      { get; set; } = [];

    public async Task OnGetAsync()
    {
        // Mal kabul alanındaki stokları ve hedef depolama raflarını yükler
        using var conn = db.Open();

        // tvf_InventoryBalance: CompanyId parametreli inline TVF — view'dan daha güvenli
        ItemsAtReceiving = await conn.QueryAsync<StockAtReceivingDto>(@"
            SELECT
                inv.ItemId, i.Code as ItemCode, i.Name as ItemName,
                inv.BinId, b.Code as BinCode,
                inv.QtyBalance as Qty
            FROM tvf_InventoryBalance(@CompanyId) inv
            JOIN Item i ON i.Id = inv.ItemId
            JOIN Bin b ON b.Id = inv.BinId
            WHERE b.IsReceivingArea = 1",
            new { CompanyId = company.Id });

        // Depolama rafları — CompanyId depo üzerinden filtrelenir
        StorageBins = await conn.QueryAsync<DdlDto>(@"
            SELECT b.Id, b.Code, b.Code as Name
            FROM Bin b
            JOIN Warehouse w ON w.Id = b.WarehouseId
            WHERE w.CompanyId = @CompanyId
              AND (b.IsPickingArea = 1 OR b.IsStorageArea = 1)
              AND b.IsActive = 1",
            new { CompanyId = company.Id });
    }

    public async Task<IActionResult> OnPostPutawayAsync(Guid itemId, Guid fromBinId, Guid toBinId, decimal qty)
    {
        // Mal kabul rafından depolama rafına mal yerleme (bin-to-bin transfer)
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            // Ürünün temel birimini önceden al — aynı sorgu iki kez tekrar etmesin
            var baseUomId = await conn.ExecuteScalarAsync<Guid>(
                "SELECT BaseUomId FROM Item WHERE Id = @Id", new { Id = itemId }, trans);

            var transferId = Guid.NewGuid();
            var docNo      = $"{DocPrefix.Replenishment}-{DateTime.Now:yyyyMMddHHmm}";

            // Transfer başlığı: aynı depo içinde bin-to-bin
            await conn.ExecuteAsync(@"
                INSERT INTO StockTransfer
                    (Id, CompanyId, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes, CreatedBy)
                SELECT @Id, w.CompanyId, @DocNo, @Status, 'BIN_TO_BIN', w.Id, w.Id, 'ADRESLEME (PUTAWAY)', @UserId
                FROM Bin b
                JOIN Warehouse w ON w.Id = b.WarehouseId
                WHERE b.Id = @BinId",
                new { Id = transferId, DocNo = docNo, Status = DocStatus.Posted, BinId = fromBinId, UserId = user.Id }, trans);

            // Çıkış hareketi (kaynak raf — mal kabul alanı)
            await conn.ExecuteAsync(@"
                INSERT INTO StockMovement
                    (CompanyId, WarehouseId, BinId, ItemId, MovementType,
                     QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
                SELECT w.CompanyId, w.Id, @FromBin, @ItemId, 'TRANSFER',
                       -@Qty, @UomId, @Qty, 'TRANSFER', @TransferId, @DocNo, @UserId
                FROM Bin b JOIN Warehouse w ON w.Id = b.WarehouseId WHERE b.Id = @FromBin",
                new { ItemId = itemId, Qty = qty, UomId = baseUomId, TransferId = transferId, DocNo = docNo, UserId = user.Id, FromBin = fromBinId }, trans);

            // Giriş hareketi (hedef raf — depolama alanı)
            await conn.ExecuteAsync(@"
                INSERT INTO StockMovement
                    (CompanyId, WarehouseId, BinId, ItemId, MovementType,
                     QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
                SELECT w.CompanyId, w.Id, @ToBin, @ItemId, 'TRANSFER',
                       @Qty, @UomId, @Qty, 'TRANSFER', @TransferId, @DocNo, @UserId
                FROM Bin b JOIN Warehouse w ON w.Id = b.WarehouseId WHERE b.Id = @ToBin",
                new { ItemId = itemId, Qty = qty, UomId = baseUomId, TransferId = transferId, DocNo = docNo, UserId = user.Id, ToBin = toBinId }, trans);

            trans.Commit();
        }
        catch { trans.Rollback(); throw; }

        return RedirectToPage();
    }

    public record StockAtReceivingDto
    {
        public Guid    ItemId    { get; set; }
        public string  ItemCode  { get; set; } = "";
        public string  ItemName  { get; set; } = "";
        public Guid    BinId     { get; set; }
        public string  BinCode   { get; set; } = "";
        public decimal Qty       { get; set; }
    }
}
