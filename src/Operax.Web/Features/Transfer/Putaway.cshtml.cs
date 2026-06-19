using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Transfer;

[Authorize]
public class PutawayModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<PutawayModel> logger) : PageModel
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
              AND b.IsPickingArea = 1
              AND b.IsActive = 1",
            new { CompanyId = company.Id });
    }

    public async Task<IActionResult> OnPostPutawayAsync(Guid itemId, Guid fromBinId, Guid toBinId, decimal qty)
    {
        // Mal kabul rafından depolama rafına yerleme — iş mantığı SP'de (SQL-First).
        // sp_PutawayPost: dönem kilidi + negatif stok engeli + atomik bin-to-bin transfer.
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_PutawayPost",
                new { ItemId = itemId, FromBinId = fromBinId, ToBinId = toBinId, Qty = qty, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure);
            TempData["Success"] = "Yerleme tamamlandı.";
        }
        catch (SqlException sqlEx) when (sqlEx.Number is >= 50000 and < 60000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı, kullanıcıya göster
            TempData["Error"] = sqlEx.Message;
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Putaway SQL hatası");
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
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
