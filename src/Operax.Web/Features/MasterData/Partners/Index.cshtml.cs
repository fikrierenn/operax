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

    public IEnumerable<PartnerDto> Partners { get; set; } = [];

    public int TotalPartners { get; set; }
    public int CustomerCount { get; set; }
    public int VendorCount { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        // Arama ve tip filtresi parametreli — injection güvenli
        Partners = await conn.QueryAsync<PartnerDto>(@"
            SELECT p.Id, p.Code, p.Name, p.Type, p.TaxNumber, p.Email
            FROM Partner p
            WHERE p.CompanyId = @CompanyId AND p.IsDeleted = 0
              AND (@Q IS NULL OR @Q = '' OR p.Name LIKE '%' + @Q + '%' OR p.Code LIKE '%' + @Q + '%' OR p.TaxNumber LIKE '%' + @Q + '%')
              AND (@Type IS NULL OR @Type = '' OR p.Type = @Type)
            ORDER BY p.Name",
            new { CompanyId = company.Id, Q, Type });

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
