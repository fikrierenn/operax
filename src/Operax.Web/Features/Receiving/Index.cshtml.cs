using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Receiving;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ParameterStore parameters) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    // Faturalama durumu filtresi: AWAITING (faturasız+kısmi) — "fatura bekleyen mal kabuller"
    [BindProperty(SupportsGet = true)]
    public string? Billing { get; set; }

    public IEnumerable<ReceivingDto> Documents { get; set; } = [];

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public int DraftCount { get; set; } = 0;
    public int PostedCount { get; set; } = 0;
    public int CancelledCount { get; set; } = 0;
    public int AwaitingInvoiceCount { get; set; } = 0;
    public int OverdueInvoiceCount { get; set; } = 0;

    // GRNI yaşlandırma eşiği — parametrik (Parameter: INVOICE_AGING_DAYS), kayıt yoksa 30 gün varsayılan
    public int InvoiceAgingThresholdDays { get; set; } = 30;

    public async Task OnGetAsync(CancellationToken ct)
    {
        using var conn = db.Open();

        // GRNI yaşlandırma eşiğini parametreden oku (Admin > Parametreler ile değiştirilebilir)
        InvoiceAgingThresholdDays = await parameters.GetIntAsync("INVOICE_AGING_DAYS", 30);

        // KPI sayaçları: filtre uygulanmadan tüm belgelerin durum dağılımı
        const string countSql = @"
            SELECT r.Status, COUNT(*) AS Cnt
            FROM ReceivingHeader r
            WHERE r.CompanyId = @CompanyId AND r.IsDeleted = 0
            GROUP BY r.Status";

        var statusCounts = await conn.QueryAsync<(string Status, int Cnt)>(
            new CommandDefinition(countSql, new { CompanyId = company.Id }, cancellationToken: ct));
        foreach (var (status, cnt) in statusCounts)
        {
            if (status == DocStatus.Draft)     DraftCount     = cnt;
            if (status == DocStatus.Posted)    PostedCount    = cnt;
            if (status == DocStatus.Cancelled) CancelledCount = cnt;
        }

        // Fatura bekleyen (POSTED + faturasız/kısmi) + geciken (eşik aşan) sayaçları — GRNI
        var grni = await conn.QueryFirstAsync<(int Awaiting, int Overdue)>(new CommandDefinition(@"
            SELECT
                COUNT(*) AS Awaiting,
                SUM(CASE WHEN DATEDIFF(DAY, r.DocDate, CAST(GETUTCDATE() AS DATE)) >= @Threshold THEN 1 ELSE 0 END) AS Overdue
            FROM ReceivingHeader r
            WHERE r.CompanyId = @CompanyId AND r.IsDeleted = 0 AND r.Status = @Posted
              AND EXISTS (SELECT 1 FROM ReceivingLine l
                          WHERE l.HeaderId = r.Id AND (l.QtyBase - l.InvoicedQty) > 0.000001)",
            new { CompanyId = company.Id, Posted = DocStatus.Posted, Threshold = InvoiceAgingThresholdDays },
            cancellationToken: ct));
        AwaitingInvoiceCount = grni.Awaiting;
        OverdueInvoiceCount  = grni.Overdue;

        // Faturalama durumu satır toplamlarından türetilir (BillingStatus):
        //   NONE (faturasız) · PARTIAL (kısmi) · FULL (faturalı). Yalnız POSTED belge için anlamlı.
        // Billing=AWAITING filtresi faturasız+kısmi POSTED belgeleri getirir (açıkta kalanlar).
        const string sql = @"
            SELECT r.Id, r.DocNo, r.DocDate, r.Status, p.Name as PartnerName, wh.Code as WarehouseCode,
                   CASE WHEN r.Status <> @Posted THEN NULL
                        WHEN ISNULL(agg.Inv,0) <= 0.000001 THEN 'NONE'
                        WHEN agg.Inv < agg.Rcv - 0.000001 THEN 'PARTIAL'
                        ELSE 'FULL' END AS BillingStatus,
                   DATEDIFF(DAY, r.DocDate, CAST(GETUTCDATE() AS DATE)) AS DaysSinceReceipt
            FROM ReceivingHeader r
            JOIN Partner p  ON p.Id  = r.PartnerId
            JOIN Warehouse wh ON wh.Id = r.WarehouseId
            OUTER APPLY (SELECT SUM(l.QtyBase) AS Rcv, SUM(l.InvoicedQty) AS Inv
                         FROM ReceivingLine l WHERE l.HeaderId = r.Id) agg
            WHERE r.CompanyId = @CompanyId
              AND r.IsDeleted = 0
              AND (@Q      IS NULL OR @Q      = '' OR r.DocNo LIKE '%' + @Q + '%' OR p.Name LIKE '%' + @Q + '%')
              AND (@Status IS NULL OR @Status = '' OR r.Status = @Status)
              AND (@Billing <> 'AWAITING' OR (r.Status = @Posted
                   AND EXISTS (SELECT 1 FROM ReceivingLine l2
                               WHERE l2.HeaderId = r.Id AND (l2.QtyBase - l2.InvoicedQty) > 0.000001)))
            ORDER BY r.DocDate DESC, r.DocNo DESC
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM ReceivingHeader r
            JOIN Partner p ON p.Id = r.PartnerId
            WHERE r.CompanyId = @CompanyId AND r.IsDeleted = 0
              AND (@Q      IS NULL OR @Q      = '' OR r.DocNo LIKE '%' + @Q + '%' OR p.Name LIKE '%' + @Q + '%')
              AND (@Status IS NULL OR @Status = '' OR r.Status = @Status)
              AND (@Billing <> 'AWAITING' OR (r.Status = @Posted
                   AND EXISTS (SELECT 1 FROM ReceivingLine l2
                               WHERE l2.HeaderId = r.Id AND (l2.QtyBase - l2.InvoicedQty) > 0.000001)));";

        var page = Page < 1 ? 1 : Page;
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            CompanyId = company.Id,
            Q         = Q,
            Status    = Status,
            Billing   = Billing ?? "",
            Posted    = DocStatus.Posted,
            Page      = page,
            PageSize
        }, cancellationToken: ct));
        Documents = (await grid.ReadAsync<ReceivingDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
    }

    public record ReceivingDto(Guid Id, string DocNo, DateTime DocDate, string Status,
        string PartnerName, string WarehouseCode, string? BillingStatus, int DaysSinceReceipt);
}
