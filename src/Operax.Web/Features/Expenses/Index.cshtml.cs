using Microsoft.AspNetCore.Authorization;
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

    public async Task OnGetAsync()
    {
        // Şirkete ait tüm gider faturalarını listeler
        using var conn = db.Open();
        Invoices = await conn.QueryAsync<ExpenseInvoiceDto>(@"
            SELECT TOP 200
                e.Id, e.DocNo, e.InvoiceDate, e.DueDate,
                e.TotalAmount, e.Currency, e.Status,
                p.Name AS PartnerName
            FROM ExpenseInvoice e
            LEFT JOIN Partner p ON p.Id = e.PartnerId
            WHERE e.CompanyId = @CompanyId
            ORDER BY e.InvoiceDate DESC",
            new { CompanyId = company.Id });

        TotalDraft  = Invoices.Where(x => x.Status == DocStatus.Draft).Sum(x => x.TotalAmount);
        TotalPosted = Invoices.Where(x => x.Status == DocStatus.Posted).Sum(x => x.TotalAmount);
        TotalPaid   = Invoices.Where(x => x.Status == DocStatus.Paid).Sum(x => x.TotalAmount);
    }

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
