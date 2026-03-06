using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Transfer;

public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    [BindProperty]
    public TransferHeaderDto Header { get; set; } = new();
    public IEnumerable<TransferLineDto> Lines { get; set; } = [];
    
    public IEnumerable<DdlDto> Warehouses { get; set; } = [];
    public IEnumerable<DdlDto> Items { get; set; } = [];
    public IEnumerable<BinDto> Bins { get; set; } = [];

    public bool IsNew => Header.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        using var conn = db.Open();
        
        Warehouses = await conn.QueryAsync<DdlDto>("SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0", new { CompanyId = company.Id });
        Items = await conn.QueryAsync<DdlDto>("SELECT Id, Code, Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1", new { CompanyId = company.Id });

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<TransferHeaderDto>(
                "SELECT * FROM StockTransfer WHERE Id = @Id", new { Id = id }) ?? new();
            
            Lines = await conn.QueryAsync<TransferLineDto>(@"
                SELECT l.*, i.Code as ItemCode, i.Name as ItemName, fb.Code as FromBinCode, tb.Code as ToBinCode
                FROM StockTransferLine l
                JOIN Item i ON i.Id = l.ItemId
                LEFT JOIN Bin fb ON fb.Id = l.FromBinId
                LEFT JOIN Bin tb ON tb.Id = l.ToBinId
                WHERE l.TransferId = @Id", new { Id = id });

            Bins = await conn.QueryAsync<BinDto>("SELECT Id, Code, WarehouseId FROM Bin WHERE IsActive = 1");
        }
        else
        {
            Header.DocNo = "NEW";
            Header.TransferType = "BIN_TO_BIN";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        using var conn = db.Open();
        if (IsNew)
        {
            Header.Id = Guid.NewGuid();
            Header.DocNo = $"TRF-{DateTime.Now:yyyyMMddHHmm}";
            
            await conn.ExecuteAsync(@"
                INSERT INTO StockTransfer (Id, CompanyId, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes, CreatedBy)
                VALUES (@Id, @CompanyId, @DocNo, 'DRAFT', @TransferType, @FromWarehouseId, @ToWarehouseId, @Notes, @UserId)",
                new { Header.Id, CompanyId = company.Id, Header.DocNo, Header.TransferType, Header.FromWarehouseId, Header.ToWarehouseId, Header.Notes, UserId = user.Id });
        }
        else
        {
            await conn.ExecuteAsync(@"
                UPDATE StockTransfer SET Notes = @Notes, TransferType = @TransferType 
                WHERE Id = @Id", Header);
        }
        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, Guid? fromBinId, Guid? toBinId, decimal qty)
    {
        using var conn = db.Open();
        var item = await conn.QueryFirstOrDefaultAsync("SELECT BaseUomId FROM Item WHERE Id = @Id", new { Id = itemId });
        // Ürün bulunamadıysa yönlendir
        if (item is null) return RedirectToPage(new { id });

        await conn.ExecuteAsync(@"
            INSERT INTO StockTransferLine (TransferId, ItemId, UomId, FromBinId, ToBinId, Qty, QtyBase)
            VALUES (@TransferId, @ItemId, @UomId, @FromBinId, @ToBinId, @Qty, @Qty)",
            new { TransferId = id, ItemId = itemId, UomId = (Guid)item.BaseUomId, FromBinId = fromBinId, ToBinId = toBinId, Qty = qty });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPostAsync(Guid id)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try 
        {
            var header = await conn.QueryFirstOrDefaultAsync<TransferHeaderDto>("SELECT * FROM StockTransfer WHERE Id = @Id", new { Id = id }, trans);
            // Transfer belgesi bulunamadıysa işlemi iptal et
            if (header is null) { trans.Rollback(); return RedirectToPage(new { id }); }
            var lines = await conn.QueryAsync<TransferLineDto>("SELECT * FROM StockTransferLine WHERE TransferId = @Id", new { Id = id }, trans);

            foreach (var line in lines)
            {
                // Stock Movement (TRANSFER)
                // 1. Out (Issue)
                await conn.ExecuteAsync(@"
                    INSERT INTO StockMovement (CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
                    VALUES (@CompanyId, @WhId, @BinId, @ItemId, 'TRANSFER', -@Qty, @UomId, @Qty, 'TRANSFER', @TransferId, @DocNo, @UserId)",
                    new { CompanyId = company.Id, WhId = header.FromWarehouseId, BinId = line.FromBinId, ItemId = line.ItemId, Qty = line.QtyBase, UomId = line.UomId, TransferId = id, DocNo = header.DocNo, UserId = user.Id }, trans);

                // 2. In (Receipt)
                await conn.ExecuteAsync(@"
                    INSERT INTO StockMovement (CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
                    VALUES (@CompanyId, @WhId, @BinId, @ItemId, 'TRANSFER', @Qty, @UomId, @Qty, 'TRANSFER', @TransferId, @DocNo, @UserId)",
                    new { CompanyId = company.Id, WhId = header.ToWarehouseId, BinId = line.ToBinId, ItemId = line.ItemId, Qty = line.QtyBase, UomId = line.UomId, TransferId = id, DocNo = header.DocNo, UserId = user.Id }, trans);
            }

            await conn.ExecuteAsync("UPDATE StockTransfer SET Status = 'POSTED', PostedAt = GETUTCDATE() WHERE Id = @Id", new { Id = id }, trans);
            trans.Commit();
        }
        catch { trans.Rollback(); throw; }

        return RedirectToPage(new { id });
    }

    public record TransferHeaderDto { public Guid Id { get; set; } public string DocNo { get; set; } = ""; public string Status { get; set; } = ""; public string TransferType { get; set; } = "BIN_TO_BIN"; public Guid FromWarehouseId { get; set; } public Guid ToWarehouseId { get; set; } public string? Notes { get; set; } }
    public record TransferLineDto { public Guid Id { get; set; } public Guid ItemId { get; set; } public string ItemCode { get; set; } = ""; public string ItemName { get; set; } = ""; public Guid? FromBinId { get; set; } public string? FromBinCode { get; set; } public Guid? ToBinId { get; set; } public string? ToBinCode { get; set; } public decimal Qty { get; set; } public decimal QtyBase { get; set; } public Guid UomId { get; set; } }
    public record DdlDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; }
    public record BinDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public Guid WarehouseId { get; set; } }
}
