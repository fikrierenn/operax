using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Accounts;

/// <summary>
/// Yeni finansal hesap (Kasa / Banka / Kredi Kartı / POS) tanımlama formu.
/// </summary>
[Authorize]
public class CreateModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<CreateModel> logger) : PageModel
{
    [BindProperty] public AccountForm Form { get; set; } = new();

    public void OnGet()
    {
        Form.AccountType = "BANK";
        Form.Currency    = "TRY";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        using var conn = db.Open();
        try
        {
            // İş kuralı: Aynı kodlu hesap engellenir
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT 1 FROM FinancialAccount WHERE CompanyId = @CompanyId AND Code = @Code AND IsDeleted = 0",
                new { CompanyId = company.Id, Form.Code });
            if (exists)
            {
                ModelState.AddModelError(string.Empty, "Bu kod ile hesap zaten var.");
                return Page();
            }

            var id = Guid.NewGuid();
            await conn.ExecuteAsync(@"
                INSERT INTO FinancialAccount
                    (Id, CompanyId, Code, Name, AccountType, Currency,
                     BankName, BranchName, AccountNumber, IBAN,
                     CreditLimit, InterestRate, OpeningBalance, Notes, CreatedBy)
                VALUES
                    (@Id, @CompanyId, @Code, @Name, @AccountType, @Currency,
                     @BankName, @BranchName, @AccountNumber, @IBAN,
                     @CreditLimit, @InterestRate, @OpeningBalance, @Notes, @UserId)",
                new {
                    Id = id, CompanyId = company.Id, Form.Code, Form.Name, Form.AccountType, Form.Currency,
                    Form.BankName, Form.BranchName, Form.AccountNumber, Form.IBAN,
                    Form.CreditLimit, Form.InterestRate, Form.OpeningBalance, Form.Notes,
                    UserId = user.Id
                });

            TempData["Success"] = "Hesap oluşturuldu.";
            return RedirectToPage("Details", new { id });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Hesap oluşturma hatası: {Code}", Form.Code);
            ModelState.AddModelError(string.Empty, "Hesap oluşturulurken veritabanı hatası oluştu.");
            return Page();
        }
    }

    public class AccountForm
    {
        public string   Code           { get; set; } = "";
        public string   Name           { get; set; } = "";
        public string   AccountType    { get; set; } = "BANK";  // CASH/BANK/CREDIT_CARD/POS
        public string   Currency       { get; set; } = "TRY";
        public string?  BankName       { get; set; }
        public string?  BranchName     { get; set; }
        public string?  AccountNumber  { get; set; }
        public string?  IBAN           { get; set; }
        public decimal? CreditLimit    { get; set; }
        public decimal? InterestRate   { get; set; }
        public decimal  OpeningBalance { get; set; }
        public string?  Notes          { get; set; }
    }
}
