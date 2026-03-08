using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Production;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    public ProductionOrderDto           Order            { get; set; } = new();
    public IEnumerable<ProductionLineDto> Lines          { get; set; } = [];
    public Guid?                        ActivePickTaskId { get; set; }

    public async Task OnGetAsync(Guid id)
    {
        // Üretim emri detaylarını ve hammadde ihtiyaç listesini yükler
        using var conn = db.Open();

        Order = await conn.QueryFirstOrDefaultAsync<ProductionOrderDto>(@"
            SELECT p.*, i.Code as ItemCode, i.Name as ItemName
            FROM ProductionOrder p
            JOIN Item i ON i.Id = p.ItemId
            WHERE p.Id = @Id AND p.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }) ?? new();

        Lines = await conn.QueryAsync<ProductionLineDto>(@"
            SELECT l.*, i.Code as ItemCode, i.Name as ItemName, dv.Code as UomCode
            FROM ProductionOrderLine l
            JOIN ProductionOrder po ON po.Id = l.ProductionOrderId
            JOIN Item i ON i.Id = l.ItemId
            JOIN DictionaryValue dv ON dv.Id = i.BaseUomId
            WHERE l.ProductionOrderId = @Id AND po.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id });

        // Aktif hammadde toplama görevi (PRD-PCK- prefix'li)
        ActivePickTaskId = await conn.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT Id FROM PickTask WHERE SourceDocId = @Id AND CompanyId = @CompanyId AND DocNo LIKE 'PRD-PCK%'",
            new { Id = id, CompanyId = company.Id });
    }

    public async Task<IActionResult> OnPostLoadBOMAsync(Guid id)
    {
        // sp_ProductionLoadBOM: BOM'dan hammadde ihtiyaçlarını üretim emrine yükler
        using var conn = db.Open();
        await conn.ExecuteAsync("sp_ProductionLoadBOM",
            new { OrderId = id, CompanyId = company.Id },
            commandType: CommandType.StoredProcedure);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreatePickTaskAsync(Guid id)
    {
        // sp_ProductionCreatePickTask: hammadde toplama görevi oluşturur
        using var conn = db.Open();
        var result = await conn.QueryFirstOrDefaultAsync<dynamic>("sp_ProductionCreatePickTask",
            new { OrderId = id, CompanyId = company.Id, UserId = user.Id },
            commandType: CommandType.StoredProcedure);

        // SP oluşturulan TaskId'yi döner; picking sayfasına yönlendir
        if (result?.TaskId is Guid taskId)
            return RedirectToPage("/Picking/Details", new { id = taskId });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostFinishAsync(Guid id, decimal qty)
    {
        // sp_ProductionFinish: mamul stoğa girer, emri COMPLETED yapar
        using var conn = db.Open();
        await conn.ExecuteAsync("sp_ProductionFinish",
            new { OrderId = id, CompanyId = company.Id, Qty = qty, UserId = user.Id },
            commandType: CommandType.StoredProcedure);
        return RedirectToPage("./Index");
    }

    public record ProductionOrderDto
    {
        public Guid    Id               { get; set; }
        public string  DocNo            { get; set; } = "";
        public Guid    ItemId           { get; set; }
        public string  ItemCode         { get; set; } = "";
        public string  ItemName         { get; set; } = "";
        public decimal QtyTarget        { get; set; }
        public decimal QtyProduced      { get; set; }
        public string  Status           { get; set; } = DocStatus.Draft;
        public Guid?   TargetWarehouseId { get; set; }
        public Guid?   TargetBinId      { get; set; }
    }

    public record ProductionLineDto
    {
        public Guid    Id          { get; set; }
        public Guid    ItemId      { get; set; }
        public Guid    UomId       { get; set; }
        public string  ItemCode    { get; set; } = "";
        public string  ItemName    { get; set; } = "";
        public string  UomCode     { get; set; } = "";
        public decimal QtyRequired { get; set; }
        public decimal QtyIssued   { get; set; }
    }
}
