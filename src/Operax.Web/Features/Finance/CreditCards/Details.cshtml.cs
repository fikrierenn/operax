using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.CreditCards;

/// <summary>
/// Kredi kartı detayı — kart bilgileri + ekstre listesi + son slip işlemleri + ekstre ödeme.
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }

    public CardInfoDto?         Card         { get; set; }
    public List<StatementDto>   Statements   { get; set; } = [];
    public List<TxnDto>         Transactions { get; set; } = [];
    public List<DdlDto>         BankAccounts { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Id == Guid.Empty) return RedirectToPage("Index");

        using var conn = db.Open();
        var p = new { CompanyId = company.Id, Id };

        Card = await conn.QuerySingleOrDefaultAsync<CardInfoDto>(new CommandDefinition(@"
            SELECT cc.Id, cc.CardNoMasked, cc.HolderName, cc.BankName, cc.CardType,
                   cc.CreditLimit, cc.AvailableLimit, cc.StatementDay, cc.DueDay,
                   cc.InterestRate, cc.Currency, cc.ExpiresAt,
                   ba.Name AS LinkedBankAccountName
            FROM CreditCard cc
            LEFT JOIN FinancialAccount ba ON ba.Id = cc.LinkedBankAccountId
            WHERE cc.Id = @Id AND cc.CompanyId = @CompanyId AND cc.IsDeleted = 0", p, cancellationToken: ct));

        if (Card == null) return NotFound();

        Statements = (await conn.QueryAsync<StatementDto>(new CommandDefinition(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: üst kayıt CreditCard aynı handler içinde daha önce
            -- WHERE cc.Id = @Id AND cc.CompanyId = @CompanyId ile yüklendi; bulunamazsa NotFound döndü.
            -- Bu sorgu yalnızca o doğrulanmış CreditCard.Id üzerinden ekstreleri okuyduğundan
            -- başka firmanın kredi kartı ekstresine erişilemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            SELECT TOP 12 Id, PeriodStart, PeriodEnd, StatementDate, DueDate,
                   ClosingBalance, MinPayment, PaidAmount, IsClosed
            FROM CreditCardStatement
            WHERE CardId = @Id
            ORDER BY PeriodEnd DESC", new { Id }, cancellationToken: ct))).ToList();

        Transactions = (await conn.QueryAsync<TxnDto>(new CommandDefinition(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: üst kayıt CreditCard aynı handler içinde daha önce
            -- WHERE cc.Id = @Id AND cc.CompanyId = @CompanyId ile yüklendi; bulunamazsa NotFound döndü.
            -- Bu sorgu yalnızca o doğrulanmış CreditCard.Id üzerinden slip işlemlerini okuyduğundan
            -- başka firmanın kart hareketlerine erişilemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            SELECT TOP 30 Id, TransactionDate, MerchantName, Category,
                   Amount, Currency, InstallmentCount, InstallmentNo
            FROM CreditCardTransaction
            WHERE CardId = @Id
            ORDER BY TransactionDate DESC", new { Id }, cancellationToken: ct))).ToList();

        BankAccounts = (await conn.QueryAsync<DdlDto>(new CommandDefinition(@"
            SELECT Id, Code, Name FROM FinancialAccount
            WHERE CompanyId = @CompanyId AND AccountType = 'BANK' AND IsDeleted = 0
            ORDER BY Name", new { CompanyId = company.Id }, cancellationToken: ct))).ToList();

        logger.LogInformation("Kredi kartı detayı görüntülendi: {CardId}", Id);
        return Page();
    }

    // Ekstre ödeme — banka hesabından gider, ekstre kapatma, limit iadesi
    public async Task<IActionResult> OnPostPayStatementAsync(Guid statementId, Guid fromAccountId, decimal amount, CancellationToken ct)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_PayCreditCardStatement",
                new { StatementId = statementId, CompanyId = company.Id,
                      FromAccountId = fromAccountId, Amount = amount,
                      PayDate = (DateTime?)null, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
            // Audit izi (A-4): kredi kartı ekstre ödemesi — finansal mutasyon
            await audit.LogAsync("STATEMENT_PAID", "CreditCardStatement", statementId, $"Tutar: {amount}");
            TempData["Success"] = "Ekstre ödemesi yapıldı.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Ekstre ödeme hatası: {StatementId}", statementId);
            TempData["Error"] = "Ekstre ödenirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id = Id });
    }

    public record CardInfoDto(
        Guid Id, string CardNoMasked, string HolderName, string BankName, string CardType,
        decimal CreditLimit, decimal AvailableLimit, int StatementDay, int DueDay,
        decimal? InterestRate, string Currency, DateTime? ExpiresAt,
        string? LinkedBankAccountName)
    {
        public decimal UsedLimit => CreditLimit - AvailableLimit;
        public decimal UsagePercent => CreditLimit > 0 ? System.Math.Round(UsedLimit / CreditLimit * 100, 1) : 0;
    }

    public record StatementDto(
        Guid Id, DateTime PeriodStart, DateTime PeriodEnd, DateTime StatementDate, DateTime DueDate,
        decimal ClosingBalance, decimal MinPayment, decimal PaidAmount, bool IsClosed);

    public record TxnDto(
        Guid Id, DateTime TransactionDate, string MerchantName, string? Category,
        decimal Amount, string Currency, int InstallmentCount, int InstallmentNo);
}
