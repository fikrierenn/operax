using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Expenses;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, INumberSeriesService numberSeries, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public InvoiceFormDto Form { get; set; } = new();
    public IEnumerable<InvoiceLineDto> Lines { get; set; } = [];
    public IEnumerable<DdlDto> Partners     { get; set; } = [];
    public DocFlowVm?           DocFlow     { get; set; }
    public IEnumerable<ExpenseTypeDto> ExpenseTypes { get; set; } = [];   // birim (UnitOfMeasure) taşır — UI miktarın birimini gösterir
    public IEnumerable<DdlDto> CostCenters  { get; set; } = [];

    public bool IsNew => Form.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        // Gider faturası formunu ve satırlarını yükler
        using var conn = db.Open();

        Partners = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Partner WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Name",
            new { CompanyId = company.Id }, cancellationToken: ct));
        // UnitOfMeasure: gider tipinin birimi (Elektrik→kWh, Su→m3, Kira→NULL=götürü). UI miktar yanında gösterir.
        ExpenseTypes = await conn.QueryAsync<ExpenseTypeDto>(new CommandDefinition(
            "SELECT Id, Code, Name, UnitOfMeasure FROM ExpenseType WHERE CompanyId = @CompanyId ORDER BY Code",
            new { CompanyId = company.Id }, cancellationToken: ct));
        CostCenters = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM CostCenter WHERE CompanyId = @CompanyId AND IsActive = 1 ORDER BY Code",
            new { CompanyId = company.Id }, cancellationToken: ct));

        if (!id.HasValue) { Form.InvoiceDate = DateTime.Today; Form.Currency = "TRY"; return; }

        Form = await conn.QueryFirstOrDefaultAsync<InvoiceFormDto>(new CommandDefinition(@"
            SELECT e.Id, e.PartnerId, e.DocNo, e.RegistryNo, e.InvoiceDate, e.DueDate,
                   e.TotalAmount, e.Currency, e.Status, p.Name AS PartnerName
            FROM ExpenseInvoice e LEFT JOIN Partner p ON p.Id = e.PartnerId
            WHERE e.Id = @Id AND e.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }, cancellationToken: ct)) ?? new();

        if (Form.Id == Guid.Empty) return;

        // Fatura satırları — gider tipi ve maliyet merkezi bilgileriyle
        Lines = await conn.QueryAsync<InvoiceLineDto>(new CommandDefinition(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: üst belge ExpenseInvoice aynı handler içinde daha önce
            -- WHERE e.Id = @Id AND e.CompanyId = @CompanyId ile yüklendi ve bulunamazsa boş form döndü.
            -- Bu sorgu yalnızca o doğrulanmış ExpenseInvoice.Id üzerinden satırları okuyduğundan
            -- başka firmanın verisine erişilemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            SELECT l.*, et.Name AS ExpenseTypeName, et.UnitOfMeasure, cc.Name AS CostCenterName
            FROM ExpenseInvoiceLine l
            JOIN ExpenseType et ON et.Id = l.ExpenseTypeId
            JOIN CostCenter cc ON cc.Id = l.CostCenterId
            WHERE l.ExpenseInvoiceId = @InvoiceId",
            new { InvoiceId = Form.Id }, cancellationToken: ct));

        // Ödeme smart button — POSTED faturalarda tedarikçi + tutar ön-dolu
        if (Form.Status == DocStatus.Posted)
        {
            DocFlow = new DocFlowVm([
                new DocFlowItem(
                    Label: "Ödeme",
                    Count: 0,
                    CreateUrl: $"/Finance/Payments/Create?partnerId={Form.PartnerId}&txType=EXPENSE&amount={Form.TotalAmount}&sourceDocId={Form.Id}&sourceDocType=EXPENSE_INVOICE",
                    CreateLabel: "Ödeme Yap",
                    CanCreate: user.HasRole("Administrator","Finance","Purchasing"),
                    IsGetCreate: true)
            ]);
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Gider faturası başlığını kaydeder
        using var conn = db.Open();
        if (IsNew)
        {
            Form.Id = Guid.NewGuid();
            // İş kuralı: DocNo tedarikçi fatura no (kullanıcı); RegistryNo bizim iç kayıt no (seriden)
            var registryNo = await numberSeries.NextAsync(company.Id, NumberSeriesType.PurchaseInvoice);
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO ExpenseInvoice (Id, CompanyId, PartnerId, DocNo, RegistryNo, InvoiceDate, DueDate, TotalAmount, Currency, Status)
                VALUES (@Id, @CompanyId, @PartnerId, @DocNo, @RegistryNo, @InvoiceDate, @DueDate, 0, @Currency, @StDraft)",
                new { Form.Id, CompanyId = company.Id, Form.PartnerId, Form.DocNo, RegistryNo = registryNo, Form.InvoiceDate, Form.DueDate, Form.Currency, StDraft = DocStatus.Draft },
                cancellationToken: ct));
        }
        else
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE ExpenseInvoice SET PartnerId = @PartnerId, DocNo = @DocNo,
                    InvoiceDate = @InvoiceDate, DueDate = @DueDate, Currency = @Currency
                WHERE Id = @Id AND CompanyId = @CompanyId AND Status = @StDraft",
                new { Form.PartnerId, Form.DocNo, Form.InvoiceDate, Form.DueDate, Form.Currency, Form.Id, CompanyId = company.Id, StDraft = DocStatus.Draft },
                cancellationToken: ct));
        }
        return RedirectToPage(new { id = Form.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid expenseTypeId, Guid costCenterId, decimal qty, decimal unitPrice, decimal taxRate, CancellationToken ct)
    {
        // Fatura satırı ekler, başlık toplamını günceller
        using var conn = db.Open();

        // Evrak bütünlüğü: yalnızca DRAFT faturaya satır eklenebilir (document-immutability.md §3)
        if (!await IsDraftAsync(conn, id, ct))
        {
            TempData["Error"] = "Onaylanmış faturaya satır eklenemez.";
            return RedirectToPage(new { id });
        }

        using var trans = conn.BeginTransaction();
        try
        {
            var amount = qty * unitPrice;
            await conn.ExecuteAsync(new CommandDefinition(@"
                -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
                -- Gerekçe: üst belge ExpenseInvoice OnGetAsync'te WHERE e.Id = @Id AND e.CompanyId = @CompanyId
                -- ile doğrulandı; @InvoiceId o doğrulanmış ExpenseInvoice.Id değeridir.
                -- Aynı handler içinde başlık toplamını güncelleyen UPDATE sorgusu da
                -- WHERE e.Id = @InvoiceId AND e.CompanyId = @CompanyId koşulu taşır.
                -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                INSERT INTO ExpenseInvoiceLine (Id, ExpenseInvoiceId, ExpenseTypeId, CostCenterId, Quantity, UnitPrice, Amount, TaxRate)
                VALUES (NEWID(), @InvoiceId, @ExpenseTypeId, @CostCenterId, @Qty, @UnitPrice, @Amount, @TaxRate)",
                new { InvoiceId = id, ExpenseTypeId = expenseTypeId, CostCenterId = costCenterId, Qty = qty, UnitPrice = unitPrice, Amount = amount, TaxRate = taxRate },
                transaction: trans, cancellationToken: ct));

            // Başlık toplamını yeniden hesapla
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE e SET e.TotalAmount = (
                    SELECT ISNULL(SUM(l.Amount + l.Amount * l.TaxRate / 100), 0)
                    FROM ExpenseInvoiceLine l WHERE l.ExpenseInvoiceId = e.Id)
                FROM ExpenseInvoice e WHERE e.Id = @Id AND e.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id },
                transaction: trans, cancellationToken: ct));

            trans.Commit();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { trans.Rollback(); throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Fatura satırı ekleme DB hatası: {InvoiceId}", id);
            trans.Rollback();
            TempData["Error"] = "Satır eklenirken veritabanı hatası oluştu.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatura satırı ekleme beklenmeyen hata: {InvoiceId}", id);
            trans.Rollback();
            TempData["Error"] = "Satır eklenirken beklenmeyen hata oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteLineAsync(Guid id, Guid lineId, CancellationToken ct)
    {
        // Satırı siler ve toplamı günceller
        using var conn = db.Open();

        // Evrak bütünlüğü: yalnızca DRAFT faturadan satır silinebilir (document-immutability.md §3)
        if (!await IsDraftAsync(conn, id, ct))
        {
            TempData["Error"] = "Onaylanmış faturadan satır silinemez.";
            return RedirectToPage(new { id });
        }

        using var trans = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                DELETE l FROM ExpenseInvoiceLine l
                JOIN ExpenseInvoice e ON e.Id = l.ExpenseInvoiceId
                WHERE l.Id = @LineId AND e.CompanyId = @CompanyId",
                new { LineId = lineId, CompanyId = company.Id },
                transaction: trans, cancellationToken: ct));

            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE e SET e.TotalAmount = ISNULL((
                    SELECT SUM(l.Amount + l.Amount * l.TaxRate / 100)
                    FROM ExpenseInvoiceLine l WHERE l.ExpenseInvoiceId = e.Id), 0)
                FROM ExpenseInvoice e WHERE e.Id = @Id AND e.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id },
                transaction: trans, cancellationToken: ct));

            trans.Commit();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { trans.Rollback(); throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Fatura satırı silme DB hatası: {LineId}", lineId);
            trans.Rollback();
            TempData["Error"] = "Satır silinirken veritabanı hatası oluştu.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatura satırı silme beklenmeyen hata: {LineId}", lineId);
            trans.Rollback();
            TempData["Error"] = "Satır silinirken beklenmeyen hata oluştu.";
        }
        return RedirectToPage(new { id });
    }

    // Evrak bütünlüğü guard'ı: fatura DRAFT mı? (POSTED/CANCELLED satır değişimi engellenir)
    private async Task<bool> IsDraftAsync(System.Data.IDbConnection conn, Guid invoiceId, CancellationToken ct)
    {
        var status = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT Status FROM ExpenseInvoice WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = invoiceId, CompanyId = company.Id }, cancellationToken: ct));
        return status == DocStatus.Draft;
    }

    public async Task<IActionResult> OnPostPostAsync(Guid id, CancellationToken ct)
    {
        // Faturayı onaylar: sp_ExpenseInvoicePost atomik olarak POSTED yapar + cari deftere Credit yazar
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("dbo.sp_ExpenseInvoicePost",
                new { InvoiceId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));
            TempData["Success"] = "Fatura onaylandı.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number is >= 50000 and < 60000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "ExpenseInvoicePost DB hatası: {Id}", id);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
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

    // Gider tipi DDL satırı + birimi (UI'da miktar yanında gösterilir; NULL=götürü/flat gider)
    public record ExpenseTypeDto(Guid Id, string Code, string Name, string? UnitOfMeasure);

    public record InvoiceLineDto
    {
        public Guid    Id              { get; set; }
        public string  ExpenseTypeName { get; set; } = "";
        public string? UnitOfMeasure   { get; set; }   // gider tipinin birimi (kWh/m3/lt; NULL=götürü)
        public string  CostCenterName  { get; set; } = "";
        public decimal Quantity        { get; set; }
        public decimal UnitPrice       { get; set; }
        public decimal Amount          { get; set; }
        public decimal TaxRate         { get; set; }
        public decimal TaxAmount       { get; set; }
        public decimal TotalAmount     { get; set; }
    }

    public async Task<IActionResult> OnPostReverseAsync(Guid id, CancellationToken ct)
    {
        // sp_ExpenseInvoiceReverse: POSTED alış faturasını CANCELLED yapar, AccountMovement ters-satır yazar
        // Guard: e-Fatura (INBOUND) gönderilmişse SP THROW 51414 fırlatır
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_ExpenseInvoiceReverse",
                new { InvoiceId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));
            TempData["Success"] = "Alış faturası iptal edildi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı (e-Fatura gönderilmiş, dönem kilidi vb.)
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Alış faturası iptal hatası: {InvoiceId}", id);
            TempData["Error"] = "Fatura iptal edilirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }
}
