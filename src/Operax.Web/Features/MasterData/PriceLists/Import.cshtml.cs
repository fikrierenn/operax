using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Operax.Web.Features.MasterData.PriceLists;

[Authorize]
public class ImportModel(Db db, ICurrentCompany company, ICurrentUser user,
    PriceListBulkService bulk, ILogger<ImportModel> logger) : PageModel
{
    public Guid ListId { get; set; }
    public string ListName { get; set; } = "";
    public PriceListBulkService.PreviewSummary? Preview { get; set; }
    public string ParsedJson { get; set; } = "[]";   // önizleme sonrası onaya taşınan satırlar
    public int SkippedCount { get; set; }             // ayrıştırılamayıp atlanan satır sayısı

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LoadListAsync(id, ct)) return NotFound();
        return Page();
    }

    // Yapıştırılan/yüklenen veriyi ayrıştırır, DryRun ile önizleme (satır hataları) gösterir.
    public async Task<IActionResult> OnPostPreviewAsync(Guid id, string? pasteData, IFormFile? file,
        CancellationToken ct = default)
    {
        if (!await LoadListAsync(id, ct)) return NotFound();

        var text = await ReadInputAsync(pasteData, file);
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData["Error"] = "Veri yapıştırın veya bir CSV dosyası seçin.";
            return Page();
        }

        // Ayrıştırma: geçerli satırlar + format dışı (atlanan) satır sayısı (sessiz kayıp olmasın)
        var (lines, skipped) = ParseRows(text);
        SkippedCount = skipped;
        if (lines.Count == 0)
        {
            TempData["Error"] = "Ayrıştırılacak geçerli satır bulunamadı. Format: Ürün Kodu; Fiyat; Min Miktar; İskonto.";
            return Page();
        }

        Preview = await bulk.PreviewAsync(company.Id, id, user.Id, lines, ct);
        ParsedJson = JsonSerializer.Serialize(lines);
        return Page();
    }

    // Önizlemeden geçen satırları gerçek upsert eder (ek-mod: mevcutları korur).
    public async Task<IActionResult> OnPostConfirmAsync(Guid id, string rowsJson, CancellationToken ct = default)
    {
        if (!await LoadListAsync(id, ct)) return NotFound();

        // İş kuralı: gizli alandan gelen JSON bozuksa sistem hatası gibi 500'e gitmemeli
        List<PriceListBulkService.BulkLine> lines;
        try
        {
            lines = JsonSerializer.Deserialize<List<PriceListBulkService.BulkLine>>(rowsJson) ?? [];
        }
        catch (JsonException jsonEx)
        {
            logger.LogWarning(jsonEx, "Fiyat listesi import onay: geçersiz JSON. Liste {ListId}", id);
            TempData["Error"] = "Aktarım verisi okunamadı. Önizlemeyi tekrar çalıştırın.";
            return Page();
        }
        if (lines.Count == 0) { TempData["Error"] = "Aktarılacak satır yok."; return Page(); }

        try
        {
            var n = await bulk.ImportAsync(company.Id, id, user.Id, lines, ct);
            TempData["Success"] = $"{n} satır içe aktarıldı.";
            return RedirectToPage("Details", new { id });
        }
        catch (SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // SP hatalı satır bulduysa (önizlemeden sonra veri değiştiyse) Türkçe mesaj
            TempData["Error"] = sqlEx.Message;
            return Page();
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Fiyat listesi import DB hatası. Liste {ListId}", id);
            TempData["Error"] = "Veritabanı hatası oluştu.";
            return Page();
        }
    }

    // ─── Yardımcılar ────────────────────────────────────────────

    private async Task<bool> LoadListAsync(Guid id, CancellationToken ct)
    {
        using var conn = db.Open();
        var row = await conn.QuerySingleOrDefaultAsync<(Guid Id, string Name)?>(new CommandDefinition(
            "SELECT Id, Name FROM PriceList WHERE Id=@Id AND CompanyId=@CompanyId AND IsDeleted=0",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct));
        if (row is null) return false;
        ListId = row.Value.Id;
        ListName = row.Value.Name;
        return true;
    }

    // Dosya öncelikli; yoksa yapıştırılan metin. UTF-8 okunur.
    private static async Task<string> ReadInputAsync(string? pasteData, IFormFile? file)
    {
        if (file is { Length: > 0 })
        {
            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        return pasteData ?? "";
    }

    // Satır metnini ayrıştırır. Sütun sırası: ÜrünKodu; Fiyat; MinMiktar(ops); İskonto(ops "10+5+3").
    // Ayraç otomatik (tab > noktalı virgül > virgül). İLK satırın fiyatı sayısal değilse başlık
    // sayılır, atlanır. Sonraki satırlarda eksik kolon / boş kod / bozuk fiyat = ATLANIR + sayılır
    // (sessiz fiyat=0 fallback YOK — yanlış fiyat içe aktarmamak için). skipped kullanıcıya gösterilir.
    private static (List<PriceListBulkService.BulkLine> Lines, int Skipped) ParseRows(string text)
    {
        var result = new List<PriceListBulkService.BulkLine>();
        var rows = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int rowNo = 0, skipped = 0, physical = 0;
        foreach (var raw in rows)
        {
            physical++;
            var sep = raw.Contains('\t') ? '\t' : (raw.Contains(';') ? ';' : ',');
            var cols = raw.Split(sep);
            var code = cols.Length > 0 ? cols[0].Trim() : "";

            // Eksik kolon veya boş ürün kodu → atla (say)
            if (cols.Length < 2 || string.IsNullOrWhiteSpace(code)) { skipped++; continue; }

            // Fiyat sayısal değil: yalnız fiziksel ilk satırda başlık kabul edilir (sayılmaz);
            // veri satırında bozuk fiyat → atla + say (yanlış fiyat yazma)
            if (!TryDecimal(cols[1], out var price))
            {
                if (physical > 1) skipped++;
                continue;
            }

            decimal minQty = cols.Length > 2 && TryDecimal(cols[2], out var mq) ? mq : 0;
            string? chain = cols.Length > 3 ? cols[3].Trim() : null;
            if (string.IsNullOrWhiteSpace(chain)) chain = null;

            result.Add(new PriceListBulkService.BulkLine(
                ++rowNo, null, code, price, minQty, PriceLineType.Fixed, chain));
        }
        return (result, skipped);
    }

    // Ondalık: virgül veya nokta kabul (tr/invariant).
    private static bool TryDecimal(string s, out decimal val)
    {
        s = s.Trim().Replace(" ", "");
        return decimal.TryParse(s.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out val);
    }
}
