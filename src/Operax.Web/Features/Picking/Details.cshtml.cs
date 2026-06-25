using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Picking;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, UserManager<IdentityUser> userManager,
    ILogger<DetailsModel> logger) : PageModel
{
    public PickTaskDto              Task           { get; set; } = new();
    public IEnumerable<PickLineDto> Lines          { get; set; } = [];
    public IEnumerable<StaffDto> WarehouseStaff { get; set; } = [];

    public async Task OnGetAsync(Guid id, CancellationToken ct)
    {
        // Pick task detayları ve satırlarını yükler
        using var conn = db.Open();

        Task = await conn.QueryFirstOrDefaultAsync<PickTaskDto>(
            new CommandDefinition(@"
            SELECT t.*, u.UserName as AssignedUserName
            FROM PickTask t
            LEFT JOIN AspNetUsers u ON u.Id = t.AssignedUserId
            WHERE t.Id = @Id AND t.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct)) ?? new();

        Lines = await conn.QueryAsync<PickLineDto>(
            new CommandDefinition(@"
            SELECT l.*, i.Code as ItemCode, i.Name as ItemName,
                   tb.Code as TargetBinCode, pb.Code as PickedBinCode,
                   u.UserName as PickedByUserName
            FROM PickTaskLine l
            JOIN PickTask pt ON pt.Id = l.PickTaskId
            JOIN Item i ON i.Id = l.ItemId
            JOIN Bin tb ON tb.Id = l.TargetBinId
            LEFT JOIN Bin pb ON pb.Id = l.PickedBinId
            LEFT JOIN AspNetUsers u ON u.Id = l.PickedByUserId
            WHERE l.PickTaskId = @Id AND pt.CompanyId = @CompanyId
            ORDER BY l.PickSeq, tb.Zone, tb.SortNo, l.Id",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct));

        // Depo personeli: WarehouseManager rolü öncelikli, yoksa diğer kullanıcılar. Dapper ile —
        // DapperUserStore IQueryableUserStore implement etmez → userManager.Users patlıyordu
        // (NotSupportedException, Draft/Assigned task açılınca 500). OUTER APPLY ile tek satır/kullanıcı (dup yok).
        // Rol adı Roles.* sabitinden parametre ile geçer (magic-string yok; 'Warehouse' literal'i DB'de yok → sessizce eşleşmiyordu).
        WarehouseStaff = (await conn.QueryAsync<StaffDto>(new CommandDefinition(@"
            SELECT TOP 10 u.Id, u.UserName
            FROM AspNetUsers u
            OUTER APPLY (
                SELECT TOP 1 1 AS IsWh FROM AspNetUserRoles ur
                JOIN AspNetRoles r ON r.Id = ur.RoleId
                WHERE ur.UserId = u.Id AND r.Name = @RoleName
            ) w
            ORDER BY CASE WHEN w.IsWh = 1 THEN 0 ELSE 1 END, u.UserName",
            new { RoleName = Roles.WarehouseManager },
            cancellationToken: ct))).ToList();
    }

    public async Task<IActionResult> OnPostAssignAsync(Guid id, string userId, CancellationToken ct)
    {
        // Görevi personele atar, durum ASSIGNED olur (PickTask statüsü → PickTaskStatus sabiti)
        using var conn = db.Open();
        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE PickTask SET AssignedUserId = @UserId, Status = @Status WHERE Id = @Id AND CompanyId = @CompanyId",
                new { UserId = userId, Status = PickTaskStatus.Assigned, Id = id, CompanyId = company.Id },
                cancellationToken: ct));
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPickLineAsync(Guid id, Guid lineId, decimal qty, CancellationToken ct)
    {
        // sp_PickLinePost: satırı günceller, görev durumunu günceller (stok yazmaz — ledger sp_ShippingPost'ta)
        var userId = userManager.GetUserId(User) ?? "";
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(
                new CommandDefinition("sp_PickLinePost",
                    new { LineId = lineId, TaskId = id, Qty = qty, CompanyId = company.Id, UserId = userId },
                    commandType: CommandType.StoredProcedure, cancellationToken: ct));
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası (miktar/satır) — SP Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Sistem hatası — log'a yaz, kullanıcıya generic mesaj (connection string sızdırma)
            logger.LogError(sqlEx, "sp_PickLinePost SQL hatası: {LineId}", lineId);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    // sp_PickTaskCancel (Plan 56 IMP-3): görevi iptal eder; bağlı sevkiyat PICKING'te + başka aktif görev
    // yoksa DRAFT'a döner. Pick stok yazmaz → ledger dokunulmaz. Tamamlanmış/iptal görev reddedilir.
    public async Task<IActionResult> OnPostCancelTaskAsync(Guid id, CancellationToken ct)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(
                new CommandDefinition("sp_PickTaskCancel",
                    new { TaskId = id, CompanyId = company.Id, UserId = userManager.GetUserId(User) ?? "" },
                    commandType: CommandType.StoredProcedure, cancellationToken: ct));
            TempData["Success"] = "Toplama görevi iptal edildi.";
            return RedirectToPage("./Index");
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası (tamamlanmış/zaten iptal) — SP Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
            return RedirectToPage(new { id });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Sistem hatası — log'a yaz, kullanıcıya generic mesaj
            logger.LogError(sqlEx, "sp_PickTaskCancel SQL hatası: {TaskId}", id);
            TempData["Error"] = "Veritabanı hatası oluştu.";
            return RedirectToPage(new { id });
        }
    }

    public record StaffDto(string Id, string? UserName);

    public record PickTaskDto
    {
        public Guid    Id               { get; set; }
        public string  DocNo            { get; set; } = "";
        public string  Status           { get; set; } = PickTaskStatus.Draft;
        public string? AssignedUserId   { get; set; }
        public string? AssignedUserName { get; set; }
    }

    public record PickLineDto
    {
        public Guid      Id                { get; set; }
        public string    ItemCode          { get; set; } = "";
        public string    ItemName          { get; set; } = "";
        public string    TargetBinCode     { get; set; } = "";
        public string?   PickedBinCode     { get; set; }
        public decimal   QtyRequested      { get; set; }
        public decimal   QtyPicked         { get; set; }
        public string?   PickedByUserName  { get; set; }
        public DateTime? PickedAt          { get; set; }
    }
}
