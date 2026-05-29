using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Payments;

/// <summary>
/// Ödeme / tahsilat girişi — sp_RecordPaymentAndAutoClose çağırır.
/// TxType=INCOME → tahsilat (müşteriden para geldi), EXPENSE → ödeme (tedarikçiye para gitti).
/// </summary>
[Authorize]
public class CreateModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<CreateModel> logger) : PageModel
{
    [BindProperty] public FormDto Form { get; set; } = new();

    public List<DdlDto> Accounts  { get; set; } = [];
    public List<DdlDto> Partners  { get; set; } = [];

    public async Task OnGetAsync(Guid? partnerId, string? txType)
    {
        await LoadDropdownsAsync();
        Form.TxType         = txType ?? "INCOME";
        Form.InstrumentType = "EFT";
        Form.Currency       = "TRY";
        if (partnerId.HasValue) Form.PartnerId = partnerId.Value;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync();
            return Page();
        }

        using var conn = db.Open();
        try
        {
            var prm = new DynamicParameters();
            prm.Add("CompanyId",      company.Id);
            prm.Add("AccountId",      Form.AccountId);
            prm.Add("PartnerId",      Form.PartnerId);
            prm.Add("Amount",         Form.Amount);
            prm.Add("Currency",       Form.Currency);
            prm.Add("TxType",         Form.TxType);
            prm.Add("Description",    Form.Description);
            prm.Add("InstrumentType", Form.InstrumentType);
            prm.Add("InstrumentId",   (Guid?)null);
            prm.Add("UserId",         user.Id);
            prm.Add("NewTxId",        dbType: DbType.Guid, direction: ParameterDirection.Output);

            await conn.ExecuteAsync("sp_RecordPaymentAndAutoClose", prm,
                commandType: CommandType.StoredProcedure);

            var newId = prm.Get<Guid>("NewTxId");
            TempData["Success"] = Form.TxType == "INCOME"
                ? $"Tahsilat kaydedildi. TX: {UiHelpers.ShortGuid(newId)}"
                : $"Ödeme kaydedildi. TX: {UiHelpers.ShortGuid(newId)}";

            return RedirectToPage("/Finance/PaymentPlan/Index");
        }
        catch (Microsoft.Data.SqlClient.SqlException sex) when (sex.Number is >= 50000 and < 60000)
        {
            TempData["Error"] = sex.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sex)
        {
            logger.LogError(sex, "Ödeme/tahsilat kayıt hatası");
            TempData["Error"] = "Ödeme kaydedilirken veritabanı hatası oluştu.";
        }

        await LoadDropdownsAsync();
        return Page();
    }

    private async Task LoadDropdownsAsync()
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        using var multi = await conn.QueryMultipleAsync(@"
            SELECT Id, Code, Name FROM FinancialAccount
            WHERE CompanyId = @CompanyId AND AccountType IN ('CASH','BANK') AND IsDeleted = 0
            ORDER BY AccountType, Name;

            SELECT Id, Code, Name FROM Partner
            WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0
            ORDER BY Name;", p);

        Accounts = (await multi.ReadAsync<DdlDto>()).ToList();
        Partners = (await multi.ReadAsync<DdlDto>()).ToList();
    }

    public record FormDto
    {
        public Guid    AccountId      { get; set; }
        public Guid    PartnerId      { get; set; }
        public string  TxType         { get; set; } = "INCOME";
        public decimal Amount         { get; set; }
        public string  Currency       { get; set; } = "TRY";
        public string  InstrumentType { get; set; } = "EFT";
        public string? Description    { get; set; }
    }
}
