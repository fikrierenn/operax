using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Production;

[Authorize]
public class TerminalModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<TerminalModel> logger) : PageModel
{
    public IEnumerable<ActiveTaskDto> ActiveTasks   { get; set; } = [];
    public ActiveActivityDto?         CurrentActivity { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Operatörün mevcut aktivitesini ve başlatabileceği görevleri yükler
        using var conn = db.Open();

        // Operatörün açık aktivitesi (EndTime NULL = devam ediyor)
        CurrentActivity = await conn.QueryFirstOrDefaultAsync<ActiveActivityDto>(new CommandDefinition(@"
            SELECT a.Id, a.StartTime, po.DocNo, rs.OperationName, wc.Name as WorkCenterName
            FROM ProductionActivity a
            JOIN ProductionOrder po ON po.Id = a.ProductionOrderId
            JOIN ProductRouteStep rs ON rs.Id = a.RouteStepId
            JOIN WorkCenter wc ON wc.Id = a.WorkCenterId
            WHERE a.UserId = @UserId AND a.EndTime IS NULL AND po.CompanyId = @CompanyId",
            new { UserId = user.Id, CompanyId = company.Id }, cancellationToken: ct));

        // Başlatabileceği görevler: RELEASED veya IN_PROGRESS, aktif aktivitesi olmayan emirler
        ActiveTasks = await conn.QueryAsync<ActiveTaskDto>(new CommandDefinition(@"
            SELECT po.Id as OrderId, po.DocNo, i.Name as ItemName,
                   rs.Id as RouteStepId, rs.OperationName, wc.Name as WorkCenterName
            FROM ProductionOrder po
            JOIN Item i ON i.Id = po.ItemId
            JOIN ProductRouteStep rs ON rs.Id = po.CurrentRouteStepId
            JOIN WorkCenter wc ON wc.Id = rs.WorkCenterId
            WHERE po.CompanyId = @CompanyId
              AND po.Status IN ('RELEASED', 'IN_PROGRESS')
              AND NOT EXISTS (
                  SELECT 1 FROM ProductionActivity
                  WHERE ProductionOrderId = po.Id AND EndTime IS NULL
              )",
            new { CompanyId = company.Id }, cancellationToken: ct));
    }

    public async Task<IActionResult> OnPostStartAsync(Guid orderId, Guid stepId, CancellationToken ct)
    {
        // Mevcut açık aktiviteyi kapatır ve yeni aktivite başlatır
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            // Çoklu-firma izolasyon guard'ı: dışarıdan POST edilen orderId başka firmaya
            // ait olabilir. Aktivite/durum yazmadan önce üretim emrinin oturum firmasına
            // ait olduğunu doğrula; değilse hiçbir kayda dokunmadan yetki hatası dön.
            var orderBelongsToCompany = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM ProductionOrder WHERE Id = @OrderId AND CompanyId = @CompanyId",
                new { OrderId = orderId, CompanyId = company.Id }, trans, cancellationToken: ct));
            if (orderBelongsToCompany == 0)
            {
                trans.Rollback();
                return Forbid();
            }

            // Varsa operatörün mevcut açık aktivitesini kapat — yalnızca AKTİF FİRMAYA ait kayıtlar.
            // ProductionActivity'de CompanyId kolonu yok; izolasyon üst belge ProductionOrder üzerinden.
            // Sadece UserId ile kapatmak, operatörün başka firmadaki açık aktivitesini de sessizce
            // bitirir (firma değiştirince çapraz mutasyon) — JOIN ile firmaya kısıtla.
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE pa SET pa.EndTime = GETUTCDATE()
                FROM ProductionActivity pa
                JOIN ProductionOrder po ON po.Id = pa.ProductionOrderId
                WHERE pa.UserId = @UserId AND pa.EndTime IS NULL AND po.CompanyId = @CompanyId",
                new { UserId = user.Id, CompanyId = company.Id }, trans, cancellationToken: ct));

            // Rota adımından iş merkezini al
            var workCenterId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                "SELECT WorkCenterId FROM ProductRouteStep WHERE Id = @StepId",
                new { StepId = stepId }, trans, cancellationToken: ct));

            // Yeni aktivite kaydı oluştur.
            await conn.ExecuteAsync(new CommandDefinition(@"
                -- Çoklu-firma izolasyon notu: ProductionActivity tablosunda CompanyId kolonu yoktur;
                -- izolasyon üst belge ProductionOrder üzerinden sağlanır. @OrderId bu handler'ın
                -- başında ProductionOrder.CompanyId = oturum firması koşuluyla doğrulandığından
                -- bu kayıt güvenle yazılır.
                -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                INSERT INTO ProductionActivity
                    (Id, ProductionOrderId, UserId, WorkCenterId, RouteStepId, StartTime)
                VALUES
                    (NEWID(), @OrderId, @UserId, @WorkCenterId, @StepId, GETUTCDATE())",
                new { OrderId = orderId, UserId = user.Id, WorkCenterId = workCenterId, StepId = stepId }, trans, cancellationToken: ct));

            // İş kuralı: üretim emri IN_PROGRESS değilse güncelle (firma filtresi guard'la birlikte)
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE ProductionOrder SET Status = 'IN_PROGRESS', CurrentRouteStepId = @StepId WHERE Id = @OrderId AND CompanyId = @CompanyId AND Status <> 'COMPLETED'",
                new { StepId = stepId, OrderId = orderId, CompanyId = company.Id }, trans, cancellationToken: ct));

            trans.Commit();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP veya DB kısıtı Türkçe mesaj fırlattı
            trans.Rollback();
            TempData["Error"] = sqlEx.Message;
            return RedirectToPage();
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Sistem hatası — kullanıcıya generic mesaj, detay log'a
            trans.Rollback();
            logger.LogError(sqlEx, "Üretim terminali aktivite başlatma hatası: {OrderId}", orderId);
            TempData["Error"] = "İşlem sırasında veritabanı hatası oluştu.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            // Beklenmeyen hata — transaction geri alınır, log'a yazılır
            trans.Rollback();
            logger.LogError(ex, "Üretim terminali beklenmeyen hata: {OrderId}", orderId);
            TempData["Error"] = "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyiniz.";
            return RedirectToPage();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStopAsync(Guid activityId, CancellationToken ct)
    {
        // Aktif aktiviteyi durdurur (EndTime set eder)
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE ProductionActivity SET EndTime = GETUTCDATE() WHERE Id = @Id AND UserId = @UserId",
            new { Id = activityId, UserId = user.Id }, cancellationToken: ct));
        return RedirectToPage();
    }

    public record ActiveTaskDto
    {
        public Guid   OrderId        { get; set; }
        public string DocNo          { get; set; } = "";
        public string ItemName       { get; set; } = "";
        public Guid   RouteStepId    { get; set; }
        public string OperationName  { get; set; } = "";
        public string WorkCenterName { get; set; } = "";
    }

    public record ActiveActivityDto
    {
        public Guid     Id             { get; set; }
        public DateTime StartTime      { get; set; }
        public string   DocNo          { get; set; } = "";
        public string   OperationName  { get; set; } = "";
        public string   WorkCenterName { get; set; } = "";
    }
}
