using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;

namespace Operax.Web.Features.MasterData.PriceLists;

[Authorize]
public class ImportModel(Db db, ICurrentCompany company, ICurrentUser user, PriceListBulkService bulk) : PageModel
{
    public Guid ListId { get; set; }
    public string ListName { get; set; } = "";
    public PriceListBulkService.PreviewSummary? Preview { get; set; }
    public string ParsedJson { get; set; } = "[]";   // önizleme sonrası onaya taşınan satırlar

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await LoadListAsync(id)) return NotFound();
        return Page();
    }

    // Yapıştırılan/yüklenen veriyi ayrıştırır, DryRun ile önizleme (satır hataları) gösterir.
    public async Task<IActionResult> OnPostPreviewAsync(Guid id, string? pasteData, IFormFile? file)
    {
        if (!await LoadListAsync(id)) return NotFound();

        var text = await ReadInputAsync(pasteData, file);
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData["Error"] = "Veri yapıştırın veya bir CSV dosyası seçin.";
            return Page();
        }

        var lines = ParseRows(text);
        if (lines.Count == 0)
        {
            TempData["Error"] = "Ayrıştırılacak satır bulunamadı. Format: Ürün Kodu; Fiyat; Min Miktar; İskonto.";
            return Page();
        }

        Preview = await bulk.PreviewAsync(company.Id, id, user.Id, lines);
        ParsedJson = JsonSerializer.Serialize(lines);
        return Page();
    }

    // Önizlemeden geçen satırları gerçek upsert eder (ek-mod: mevcutları korur).
    public async Task<IActionResult> OnPostConfirmAsync(Guid id, string rowsJson)
    {
        if (!await LoadListAsync(id)) return NotFound();

        var lines = JsonSerializer.Deserialize<List<PriceListBulkService.BulkLine>>(rowsJson) ?? [];
        if (lines.Count == 0) { TempData["Error"] = "Aktarılacak satır yok."; return Page(); }

        try
        {
            var n = await bulk.ImportAsync(company.Id, id, user.Id, lines);
            TempData["Success"] = $"{n} satır içe aktarıldı.";
            return RedirectToPage("Details", new { id });
        }
        catch (SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // SP hatalı satır bulduysa (önizlemeden sonra veri değiştiyse) Türkçe mesaj
            TempData["Error"] = sqlEx.Message;
            return Page();
        }
    }

    // ─── Yardımcılar ────────────────────────────────────────────

    private async Task<bool> LoadListAsync(Guid id)
    {
        using var conn = db.Open();
        var row = await conn.QuerySingleOrDefaultAsync<(Guid Id, string Name)?>(
            "SELECT Id, Name FROM PriceList WHERE Id=@Id AND CompanyId=@CompanyId AND IsDeleted=0",
            new { Id = id, CompanyId = company.Id });
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
    // Ayraç otomatik (tab > noktalı virgül > virgül). Sayısal olmayan fiyatlı ilk satır = başlık, atlanır.
    private static List<PriceListBulkService.BulkLine> ParseRows(string text)
    {
        var result = new List<PriceListBulkService.BulkLine>();
        var rows = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int rowNo = 0;
        foreach (var raw in rows)
        {
            var sep = raw.Contains('\t') ? '\t' : (raw.Contains(';') ? ';' : ',');
            var cols = raw.Split(sep);
            if (cols.Length < 2) continue;

            var code = cols[0].Trim();
            if (string.IsNullOrWhiteSpace(code)) continue;

            // Fiyat ayrıştırılamazsa (ve henüz satır yoksa) başlık kabul edilir, atlanır
            if (!TryDecimal(cols[1], out var price))
            {
                if (result.Count == 0) continue;
                price = 0;
            }

            decimal minQty = cols.Length > 2 && TryDecimal(cols[2], out var mq) ? mq : 0;
            string? chain = cols.Length > 3 ? cols[3].Trim() : null;
            if (string.IsNullOrWhiteSpace(chain)) chain = null;

            result.Add(new PriceListBulkService.BulkLine(
                ++rowNo, null, code, price, minQty, PriceLineType.Fixed, chain));
        }
        return result;
    }

    // Ondalık: virgül veya nokta kabul (tr/invariant).
    private static bool TryDecimal(string s, out decimal val)
    {
        s = s.Trim().Replace(" ", "");
        return decimal.TryParse(s.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out val);
    }
}
