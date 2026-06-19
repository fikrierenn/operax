using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Loans;

/// <summary>
/// Yeni kredi açma formu. sp_CreateLoan 7 hesap yöntemini destekler;
/// taksit takvimi + banka hesabına gelir hareketi SP tarafından otomatik üretilir.
/// </summary>
[Authorize]
public class CreateModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<CreateModel> logger) : PageModel
{
    [BindProperty] public LoanForm Form { get; set; } = new();
    public List<DdlDto> BankAccounts { get; set; } = [];

    public async Task OnGetAsync()
    {
        await LoadBankAccountsAsync();
        // Varsayılanlar
        Form.StartDate    = DateTime.Today;
        Form.CalcMethod   = "ANUITE";
        Form.TermMonths   = 12;
        Form.InterestRate = 0;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadBankAccountsAsync();
            return Page();
        }

        using var conn = db.Open();
        try
        {
            var prm = new DynamicParameters();
            prm.Add("CompanyId",        company.Id);
            prm.Add("LoanNo",           Form.LoanNo);
            prm.Add("BankName",         Form.BankName);
            prm.Add("AccountId",        Form.AccountId);
            prm.Add("Principal",        Form.Principal);
            prm.Add("InterestRate",     Form.InterestRate);
            prm.Add("TermMonths",       Form.TermMonths);
            prm.Add("StartDate",        Form.StartDate);
            prm.Add("CalcMethod",       Form.CalcMethod);
            prm.Add("GracePeriodMonths",Form.GracePeriodMonths);
            prm.Add("BalloonAmount",    Form.BalloonAmount);
            prm.Add("UserId",           user.Id);
            prm.Add("NewLoanId", dbType: DbType.Guid, direction: ParameterDirection.Output);

            await conn.ExecuteAsync("sp_CreateLoan", prm, commandType: CommandType.StoredProcedure);

            var newId = prm.Get<Guid>("NewLoanId");
            TempData["Success"] = "Kredi açıldı, taksit takvimi oluşturuldu.";
            return RedirectToPage("Details", new { id = newId });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            // İş kuralı hatası — SP Türkçe mesaj döndü
            ModelState.AddModelError(string.Empty, sqlEx.Message);
            await LoadBankAccountsAsync();
            return Page();
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Kredi açma hatası: {LoanNo}", Form.LoanNo);
            ModelState.AddModelError(string.Empty, "Kredi açılırken veritabanı hatası oluştu.");
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

    public class LoanForm
    {
        public string   LoanNo            { get; set; } = "";
        public string   BankName          { get; set; } = "";
        public Guid?    AccountId         { get; set; }
        public decimal  Principal         { get; set; }
        public decimal  InterestRate      { get; set; }
        public int      TermMonths        { get; set; } = 12;
        public DateTime StartDate         { get; set; } = DateTime.Today;
        public string   CalcMethod        { get; set; } = "ANUITE";
        public int      GracePeriodMonths { get; set; }
        public decimal? BalloonAmount     { get; set; }
    }
}
