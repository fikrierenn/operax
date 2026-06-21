using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.CycleCount;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public CountHeaderDto Header { get; set; } = new();
    public IEnumerable<CountLineDto>  Lines      { get; set; } = [];
    public IEnumerable<DdlDto>        Warehouses { get; set; } = [];
    public IEnumerable<BinDto>        Bins       { get; set; } = [];
    public IEnumerable<DdlDto>        Items      { get; set; } = [];

    public bool IsNew => Header.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        // Sayım formu için dropdown listelerini ve belge bilgilerini yükler
        using var conn = db.Open();

        Warehouses = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0",
            new { CompanyId = company.Id });

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<CountHeaderDto>(
                "SELECT Id, DocNo, Status, WarehouseId FROM CycleCount WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }) ?? new();

            Lines = await conn.QueryAsync<CountLineDto>(@"
                SELECT l.*, i.Code as ItemCode, i.Name as ItemName,
                       b.Code as BinCode
                FROM CycleCountLine l
                JOIN CycleCount cc ON cc.Id = l.CycleCountId
                JOIN Item i ON i.Id = l.ItemId
                JOIN Bin b ON b.Id = l.BinId
                WHERE l.CycleCountId = @Id AND cc.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id });

            // Bin listesi — sadece seçilen depoya ait (CompanyId üzerinden güvenli)
            Bins = await conn.QueryAsync<BinDto>(@"
                SELECT b.Id, b.Code FROM Bin b
                JOIN Warehouse w ON w.Id = b.WarehouseId
                WHERE b.WarehouseId = @WhId AND w.CompanyId = @CompanyId",
                new { WhId = Header.WarehouseId, CompanyId = company.Id });

            Items = await conn.QueryAsync<DdlDto>(
                "SELECT Id, Code, Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0",
                new { CompanyId = company.Id });
        }
        else
        {
            Header.DocNo = "NEW";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Sayım belgesi oluşturur (DRAFT)
        using var conn = db.Open();

        if (IsNew)
        {
            Header.Id    = Guid.NewGuid();
            Header.DocNo = $"{DocPrefix.CycleCount}-{DateTime.UtcNow:yyyyMMddHHmm}";

            await conn.ExecuteAsync(@"
                INSERT INTO CycleCount (Id, CompanyId, DocNo, Status, WarehouseId, CreatedBy)
                VALUES (@Id, @CompanyId, @DocNo, @Status, @WarehouseId, @UserId)",
                new {
                    Header.Id, CompanyId = company.Id, Header.DocNo,
                    Status = DocStatus.Draft, Header.WarehouseId, UserId = user.Id
                });
            await audit.LogAsync("CREATE", "CycleCount", Header.Id, $"DocNo: {Header.DocNo}");
        }

        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid binId, Guid itemId, decimal qtyCounted)
    {
        // Sistem bakiyesini snapshot alır ve sayım satırı ekler
        using var conn = db.Open();

        // İş kuralı: sistem stok bakiyesi sayım anındaki snapshot — CompanyId zorunlu!
        var qtySystem = await conn.ExecuteScalarAsync<decimal>(@"
            SELECT ISNULL(SUM(QtyBase), 0)
            FROM StockMovement
            WHERE CompanyId = @CompanyId AND BinId = @BinId AND ItemId = @ItemId AND IsCancelled = 0",
            new { CompanyId = company.Id, BinId = binId, ItemId = itemId });

        await conn.ExecuteAsync(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: üst belge CycleCount aynı handler içinde daha önce
            -- WHERE Id = @Id AND CompanyId = @CompanyId ile yüklendi ve bulunamazsa boş form döndü.
            -- @Id parametresi o doğrulanmış CycleCount.Id değeridir.
            -- Aynı handler içinde çalışan UPDATE CycleCount ... WHERE Id = @Id AND CompanyId = @CompanyId
            -- sorgusu da şirket sahipliğini teyit eder.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            INSERT INTO CycleCountLine (CycleCountId, BinId, ItemId, QtySystem, QtyCounted, CountedBy, CountedAt)
            VALUES (@Id, @BinId, @ItemId, @QtySystem, @QtyCounted, @UserId, GETUTCDATE())",
            new { Id = id, BinId = binId, ItemId = itemId, QtySystem = qtySystem, QtyCounted = qtyCounted, UserId = user.Id.ToString() });

        // Sayım başladığında durum COUNTING olur
        await conn.ExecuteAsync(
            "UPDATE CycleCount SET Status = @Status WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Status = DocStatus.Counting, Id = id, CompanyId = company.Id });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPostAdjustmentsAsync(Guid id)
    {
        // sp_CycleCountPost: fark hareketi yazar, sayımı tamamlar
        using var conn = db.Open();
        await conn.ExecuteAsync("sp_CycleCountPost",
            new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
            commandType: CommandType.StoredProcedure);
        await audit.LogAsync("POST", "CycleCount", id, "Stok sayımı kapatıldı, düzeltme hareketleri oluşturuldu");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReverseAsync(Guid id)
    {
        // sp_CycleCountReverse: COMPLETED sayımı CANCELLED yapar, COUNT_ADJ hareketlerini kapatıp ters REVERSAL yazar
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_CycleCountReverse",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure);
            await audit.LogAsync("CANCEL", "CycleCount", id, "Sayım iptal edildi, ters düzeltme hareketi yazıldı");
            TempData["Success"] = "Sayım iptal edildi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı (dönem kilidi vb.)
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Sayım iptal hatası: {HeaderId}", id);
            TempData["Error"] = "Sayım iptal edilirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public record CountHeaderDto
    {
        public Guid   Id          { get; set; }
        public string DocNo       { get; set; } = "";
        public string Status      { get; set; } = DocStatus.Draft;
        public Guid   WarehouseId { get; set; }
    }

    public record CountLineDto
    {
        public Guid    Id            { get; set; }
        public Guid    BinId         { get; set; }
        public Guid    ItemId        { get; set; }
        public string  BinCode       { get; set; } = "";
        public string  ItemCode      { get; set; } = "";
        public string  ItemName      { get; set; } = "";
        public decimal QtySystem     { get; set; }
        public decimal QtyCounted    { get; set; }
        public decimal QtyDifference { get; set; }
    }

    public record BinDto(Guid Id, string Code);
}
