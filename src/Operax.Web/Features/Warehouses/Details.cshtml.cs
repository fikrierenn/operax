using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Warehouses;

public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty]
    public WarehouseDto Warehouse { get; set; } = new();
    public IEnumerable<BinDto> Bins { get; set; } = [];

    public bool IsNew => Warehouse.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        if (id.HasValue)
        {
            using var conn = db.Open();
            Warehouse = await conn.QueryFirstOrDefaultAsync<WarehouseDto>(
                "SELECT * FROM Warehouse WHERE Id = @Id", new { Id = id }) ?? new();
            
            Bins = await conn.QueryAsync<BinDto>(
                "SELECT * FROM Bin WHERE WarehouseId = @Id AND IsDeleted = 0 ORDER BY Code", 
                new { Id = id });
        }
        else
        {
            Warehouse.IsActive = true;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        using var conn = db.Open();

        if (IsNew)
        {
            Warehouse.Id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO Warehouse (Id, CompanyId, Code, Name, IsActive)
                VALUES (@Id, @CompanyId, @Code, @Name, @IsActive)";
            await conn.ExecuteAsync(sql, new { Warehouse.Id, CompanyId = company.Id, Warehouse.Code, Warehouse.Name, Warehouse.IsActive });
        }
        else
        {
            const string sql = "UPDATE Warehouse SET Code = @Code, Name = @Name, IsActive = @IsActive WHERE Id = @Id";
            await conn.ExecuteAsync(sql, Warehouse);
        }

        return RedirectToPage(new { id = Warehouse.Id });
    }

    public async Task<IActionResult> OnPostAddBinAsync(Guid id, string code, string? zone, bool isPicking, bool isReceiving)
    {
        using var conn = db.Open();
        const string sql = @"
            INSERT INTO Bin (WarehouseId, Code, Zone, IsPickingArea, IsReceivingArea)
            VALUES (@WarehouseId, @Code, @Zone, @IsPicking, @IsReceiving)";
        
        await conn.ExecuteAsync(sql, new { WarehouseId = id, Code = code, Zone = zone, IsPicking = isPicking, IsReceiving = isReceiving });
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteBinAsync(Guid id, Guid binId)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync("UPDATE Bin SET IsDeleted = 1 WHERE Id = @Id", new { Id = binId });
        return RedirectToPage(new { id });
    }

    public record WarehouseDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public bool IsActive { get; set; } }
    public record BinDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string? Zone { get; set; } public bool IsPickingArea { get; set; } public bool IsReceivingArea { get; set; } }
}
