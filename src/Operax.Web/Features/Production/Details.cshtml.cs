using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Production;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, ILogger<DetailsModel> logger) : PageModel
{
    public ProductionOrderDto           Order            { get; set; } = new();
    public IEnumerable<ProductionLineDto> Lines          { get; set; } = [];
    public Guid?                        ActivePickTaskId { get; set; }

    public async Task OnGetAsync(Guid id, CancellationToken ct)
    {
        // Üretim emri detaylarını ve hammadde ihtiyaç listesini yükler
        using var conn = db.Open();

        Order = await conn.QueryFirstOrDefaultAsync<ProductionOrderDto>(new CommandDefinition(@"
            SELECT p.*, i.Code as ItemCode, i.Name as ItemName
            FROM ProductionOrder p
            JOIN Item i ON i.Id = p.ItemId
            WHERE p.Id = @Id AND p.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct)) ?? new();

        Lines = await conn.QueryAsync<ProductionLineDto>(new CommandDefinition(@"
            SELECT l.*, i.Code as ItemCode, i.Name as ItemName, dv.Code as UomCode
            FROM ProductionOrderLine l
            JOIN ProductionOrder po ON po.Id = l.ProductionOrderId
            JOIN Item i ON i.Id = l.ItemId
            JOIN DictionaryValue dv ON dv.Id = i.BaseUomId
            WHERE l.ProductionOrderId = @Id AND po.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct));

        // Aktif hammadde toplama görevi (PRD-PCK- prefix'li)
        ActivePickTaskId = await conn.QueryFirstOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT Id FROM PickTask WHERE SourceDocId = @Id AND CompanyId = @CompanyId AND DocNo LIKE 'PRD-PCK%'",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct));
    }

    public async Task<IActionResult> OnPostLoadBOMAsync(Guid id, CancellationToken ct)
    {
        // sp_ProductionLoadBOM: BOM'dan hammadde ihtiyaçlarını üretim emrine yükler
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_ProductionLoadBOM",
                new { OrderId = id, CompanyId = company.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
            TempData["Success"] = "Reçete başarıyla yüklendi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "BOM yükleme SP hatası: {OrderId}", id);
            TempData["Error"] = "İşlem sırasında veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreatePickTaskAsync(Guid id, CancellationToken ct)
    {
        // sp_ProductionCreatePickTask: hammadde toplama görevi oluşturur
        using var conn = db.Open();
        try
        {
            var result = await conn.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition("sp_ProductionCreatePickTask",
                new { OrderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));

            TempData["Success"] = "Hammadde toplama görevi oluşturuldu.";

            // SP oluşturulan TaskId'yi döner; picking sayfasına yönlendir
            if (result?.TaskId is Guid taskId)
                return RedirectToPage("/Picking/Details", new { id = taskId });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Toplama görevi oluşturma SP hatası: {OrderId}", id);
            TempData["Error"] = "İşlem sırasında veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostFinishAsync(Guid id, decimal qty, CancellationToken ct)
    {
        // sp_ProductionFinish: mamul stoğa girer, emri COMPLETED yapar
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_ProductionFinish",
                new { OrderId = id, CompanyId = company.Id, Qty = qty, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
            TempData["Success"] = "Üretim tamamlandı, mamul stoğa girdi.";
            return RedirectToPage("./Index");
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Üretim tamamlama SP hatası: {OrderId}", id);
            TempData["Error"] = "İşlem sırasında veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReverseAsync(Guid id, CancellationToken ct)
    {
        // sp_ProductionReverse: COMPLETED üretim emrini CANCELLED yapar, hammadde sarfı (ISSUE) ve
        // mamul girişi (RECEIPT) hareketlerini kapatıp ters REVERSAL yazar
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_ProductionReverse",
                new { OrderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
            await audit.LogAsync("CANCEL", "ProductionOrder", id, "Üretim emri iptal edildi, ters stok hareketi yazıldı");
            TempData["Success"] = "Üretim emri iptal edildi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı (dönem kilidi vb.)
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Üretim emri iptal hatası: {OrderId}", id);
            TempData["Error"] = "Üretim emri iptal edilirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
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
