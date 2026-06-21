using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Picking;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, UserManager<IdentityUser> userManager) : PageModel
{
    public PickTaskDto              Task           { get; set; } = new();
    public IEnumerable<PickLineDto> Lines          { get; set; } = [];
    public IEnumerable<IdentityUser> WarehouseStaff { get; set; } = [];

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
            WHERE l.PickTaskId = @Id AND pt.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct));

        // Depo personeli: Warehouse rolü tercih edilir
        WarehouseStaff = await userManager.GetUsersInRoleAsync("Warehouse");
        if (!WarehouseStaff.Any())
            WarehouseStaff = userManager.Users.Take(10).ToList();
    }

    public async Task<IActionResult> OnPostAssignAsync(Guid id, string userId, CancellationToken ct)
    {
        // Görevi personele atar, durum ASSIGNED olur
        using var conn = db.Open();
        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE PickTask SET AssignedUserId = @UserId, Status = @Status WHERE Id = @Id AND CompanyId = @CompanyId",
                new { UserId = userId, Status = DocStatus.Assigned, Id = id, CompanyId = company.Id },
                cancellationToken: ct));
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPickLineAsync(Guid id, Guid lineId, decimal qty, CancellationToken ct)
    {
        // sp_PickLinePost: satırı günceller, stok hareketi yazar, görev durumunu günceller
        var userId = userManager.GetUserId(User) ?? "";
        using var conn = db.Open();
        await conn.ExecuteAsync(
            new CommandDefinition("sp_PickLinePost",
                new { LineId = lineId, TaskId = id, Qty = qty, CompanyId = company.Id, UserId = userId },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
        return RedirectToPage(new { id });
    }

    public record PickTaskDto
    {
        public Guid    Id               { get; set; }
        public string  DocNo            { get; set; } = "";
        public string  Status           { get; set; } = DocStatus.Draft;
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
