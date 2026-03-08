using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Partners;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty]
    public PartnerDto Partner { get; set; } = new();

    public bool IsNew => Partner.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        if (id.HasValue)
        {
            using var conn = db.Open();
            // CompanyId zorunlu — başka şirket cari görüntülenemez
            Partner = await conn.QueryFirstOrDefaultAsync<PartnerDto>(
                "SELECT * FROM Partner WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }) ?? new();
        }
        else
        {
            Partner.IsActive = true;
            Partner.Type = "BOTH";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        using var conn = db.Open();

        if (IsNew)
        {
            Partner.Id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO Partner (Id, CompanyId, Code, Name, Type, TaxNumber, Email, IsActive)
                VALUES (@Id, @CompanyId, @Code, @Name, @Type, @TaxNumber, @Email, @IsActive)";
            await conn.ExecuteAsync(sql, new { Partner.Id, CompanyId = company.Id, Partner.Code, Partner.Name, Partner.Type, Partner.TaxNumber, Partner.Email, Partner.IsActive });
        }
        else
        {
            // CompanyId zorunlu — başka şirket carisini güncelleyemez
            const string sql = @"
                UPDATE Partner
                SET Code = @Code, Name = @Name, Type = @Type, TaxNumber = @TaxNumber, Email = @Email, IsActive = @IsActive
                WHERE Id = @Id AND CompanyId = @CompanyId";
            await conn.ExecuteAsync(sql, new { Partner.Code, Partner.Name, Partner.Type, Partner.TaxNumber, Partner.Email, Partner.IsActive, Partner.Id, CompanyId = company.Id });
        }

        return RedirectToPage("./Index");
    }

    public record PartnerDto 
    { 
        public Guid Id { get; set; } 
        public string Code { get; set; } = ""; 
        public string Name { get; set; } = ""; 
        public string Type { get; set; } = "BOTH"; 
        public string? TaxNumber { get; set; } 
        public string? Email { get; set; } 
        public bool IsActive { get; set; } 
    }
}
