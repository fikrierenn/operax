using Dapper;
using Operax.Web.Lib; // Db bağlantı fabrikası

namespace Operax.Web.Features.Receiving;

public class AutoTraceabilityService(Db db)
{
    // Lot numarası üretir — şirket izolasyonu için companyId zorunlu
    public async Task<string> GenerateLotAsync(Guid itemId, Guid companyId)
    {
        using var conn = db.Open();
        var item = await conn.QueryFirstOrDefaultAsync(
            "SELECT LotPrefix FROM Item WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = itemId, CompanyId = companyId });
        string prefix = item?.LotPrefix ?? "LOT";
        return $"{prefix}-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
    }

    // Seri numarası üretir — şirket izolasyonu için companyId zorunlu
    public async Task<string> GenerateSerialAsync(Guid itemId, Guid companyId)
    {
        using var conn = db.Open();
        var item = await conn.QueryFirstOrDefaultAsync(
            "SELECT SerialPrefix FROM Item WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = itemId, CompanyId = companyId });
        string prefix = item?.SerialPrefix ?? "SN";
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
    }

    public async Task EnqueueLabelPrintAsync(string code, string type, string itemName)
    {
        // Gerçek dünyada burası bir print queue tablosuna yazar 
        // veya Zebra/ZPL kütüphanesine gönderir.
        // Simülasyon: Log'a yazıyoruz.
        System.Diagnostics.Debug.WriteLine($"[LABEL PRINT] Type: {type}, Code: {code}, Item: {itemName}");
    }
}
