using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Operax.Web.Features.MasterData.PriceLists;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user,
    PriceListBulkService bulk, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty] public PriceListInput Header { get; set; } = new();

    public bool IsNew => Header.Id == Guid.Empty;
    public IEnumerable<PartnerOption> Partners { get; set; } = [];
    public IEnumerable<DdlDto> Branches { get; set; } = [];
    public IEnumerable<DdlDto> OtherLists { get; set; } = [];   // çoğaltma kaynağı
    public string ItemsJson { get; set; } = "[]";              // grid ürün editörü
    public string GridLinesJson { get; set; } = "[]";          // grid mevcut satırlar

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        using var conn = db.Open();
        await LoadLookupsAsync(conn, id, ct);

        // İş kuralı: id yoksa yeni liste — varsayılan satış/TRY/aktif
        if (id is null)
        {
            Header = new PriceListInput { Direction = PriceDirection.Sales, Currency = "TRY", IsActive = true };
            return Page();
        }

        var h = await conn.QuerySingleOrDefaultAsync<PriceListInput>(new CommandDefinition(
            @"SELECT Id, Code, Name, Direction, Currency, PartnerId, BranchId, Priority, ValidFrom, ValidTo, IsActive
              FROM PriceList WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct));

        // İş kuralı: liste bulunamazsa 404
        if (h is null) return NotFound();
        Header = h;

        GridLinesJson = await BuildGridLinesJsonAsync(conn, id.Value, ct);
        return Page();
    }

    // Başlık kaydet (route id yoksa insert, varsa update). Id route'tan (PriceListInput.Id [BindNever]).
    public async Task<IActionResult> OnPostSaveHeaderAsync(Guid? id, CancellationToken ct)
    {
        // İş kuralı: Kod ve ad zorunlu
        if (string.IsNullOrWhiteSpace(Header.Code) || string.IsNullOrWhiteSpace(Header.Name))
        {
            using var c = db.Open();
            Header.Id = id ?? Guid.Empty;
            await LoadLookupsAsync(c, id, ct);
            if (id is not null) GridLinesJson = await BuildGridLinesJsonAsync(c, id.Value, ct);
            TempData["Error"] = "Kod ve ad alanları zorunludur.";
            return Page();
        }

        using var conn = db.Open();
        if (id is null)
        {
            var newId = Guid.NewGuid();
            await conn.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO PriceList (Id, CompanyId, Code, Name, Direction, Currency, PartnerId, BranchId,
                     Priority, ValidFrom, ValidTo, IsActive, CreatedBy)
                  VALUES (@Id, @CompanyId, @Code, @Name, @Direction, @Currency, @PartnerId, @BranchId,
                     @Priority, @ValidFrom, @ValidTo, @IsActive, @UserId)",
                Bind(newId), cancellationToken: ct));
            TempData["Success"] = "Fiyat listesi oluşturuldu. Şimdi satır ekleyebilirsiniz.";
            return RedirectToPage("Details", new { id = newId });
        }

        await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE PriceList SET Code=@Code, Name=@Name, Direction=@Direction, Currency=@Currency,
                PartnerId=@PartnerId, BranchId=@BranchId, Priority=@Priority,
                ValidFrom=@ValidFrom, ValidTo=@ValidTo, IsActive=@IsActive,
                UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId
              WHERE Id=@Id AND CompanyId=@CompanyId AND IsDeleted=0",
            Bind(id.Value), cancellationToken: ct));
        TempData["Success"] = "Fiyat listesi güncellendi.";
        return RedirectToPage("Details", new { id });
    }

    // Grid "Kaydet": JSON satır dizisini bulk-upsert eder (SyncDelete=1 → grid otoritedir).
    public async Task<IActionResult> OnPostBulkSaveAsync(Guid id, CancellationToken ct = default)
    {
        // İş kuralı: gövdeyi oku + ayrıştır; bozuk JSON sistem hatası gibi 500'e gitmemeli
        List<GridRow> rows;
        try
        {
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync(ct);
            rows = JsonSerializer.Deserialize<List<GridRow>>(json, JsonOpts) ?? [];
        }
        catch (JsonException jsonEx)
        {
            logger.LogWarning(jsonEx, "Fiyat listesi toplu kaydet: geçersiz JSON gövde. Liste {ListId}", id);
            return new JsonResult(new { ok = false, error = "Gönderilen veri okunamadı. Sayfayı yenileyip tekrar deneyin." });
        }

        // Ürün seçilmemiş satırları ele; grid ItemId yolunu kullanır
        var lines = rows.Where(r => r.ItemId != Guid.Empty)
            .Select((r, i) => new PriceListBulkService.BulkLine(
                i + 1, r.ItemId, null, r.UnitPrice, r.MinQty,
                r.LineType ?? PriceLineType.Fixed, r.DiscountChain));
        try
        {
            var written = await bulk.BulkSaveAsync(company.Id, id, user.Id, lines, ct);
            return new JsonResult(new { ok = true, written });
        }
        catch (SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP Türkçe yazdı, kullanıcıya gösterilebilir
            return new JsonResult(new { ok = false, error = sqlEx.Message });
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Fiyat listesi toplu kaydet DB hatası. Liste {ListId}", id);
            return new JsonResult(new { ok = false, error = "Veritabanı hatası oluştu." });
        }
    }

    // Tüm aktif ürünleri listeye ekle (fiyat = ortalama maliyet/0). Mevcutları korur.
    public async Task<IActionResult> OnPostAddAllItemsAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var n = await bulk.AddAllItemsAsync(company.Id, id, user.Id, ct);
            TempData["Success"] = $"{n} ürün eklendi/güncellendi.";
        }
        catch (SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Fiyat listesi tüm-ürün ekleme DB hatası. Liste {ListId}", id);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage("Details", new { id });
    }

    // Başka bir listenin satır + iskontolarını bu listeye kopyala.
    public async Task<IActionResult> OnPostCloneAsync(Guid id, Guid sourceId, CancellationToken ct = default)
    {
        // Guard: kaynak seçilmemişse işlem yok
        if (sourceId == Guid.Empty) { TempData["Error"] = "Kaynak liste seçiniz."; return RedirectToPage("Details", new { id }); }
        try
        {
            var n = await bulk.CloneAsync(company.Id, sourceId, id, user.Id, ct);
            TempData["Success"] = $"{n} satır kopyalandı.";
        }
        catch (SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Fiyat listesi çoğaltma DB hatası. Liste {ListId}", id);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage("Details", new { id });
    }

    // ─── Yardımcılar ────────────────────────────────────────────

    // Form girdisini SQL parametre nesnesine çevirir (CompanyId/UserId sunucudan).
    private object Bind(Guid id) => new
    {
        Id = id, CompanyId = company.Id, Header.Code, Header.Name, Header.Direction, Header.Currency,
        Header.PartnerId, Header.BranchId, Header.Priority, Header.ValidFrom, Header.ValidTo,
        Header.IsActive, UserId = user.Id
    };

    private async Task LoadLookupsAsync(System.Data.IDbConnection conn, Guid? currentId, CancellationToken ct)
    {
        var p = new { CompanyId = company.Id };
        var items = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Item WHERE CompanyId=@CompanyId AND IsActive=1 AND IsDeleted=0 ORDER BY Code", p, cancellationToken: ct));
        ItemsJson = JsonSerializer.Serialize(items.Select(i => new { id = i.Id, code = i.Code, name = i.Name }));

        Partners = await conn.QueryAsync<PartnerOption>(new CommandDefinition(
            "SELECT Id, Code, Name, Type FROM Partner WHERE CompanyId=@CompanyId AND IsDeleted=0 ORDER BY Name", p, cancellationToken: ct));
        Branches = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Branch WHERE CompanyId=@CompanyId AND IsActive=1 AND IsDeleted=0 ORDER BY Name", p, cancellationToken: ct));

        // Çoğaltma kaynağı: bu liste hariç diğer aktif listeler
        OtherLists = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            @"SELECT Id, Code, Name FROM PriceList
              WHERE CompanyId=@CompanyId AND IsDeleted=0 AND (@Cur IS NULL OR Id <> @Cur)
              ORDER BY Code",
            new { CompanyId = company.Id, Cur = currentId }, cancellationToken: ct));
    }

    // Mevcut satırları grid JSON'una çevirir (zincir iskonto ham "10+5+3" biçiminde).
    private static async Task<string> BuildGridLinesJsonAsync(System.Data.IDbConnection conn, Guid priceListId, CancellationToken ct)
    {
        var rawLines = await conn.QueryAsync<LineRaw>(new CommandDefinition(
            @"/* isolation-guard:ignore: parent liste OnGet'te CompanyId ile doğrulandı, @Pid güvenli */
              SELECT l.Id, l.ItemId, l.UnitPrice, l.MinQty, l.LineType
              FROM PriceListLine l WHERE l.PriceListId = @Pid AND l.IsDeleted = 0",
            new { Pid = priceListId }, cancellationToken: ct));

        var discounts = (await conn.QueryAsync<(Guid LineId, int Seq, decimal Pct)>(new CommandDefinition(
            @"/* isolation-guard:ignore: parent liste OnGet'te CompanyId ile doğrulandı, @Pid güvenli */
              SELECT d.LineId, d.Seq, d.Pct FROM PriceListLineDiscount d
              JOIN PriceListLine l ON l.Id = d.LineId
              WHERE l.PriceListId = @Pid AND l.IsDeleted = 0 ORDER BY d.LineId, d.Seq",
            new { Pid = priceListId }, cancellationToken: ct))).ToList();

        var rows = rawLines.Select(l => new
        {
            itemId = l.ItemId,
            unitPrice = l.UnitPrice,
            minQty = l.MinQty,
            lineType = l.LineType,
            discountChain = string.Join("+", discounts.Where(d => d.LineId == l.Id)
                .OrderBy(d => d.Seq).Select(d => d.Pct.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)))
        });
        return JsonSerializer.Serialize(rows);
    }

    // ─── DTO'lar ────────────────────────────────────────────────
    public class PriceListInput
    {
        [BindNever] public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Direction { get; set; } = PriceDirection.Sales;
        public string Currency { get; set; } = "TRY";
        public Guid? PartnerId { get; set; }
        public Guid? BranchId { get; set; }
        public int Priority { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public record PartnerOption(Guid Id, string Code, string Name, string Type);
    private record LineRaw(Guid Id, Guid ItemId, decimal UnitPrice, decimal MinQty, string LineType);
    private sealed record GridRow(Guid ItemId, decimal UnitPrice, decimal MinQty, string? LineType, string? DiscountChain);
}
