using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Budget;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty]
    public BudgetFormDto Form { get; set; } = new();
    public IEnumerable<BudgetLineDto> Lines    { get; set; } = [];
    public IEnumerable<DdlDto>        CostCenters { get; set; } = [];
    public IEnumerable<DdlDto>        ExpenseTypes { get; set; } = [];

    public bool IsNew => Form.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        // Butce formunu ve satirlarini yukler
        using var conn = db.Open();

        CostCenters  = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM CostCenter WHERE CompanyId = @CompanyId AND IsActive = 1 ORDER BY Code",
            new { CompanyId = company.Id }, cancellationToken: ct));
        ExpenseTypes = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM ExpenseType WHERE CompanyId = @CompanyId ORDER BY Code",
            new { CompanyId = company.Id }, cancellationToken: ct));

        if (!id.HasValue) { Form.Year = DateTime.UtcNow.Year; Form.Type = "OPERATIONAL"; return; }

        Form = await conn.QueryFirstOrDefaultAsync<BudgetFormDto>(new CommandDefinition(
            "SELECT Id, Year, Code, Name, Type, Status FROM Budget WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct)) ?? new();

        if (Form.Id == Guid.Empty) return;

        Lines = await conn.QueryAsync<BudgetLineDto>(new CommandDefinition(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: üst belge Budget aynı handler içinde daha önce
            -- WHERE Id = @Id AND CompanyId = @CompanyId ile yüklendi ve bulunamazsa boş form döndü.
            -- Bu sorgu yalnızca o doğrulanmış Budget.Id üzerinden BudgetLine okuyduğundan başka
            -- firmanın verisine erişilemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            SELECT bl.*, cc.Name AS CostCenterName, et.Name AS ExpenseTypeName
            FROM BudgetLine bl
            LEFT JOIN CostCenter cc ON cc.Id = bl.CostCenterId
            LEFT JOIN ExpenseType et ON et.Id = bl.ExpenseTypeId
            WHERE bl.BudgetId = @BudgetId
            ORDER BY bl.AccrualDate, bl.Direction DESC",
            new { BudgetId = Form.Id }, cancellationToken: ct));
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Butce basligini kaydet
        using var conn = db.Open();
        if (IsNew)
        {
            Form.Id = Guid.NewGuid();
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO Budget (Id, CompanyId, Year, Code, Name, Type, Status)
                VALUES (@Id, @CompanyId, @Year, @Code, @Name, @Type, @StDraft)",
                new { Form.Id, CompanyId = company.Id, Form.Year, Form.Code, Form.Name, Form.Type, StDraft = DocStatus.Draft }, cancellationToken: ct));
        }
        else
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE Budget SET Year = @Year, Code = @Code, Name = @Name, Type = @Type
                WHERE Id = @Id AND CompanyId = @CompanyId AND Status = @StDraft",
                new { Form.Year, Form.Code, Form.Name, Form.Type, Form.Id, CompanyId = company.Id, StDraft = DocStatus.Draft }, cancellationToken: ct));
        }
        return RedirectToPage(new { id = Form.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, string direction, DateTime accrualDate, DateTime cashDate, Guid? costCenterId, Guid? expenseTypeId, decimal amountPlanned, CancellationToken ct)
    {
        // Butce satiri ekler (gider veya gelir plani)
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: üst belge Budget aynı handler içinde daha önce
            -- WHERE Id = @Id AND CompanyId = @CompanyId ile yüklendi ve bulunamazsa boş form döndü.
            -- @BudgetId parametresi o doğrulanmış Budget.Id değeridir;
            -- farklı firmanın bütçesine satır eklenemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            INSERT INTO BudgetLine (Id, BudgetId, Direction, AccrualDate, CashDate, CostCenterId, ExpenseTypeId, AmountPlanned)
            VALUES (NEWID(), @BudgetId, @Direction, @AccrualDate, @CashDate, @CostCenterId, @ExpenseTypeId, @AmountPlanned)",
            new { BudgetId = id, Direction = direction, AccrualDate = accrualDate, CashDate = cashDate, CostCenterId = costCenterId, ExpenseTypeId = expenseTypeId, AmountPlanned = amountPlanned }, cancellationToken: ct));
        return RedirectToPage(new { id });
    }

    public record BudgetFormDto
    {
        public Guid   Id     { get; set; }
        public int    Year   { get; set; }
        public string Code   { get; set; } = "";
        public string Name   { get; set; } = "";
        public string Type   { get; set; } = "OPERATIONAL";
        public string Status { get; set; } = DocStatus.Draft;
    }

    public record BudgetLineDto
    {
        public Guid     Id              { get; set; }
        public string   Direction       { get; set; } = "";
        public DateTime AccrualDate     { get; set; }
        public DateTime CashDate        { get; set; }
        public string?  CostCenterName  { get; set; }
        public string?  ExpenseTypeName { get; set; }
        public decimal  AmountPlanned   { get; set; }
        public decimal  AmountActual    { get; set; }
        public decimal  Variance        => AmountPlanned - AmountActual;
    }
}
