using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.PriceLists;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    [BindProperty] public PriceListInput Header { get; set; } = new();

    public bool IsNew => Header.Id == Guid.Empty;
    public List<LineVm> Lines { get; set; } = [];
    public IEnumerable<DdlDto> Items { get; set; } = [];
    public IEnumerable<PartnerOption> Partners { get; set; } = [];
    public IEnumerable<DdlDto> Branches { get; set; } = [];
    public string ItemPriceJson { get; set; } = "{}";

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        using var conn = db.Open();
        await LoadLookupsAsync(conn);

        // İş kuralı: id yoksa yeni liste — varsayılan satış/TRY/aktif
        if (id is null)
        {
            Header = new PriceListInput
            {
                Direction = PriceDirection.Sales,
                Currency = "TRY",
                IsActive = true,
                Priority = 0
            };
            return Page();
        }

        var h = await conn.QuerySingleOrDefaultAsync<PriceListInput>(
            @"SELECT Id, Code, Name, Direction, Currency, PartnerId, BranchId,
                     Priority, ValidFrom, ValidTo, IsActive
              FROM PriceList
              WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0",
            new { Id = id, CompanyId = company.Id });

        // İş kuralı: liste bulunamazsa 404
        if (h is null) return NotFound();
        Header = h;

        await LoadLinesAsync(conn, id.Value);
        return Page();
    }

    // Başlık kaydet (route id yoksa insert, varsa update). Sonra Details'e döner.
    // Id route'tan gelir (mass-assignment'a karşı PriceListInput.Id [BindNever]).
    public async Task<IActionResult> OnPostSaveHeaderAsync(Guid? id)
    {
        // İş kuralı: Kod ve ad zorunlu
        if (string.IsNullOrWhiteSpace(Header.Code) || string.IsNullOrWhiteSpace(Header.Name))
        {
            using var c = db.Open();
            Header.Id = id ?? Guid.Empty;
            await LoadLookupsAsync(c);
            if (id is not null) await LoadLinesAsync(c, id.Value);
            TempData["Error"] = "Kod ve ad alanları zorunludur.";
            return Page();
        }

        using var conn = db.Open();
        if (id is null)
        {
            var newId = Guid.NewGuid();
            await conn.ExecuteAsync(
                @"INSERT INTO PriceList
                    (Id, CompanyId, Code, Name, Direction, Currency, PartnerId, BranchId,
                     Priority, ValidFrom, ValidTo, IsActive, CreatedBy)
                  VALUES
                    (@Id, @CompanyId, @Code, @Name, @Direction, @Currency, @PartnerId, @BranchId,
                     @Priority, @ValidFrom, @ValidTo, @IsActive, @UserId)",
                Bind(newId));
            TempData["Success"] = "Fiyat listesi oluşturuldu. Şimdi satır ekleyebilirsiniz.";
            return RedirectToPage("Details", new { id = newId });
        }

        await conn.ExecuteAsync(
            @"UPDATE PriceList SET
                Code=@Code, Name=@Name, Direction=@Direction, Currency=@Currency,
                PartnerId=@PartnerId, BranchId=@BranchId, Priority=@Priority,
                ValidFrom=@ValidFrom, ValidTo=@ValidTo, IsActive=@IsActive,
                UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId
              WHERE Id=@Id AND CompanyId=@CompanyId AND IsDeleted=0",
            Bind(id.Value));
        TempData["Success"] = "Fiyat listesi güncellendi.";
        return RedirectToPage("Details", new { id });
    }

    // Satır ekle — zincir iskonto kısayolu "10+5+3" child kademelere açılır.
    public async Task<IActionResult> OnPostAddLineAsync(
        Guid id, Guid itemId, decimal unitPrice, decimal minQty,
        string? lineType, string? discountChain)
    {
        using var conn = db.Open();

        // İş kuralı: liste mevcut ve şirkete ait olmalı
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM PriceList WHERE Id=@Id AND CompanyId=@CompanyId AND IsDeleted=0",
            new { Id = id, CompanyId = company.Id });
        if (exists == 0) return NotFound();

        var lineId = Guid.NewGuid();
        var type = lineType == PriceLineType.Discount ? PriceLineType.Discount : PriceLineType.Fixed;
        await conn.ExecuteAsync(
            @"INSERT INTO PriceListLine (Id, PriceListId, ItemId, UnitPrice, MinQty, LineType)
              VALUES (@Id, @PriceListId, @ItemId, @UnitPrice, @MinQty, @LineType)",
            new { Id = lineId, PriceListId = id, ItemId = itemId, UnitPrice = unitPrice,
                  MinQty = minQty, LineType = type });

        // Zincir iskonto kademeleri: "10+5+3" → Seq 1/2/3
        var pcts = ParseDiscountChain(discountChain);
        for (int i = 0; i < pcts.Count; i++)
        {
            await conn.ExecuteAsync(
                "INSERT INTO PriceListLineDiscount (Id, LineId, Seq, Pct) VALUES (NEWID(), @LineId, @Seq, @Pct)",
                new { LineId = lineId, Seq = i + 1, Pct = pcts[i] });
        }

        TempData["Success"] = "Satır eklendi.";
        return RedirectToPage("Details", new { id });
    }

    // Satırı soft-delete eder (iskonto kademeleri satıra bağlı kalır, listede gizlenir).
    public async Task<IActionResult> OnPostDeleteLineAsync(Guid id, Guid lineId)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(
            @"UPDATE PriceListLine SET IsDeleted=1
              FROM PriceListLine l JOIN PriceList pl ON pl.Id = l.PriceListId
              WHERE l.Id=@LineId AND pl.CompanyId=@CompanyId",
            new { LineId = lineId, CompanyId = company.Id });
        TempData["Success"] = "Satır silindi.";
        return RedirectToPage("Details", new { id });
    }

    // ─── Yardımcılar ────────────────────────────────────────────

    // Form girdisini SQL parametre nesnesine çevirir (CompanyId/UserId sunucudan).
    private object Bind(Guid id) => new
    {
        Id = id,
        CompanyId = company.Id,
        Header.Code,
        Header.Name,
        Header.Direction,
        Header.Currency,
        Header.PartnerId,
        Header.BranchId,
        Header.Priority,
        Header.ValidFrom,
        Header.ValidTo,
        Header.IsActive,
        UserId = user.Id
    };

    // "10+5+3" / "10 + 5 + 3" / "7,5+3" → [10, 5, 3] (0-100 dışı atılır). Ondalık , veya .
    private static List<decimal> ParseDiscountChain(string? chain)
    {
        var result = new List<decimal>();
        if (string.IsNullOrWhiteSpace(chain)) return result;

        foreach (var part in chain.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var norm = part.Replace(',', '.');
            if (decimal.TryParse(norm, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct)
                && pct > 0 && pct <= 100)
            {
                result.Add(pct);
            }
        }
        return result;
    }

    private async Task LoadLookupsAsync(System.Data.IDbConnection conn)
    {
        var p = new { CompanyId = company.Id };
        Items = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Item WHERE CompanyId=@CompanyId AND IsActive=1 AND IsDeleted=0 ORDER BY Code", p);
        Partners = await conn.QueryAsync<PartnerOption>(
            "SELECT Id, Code, Name, Type FROM Partner WHERE CompanyId=@CompanyId AND IsDeleted=0 ORDER BY Name", p);
        Branches = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Branch WHERE CompanyId=@CompanyId AND IsActive=1 AND IsDeleted=0 ORDER BY Name", p);

        // Ürün → ortalama maliyet (fiyat önerisi); JSON sözlük (itemId → fiyat)
        var prices = await conn.QueryAsync<(Guid Id, decimal Cost)>(
            @"SELECT i.Id, ISNULL(MAX(ic.AvgCost), 0) AS Cost
              FROM Item i LEFT JOIN ItemCost ic ON ic.ItemId = i.Id AND ic.CompanyId = @CompanyId
              WHERE i.CompanyId = @CompanyId AND i.IsActive = 1 AND i.IsDeleted = 0
              GROUP BY i.Id", p);
        ItemPriceJson = System.Text.Json.JsonSerializer.Serialize(
            prices.ToDictionary(x => x.Id.ToString(), x => x.Cost));
    }

    // Satırları + zincir iskonto kademelerini yükler, net efektif fiyatı C#'ta hesaplar.
    private async Task LoadLinesAsync(System.Data.IDbConnection conn, Guid priceListId)
    {
        var rawLines = await conn.QueryAsync<LineRaw>(
            @"SELECT l.Id, l.ItemId, i.Code AS ItemCode, i.Name AS ItemName,
                     l.UnitPrice, l.MinQty, l.LineType
              FROM PriceListLine l JOIN Item i ON i.Id = l.ItemId
              WHERE l.PriceListId = @Pid AND l.IsDeleted = 0
              ORDER BY i.Code",
            new { Pid = priceListId });

        var discounts = (await conn.QueryAsync<(Guid LineId, int Seq, decimal Pct)>(
            @"SELECT d.LineId, d.Seq, d.Pct
              FROM PriceListLineDiscount d JOIN PriceListLine l ON l.Id = d.LineId
              WHERE l.PriceListId = @Pid AND l.IsDeleted = 0
              ORDER BY d.LineId, d.Seq",
            new { Pid = priceListId })).ToList();

        Lines = rawLines.Select(l =>
        {
            var chain = discounts.Where(d => d.LineId == l.Id).OrderBy(d => d.Seq).ToList();
            decimal mult = 1m;
            foreach (var d in chain) mult *= (1m - d.Pct / 100m);
            var effective = Math.Round(l.UnitPrice * mult, 4);
            var chainText = chain.Count == 0 ? "—"
                : string.Join("+", chain.Select(d => "%" + d.Pct.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"))));
            return new LineVm(l.Id, l.ItemCode, l.ItemName, l.UnitPrice, l.MinQty,
                l.LineType, chainText, effective);
        }).ToList();
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
    private record LineRaw(Guid Id, Guid ItemId, string ItemCode, string ItemName,
        decimal UnitPrice, decimal MinQty, string LineType);
    public record LineVm(Guid Id, string ItemCode, string ItemName, decimal UnitPrice,
        decimal MinQty, string LineType, string ChainText, decimal EffectivePrice);
}
