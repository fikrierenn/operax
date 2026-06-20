using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Partners;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public IEnumerable<PartnerDto> Partners { get; set; } = [];

    public int TotalPartners { get; set; }
    public int CustomerCount { get; set; }
    public int VendorCount { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        // Arama + tip filtresi parametreli + sayfalama (OFFSET/FETCH); sayfa + filtreli count tek round-trip
        const string sql = @"
            SELECT p.Id, p.Code, p.Name, p.Type, p.TaxNumber, p.Email
            FROM Partner p
            WHERE p.CompanyId = @CompanyId AND p.IsDeleted = 0
              AND (@Q IS NULL OR @Q = '' OR p.Name LIKE '%' + @Q + '%' OR p.Code LIKE '%' + @Q + '%' OR p.TaxNumber LIKE '%' + @Q + '%')
              AND (@Type IS NULL OR @Type = '' OR p.Type = @Type)
            ORDER BY p.Name
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM Partner p
            WHERE p.CompanyId = @CompanyId AND p.IsDeleted = 0
              AND (@Q IS NULL OR @Q = '' OR p.Name LIKE '%' + @Q + '%' OR p.Code LIKE '%' + @Q + '%' OR p.TaxNumber LIKE '%' + @Q + '%')
              AND (@Type IS NULL OR @Type = '' OR p.Type = @Type);";

        using (var grid = await conn.QueryMultipleAsync(sql, new { CompanyId = company.Id, Q, Type, Page = page, PageSize }))
        {
            Partners = (await grid.ReadAsync<PartnerDto>()).ToList();
            FilteredCount = await grid.ReadSingleAsync<int>();
        }

        TotalPartners = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Partner WHERE CompanyId = @CompanyId AND IsDeleted = 0",
            new { CompanyId = company.Id });

        CustomerCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Partner WHERE CompanyId = @CompanyId AND Type IN ('CUSTOMER', 'BOTH') AND IsDeleted = 0",
            new { CompanyId = company.Id });

        // İş kuralı: tedarikçi tipi VENDOR veya SUPPLIER (seed vocab farkı) + BOTH
        VendorCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Partner WHERE CompanyId = @CompanyId AND Type IN ('VENDOR', 'SUPPLIER', 'BOTH') AND IsDeleted = 0",
            new { CompanyId = company.Id });
    }

    public record PartnerDto(Guid Id, string Code, string Name, string Type, string TaxNumber, string Email);
}
