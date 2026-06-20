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
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, ILogger<DetailsModel> logger) : PageModel
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
                SELECT n.Id, n.Direction, n.NoteNo AS DocNo, NULL AS RegistryNo, NULL AS BankName, NULL AS BranchName,
                       n.DrawerName, n.DrawerTaxNo, n.Amount, n.Currency,
                       n.IssueDate AS DocDate, n.DueDate, n.Status,
                       n.DepositedAt, n.CollectedAt, n.ReturnReason,
                       p.Name AS PartnerName
                FROM PromissoryNote n
                LEFT JOIN Partner p ON p.Id = n.PartnerId
                WHERE n.Id = @Id AND n.CompanyId = @CompanyId AND n.IsDeleted = 0", p)
            : await conn.QuerySingleOrDefaultAsync<ChequeInfoDto>(@"
                SELECT c.Id, c.Direction, c.ChequeNo AS DocNo, c.RegistryNo, c.BankName, c.BranchName,
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

    // Bankaya verme — çek ve senet için ayrı SP (type'a göre)
    // İş kuralı: CompanyId SP'ye geçirilerek IDOR koruması sağlanır (güvenlik)
    public async Task<IActionResult> OnPostDepositAsync(Guid accountId)
        => IsNote
            ? await RunSpAsync("sp_DepositNote",
                new { NoteId = Id, AccountId = accountId, CompanyId = company.Id, DepositDate = (DateTime?)null, UserId = user.Id },
                "Senet bankaya tahsile verildi.", "DEPOSITED")
            : await RunSpAsync("sp_DepositCheque",
                new { ChequeId = Id, AccountId = accountId, CompanyId = company.Id, DepositDate = (DateTime?)null, UserId = user.Id },
                "Çek bankaya tahsile verildi.", "DEPOSITED");

    public async Task<IActionResult> OnPostCollectAsync()
        => IsNote
            ? await RunSpAsync("sp_CollectNote",
                new { NoteId = Id, CompanyId = company.Id, CollectDate = (DateTime?)null, UserId = user.Id },
                "Senet tahsil edildi, banka hesabına gelir işlendi.", "COLLECTED")
            : await RunSpAsync("sp_CollectCheque",
                new { ChequeId = Id, CompanyId = company.Id, CollectDate = (DateTime?)null, UserId = user.Id },
                "Çek tahsil edildi, banka hesabına gelir işlendi.", "COLLECTED");

    public async Task<IActionResult> OnPostReturnAsync(string reason)
        => IsNote
            ? await RunSpAsync("sp_ReturnNote",
                new { NoteId = Id, CompanyId = company.Id, Reason = reason ?? "Karşılıksız", UserId = user.Id },
                "Senet karşılıksız olarak işaretlendi.", "RETURNED")
            : await RunSpAsync("sp_ReturnCheque",
                new { ChequeId = Id, CompanyId = company.Id, Reason = reason ?? "Karşılıksız", UserId = user.Id },
                "Çek karşılıksız olarak işaretlendi.", "RETURNED");

    // Ortak SP çağrı + hata yakalama + audit izi helper'ı
    private async Task<IActionResult> RunSpAsync(string sp, object args, string successMsg, string auditAction)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(sp, args, commandType: CommandType.StoredProcedure);
            // Audit izi (A-4): finansal kıymet statü değişimi — security-principles zorunlu kıldığı kayıt
            await audit.LogAsync(auditAction, IsNote ? "PromissoryNote" : "Cheque", Id, $"{sp} · {Type}");
            TempData["Success"] = successMsg;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Çek işlemi hatası: {Sp} {Id}", sp, Id);
            TempData["Error"] = "İşlem sırasında veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id = Id, type = Type });
    }

    public record ChequeInfoDto(
        Guid Id, string Direction, string DocNo, string? RegistryNo, string? BankName, string? BranchName,
        string DrawerName, string? DrawerTaxNo, decimal Amount, string Currency,
        DateTime DocDate, DateTime DueDate, string Status,
        DateTime? DepositedAt, DateTime? CollectedAt, string? ReturnReason,
        string? PartnerName);
}
