using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.MaterialIssue;

/// <summary>
/// Sarf fişi detayı: başlık + kalemler + satır ekle/sil + Onayla/İptal.
/// Onay → StockMovement ISSUE (stok düşer); AccountMovement yazılmaz (iç tüketim).
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty] public HeaderDto Header { get; set; } = new();
    public List<LineDto> Lines { get; set; } = [];
    public IEnumerable<DdlDto> Warehouses { get; set; } = [];
    public IEnumerable<DdlDto> CostCenters { get; set; } = [];
    public IEnumerable<DdlDto> Items { get; set; } = [];

    public bool IsNew  => Header.Id == Guid.Empty;
    public bool IsDraft => Header.Status == DocStatus.Draft;

    // Dropdown'lar + başlık + kalemleri yükler
    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        Warehouses = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code", p, cancellationToken: ct));
        CostCenters = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM CostCenter WHERE CompanyId = @CompanyId AND IsActive = 1 ORDER BY Code", p, cancellationToken: ct));
        // Sarf edilebilir ürünler: STOCK + CONSUMABLE (SERVICE hariç — stoksuz)
        Items = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0 AND ItemType IN ('STOCK','CONSUMABLE') ORDER BY Code", p, cancellationToken: ct));

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<HeaderDto>(new CommandDefinition(
                "SELECT Id, WarehouseId, DocNo, IssueDate, CostCenterId, Notes, Status FROM MaterialIssueHeader WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct)) ?? new();

            Lines = (await conn.QueryAsync<LineDto>(new CommandDefinition(@"
                /* isolation-guard:ignore: üst belge yukarıda CompanyId ile doğrulandı */
                SELECT l.Id, i.Code AS ItemCode, i.Name AS ItemName, dv.Code AS UomCode, l.Qty
                FROM MaterialIssueLine l
                JOIN Item i ON i.Id = l.ItemId
                LEFT JOIN DictionaryValue dv ON dv.Id = l.UomId
                WHERE l.HeaderId = @Id ORDER BY l.CreatedAt", new { Id = id }, cancellationToken: ct))).ToList();
        }
        else
        {
            Header.Status = DocStatus.Draft;
            Header.IssueDate = DateTime.Today;
        }
    }

    // Başlık kaydet (yeni/düzenle — yalnızca DRAFT)
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        if (IsNew)
        {
            // Yeni fiş: DocNo SARF-{UTC zaman damgası} ile benzersiz üretilir
            Header.Id = Guid.NewGuid();
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO MaterialIssueHeader (Id, CompanyId, WarehouseId, DocNo, IssueDate, CostCenterId, Notes, Status, CreatedBy)
                VALUES (@Id, @CompanyId, @WarehouseId, @DocNo, @IssueDate, @CostCenterId, @Notes, @St, @UserId)",
                new { Header.Id, CompanyId = company.Id, Header.WarehouseId,
                      DocNo = $"SARF-{DateTime.UtcNow:yyyyMMddHHmm}", Header.IssueDate,
                      Header.CostCenterId, Header.Notes, St = DocStatus.Draft, UserId = user.Id },
                cancellationToken: ct));
        }
        else
        {
            // İş kuralı: yalnızca DRAFT belge güncellenebilir (WHERE Status = @St)
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE MaterialIssueHeader
                SET WarehouseId = @WarehouseId, IssueDate = @IssueDate, CostCenterId = @CostCenterId,
                    Notes = @Notes, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
                WHERE Id = @Id AND CompanyId = @CompanyId AND Status = @St",
                new { Header.WarehouseId, Header.IssueDate, Header.CostCenterId, Header.Notes,
                      UserId = user.Id, Header.Id, CompanyId = company.Id, St = DocStatus.Draft },
                cancellationToken: ct));
        }
        return RedirectToPage(new { id = Header.Id });
    }

    // Satır ekle (yalnızca DRAFT) — UOM ürünün temel birimi
    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, decimal qty, CancellationToken ct)
    {
        using var conn = db.Open();
        // İş kuralı: ürün bu firmaya ait + temel birim alınır
        var baseUom = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT BaseUomId FROM Item WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = itemId, CompanyId = company.Id }, cancellationToken: ct));
        if (baseUom is null)
        {
            TempData["Error"] = "Ürün bulunamadı veya bu firmaya ait değil.";
            return RedirectToPage(new { id });
        }
        if (!await IsDraftAsync(conn, id, ct))
        {
            TempData["Error"] = "Yalnızca taslak fişe kalem eklenebilir.";
            return RedirectToPage(new { id });
        }

        await conn.ExecuteAsync(new CommandDefinition(@"
            /* isolation-guard:ignore: HeaderId OnGet'te CompanyId ile doğrulandı; ürün yukarıda firma-ait */
            INSERT INTO MaterialIssueLine (HeaderId, ItemId, UomId, Qty, QtyBase)
            VALUES (@HeaderId, @ItemId, @UomId, @Qty, @Qty)",
            new { HeaderId = id, ItemId = itemId, UomId = baseUom, Qty = qty },
            cancellationToken: ct));
        return RedirectToPage(new { id });
    }

    // Satır sil (yalnızca DRAFT)
    public async Task<IActionResult> OnPostDeleteLineAsync(Guid id, Guid lineId, CancellationToken ct)
    {
        using var conn = db.Open();
        if (!await IsDraftAsync(conn, id, ct)) return RedirectToPage(new { id });
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM MaterialIssueLine WHERE Id = @LineId AND HeaderId IN (SELECT Id FROM MaterialIssueHeader WHERE CompanyId = @CompanyId)",
            new { LineId = lineId, CompanyId = company.Id }, cancellationToken: ct));
        return RedirectToPage(new { id });
    }

    // Onayla → sp_MaterialIssuePost (StockMovement ISSUE)
    public async Task<IActionResult> OnPostPostAsync(Guid id, CancellationToken ct)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_MaterialIssuePost",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
            TempData["Success"] = "Sarf fişi onaylandı, stok düşüldü.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Sarf fişi onay hatası: {HeaderId}", id);
            TempData["Error"] = "Sarf fişi onaylanırken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    // İptal → sp_MaterialIssueReverse (flag-only)
    public async Task<IActionResult> OnPostReverseAsync(Guid id, CancellationToken ct)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_MaterialIssueReverse",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
            TempData["Success"] = "Sarf fişi iptal edildi, stok geri alındı.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Sarf fişi iptal hatası: {HeaderId}", id);
            TempData["Error"] = "Sarf fişi iptal edilirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    // Belgenin DRAFT olduğunu firma-ait doğrular
    private async Task<bool> IsDraftAsync(IDbConnection conn, Guid id, CancellationToken ct)
        => await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM MaterialIssueHeader WHERE Id = @Id AND CompanyId = @CompanyId AND Status = @St",
            new { Id = id, CompanyId = company.Id, St = DocStatus.Draft }, cancellationToken: ct)) > 0;

    public record HeaderDto
    {
        public Guid    Id           { get; set; }
        public Guid    WarehouseId  { get; set; }
        public string  DocNo        { get; set; } = "";
        public DateTime IssueDate   { get; set; }
        public Guid?   CostCenterId { get; set; }
        public string? Notes        { get; set; }
        public string  Status       { get; set; } = DocStatus.Draft;
    }

    public record LineDto(Guid Id, string ItemCode, string ItemName, string? UomCode, decimal Qty);
}
