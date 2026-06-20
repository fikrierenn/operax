using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.PurchaseInvoices;

/// <summary>
/// Mal alım faturası listesi: durum sekmeleri + arama + sayaçlar.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "all";
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public List<InvoiceRowDto> Invoices { get; set; } = [];
    public int DraftCount { get; set; }
    public int PostedCount { get; set; }

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    // Mal alım faturalarını durum/arama filtresiyle ve sekme sayaçlarıyla yükler
    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        try
        {
            var page = Page < 1 ? 1 : Page;
            // Sayfa satırları + aynı filtrenin toplam sayısı tek round-trip (PF-1)
            const string sql = @"
                SELECT pi.Id, pi.DocNo, pi.SupplierInvoiceNo, pi.SupplierInvoiceDate,
                       pi.InvoiceDate, pi.PartnerId, p.Name AS PartnerName,
                       pi.GrandTotal, pi.PaidAmount, pi.Status,
                       (SELECT COUNT(1) FROM PurchaseInvoiceLine WHERE InvoiceId = pi.Id) AS LineCount
                FROM PurchaseInvoice pi
                JOIN Partner p ON p.Id = pi.PartnerId
                WHERE pi.CompanyId = @CompanyId
                  AND (@Status = 'all' OR pi.Status = @Status)
                  AND (@Q IS NULL OR pi.SupplierInvoiceNo LIKE @Like OR p.Name LIKE @Like)
                ORDER BY pi.CreatedAt DESC
                OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

                SELECT COUNT(1)
                FROM PurchaseInvoice pi
                JOIN Partner p ON p.Id = pi.PartnerId
                WHERE pi.CompanyId = @CompanyId
                  AND (@Status = 'all' OR pi.Status = @Status)
                  AND (@Q IS NULL OR pi.SupplierInvoiceNo LIKE @Like OR p.Name LIKE @Like);";

            using var grid = await conn.QueryMultipleAsync(sql, new
            {
                CompanyId = company.Id, Status, Q, Like = $"%{Q}%", Page = page, PageSize
            });
            Invoices = (await grid.ReadAsync<InvoiceRowDto>()).ToList();
            FilteredCount = await grid.ReadSingleAsync<int>();

            DraftCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM PurchaseInvoice WHERE CompanyId = @CompanyId AND Status = @St",
                new { CompanyId = company.Id, St = DocStatus.Draft });
            PostedCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM PurchaseInvoice WHERE CompanyId = @CompanyId AND Status = @St",
                new { CompanyId = company.Id, St = DocStatus.Posted });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Alış faturası listesi yükleme hatası");
            TempData["Error"] = "Liste yüklenirken veritabanı hatası oluştu.";
        }
    }

    public record InvoiceRowDto(
        Guid Id, string DocNo, string? SupplierInvoiceNo, DateTime? SupplierInvoiceDate,
        DateTime InvoiceDate, Guid PartnerId, string PartnerName,
        decimal GrandTotal, decimal PaidAmount, string Status, int LineCount);
}
