using Dapper;
using System.Data;

namespace Operax.Web.Lib.Jobs;

/// <summary>
/// Cari mutabakat sessiz onay job'ı (Plan 19 Faz 6 — TTK md.94).
/// Süresi dolmuş (1 ay), ispatlı kanaldan (KEP/NOTER) gönderilmiş, yanıtsız
/// mutabakatları EXPIRED_CONFIRMED yapar. Hangfire ile günlük çalışır.
/// </summary>
public sealed class ReconciliationExpiryJob(Db db, ILogger<ReconciliationExpiryJob> logger)
{
    // Hangfire recurring giriş noktası — sp_ExpireReconciliations çağırır
    public async Task RunAsync()
    {
        try
        {
            using var conn = db.Open();
            await conn.ExecuteAsync("sp_ExpireReconciliations",
                commandType: CommandType.StoredProcedure);
            logger.LogInformation("Mutabakat sessiz onay job'ı tamamlandı.");
        }
        catch (Exception ex)
        {
            // İş kuralı: job hatası loglanır, Hangfire retry'a bırakılır (sessiz yutma yok)
            logger.LogError(ex, "Mutabakat sessiz onay job'ı başarısız.");
            throw;
        }
    }
}
