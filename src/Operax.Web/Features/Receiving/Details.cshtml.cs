using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Receiving;

public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    [BindProperty]
    public ReceivingHeaderDto Header { get; set; } = new();
    public IEnumerable<ReceivingLineDto> Lines { get; set; } = [];
    
    public IEnumerable<DdlDto> Warehouses { get; set; } = [];
    public IEnumerable<DdlDto> Partners { get; set; } = [];
    public IEnumerable<DdlDto> AvailableItems { get; set; } = [];
    public IEnumerable<DdlDto> OpenPurchaseOrders { get; set; } = [];
    public IEnumerable<DdlDto> AllUoms { get; set; } = [];

    public bool IsNew => Header.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        using var conn = db.Open();

        Warehouses = await conn.QueryAsync<DdlDto>("SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0", new { CompanyId = company.Id });
        Partners = await conn.QueryAsync<DdlDto>("SELECT Id, Code, Name FROM Partner WHERE CompanyId = @CompanyId AND Type IN ('VENDOR', 'BOTH') AND IsDeleted = 0", new { CompanyId = company.Id });
        AvailableItems = await conn.QueryAsync<DdlDto>("SELECT Id, Code, Name, BaseUomId FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0", new { CompanyId = company.Id });
        OpenPurchaseOrders = await conn.QueryAsync<DdlDto>("SELECT Id, OrderNo as Code, '' as Name FROM PurchaseOrderHeader WHERE CompanyId = @CompanyId AND Status = 'APPROVED' AND IsDeleted = 0", new { CompanyId = company.Id });
        AllUoms = await conn.QueryAsync<DdlDto>("SELECT Id, Code, NameTr as Name FROM DictionaryValue WHERE TypeId = (SELECT Id FROM DictionaryType WHERE Code = 'UOM') AND IsActive = 1");

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<ReceivingHeaderDto>(@"
                SELECT r.Id, r.WarehouseId, r.PartnerId, r.PurchaseOrderId, r.DocNo, r.Status, r.Notes, p.Name as PartnerName
                FROM ReceivingHeader r
                JOIN Partner p ON p.Id = r.PartnerId
                WHERE r.Id = @Id", new { Id = id }) ?? new();

            Lines = await conn.QueryAsync<ReceivingLineDto>(@"
                SELECT l.Id, i.Code as ItemCode, i.Name as ItemName, dv.Code as UomCode, 
                       l.QtyOriginal, l.QtyBase, l.LotNo, l.PurchaseOrderLineId
                FROM ReceivingLine l
                JOIN Item i ON i.Id = l.ItemId
                JOIN DictionaryValue dv ON dv.Id = l.UomId
                WHERE l.HeaderId = @Id", new { Id = id });
        }
        else
        {
            Header.Status = "DRAFT";
            Header.DocNo = "NEW";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        using var conn = db.Open();

        if (IsNew)
        {
            Header.Id = Guid.NewGuid();
            Header.DocNo = $"RCV-{DateTime.Now:yyyyMMdd}-{conn.ExecuteScalar<int>("SELECT COUNT(1) + 1 FROM ReceivingHeader WHERE CAST(DocDate AS DATE) = CAST(GETDATE() AS DATE)"):D5}";
            
            const string sql = @"
                INSERT INTO ReceivingHeader (Id, CompanyId, WarehouseId, PartnerId, PurchaseOrderId, DocNo, Status, Notes, CreatedBy)
                VALUES (@Id, @CompanyId, @WarehouseId, @PartnerId, @PurchaseOrderId, @DocNo, 'DRAFT', @Notes, @UserId)";
            
            await conn.ExecuteAsync(sql, new { 
                Header.Id, CompanyId = company.Id, Header.WarehouseId, Header.PartnerId, Header.PurchaseOrderId,
                Header.DocNo, Header.Notes, UserId = user.Id 
            });
        }
        else
        {
            const string sql = "UPDATE ReceivingHeader SET WarehouseId = @WarehouseId, PartnerId = @PartnerId, PurchaseOrderId = @PurchaseOrderId, Notes = @Notes WHERE Id = @Id";
            await conn.ExecuteAsync(sql, Header);
        }

        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, Guid uomId, decimal qty, string? lotNo, Guid? poLineId)
    {
        using var conn = db.Open();
        
        // 1. Get Item Info (Base UOM)
        var item = await conn.QueryFirstOrDefaultAsync("SELECT BaseUomId FROM Item WHERE Id = @ItemId", new { ItemId = itemId });
        // Ürün bulunamadıysa yönlendir
        if (item is null) return RedirectToPage(new { id });

        // 2. Get Conversion Rate
        decimal rate = 1;
        if (uomId != (Guid)item.BaseUomId)
        {
            rate = await conn.QueryFirstOrDefaultAsync<decimal>(
                "SELECT ConversionRate FROM ItemUOM WHERE ItemId = @ItemId AND UomId = @UomId", 
                new { ItemId = itemId, UomId = uomId });
            
            if (rate == 0) rate = 1; // Fallback
        }

        const string sql = @"
            INSERT INTO ReceivingLine (HeaderId, ItemId, UomId, QtyOriginal, QtyBase, LotNo, PurchaseOrderLineId)
            VALUES (@HeaderId, @ItemId, @UomId, @Qty, @QtyBase, @LotNo, @poLineId)";

        await conn.ExecuteAsync(sql, new { 
            HeaderId = id, ItemId = itemId, UomId = uomId, Qty = qty, QtyBase = qty * rate, LotNo = lotNo, poLineId 
        });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPostAsync(Guid id)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();

        try 
        {
            var header = await conn.QueryFirstOrDefaultAsync<ReceivingHeaderDto>("SELECT * FROM ReceivingHeader WHERE Id = @Id", new { Id = id }, trans);
            // Belge bulunamadıysa işlemi iptal et
            if (header is null) { trans.Rollback(); return RedirectToPage(new { id }); }
            var lines = await conn.QueryAsync<ReceivingLineDto>("SELECT * FROM ReceivingLine WHERE HeaderId = @Id", new { Id = id }, trans);

            foreach (var line in lines)
            {
                // 1. Stock Movement
                const string moveSql = @"
                    INSERT INTO StockMovement (CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, LotNo, CreatedBy)
                    SELECT @CompanyId, @WarehouseId, b.Id, @ItemId, 'RECEIPT', @Qty, @UomId, @Qty, 'RECEIVING', @HeaderId, @DocNo, @LotNo, @UserId
                    FROM Bin b WHERE b.WarehouseId = @WarehouseId AND b.IsReceivingArea = 1";
                
                await conn.ExecuteAsync(moveSql, new { 
                    CompanyId = company.Id, WarehouseId = header.WarehouseId, ItemId = line.ItemId, 
                    Qty = line.QtyBase, UomId = line.UomId, HeaderId = id, DocNo = header.DocNo, 
                    LotNo = line.LotNo, UserId = user.Id 
                }, trans);

                // 2. Update PO Line if exists
                if (line.PurchaseOrderLineId.HasValue)
                {
                    await conn.ExecuteAsync("UPDATE PurchaseOrderLine SET QtyReceived = QtyReceived + @Qty WHERE Id = @Id", new { Qty = line.QtyBase, Id = line.PurchaseOrderLineId }, trans);
                }
            }

            // 3. Update Header Status
            await conn.ExecuteAsync("UPDATE ReceivingHeader SET Status = 'POSTED', UpdatedAt = GETUTCDATE() WHERE Id = @Id", new { Id = id }, trans);

            // 4. Update PO Status if all received
            if (header.PurchaseOrderId.HasValue)
            {
                var poStatusSql = @"
                    IF NOT EXISTS (SELECT 1 FROM PurchaseOrderLine WHERE HeaderId = @POId AND QtyReceived < QtyOrdered)
                    UPDATE PurchaseOrderHeader SET Status = 'RECEIVED' WHERE Id = @POId";
                await conn.ExecuteAsync(poStatusSql, new { POId = header.PurchaseOrderId }, trans);
            }

            trans.Commit();
        }
        catch 
        {
            trans.Rollback();
            throw;
        }

        return RedirectToPage(new { id });
    }

    public record ReceivingHeaderDto { public Guid Id { get; set; } public Guid WarehouseId { get; set; } public Guid PartnerId { get; set; } public Guid? PurchaseOrderId { get; set; } public string DocNo { get; set; } = ""; public string Status { get; set; } = "DRAFT"; public string? Notes { get; set; } public string? PartnerName { get; set; } }
    public record ReceivingLineDto { public Guid Id { get; set; } public string? ItemCode { get; set; } public string? ItemName { get; set; } public string? UomCode { get; set; } public decimal QtyOriginal { get; set; } public decimal QtyBase { get; set; } public string? LotNo { get; set; } public Guid ItemId { get; set; } public Guid UomId { get; set; } public Guid? PurchaseOrderLineId { get; set; } }
    public record DdlDto { public Guid Id { get; set; } public string? Code { get; set; } public string? Name { get; set; } public Guid? BaseUomId { get; set; } }
}
