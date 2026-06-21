using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Transfer;

[Authorize]
public class ReplenishmentModel(Db db, ICurrentCompany company, ILogger<ReplenishmentModel> logger) : PageModel
{
    public IEnumerable<ReplenishmentSuggestionDto> Suggestions { get; set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        // tvf_ReplenishmentSuggestions: MinQty altına düşmüş rafları CompanyId ile yalıtılmış döner
        using var conn = db.Open();
        Suggestions = await conn.QueryAsync<ReplenishmentSuggestionDto>(new CommandDefinition(
            "SELECT * FROM tvf_ReplenishmentSuggestions(@CompanyId)",
            new { CompanyId = company.Id }, cancellationToken: ct));
    }

    public async Task<IActionResult> OnPostCreateTransferAsync(Guid itemId, Guid pickingBinId, decimal neededQty, CancellationToken ct)
    {
        // En uygun besleme kaynağını (bulk/overstock raf) bularak yenileme transferi oluşturur
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            // tvf_InventoryBalance: en dolu kaynak rafı bul (CompanyId yalıtılmış)
            var source = await conn.QueryFirstOrDefaultAsync(new CommandDefinition(@"
                SELECT TOP 1 BinId, QtyBalance
                FROM tvf_InventoryBalance(@CompanyId)
                WHERE ItemId   = @ItemId
                  AND BinId   <> @PickingBinId
                  AND QtyBalance > 0
                ORDER BY QtyBalance DESC",
                new { ItemId = itemId, CompanyId = company.Id, PickingBinId = pickingBinId },
                transaction: trans, cancellationToken: ct));

            // İş kuralı: uygun kaynak raf (stoklu) yoksa sessizce geçme — kullanıcıya bildir
            if (source == null)
            {
                trans.Rollback();
                TempData["Error"] = "Bu ürün için besleme yapılacak stoklu kaynak raf bulunamadı.";
                return RedirectToPage();
            }

            var qtyToMove  = Math.Min(neededQty, (decimal)source.QtyBalance);
            var transferId = Guid.NewGuid();
            var docNo      = $"{DocPrefix.Replenishment}-{DateTime.UtcNow:yyyyMMddHHmm}";

            // Transfer başlığı — aynı depo içinde bin-to-bin
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO StockTransfer
                    (Id, CompanyId, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes)
                SELECT @Id, w.CompanyId, @DocNo, @Status, 'BIN_TO_BIN', w.Id, w.Id, 'OTO YENİLEME'
                FROM Bin b JOIN Warehouse w ON w.Id = b.WarehouseId WHERE b.Id = @BinId",
                new { Id = transferId, DocNo = docNo, Status = DocStatus.Draft, BinId = pickingBinId },
                transaction: trans, cancellationToken: ct));

            // Transfer satırı — CompanyId ile ürün doğrulaması yapılır
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO StockTransferLine (TransferId, ItemId, UomId, FromBinId, ToBinId, Qty, QtyBase)
                SELECT @TransferId, @ItemId, BaseUomId, @FromBin, @ToBin, @Qty, @Qty
                FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId",
                new { TransferId = transferId, ItemId = itemId, CompanyId = company.Id, FromBin = source.BinId, ToBin = pickingBinId, Qty = qtyToMove },
                transaction: trans, cancellationToken: ct));

            trans.Commit();
            TempData["Success"] = $"Besleme transferi oluşturuldu ({docNo}): {qtyToMove:N2} adet taşınacak.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "İkmal transferi oluşturma hatası");
            trans.Rollback();
            TempData["Error"] = "Besleme transferi oluşturulurken hata oluştu.";
        }

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
        public string? UomCode       { get; set; }

        // Plan 32: tercih edilen tedarikçi bilgisi (kimden/ne sürede sipariş)
        public Guid?    PreferredSupplierId   { get; set; }
        public string?  PreferredSupplierName { get; set; }
        public string?  SupplierItemCode      { get; set; }
        public int?     LeadTimeDays          { get; set; }
        public decimal? SupplierMinOrderQty   { get; set; }
    }
}
