using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.NumberSeries;

/// <summary>
/// Belge seri yönetimi — otomatik numaralama önekleri ve sıraları (NumberSeries).
/// Tüm belge tiplerinin prefix/sonraki no/dolgu ayarları buradan düzenlenir.
/// </summary>
[Authorize(Roles = "Administrator")]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    public List<SeriesRowDto> Series { get; set; } = [];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;
        const string sql = @"
            SELECT Id, DocType, Prefix, NextNo, Padding, Separator, IsActive
            FROM NumberSeries
            WHERE CompanyId = @CompanyId AND IsDeleted = 0
            ORDER BY DocType
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM NumberSeries WHERE CompanyId = @CompanyId AND IsDeleted = 0;";
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
        Series = (await grid.ReadAsync<SeriesRowDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
    }

    // Seri ayarını güncelle (prefix / sonraki no / dolgu / ayraç / aktiflik)
    public async Task<IActionResult> OnPostSaveAsync(Guid id, string prefix, int nextNo, byte padding, string separator, bool isActive, CancellationToken ct = default)
    {
        // İş kuralı: prefix boş olamaz, sonraki no en az 1
        if (string.IsNullOrWhiteSpace(prefix)) { TempData["Error"] = "Önek zorunludur."; return RedirectToPage(); }
        if (nextNo < 1) nextNo = 1;
        if (padding < 1) padding = 1;

        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE NumberSeries
               SET Prefix = @Prefix, NextNo = @NextNo, Padding = @Padding,
                   Separator = @Separator, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
             WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, Prefix = prefix.Trim(), NextNo = nextNo, Padding = padding,
                  Separator = separator ?? "-", IsActive = isActive, CompanyId = company.Id }, cancellationToken: ct));

        logger.LogInformation("Belge serisi güncellendi: {Id}", id);
        TempData["Success"] = "Belge serisi güncellendi.";
        return RedirectToPage();
    }

    public record SeriesRowDto(Guid Id, string DocType, string Prefix, int NextNo, byte Padding, string Separator, bool IsActive);

    // DocType → Türkçe etiket
    public static string DocTypeLabel(string docType) => docType switch
    {
        "PARTNER_CUST"     => "Müşteri Kartı",
        "PARTNER_VEND"     => "Tedarikçi Kartı",
        "PARTNER_BOTH"     => "Cari Kartı (Her İkisi)",
        "SALES_INVOICE"    => "Satış Faturası",
        "PURCHASE_INVOICE" => "Alış Faturası",
        "SALES_ORDER"      => "Satış Siparişi",
        "PURCHASE_ORDER"   => "Alış Siparişi",
        "CHEQUE"           => "Çek",
        "PROMISSORY_NOTE"  => "Senet",
        _ => docType
    };
}
