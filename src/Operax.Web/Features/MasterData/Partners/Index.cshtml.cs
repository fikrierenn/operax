using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Partners;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<PartnerDto> Partners { get; set; } = [];
    
    public int TotalPartners { get; set; }
    public int CustomerCount { get; set; }
    public int VendorCount { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        Partners = await conn.QueryAsync<PartnerDto>(@"
            SELECT Id, Code, Name, Type, TaxNumber, Email 
            FROM Partner 
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 
            ORDER BY Name", 
            new { CompanyId = company.Id });

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
