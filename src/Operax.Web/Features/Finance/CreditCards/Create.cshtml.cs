using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.CreditCards;

/// <summary>
/// Yeni kredi kartı tanımı. Tek işlemde hem FinancialAccount (Type=CREDIT_CARD)
/// hem CreditCard kaydı oluşur. LinkedBankAccountId = ödemenin yapılacağı banka hesabı.
/// </summary>
[Authorize]
public class CreateModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<CreateModel> logger) : PageModel
{
    [BindProperty] public CardForm Form { get; set; } = new();
    public List<DdlDto> BankAccounts { get; set; } = [];

    public async Task OnGetAsync()
    {
        await LoadBankAccountsAsync();
        Form.StatementDay = 1;
        Form.DueDay       = 10;
        Form.Currency     = "TRY";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { await LoadBankAccountsAsync(); return Page(); }

        using var conn = db.Open();
        try
        {
            var accountId = Guid.NewGuid();
            // İş kuralı: Kart için FinancialAccount (Type=CREDIT_CARD) açılır — bakiye/hareket buradan
            await conn.ExecuteAsync(@"
                INSERT INTO FinancialAccount
                    (Id, CompanyId, Code, Name, AccountType, Currency, BankName,
                     CreditLimit, OpeningBalance, CreatedBy)
                VALUES
                    (@Id, @CompanyId, @Code, @Name, 'CREDIT_CARD', @Currency, @BankName,
                     @CreditLimit, 0, @UserId)",
                new {
                    Id = accountId, CompanyId = company.Id,
                    Code = "KART-" + Form.CardNoMasked, Name = Form.HolderName + " - " + Form.BankName,
                    Form.Currency, Form.BankName, Form.CreditLimit, UserId = user.Id
                });

            var cardId = Guid.NewGuid();
            await conn.ExecuteAsync(@"
                INSERT INTO CreditCard
                    (Id, CompanyId, AccountId, CardNoMasked, HolderName, BankName, CardType,
                     CreditLimit, AvailableLimit, StatementDay, DueDay, InterestRate,
                     Currency, ExpiresAt, LinkedBankAccountId)
                VALUES
                    (@Id, @CompanyId, @AccountId, @CardNoMasked, @HolderName, @BankName, @CardType,
                     @CreditLimit, @CreditLimit, @StatementDay, @DueDay, @InterestRate,
                     @Currency, @ExpiresAt, @LinkedBankAccountId)",
                new {
                    Id = cardId, CompanyId = company.Id, AccountId = accountId,
                    Form.CardNoMasked, Form.HolderName, Form.BankName, Form.CardType,
                    Form.CreditLimit, Form.StatementDay, Form.DueDay, Form.InterestRate,
                    Form.Currency, Form.ExpiresAt, Form.LinkedBankAccountId
                });

            TempData["Success"] = "Kredi kartı tanımlandı.";
            return RedirectToPage("Details", new { id = cardId });
        }
        catch (Microsoft.Data.SqlClient.SqlException sex)
        {
            logger.LogError(sex, "Kart tanımlama hatası: {Card}", Form.CardNoMasked);
            ModelState.AddModelError(string.Empty, "Kart tanımlanırken veritabanı hatası oluştu.");
            await LoadBankAccountsAsync();
            return Page();
        }
    }

    private async Task LoadBankAccountsAsync()
    {
        using var conn = db.Open();
        BankAccounts = (await conn.QueryAsync<DdlDto>(@"
            SELECT Id, Code, Name FROM FinancialAccount
            WHERE CompanyId = @CompanyId AND AccountType = 'BANK' AND IsDeleted = 0
            ORDER BY Name", new { CompanyId = company.Id })).ToList();
    }

    public class CardForm
    {
        public string    CardNoMasked        { get; set; } = "";
        public string    HolderName          { get; set; } = "";
        public string    BankName            { get; set; } = "";
        public string    CardType            { get; set; } = "CREDIT";
        public decimal   CreditLimit         { get; set; }
        public int       StatementDay        { get; set; } = 1;
        public int       DueDay              { get; set; } = 10;
        public decimal?  InterestRate        { get; set; }
        public string    Currency            { get; set; } = "TRY";
        public DateTime? ExpiresAt           { get; set; }
        public Guid?     LinkedBankAccountId { get; set; }
    }
}
