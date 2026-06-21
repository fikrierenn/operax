using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Cheques;

/// <summary>
/// Yeni çek/senet girişi. type=cheque → Cheque, type=note → PromissoryNote.
/// Yön (alınan/verilen) ve cari seçimi ile portföye eklenir (PORTFOLIO statü).
/// </summary>
[Authorize]
public class CreateModel(Db db, ICurrentCompany company, ICurrentUser user, INumberSeriesService numberSeries, ILogger<CreateModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Type { get; set; } = "cheque";
    [BindProperty] public ChequeForm Form { get; set; } = new();
    public List<DdlDto> Partners { get; set; } = [];

    public bool IsNote => Type == "note";

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadPartnersAsync(ct);
        Form.Direction = "RECEIVED";
        Form.DocDate   = DateTime.Today;
        Form.DueDate   = DateTime.Today.AddDays(30);
        Form.Currency  = "TRY";
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) { await LoadPartnersAsync(ct); return Page(); }

        using var conn = db.Open();
        try
        {
            var id = Guid.NewGuid();
            if (IsNote)
            {
                await conn.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO PromissoryNote
                        (Id, CompanyId, Direction, NoteNo, DrawerName, DrawerTaxNo,
                         Amount, Currency, IssueDate, DueDate, Status, PartnerId, CreatedBy)
                    VALUES
                        (@Id, @CompanyId, @Direction, @DocNo, @DrawerName, @DrawerTaxNo,
                         @Amount, @Currency, @DocDate, @DueDate, 'PORTFOLIO', @PartnerId, @UserId)",
                    new { id, CompanyId = company.Id, Form.Direction, Form.DocNo, Form.DrawerName,
                          Form.DrawerTaxNo, Form.Amount, Form.Currency, Form.DocDate, Form.DueDate,
                          Form.PartnerId, UserId = user.Id }, cancellationToken: ct));
            }
            else
            {
                // İş kuralı: çek üstündeki numara (ChequeNo) kullanıcıdan; bizim iç kayıt no seriden
                var registryNo = await numberSeries.NextAsync(company.Id, NumberSeriesType.Cheque);
                await conn.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO Cheque
                        (Id, CompanyId, Direction, ChequeNo, RegistryNo, BankName, BranchName,
                         DrawerName, DrawerTaxNo, Amount, Currency, ChequeDate, DueDate,
                         Status, PartnerId, CreatedBy)
                    VALUES
                        (@Id, @CompanyId, @Direction, @DocNo, @RegistryNo, @BankName, @BranchName,
                         @DrawerName, @DrawerTaxNo, @Amount, @Currency, @DocDate, @DueDate,
                         'PORTFOLIO', @PartnerId, @UserId)",
                    new { id, CompanyId = company.Id, Form.Direction, Form.DocNo, RegistryNo = registryNo,
                          Form.BankName, Form.BranchName, Form.DrawerName, Form.DrawerTaxNo, Form.Amount,
                          Form.Currency, Form.DocDate, Form.DueDate, Form.PartnerId, UserId = user.Id },
                    cancellationToken: ct));
            }

            TempData["Success"] = IsNote ? "Senet eklendi." : "Çek eklendi.";
            return RedirectToPage("Details", new { id, type = Type });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Çek/senet ekleme hatası: {DocNo}", Form.DocNo);
            ModelState.AddModelError(string.Empty, "Kayıt sırasında veritabanı hatası oluştu.");
            await LoadPartnersAsync(ct);
            return Page();
        }
    }

    private async Task LoadPartnersAsync(CancellationToken ct = default)
    {
        using var conn = db.Open();
        Partners = (await conn.QueryAsync<DdlDto>(new CommandDefinition(@"
            SELECT Id, Code, Name FROM Partner
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Name",
            new { CompanyId = company.Id }, cancellationToken: ct))).ToList();
    }

    public class ChequeForm
    {
        public string   Direction   { get; set; } = "RECEIVED";
        public string   DocNo       { get; set; } = "";
        public string?  BankName    { get; set; }
        public string?  BranchName  { get; set; }
        public string   DrawerName  { get; set; } = "";
        public string?  DrawerTaxNo { get; set; }
        public decimal  Amount      { get; set; }
        public string   Currency    { get; set; } = "TRY";
        public DateTime DocDate     { get; set; } = DateTime.Today;
        public DateTime DueDate     { get; set; } = DateTime.Today.AddDays(30);
        public Guid?    PartnerId   { get; set; }
    }
}
