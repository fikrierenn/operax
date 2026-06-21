using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Expenses;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<ExpenseInvoiceDto> Invoices { get; set; } = [];
    public decimal TotalDraft  { get; set; }
    public decimal TotalPosted { get; set; }
    public decimal TotalPaid   { get; set; }

    // Sayfalama (PF-1) — Items/Index template'i (eski TOP 200 yerine)
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Şirkete ait gider faturaları (sayfalanmış) + durum bazlı tutar toplamları (rows yerine SQL)
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        const string sql = @"
            SELECT e.Id, e.DocNo, e.InvoiceDate, e.DueDate,
                   e.TotalAmount, e.Currency, e.Status, p.Name AS PartnerName
            FROM ExpenseInvoice e
            LEFT JOIN Partner p ON p.Id = e.PartnerId
            WHERE e.CompanyId = @CompanyId
            ORDER BY e.InvoiceDate DESC
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT
                COUNT(*) AS Total,
                ISNULL(SUM(CASE WHEN Status = @StDraft  THEN TotalAmount ELSE 0 END), 0) AS TotalDraft,
                ISNULL(SUM(CASE WHEN Status = @StPosted THEN TotalAmount ELSE 0 END), 0) AS TotalPosted,
                ISNULL(SUM(CASE WHEN Status = @StPaid   THEN TotalAmount ELSE 0 END), 0) AS TotalPaid
            FROM ExpenseInvoice WHERE CompanyId = @CompanyId;";

        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            CompanyId = company.Id, Page = page, PageSize,
            StDraft = DocStatus.Draft, StPosted = DocStatus.Posted, StPaid = DocStatus.Paid
        }, cancellationToken: ct));
        Invoices = (await grid.ReadAsync<ExpenseInvoiceDto>()).ToList();
        var agg = await grid.ReadSingleAsync<AggRow>();
        FilteredCount = agg.Total;
        TotalDraft    = agg.TotalDraft;
        TotalPosted   = agg.TotalPosted;
        TotalPaid     = agg.TotalPaid;
    }

    private record AggRow(int Total, decimal TotalDraft, decimal TotalPosted, decimal TotalPaid);

    public record ExpenseInvoiceDto
    {
        public Guid     Id          { get; set; }
        public string   DocNo       { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate    { get; set; }
        public decimal  TotalAmount { get; set; }
        public string   Currency    { get; set; } = "TRY";
        public string   Status      { get; set; } = "";
        public string?  PartnerName { get; set; }
        public bool     IsOverdue   => Status != "PAID" && DueDate.HasValue && DueDate < DateTime.UtcNow;
    }
}
