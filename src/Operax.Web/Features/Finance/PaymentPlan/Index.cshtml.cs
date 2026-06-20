using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.PaymentPlan;

/// <summary>
/// PaymentPlan listesi: vade planı + durum + cari + kaynak belge.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Direction { get; set; } = "all";  // all/RECEIVABLE/PAYABLE
    [BindProperty(SupportsGet = true)] public string Status    { get; set; } = "OPEN"; // OPEN/PARTIAL/PAID/OVERDUE/all

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public List<PlanRowDto> Plans       { get; set; } = [];
    public DirCountsDto     DirCounts   { get; set; } = new(0, 0, 0);
    public decimal          TotalReceivable { get; set; }
    public decimal          TotalPayable    { get; set; }

    // Vade planı listesini, yön sayaçlarını ve toplam tutarları yükler
    public async Task OnGetAsync()
    {
        try
        {
            using var conn = db.Open();
            var p = new { CompanyId = company.Id };

            DirCounts = await conn.QuerySingleAsync<DirCountsDto>(@"
                SELECT
                    COUNT(*) AS Total,
                    SUM(CASE WHEN Direction = 'RECEIVABLE' THEN 1 ELSE 0 END) AS Receivable,
                    SUM(CASE WHEN Direction = 'PAYABLE'    THEN 1 ELSE 0 END) AS Payable
                FROM PaymentPlan
                WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND Status IN ('OPEN','PARTIAL','OVERDUE')", p);

            TotalReceivable = await conn.ExecuteScalarAsync<decimal>(@"
                SELECT ISNULL(SUM(Amount - PaidAmount), 0)
                FROM PaymentPlan
                WHERE CompanyId = @CompanyId AND Direction = 'RECEIVABLE'
                  AND Status IN ('OPEN','PARTIAL','OVERDUE') AND IsDeleted = 0", p);
            TotalPayable = await conn.ExecuteScalarAsync<decimal>(@"
                SELECT ISNULL(SUM(Amount - PaidAmount), 0)
                FROM PaymentPlan
                WHERE CompanyId = @CompanyId AND Direction = 'PAYABLE'
                  AND Status IN ('OPEN','PARTIAL','OVERDUE') AND IsDeleted = 0", p);

            var page = Page < 1 ? 1 : Page;
            const string fromWhere = @"
                FROM PaymentPlan pp
                LEFT JOIN Partner p ON p.Id = pp.PartnerId
                WHERE pp.CompanyId = @CompanyId AND pp.IsDeleted = 0";

            var parms = new DynamicParameters();
            parms.Add("CompanyId", company.Id);
            parms.Add("Page", page);
            parms.Add("PageSize", PageSize);

            var filter = "";
            if (Direction != "all") { filter += " AND pp.Direction = @Direction"; parms.Add("Direction", Direction); }
            if (Status != "all")    { filter += " AND pp.Status = @Status";       parms.Add("Status", Status); }

            // Sayfa satırları + aynı filtrenin toplam sayısı tek round-trip (PF-1)
            var sql = $@"
                SELECT
                    pp.Id, pp.SourceDocType, pp.SourceDocNo,
                    pp.PartnerId, p.Name AS PartnerName,
                    pp.Direction, pp.InstallmentNo, pp.TotalInstallments,
                    pp.DueDate, pp.Amount, pp.PaidAmount, pp.Status,
                    pp.PaidAt,
                    DATEDIFF(DAY, pp.DueDate, GETUTCDATE()) AS DaysOverdue
                {fromWhere}{filter}
                ORDER BY pp.DueDate ASC
                OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

                SELECT COUNT(*) {fromWhere}{filter};";

            using var grid = await conn.QueryMultipleAsync(sql, parms);
            Plans = (await grid.ReadAsync<PlanRowDto>()).ToList();
            FilteredCount = await grid.ReadSingleAsync<int>();
        }
        catch (SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Vade planı listesi veri yükleme hatası");
            TempData["Error"] = "Veriler yüklenirken bir hata oluştu.";
        }
    }

    public record PlanRowDto(
        Guid     Id,
        string   SourceDocType,
        string   SourceDocNo,
        Guid?    PartnerId,
        string?  PartnerName,
        string   Direction,
        int      InstallmentNo,
        int      TotalInstallments,
        DateTime DueDate,
        decimal  Amount,
        decimal  PaidAmount,
        string   Status,
        DateTime? PaidAt,
        int      DaysOverdue);

    public record DirCountsDto(int Total, int Receivable, int Payable);
}
