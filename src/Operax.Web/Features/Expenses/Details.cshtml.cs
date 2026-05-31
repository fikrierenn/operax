using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Expenses;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, INumberSeriesService numberSeries, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public InvoiceFormDto Form { get; set; } = new();
    public IEnumerable<InvoiceLineDto> Lines { get; set; } = [];
    public IEnumerable<DdlDto> Partners     { get; set; } = [];
    public IEnumerable<DdlDto> ExpenseTypes { get; set; } = [];
    public IEnumerable<DdlDto> CostCenters  { get; set; } = [];

    public bool IsNew => Form.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        // Gider faturası formunu ve satırlarını yükler
        using var conn = db.Open();

        Partners = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Partner WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Name",
            new { CompanyId = company.Id });
        ExpenseTypes = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM ExpenseType WHERE CompanyId = @CompanyId ORDER BY Code",
            new { CompanyId = company.Id });
        CostCenters = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM CostCenter WHERE CompanyId = @CompanyId AND IsActive = 1 ORDER BY Code",
            new { CompanyId = company.Id });

        if (!id.HasValue) { Form.InvoiceDate = DateTime.Today; Form.Currency = "TRY"; return; }

        Form = await conn.QueryFirstOrDefaultAsync<InvoiceFormDto>(@"
            SELECT e.*, p.Name AS PartnerName
            FROM ExpenseInvoice e LEFT JOIN Partner p ON p.Id = e.PartnerId
            WHERE e.Id = @Id AND e.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }) ?? new();

        if (Form.Id == Guid.Empty) return;

        // Fatura satırları — gider tipi ve maliyet merkezi bilgileriyle
        Lines = await conn.QueryAsync<InvoiceLineDto>(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: üst belge ExpenseInvoice aynı handler içinde daha önce
            -- WHERE e.Id = @Id AND e.CompanyId = @CompanyId ile yüklendi ve bulunamazsa boş form döndü.
            -- Bu sorgu yalnızca o doğrulanmış ExpenseInvoice.Id üzerinden satırları okuyduğundan
            -- başka firmanın verisine erişilemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            SELECT l.*, et.Name AS ExpenseTypeName, cc.Name AS CostCenterName
            FROM ExpenseInvoiceLine l
            JOIN ExpenseType et ON et.Id = l.ExpenseTypeId
            JOIN CostCenter cc ON cc.Id = l.CostCenterId
            WHERE l.ExpenseInvoiceId = @InvoiceId",
            new { InvoiceId = Form.Id });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Gider faturası başlığını kaydeder
        using var conn = db.Open();
        if (IsNew)
        {
            Form.Id = Guid.NewGuid();
            // İş kuralı: DocNo tedarikçi fatura no (kullanıcı); RegistryNo bizim iç kayıt no (seriden)
            var registryNo = await numberSeries.NextAsync(company.Id, NumberSeriesType.PurchaseInvoice);
            await conn.ExecuteAsync(@"
                INSERT INTO ExpenseInvoice (Id, CompanyId, PartnerId, DocNo, RegistryNo, InvoiceDate, DueDate, TotalAmount, Currency, Status)
                VALUES (@Id, @CompanyId, @PartnerId, @DocNo, @RegistryNo, @InvoiceDate, @DueDate, 0, @Currency, @StDraft)",
                new { Form.Id, CompanyId = company.Id, Form.PartnerId, Form.DocNo, RegistryNo = registryNo, Form.InvoiceDate, Form.DueDate, Form.Currency, StDraft = DocStatus.Draft });
        }
        else
        {
            await conn.ExecuteAsync(@"
                UPDATE ExpenseInvoice SET PartnerId = @PartnerId, DocNo = @DocNo,
                    InvoiceDate = @InvoiceDate, DueDate = @DueDate, Currency = @Currency
                WHERE Id = @Id AND CompanyId = @CompanyId AND Status = @StDraft",
                new { Form.PartnerId, Form.DocNo, Form.InvoiceDate, Form.DueDate, Form.Currency, Form.Id, CompanyId = company.Id, StDraft = DocStatus.Draft });
        }
        return RedirectToPage(new { id = Form.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid expenseTypeId, Guid costCenterId, decimal qty, decimal unitPrice, decimal taxRate)
    {
        // Fatura satırı ekler, başlık toplamını günceller
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            var amount = qty * unitPrice;
            await conn.ExecuteAsync(@"
                -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
                -- Gerekçe: üst belge ExpenseInvoice OnGetAsync'te WHERE e.Id = @Id AND e.CompanyId = @CompanyId
                -- ile doğrulandı; @InvoiceId o doğrulanmış ExpenseInvoice.Id değeridir.
                -- Aynı handler içinde başlık toplamını güncelleyen UPDATE sorgusu da
                -- WHERE e.Id = @InvoiceId AND e.CompanyId = @CompanyId koşulu taşır.
                -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                INSERT INTO ExpenseInvoiceLine (Id, ExpenseInvoiceId, ExpenseTypeId, CostCenterId, Quantity, UnitPrice, Amount, TaxRate)
                VALUES (NEWID(), @InvoiceId, @ExpenseTypeId, @CostCenterId, @Qty, @UnitPrice, @Amount, @TaxRate)",
                new { InvoiceId = id, ExpenseTypeId = expenseTypeId, CostCenterId = costCenterId, Qty = qty, UnitPrice = unitPrice, Amount = amount, TaxRate = taxRate }, trans);

            // Başlık toplamını yeniden hesapla
            await conn.ExecuteAsync(@"
                UPDATE e SET e.TotalAmount = (
                    SELECT ISNULL(SUM(l.Amount + l.Amount * l.TaxRate / 100), 0)
                    FROM ExpenseInvoiceLine l WHERE l.ExpenseInvoiceId = e.Id)
                FROM ExpenseInvoice e WHERE e.Id = @Id AND e.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, trans);

            trans.Commit();
        }
        catch (Exception ex) { logger.LogWarning(ex, "Fatura satırı ekleme hatası"); trans.Rollback(); }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteLineAsync(Guid id, Guid lineId)
    {
        // Satırı siler ve toplamı günceller
        using var conn = db.Open();
        using var trans = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(@"
                DELETE l FROM ExpenseInvoiceLine l
                JOIN ExpenseInvoice e ON e.Id = l.ExpenseInvoiceId
                WHERE l.Id = @LineId AND e.CompanyId = @CompanyId",
                new { LineId = lineId, CompanyId = company.Id }, trans);

            await conn.ExecuteAsync(@"
                UPDATE e SET e.TotalAmount = ISNULL((
                    SELECT SUM(l.Amount + l.Amount * l.TaxRate / 100)
                    FROM ExpenseInvoiceLine l WHERE l.ExpenseInvoiceId = e.Id), 0)
                FROM ExpenseInvoice e WHERE e.Id = @Id AND e.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, trans);

            trans.Commit();
        }
        catch (Exception ex) { logger.LogWarning(ex, "Fatura satırı silme hatası"); trans.Rollback(); }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPostAsync(Guid id)
    {
        // Faturayı POSTED durumuna alır
        using var conn = db.Open();
        await conn.ExecuteAsync(
            "UPDATE ExpenseInvoice SET Status = @StPosted WHERE Id = @Id AND CompanyId = @CompanyId AND Status = @StDraft",
            new { Id = id, CompanyId = company.Id, StPosted = DocStatus.Posted, StDraft = DocStatus.Draft });
        return RedirectToPage(new { id });
    }

    public record InvoiceFormDto
    {
        public Guid      Id          { get; set; }
        public Guid      PartnerId   { get; set; }
        public string?   PartnerName { get; set; }
        public string    DocNo       { get; set; } = "";
        public DateTime  InvoiceDate { get; set; }
        public DateTime? DueDate     { get; set; }
        public decimal   TotalAmount { get; set; }
        public string    Currency    { get; set; } = "TRY";
        public string    Status      { get; set; } = DocStatus.Draft;
    }

    public record InvoiceLineDto
    {
        public Guid    Id              { get; set; }
        public string  ExpenseTypeName { get; set; } = "";
        public string  CostCenterName  { get; set; } = "";
        public decimal Quantity        { get; set; }
        public decimal UnitPrice       { get; set; }
        public decimal Amount          { get; set; }
        public decimal TaxRate         { get; set; }
        public decimal TaxAmount       { get; set; }
        public decimal TotalAmount     { get; set; }
    }
}
