using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.CycleCount;

public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    [BindProperty]
    public CountHeaderDto Header { get; set; } = new();
    public IEnumerable<CountLineDto> Lines { get; set; } = [];
    public IEnumerable<WarehouseDto> Warehouses { get; set; } = [];
    public IEnumerable<BinDto> Bins { get; set; } = [];
    public IEnumerable<ItemDto> Items { get; set; } = [];

    public bool IsNew => Header.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        using var conn = db.Open();
        Warehouses = await conn.QueryAsync<WarehouseDto>("SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0", new { CompanyId = company.Id });

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<CountHeaderDto>("SELECT * FROM CycleCount WHERE Id = @Id", new { Id = id }) ?? new();
            Lines = await conn.QueryAsync<CountLineDto>(@"
                SELECT l.*, i.Code as ItemCode, i.Name as ItemName, b.Code as BinCode, lpn.Code as LpnCode
                FROM CycleCountLine l
                JOIN Item i ON i.Id = l.ItemId
                JOIN Bin b ON b.Id = l.BinId
                LEFT JOIN LPN lpn ON lpn.Id = l.LpnId
                WHERE l.CycleCountId = @Id", new { Id = id });

            Bins = await conn.QueryAsync<BinDto>("SELECT Id, Code FROM Bin WHERE WarehouseId = @WhId", new { WhId = Header.WarehouseId });
            Items = await conn.QueryAsync<ItemDto>("SELECT Id, Code, Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1", new { CompanyId = company.Id });
        }
        else
        {
            Header.DocNo = "NEW";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        using var conn = db.Open();
        if (IsNew)
        {
            Header.Id = Guid.NewGuid();
            Header.DocNo = $"CNT-{DateTime.Now:yyyyMMddHHmm}";
            await conn.ExecuteAsync(@"
                INSERT INTO CycleCount (Id, CompanyId, DocNo, Status, WarehouseId, CreatedBy)
                VALUES (@Id, @CompanyId, @DocNo, 'DRAFT', @WarehouseId, @UserId)",
                new { Header.Id, CompanyId = company.Id, Header.DocNo, Header.WarehouseId, UserId = user.Id });
        }
        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid binId, Guid itemId, decimal qtyCounted)
    {
        using var conn = db.Open();
        
        // 1. Sistem bakiyesini al (Snapshot)
        var qtySystem = await conn.QueryFirstOrDefaultAsync<decimal>(@"
            SELECT ISNULL(SUM(QtyBase), 0) FROM StockMovement 
            WHERE BinId = @BinId AND ItemId = @ItemId AND IsCancelled = 0", 
            new { BinId = binId, ItemId = itemId });

        // 2. Satırı ekle
        await conn.ExecuteAsync(@"
            INSERT INTO CycleCountLine (CycleCountId, BinId, ItemId, QtySystem, QtyCounted, CountedBy, CountedAt)
            VALUES (@Id, @BinId, @ItemId, @QtySystem, @QtyCounted, @UserId, GETUTCDATE())",
            new { Id = id, BinId = binId, ItemId = itemId, QtySystem = qtySystem, QtyCounted = qtyCounted, UserId = user.Id.ToString() });

        await conn.ExecuteAsync("UPDATE CycleCount SET Status = 'COUNTING' WHERE Id = @Id", new { Id = id });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPostAdjustmentsAsync(Guid id)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try 
        {
            var header = await conn.QueryFirstOrDefaultAsync<CountHeaderDto>("SELECT * FROM CycleCount WHERE Id = @Id", new { Id = id }, trans);
            // Sayım belgesi bulunamadıysa işlemi iptal et
            if (header is null) { trans.Rollback(); return RedirectToPage(new { id }); }
            var lines = await conn.QueryAsync<CountLineDto>("SELECT * FROM CycleCountLine WHERE CycleCountId = @Id", new { Id = id }, trans);

            foreach (var line in lines)
            {
                if (line.QtyDifference != 0)
                {
                    // Stok Düzeltme Hareketi (Adjustment)
                    const string sql = @"
                        INSERT INTO StockMovement (CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy)
                        SELECT @CompanyId, @WhId, @BinId, @ItemId, 'COUNT_ADJ', @Diff, i.BaseUomId, ABS(@Diff), 'COUNT', @Id, @DocNo, @UserId
                        FROM Item i WHERE i.Id = @ItemId";
                    
                    await conn.ExecuteAsync(sql, new { 
                        CompanyId = company.Id, WhId = header.WarehouseId, BinId = line.BinId, ItemId = line.ItemId,
                        Diff = line.QtyDifference, Id = id, DocNo = header.DocNo, UserId = user.Id 
                    }, trans);
                }
            }

            await conn.ExecuteAsync("UPDATE CycleCount SET Status = 'COMPLETED', PostedAt = GETUTCDATE() WHERE Id = @Id", new { Id = id }, trans);
            trans.Commit();
        }
        catch { trans.Rollback(); throw; }

        return RedirectToPage(new { id });
    }

    public record CountHeaderDto { public Guid Id { get; set; } public string DocNo { get; set; } = ""; public string Status { get; set; } = ""; public Guid WarehouseId { get; set; } }
    public record CountLineDto { public Guid Id { get; set; } public Guid BinId { get; set; } public string BinCode { get; set; } = ""; public Guid ItemId { get; set; } public string ItemCode { get; set; } = ""; public string ItemName { get; set; } = ""; public decimal QtySystem { get; set; } public decimal QtyCounted { get; set; } public decimal QtyDifference { get; set; } }
    public record WarehouseDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; }
    public record BinDto { public Guid Id { get; set; } public string Code { get; set; } = ""; }
    public record ItemDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; }
}
