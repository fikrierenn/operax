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

    // Cari bakiye ve hareket bilgileri (sadece mevcut kayıtta yüklenir)
    public BalanceSummaryDto?      Balance      { get; set; }
    public List<TxRowDto>          Transactions { get; set; } = [];
    public VadeAnalysisDto?        VadeAnalysis { get; set; }

    public async Task OnGetAsync(Guid? id)
    {
        if (id.HasValue)
        {
            using var conn = db.Open();
            var p = new { Id = id, CompanyId = company.Id };

            Partner = await conn.QueryFirstOrDefaultAsync<PartnerDto>(
                @"SELECT Id, Code, Name, Type, TaxNumber, Email, Phone, Address,
                         IsActive, Notes,
                         PaymentTermDays, CreditLimit, BlockOnLimitExceed,
                         RiskScore, RiskCategory, MaxOverdueDays,
                         DefaultPaymentMethod,
                         EFaturaMukellef, EFaturaAlias, IbanForRefund
                  FROM Partner WHERE Id = @Id AND CompanyId = @CompanyId", p) ?? new();

            if (Partner.Id != Guid.Empty)
                await LoadLedgerAsync(conn, Partner.Id);
        }
        else
        {
            Partner.IsActive             = true;
            Partner.Type                 = "BOTH";
            Partner.RiskScore            = 3;
            Partner.RiskCategory         = "MEDIUM";
            Partner.MaxOverdueDays       = 30;
            Partner.PaymentTermDays      = 30;
            Partner.DefaultPaymentMethod = "EFT";
        }
    }

    // Bakiye + hareket + vade analizi — QueryMultiple ile tek round-trip
    private async Task LoadLedgerAsync(System.Data.IDbConnection conn, Guid partnerId)
    {
        var p = new { CompanyId = company.Id, PartnerId = partnerId };
        using var multi = await conn.QueryMultipleAsync(@"
            -- 1) Bakiye özeti (alacak + borç ayrı)
            SELECT
                ISNULL(SUM(CASE WHEN Direction = 'RECEIVABLE' THEN TotalOpen ELSE 0 END), 0) AS TotalReceivable,
                ISNULL(SUM(CASE WHEN Direction = 'PAYABLE'    THEN TotalOpen ELSE 0 END), 0) AS TotalPayable,
                ISNULL(SUM(CASE WHEN Direction = 'RECEIVABLE' AND TotalOpen > 0 THEN TotalOpen ELSE 0 END)
                     - SUM(CASE WHEN Direction = 'PAYABLE'    AND TotalOpen > 0 THEN TotalOpen ELSE 0 END), 0) AS NetBalance
            FROM dbo.tvf_PaymentPlanAging(@CompanyId)
            WHERE PartnerId = @PartnerId;

            -- 2) Son 30 finansal hareket
            SELECT TOP 30
                t.Id, t.TransactionDate, t.TransactionType,
                t.Amount, t.AmountTRY, t.Currency,
                t.Description, t.InstrumentType,
                t.SourceDocType, t.SourceDocNo,
                a.Name AS AccountName
            FROM FinancialTransaction t
            LEFT JOIN FinancialAccount a ON a.Id = t.AccountId
            WHERE t.CompanyId = @CompanyId AND t.PartnerId = @PartnerId AND t.IsDeleted = 0
            ORDER BY t.TransactionDate DESC, t.CreatedAt DESC;

            -- 3) Ödeme davranışı analizi
            SELECT Direction, PaidCount, AvgDelayDays, AvgInvoiceAmount, TotalPaidAmount, LastPayment
            FROM v_PartnerVadeAnalysis
            WHERE CompanyId = @CompanyId AND PartnerId = @PartnerId;", p);

        Balance      = await multi.ReadFirstOrDefaultAsync<BalanceSummaryDto>();
        Transactions = (await multi.ReadAsync<TxRowDto>()).ToList();
        VadeAnalysis = await multi.ReadFirstOrDefaultAsync<VadeAnalysisDto>();
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
        public int     PaymentTermDays       { get; set; } = 30;
        public decimal CreditLimit           { get; set; }
        public bool    BlockOnLimitExceed    { get; set; }
        public byte    RiskScore             { get; set; } = 3;
        public string  RiskCategory          { get; set; } = "MEDIUM";
        public int     MaxOverdueDays        { get; set; } = 30;
        public string  DefaultPaymentMethod  { get; set; } = "EFT";
        public bool    EFaturaMukellef       { get; set; }
        public string? EFaturaAlias          { get; set; }
        public string? IbanForRefund         { get; set; }
    }

    public record BalanceSummaryDto(decimal TotalReceivable, decimal TotalPayable, decimal NetBalance);

    public record TxRowDto(
        Guid     Id,
        DateTime TransactionDate,
        string   TransactionType,
        decimal  Amount,
        decimal  AmountTRY,
        string   Currency,
        string?  Description,
        string?  InstrumentType,
        string?  SourceDocType,
        string?  SourceDocNo,
        string?  AccountName);

    public record VadeAnalysisDto(
        string   Direction,
        int      PaidCount,
        decimal? AvgDelayDays,
        decimal? AvgInvoiceAmount,
        decimal  TotalPaidAmount,
        DateTime? LastPayment);
}
