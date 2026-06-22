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
public class IndexModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<IndexModel> logger) : PageModel
{
    public List<SeriesRowDto> Series { get; set; } = [];

    // Yeni seri formu için: tanımlı olmayan (eklenebilir) belge tipleri
    public List<DocTypeOption> AvailableDocTypes { get; set; } = [];

    // Bilinen belge tipi kataloğu — yalnızca bu tipler için seri oluşturulabilir (whitelist).
    // Rastgele DocType serisi engellenir; çünkü hiçbir caller okumaz (sp_NextNumber DocType sabitleriyle çağrılır).
    private static readonly string[] DocTypeCatalog =
    [
        NumberSeriesType.PartnerCustomer, NumberSeriesType.PartnerVendor, NumberSeriesType.PartnerBoth,
        NumberSeriesType.SalesInvoice, NumberSeriesType.PurchaseInvoice,
        NumberSeriesType.SalesOrder, NumberSeriesType.PurchaseOrder,
        NumberSeriesType.Cheque, NumberSeriesType.PromissoryNote
    ];

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

            SELECT COUNT(1) FROM NumberSeries WHERE CompanyId = @CompanyId AND IsDeleted = 0;

            SELECT DISTINCT DocType FROM NumberSeries WHERE CompanyId = @CompanyId AND IsDeleted = 0;";
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
        Series = (await grid.ReadAsync<SeriesRowDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();

        // Yeni seri dropdown'ı = katalog − halihazırda tanımlı (silinmemiş) tipler
        var existing = (await grid.ReadAsync<string>()).ToHashSet(StringComparer.Ordinal);
        AvailableDocTypes = DocTypeCatalog
            .Where(d => !existing.Contains(d))
            .Select(d => new DocTypeOption(d, DocTypeLabel(d)))
            .ToList();
    }

    // Yeni belge serisi oluştur — yalnızca bilinen katalog tipleri (whitelist).
    // Soft-delete edilmiş aynı tip varsa diriltilir (unique (CompanyId,DocType) ihlali önlenir).
    public async Task<IActionResult> OnPostCreateAsync(string docType, string prefix, int nextNo, byte padding, string separator, CancellationToken ct = default)
    {
        // İş kuralı: yalnızca bilinen belge tipi — rastgele DocType serisi engellenir
        if (string.IsNullOrWhiteSpace(docType) || Array.IndexOf(DocTypeCatalog, docType) < 0)
        { TempData["Error"] = "Geçersiz belge tipi."; return RedirectToPage(); }

        // İş kuralı: önek zorunlu, sonraki no en az 1, dolgu 1-9
        if (string.IsNullOrWhiteSpace(prefix)) { TempData["Error"] = "Önek zorunludur."; return RedirectToPage(); }
        if (nextNo < 1) nextNo = 1;
        padding = padding < 1 ? (byte)1 : padding > 9 ? (byte)9 : padding;
        var sep = string.IsNullOrEmpty(separator) ? "-" : separator;

        using var conn = db.Open();

        // İş kuralı: aktif (silinmemiş) seri zaten varsa tekrar oluşturulamaz
        var activeExists = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM NumberSeries WHERE CompanyId = @CompanyId AND DocType = @DocType AND IsDeleted = 0",
            new { CompanyId = company.Id, DocType = docType }, cancellationToken: ct));
        if (activeExists > 0) { TempData["Error"] = "Bu belge tipi için seri zaten tanımlı."; return RedirectToPage(); }

        // Soft-delete kaydı varsa dirilt; yoksa yeni satır ekle
        var revived = await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE NumberSeries
               SET Prefix = @Prefix, NextNo = @NextNo, Padding = @Padding, Separator = @Separator,
                   IsActive = 1, IsDeleted = 0, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
             WHERE CompanyId = @CompanyId AND DocType = @DocType AND IsDeleted = 1",
            new { Prefix = prefix.Trim(), NextNo = nextNo, Padding = padding, Separator = sep,
                  CompanyId = company.Id, DocType = docType, UserId = user.Id }, cancellationToken: ct));

        if (revived == 0)
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO NumberSeries (Id, CompanyId, DocType, Prefix, NextNo, Padding, Separator, ResetYearly, IsActive, IsDeleted, CreatedAt, CreatedBy)
                VALUES (NEWID(), @CompanyId, @DocType, @Prefix, @NextNo, @Padding, @Separator, 0, 1, 0, GETUTCDATE(), @UserId)",
                new { CompanyId = company.Id, DocType = docType, Prefix = prefix.Trim(), NextNo = nextNo,
                      Padding = padding, Separator = sep, UserId = user.Id }, cancellationToken: ct));
        }

        logger.LogInformation("Belge serisi oluşturuldu: {DocType}", docType);
        TempData["Success"] = "Belge serisi oluşturuldu.";
        return RedirectToPage();
    }

    // Belge serisini sil (soft) — yalnızca hiç numara üretmemiş (NextNo=1) seri silinebilir.
    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = db.Open();

        // İş kuralı: seri yoksa veya kullanımdaysa (numara üretmiş) silinemez — kod çakışması riski
        var nextNo = await conn.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT NextNo FROM NumberSeries WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct));
        if (nextNo is null) { TempData["Error"] = "Belge serisi bulunamadı."; return RedirectToPage(); }
        if (nextNo > 1) { TempData["Error"] = "Bu seri numara üretmiş, silinemez. Pasifleştirmek için Aktif'i kapatın."; return RedirectToPage(); }

        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE NumberSeries SET IsDeleted = 1, IsActive = 0, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
             WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id, UserId = user.Id }, cancellationToken: ct));

        logger.LogInformation("Belge serisi silindi (soft): {Id}", id);
        TempData["Success"] = "Belge serisi silindi.";
        return RedirectToPage();
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

    // Yeni seri dropdown seçeneği (kod + Türkçe etiket)
    public record DocTypeOption(string Code, string Label);

    // DocType → Türkçe etiket
    public static string DocTypeLabel(string docType) => docType switch
    {
        NumberSeriesType.PartnerCustomer => "Müşteri Kartı",
        NumberSeriesType.PartnerVendor   => "Tedarikçi Kartı",
        NumberSeriesType.PartnerBoth     => "Cari Kartı (Her İkisi)",
        NumberSeriesType.SalesInvoice    => "Satış Faturası",
        NumberSeriesType.PurchaseInvoice => "Alış Faturası",
        NumberSeriesType.SalesOrder      => "Satış Siparişi",
        NumberSeriesType.PurchaseOrder   => "Alış Siparişi",
        NumberSeriesType.Cheque          => "Çek",
        NumberSeriesType.PromissoryNote  => "Senet",
        _ => docType
    };
}
