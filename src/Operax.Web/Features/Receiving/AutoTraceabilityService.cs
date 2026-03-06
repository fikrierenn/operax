using Dapper;
using Operax.Web.Lib; // Db bağlantı fabrikası

namespace Operax.Web.Features.Receiving;

public class AutoTraceabilityService(Db db)
{
    public async Task<string> GenerateLotAsync(Guid itemId)
    {
        using var conn = db.Open();
        var item = await conn.QueryFirstOrDefaultAsync("SELECT LotPrefix FROM Item WHERE Id = @Id", new { Id = itemId });
        string prefix = item?.LotPrefix ?? "LOT";
        return $"{prefix}-{DateTime.Now:yyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
    }

    public async Task<string> GenerateSerialAsync(Guid itemId)
    {
        using var conn = db.Open();
        var item = await conn.QueryFirstOrDefaultAsync("SELECT SerialPrefix FROM Item WHERE Id = @Id", new { Id = itemId });
        string prefix = item?.SerialPrefix ?? "SN";
        return $"{prefix}-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
    }

    public async Task EnqueueLabelPrintAsync(string code, string type, string itemName)
    {
        // Gerçek dünyada burası bir print queue tablosuna yazar 
        // veya Zebra/ZPL kütüphanesine gönderir.
        // Simülasyon: Log'a yazıyoruz.
        System.Diagnostics.Debug.WriteLine($"[LABEL PRINT] Type: {type}, Code: {code}, Item: {itemName}");
    }
}
