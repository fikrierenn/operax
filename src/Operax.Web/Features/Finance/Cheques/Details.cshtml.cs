using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Cheques;

/// <summary>
/// Çek / Senet detayı + statü işlemleri (bankaya ver, tahsil et, karşılıksız).
/// type=cheque → Cheque tablosu, type=note → PromissoryNote.
/// İşlemler sp_DepositCheque / sp_CollectCheque / sp_ReturnCheque ile yapılır.
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public string Type { get; set; } = "cheque";

    public bool IsNote => Type == "note";

    public ChequeInfoDto?  Cheque       { get; set; }
    public List<DdlDto>    BankAccounts { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (Id == Guid.Empty) return RedirectToPage("Index");

        using var conn = db.Open();
        var p = new { CompanyId = company.Id, Id };

        // İki ayrı sabit sorgu — tablo adı interpolation yasağı (IMP-1 düzeltme)
        Cheque = IsNote
            ? await conn.QuerySingleOrDefaultAsync<ChequeInfoDto>(@"
                SELECT n.Id, n.Direction, n.NoteNo AS DocNo, NULL AS BankName, NULL AS BranchName,
                       n.DrawerName, n.DrawerTaxNo, n.Amount, n.Currency,
                       n.IssueDate AS DocDate, n.DueDate, n.Status,
                       n.DepositedAt, n.CollectedAt, n.ReturnReason,
                       p.Name AS PartnerName
                FROM PromissoryNote n
                LEFT JOIN Partner p ON p.Id = n.PartnerId
                WHERE n.Id = @Id AND n.CompanyId = @CompanyId AND n.IsDeleted = 0", p)
            : await conn.QuerySingleOrDefaultAsync<ChequeInfoDto>(@"
                SELECT c.Id, c.Direction, c.ChequeNo AS DocNo, c.BankName, c.BranchName,
                       c.DrawerName, c.DrawerTaxNo, c.Amount, c.Currency,
                       c.ChequeDate AS DocDate, c.DueDate, c.Status,
                       c.DepositedAt, c.CollectedAt, c.ReturnReason,
                       p.Name AS PartnerName
                FROM Cheque c
                LEFT JOIN Partner p ON p.Id = c.PartnerId
                WHERE c.Id = @Id AND c.CompanyId = @CompanyId AND c.IsDeleted = 0", p);

        if (Cheque == null) return NotFound();

        BankAccounts = (await conn.QueryAsync<DdlDto>(@"
            SELECT Id, Code, Name FROM FinancialAccount
            WHERE CompanyId = @CompanyId AND AccountType = 'BANK' AND IsDeleted = 0
            ORDER BY Name", new { CompanyId = company.Id })).ToList();

        return Page();
    }

    // Bankaya verme — sadece çekte (senet SP'leri henüz yok, ileride sp_DepositNote)
    public async Task<IActionResult> OnPostDepositAsync(Guid accountId)
        => await RunSpAsync("sp_DepositCheque",
            new { ChequeId = Id, AccountId = accountId, DepositDate = (DateTime?)null, UserId = user.Id },
            "Çek bankaya tahsile verildi.");

    public async Task<IActionResult> OnPostCollectAsync()
        => await RunSpAsync("sp_CollectCheque",
            new { ChequeId = Id, CollectDate = (DateTime?)null, UserId = user.Id },
            "Çek tahsil edildi, banka hesabına gelir işlendi.");

    public async Task<IActionResult> OnPostReturnAsync(string reason)
        => await RunSpAsync("sp_ReturnCheque",
            new { ChequeId = Id, Reason = reason ?? "Karşılıksız", UserId = user.Id },
            "Çek karşılıksız olarak işaretlendi.");

    // Ortak SP çağrı + hata yakalama helper'ı
    private async Task<IActionResult> RunSpAsync(string sp, object args, string successMsg)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(sp, args, commandType: CommandType.StoredProcedure);
            TempData["Success"] = successMsg;
        }
        catch (Microsoft.Data.SqlClient.SqlException sex) when (sex.Number is >= 50000 and < 70000)
        {
            TempData["Error"] = sex.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sex)
        {
            logger.LogError(sex, "Çek işlemi hatası: {Sp} {Id}", sp, Id);
            TempData["Error"] = "İşlem sırasında veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id = Id, type = Type });
    }

    public record ChequeInfoDto(
        Guid Id, string Direction, string DocNo, string? BankName, string? BranchName,
        string DrawerName, string? DrawerTaxNo, decimal Amount, string Currency,
        DateTime DocDate, DateTime DueDate, string Status,
        DateTime? DepositedAt, DateTime? CollectedAt, string? ReturnReason,
        string? PartnerName);
}
