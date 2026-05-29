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
            Partner = await conn.QueryFirstOrDefaultAsync<PartnerDto>(
                @"SELECT Id, Code, Name, Type, TaxNumber, Email, Phone, Address,
                         IsActive, Notes,
                         PaymentTermDays, CreditLimit, BlockOnLimitExceed,
                         RiskScore, RiskCategory, MaxOverdueDays,
                         DefaultPaymentMethod,
                         EFaturaMukellef, EFaturaAlias, IbanForRefund
                  FROM Partner WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }) ?? new();
        }
        else
        {
            Partner.IsActive    = true;
            Partner.Type        = "BOTH";
            Partner.RiskScore   = 3;
            Partner.RiskCategory = "MEDIUM";
            Partner.MaxOverdueDays = 30;
            Partner.PaymentTermDays = 30;
            Partner.DefaultPaymentMethod = "EFT";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        using var conn = db.Open();

        if (IsNew)
        {
            Partner.Id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO Partner
                    (Id, CompanyId, Code, Name, Type, TaxNumber, Email, Phone, Address,
                     IsActive, Notes,
                     PaymentTermDays, CreditLimit, BlockOnLimitExceed,
                     RiskScore, RiskCategory, MaxOverdueDays,
                     DefaultPaymentMethod,
                     EFaturaMukellef, EFaturaAlias, IbanForRefund)
                VALUES
                    (@Id, @CompanyId, @Code, @Name, @Type, @TaxNumber, @Email, @Phone, @Address,
                     @IsActive, @Notes,
                     @PaymentTermDays, @CreditLimit, @BlockOnLimitExceed,
                     @RiskScore, @RiskCategory, @MaxOverdueDays,
                     @DefaultPaymentMethod,
                     @EFaturaMukellef, @EFaturaAlias, @IbanForRefund)";
            await conn.ExecuteAsync(sql, new
            {
                Partner.Id, CompanyId = company.Id,
                Partner.Code, Partner.Name, Partner.Type, Partner.TaxNumber,
                Partner.Email, Partner.Phone, Partner.Address, Partner.IsActive, Partner.Notes,
                Partner.PaymentTermDays, Partner.CreditLimit, Partner.BlockOnLimitExceed,
                Partner.RiskScore, Partner.RiskCategory, Partner.MaxOverdueDays,
                Partner.DefaultPaymentMethod,
                Partner.EFaturaMukellef, Partner.EFaturaAlias, Partner.IbanForRefund
            });
        }
        else
        {
            const string sql = @"
                UPDATE Partner SET
                    Code = @Code, Name = @Name, Type = @Type,
                    TaxNumber = @TaxNumber, Email = @Email, Phone = @Phone, Address = @Address,
                    IsActive = @IsActive, Notes = @Notes,
                    PaymentTermDays = @PaymentTermDays, CreditLimit = @CreditLimit,
                    BlockOnLimitExceed = @BlockOnLimitExceed,
                    RiskScore = @RiskScore, RiskCategory = @RiskCategory,
                    MaxOverdueDays = @MaxOverdueDays,
                    DefaultPaymentMethod = @DefaultPaymentMethod,
                    EFaturaMukellef = @EFaturaMukellef, EFaturaAlias = @EFaturaAlias,
                    IbanForRefund = @IbanForRefund,
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND CompanyId = @CompanyId";
            await conn.ExecuteAsync(sql, new
            {
                Partner.Code, Partner.Name, Partner.Type,
                Partner.TaxNumber, Partner.Email, Partner.Phone, Partner.Address,
                Partner.IsActive, Partner.Notes,
                Partner.PaymentTermDays, Partner.CreditLimit, Partner.BlockOnLimitExceed,
                Partner.RiskScore, Partner.RiskCategory, Partner.MaxOverdueDays,
                Partner.DefaultPaymentMethod,
                Partner.EFaturaMukellef, Partner.EFaturaAlias, Partner.IbanForRefund,
                Partner.Id, CompanyId = company.Id
            });
        }

        TempData["Success"] = "Cari kart kaydedildi.";
        return RedirectToPage("./Index");
    }

    public record PartnerDto
    {
        public Guid    Id                    { get; set; }
        public string  Code                  { get; set; } = "";
        public string  Name                  { get; set; } = "";
        public string  Type                  { get; set; } = "BOTH";
        public string? TaxNumber             { get; set; }
        public string? Email                 { get; set; }
        public string? Phone                 { get; set; }
        public string? Address               { get; set; }
        public bool    IsActive              { get; set; } = true;
        public string? Notes                 { get; set; }
        // Mali alanlar
        public int     PaymentTermDays       { get; set; } = 30;
        public decimal CreditLimit           { get; set; }
        public bool    BlockOnLimitExceed    { get; set; }
        // Risk
        public byte    RiskScore             { get; set; } = 3;
        public string  RiskCategory          { get; set; } = "MEDIUM";
        public int     MaxOverdueDays        { get; set; } = 30;
        public string  DefaultPaymentMethod  { get; set; } = "EFT";
        // e-Belge
        public bool    EFaturaMukellef       { get; set; }
        public string? EFaturaAlias          { get; set; }
        public string? IbanForRefund         { get; set; }
    }
}
