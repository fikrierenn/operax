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

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        Series = (await conn.QueryAsync<SeriesRowDto>(@"
            SELECT Id, DocType, Prefix, NextNo, Padding, Separator, IsActive
            FROM NumberSeries
            WHERE CompanyId = @CompanyId AND IsDeleted = 0
            ORDER BY DocType",
            new { CompanyId = company.Id })).ToList();
    }

    // Seri ayarını güncelle (prefix / sonraki no / dolgu / ayraç / aktiflik)
    public async Task<IActionResult> OnPostSaveAsync(Guid id, string prefix, int nextNo, byte padding, string separator, bool isActive)
    {
        // İş kuralı: prefix boş olamaz, sonraki no en az 1
        if (string.IsNullOrWhiteSpace(prefix)) { TempData["Error"] = "Önek zorunludur."; return RedirectToPage(); }
        if (nextNo < 1) nextNo = 1;
        if (padding < 1) padding = 1;

        using var conn = db.Open();
        await conn.ExecuteAsync(@"
            UPDATE NumberSeries
               SET Prefix = @Prefix, NextNo = @NextNo, Padding = @Padding,
                   Separator = @Separator, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
             WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, Prefix = prefix.Trim(), NextNo = nextNo, Padding = padding,
                  Separator = separator ?? "-", IsActive = isActive, CompanyId = company.Id });

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
