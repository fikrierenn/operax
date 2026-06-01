using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Expenses.Report;

/// <summary>
/// Kırılımlı gider raporu: gider merkezi × gider tipi bazında toplam.
/// Belge katmanından (GL'siz) — tvf_ExpenseBreakdown.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }

    public List<RowDto> Rows { get; set; } = [];
    public decimal GrandNet { get; set; }
    public decimal GrandTax { get; set; }
    public decimal GrandTotal { get; set; }

    // Tarih aralığında gider merkezi × tip kırılımını yükler
    public async Task OnGetAsync()
    {
        // Varsayılan: içinde bulunulan yılın başından bugüne
        From ??= new DateTime(DateTime.Today.Year, 1, 1);
        To ??= DateTime.Today;

        using var conn = db.Open();
        try
        {
            Rows = (await conn.QueryAsync<RowDto>(
                "SELECT CostCenterId, CostCenterName, ExpenseTypeId, ExpenseTypeName, NetAmount, TaxAmount, TotalAmount, LineCount FROM tvf_ExpenseBreakdown(@CompanyId, @From, @To) ORDER BY CostCenterName, ExpenseTypeName",
                new { CompanyId = company.Id, From = From.Value.Date, To = To.Value.Date })).ToList();

            GrandNet   = Rows.Sum(r => r.NetAmount);
            GrandTax   = Rows.Sum(r => r.TaxAmount);
            GrandTotal = Rows.Sum(r => r.TotalAmount);
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Gider raporu yükleme hatası");
            TempData["Error"] = "Rapor yüklenirken veritabanı hatası oluştu.";
        }
    }

    public record RowDto(
        Guid? CostCenterId, string? CostCenterName,
        Guid? ExpenseTypeId, string? ExpenseTypeName,
        decimal NetAmount, decimal TaxAmount, decimal TotalAmount, int LineCount);
}
