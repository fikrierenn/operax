using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Shipping;

public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    [BindProperty]
    public ShippingHeaderDto Header { get; set; } = new();
    public IEnumerable<ShippingLineDto> Lines { get; set; } = [];
    
    public IEnumerable<DdlDto> Warehouses { get; set; } = [];
    public IEnumerable<DdlDto> AvailableItems { get; set; } = [];
    public IEnumerable<DdlDto> OpenSalesOrders { get; set; } = [];
    public IEnumerable<DdlDto> AllUoms { get; set; } = [];

    public Guid? PickTaskId { get; set; }
    public bool IsNew => Header.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        using var conn = db.Open();

        Warehouses = await conn.QueryAsync<DdlDto>("SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0", new { CompanyId = company.Id });
        AvailableItems = await conn.QueryAsync<DdlDto>("SELECT Id, Code, Name, BaseUomId FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0", new { CompanyId = company.Id });
        OpenSalesOrders = await conn.QueryAsync<DdlDto>("SELECT Id, OrderNo as Code, '' as Name FROM SalesOrderHeader WHERE CompanyId = @CompanyId AND Status = 'APPROVED' AND IsDeleted = 0", new { CompanyId = company.Id });
        AllUoms = await conn.QueryAsync<DdlDto>("SELECT Id, Code, NameTr as Name FROM DictionaryValue WHERE TypeId = (SELECT Id FROM DictionaryType WHERE Code = 'UOM') AND IsActive = 1");

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<ShippingHeaderDto>(@"
                SELECT Id, WarehouseId, DocNo, Status, DocDate, CarrierName, VehiclePlate, Notes
                FROM ShippingHeader WHERE Id = @Id", new { Id = id }) ?? new();

            Lines = await conn.QueryAsync<ShippingLineDto>(@"
                SELECT l.Id, i.Code as ItemCode, i.Name as ItemName, dv.Code as UomCode, 
                       l.QtyOriginal, l.QtyBase, l.LotNo, oh.OrderNo as SourceOrderNo,
                       l.SalesOrderLineId, l.ItemId, l.UomId
                FROM ShippingLine l
                JOIN Item i ON i.Id = l.ItemId
                JOIN DictionaryValue dv ON dv.Id = l.UomId
                JOIN SalesOrderLine ol ON ol.Id = l.SalesOrderLineId
                JOIN SalesOrderHeader oh ON oh.Id = ol.HeaderId
                WHERE l.HeaderId = @Id", new { Id = id });

            PickTaskId = await conn.QueryFirstOrDefaultAsync<Guid?>("SELECT Id FROM PickTask WHERE ShipmentId = @Id", new { Id = id });
        }
        else
        {
            Header.DocDate = DateTime.Now;
            Header.Status = "DRAFT";
            Header.DocNo = "NEW";
        }
    }

    public async Task<IActionResult> OnPostCreatePickTaskAsync(Guid id)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();

        try 
        {
            var header = await conn.QueryFirstOrDefaultAsync<ShippingHeaderDto>("SELECT * FROM ShippingHeader WHERE Id = @Id", new { Id = id }, trans);
            // Sevkiyat belgesi bulunamadıysa işlemi iptal et
            if (header is null) { trans.Rollback(); return RedirectToPage(new { id }); }
            var lines = await conn.QueryAsync<ShippingLineDto>("SELECT * FROM ShippingLine WHERE HeaderId = @Id", new { Id = id }, trans);

            var taskId = Guid.NewGuid();
            var pickDocNo = $"PCK-{header.DocNo}";
            bool hasPicking = false;

            // 1. Create Pick Task Header (DRAFT)
            await conn.ExecuteAsync(@"
                INSERT INTO PickTask (Id, CompanyId, DocNo, ShipmentId, Status, Priority, CreatedBy)
                VALUES (@Id, @CompanyId, @DocNo, @ShipmentId, 'DRAFT', 10, @UserId)",
                new { Id = taskId, CompanyId = company.Id, DocNo = pickDocNo, ShipmentId = id, UserId = user.Id }, trans);

            foreach (var line in lines)
            {
                // 2. CHECK STOCK (M02)
                var stock = await conn.QueryFirstOrDefaultAsync<decimal>(
                    "SELECT SUM(QtyBase) FROM StockMovement WHERE ItemId = @ItemId AND CompanyId = @CompanyId AND IsCancelled = 0", 
                    new { line.ItemId, CompanyId = company.Id }, trans);

                if (stock >= line.QtyBase)
                {
                    // STOK VAR -> FIFO Rezervasyon (En eski stoklu rafı bul)
                    var fifoBin = await conn.QueryFirstOrDefaultAsync<Guid?>(@"
                        SELECT TOP 1 BinId 
                        FROM StockMovement 
                        WHERE ItemId = @ItemId AND CompanyId = @CompanyId AND WarehouseId = @WhId AND IsCancelled = 0
                        GROUP BY BinId 
                        HAVING SUM(QtyBase) > 0
                        ORDER BY MIN(CreatedAt) ASC", 
                        new { line.ItemId, CompanyId = company.Id, WhId = header.WarehouseId }, trans);

                    var targetBinId = fifoBin ?? await conn.QueryFirstOrDefaultAsync<Guid?>(
                        "SELECT TOP 1 Id FROM Bin WHERE WarehouseId = @WhId AND IsPickingArea = 1", 
                        new { WhId = header.WarehouseId }, trans);

                    await conn.ExecuteAsync(@"
                        INSERT INTO PickTaskLine (PickTaskId, ItemId, UomId, TargetWarehouseId, TargetBinId, QtyRequested, QtyRequestedBase)
                        VALUES (@TaskId, @ItemId, @UomId, @WhId, @BinId, @Qty, @QtyBase)",
                        new { TaskId = taskId, line.ItemId, line.UomId, WhId = header.WarehouseId, BinId = targetBinId, Qty = line.QtyOriginal, QtyBase = line.QtyBase }, trans);
                    
                    hasPicking = true;
                }
                else
                {
                    // STOK YOK -> Üretim Emri Tetikle (M10)
                    var prdId = Guid.NewGuid();
                    var prdDocNo = $"PRD-{line.ItemCode}-{DateTime.Now:MMdd}";
                    
                    await conn.ExecuteAsync(@"
                        INSERT INTO ProductionOrder (Id, CompanyId, DocNo, ItemId, QtyTarget, Status, SourceDocType, SourceDocId, CreatedBy)
                        VALUES (@PrdId, @CompanyId, @DocNo, @ItemId, @Qty, 'DRAFT', 'SHIPPING', @ShipmentId, @UserId)",
                        new { PrdId = prdId, CompanyId = company.Id, DocNo = prdDocNo, line.ItemId, Qty = line.QtyBase - stock, ShipmentId = id, UserId = user.Id }, trans);
                }
            }

            // Eğer hiç picking satırı oluşmadıysa (tümü üretime gittiyse) picking task'ı iptal et veya pasife çek
            if (!hasPicking)
            {
                await conn.ExecuteAsync("DELETE FROM PickTask WHERE Id = @Id", new { Id = taskId }, trans);
            }

            trans.Commit();
            return RedirectToPage(new { id });
        }
        catch 
        {
            trans.Rollback();
            throw;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        using var conn = db.Open();

        if (IsNew)
        {
            Header.Id = Guid.NewGuid();
            Header.DocNo = $"SHP-{DateTime.Now:yyyyMMdd}-{conn.ExecuteScalar<int>("SELECT COUNT(1) + 1 FROM ShippingHeader WHERE CAST(DocDate AS DATE) = CAST(GETDATE() AS DATE)"):D5}";
            
            const string sql = @"
                INSERT INTO ShippingHeader (Id, CompanyId, WarehouseId, DocNo, Status, DocDate, CarrierName, VehiclePlate, Notes, CreatedBy)
                VALUES (@Id, @CompanyId, @WarehouseId, @DocNo, 'DRAFT', @DocDate, @CarrierName, @VehiclePlate, @Notes, @UserId)";
            
            await conn.ExecuteAsync(sql, new { 
                Header.Id, CompanyId = company.Id, Header.WarehouseId,
                Header.DocNo, Header.DocDate, Header.CarrierName, Header.VehiclePlate, Header.Notes, UserId = user.Id 
            });
        }
        else
        {
            const string sql = "UPDATE ShippingHeader SET WarehouseId = @WarehouseId, CarrierName = @CarrierName, VehiclePlate = @VehiclePlate, Notes = @Notes WHERE Id = @Id";
            await conn.ExecuteAsync(sql, Header);
        }

        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, Guid uomId, decimal qty, string? lotNo, Guid soLineId)
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
            INSERT INTO ShippingLine (HeaderId, SalesOrderLineId, ItemId, UomId, QtyOriginal, QtyBase, LotNo)
            VALUES (@HeaderId, @SOLineId, @ItemId, @UomId, @Qty, @QtyBase, @LotNo)";

        await conn.ExecuteAsync(sql, new { 
            HeaderId = id, SOLineId = soLineId, ItemId = itemId, UomId = uomId, Qty = qty, QtyBase = qty * rate, LotNo = lotNo 
        });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPostAsync(Guid id)
    {
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();

        try 
        {
            var header = await conn.QueryFirstOrDefaultAsync<ShippingHeaderDto>("SELECT * FROM ShippingHeader WHERE Id = @Id", new { Id = id }, trans);
            // Sevkiyat belgesi bulunamadıysa işlemi iptal et
            if (header is null) { trans.Rollback(); return RedirectToPage(new { id }); }
            var lines = await conn.QueryAsync<ShippingLineDto>("SELECT * FROM ShippingLine WHERE HeaderId = @Id", new { Id = id }, trans);

            foreach (var line in lines)
            {
                // 1. Stock Movement (ISSUE)
                const string moveSql = @"
                    INSERT INTO StockMovement (CompanyId, WarehouseId, BinId, ItemId, MovementType, QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, LotNo, CreatedBy)
                    SELECT @CompanyId, @WarehouseId, b.Id, @ItemId, 'ISSUE', -@Qty, @UomId, @Qty, 'SHIPPING', @HeaderId, @DocNo, @LotNo, @UserId
                    FROM Bin b WHERE b.WarehouseId = @WarehouseId AND b.IsPickingArea = 1"; // Pick from picking bin
                
                await conn.ExecuteAsync(moveSql, new { 
                    CompanyId = company.Id, WarehouseId = header.WarehouseId, ItemId = line.ItemId, 
                    Qty = line.QtyBase, UomId = line.UomId, HeaderId = id, DocNo = header.DocNo, 
                    LotNo = line.LotNo, UserId = user.Id 
                }, trans);

                // 2. Update SO Line
                await conn.ExecuteAsync("UPDATE SalesOrderLine SET QtyShipped = QtyShipped + @Qty WHERE Id = @Id", new { Qty = line.QtyBase, Id = line.SalesOrderLineId }, trans);
            }

            // 3. Update Header Status
            await conn.ExecuteAsync("UPDATE ShippingHeader SET Status = 'POSTED', UpdatedAt = GETUTCDATE() WHERE Id = @Id", new { Id = id }, trans);

            // 4. Update SO Status if all shipped
            // (Simplification: check all lines of all SOs referenced in this shipment)
            // Real implementation would be more precise.

            trans.Commit();
        }
        catch 
        {
            trans.Rollback();
            throw;
        }

        return RedirectToPage(new { id });
    }

    public record ShippingHeaderDto { public Guid Id { get; set; } public Guid WarehouseId { get; set; } public string DocNo { get; set; } = ""; public string Status { get; set; } = "DRAFT"; public DateTime DocDate { get; set; } public string? CarrierName { get; set; } public string? VehiclePlate { get; set; } public string? Notes { get; set; } }
    public record ShippingLineDto { public Guid Id { get; set; } public string? ItemCode { get; set; } public string? ItemName { get; set; } public string? UomCode { get; set; } public decimal QtyOriginal { get; set; } public decimal QtyBase { get; set; } public string? LotNo { get; set; } public string? SourceOrderNo { get; set; } public Guid SalesOrderLineId { get; set; } public Guid ItemId { get; set; } public Guid UomId { get; set; } }
    public record DdlDto { public Guid Id { get; set; } public string? Code { get; set; } public string? Name { get; set; } public Guid? BaseUomId { get; set; } }
}
