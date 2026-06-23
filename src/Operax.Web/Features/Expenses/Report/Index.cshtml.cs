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
    public async Task OnGetAsync(CancellationToken ct)
    {
        // Varsayılan: içinde bulunulan yılın başından bugüne
        From ??= new DateTime(DateTime.Today.Year, 1, 1);
        To ??= DateTime.Today;

        using var conn = db.Open();
        try
        {
            Rows = (await conn.QueryAsync<RowDto>(new CommandDefinition(
                "SELECT CostCenterId, CostCenterName, ExpenseTypeId, ExpenseTypeName, UnitOfMeasure, TotalQuantity, UnitCost, NetAmount, TaxAmount, TotalAmount, LineCount FROM tvf_ExpenseBreakdown(@CompanyId, @From, @To) ORDER BY CostCenterName, ExpenseTypeName",
                new { CompanyId = company.Id, From = From.Value.Date, To = To.Value.Date },
                cancellationToken: ct))).ToList();

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

    // Gider kırılım raporunu CSV olarak dışa aktarır (ortak CsvExport — Plan 40). Sayı kolonları tipli.
    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        await OnGetAsync(ct);
        var headers = new[] { "Gider Merkezi", "Gider Tipi", "Birim", "Tüketim", "Birim Maliyet", "Net", "KDV", "Toplam", "Satır" };
        var csvRows = Rows
            .Select(r => new object?[] { r.CostCenterName ?? "—", r.ExpenseTypeName ?? "—",
                                         r.UnitOfMeasure ?? "—", r.UnitOfMeasure is null ? (object?)null : r.TotalQuantity,
                                         r.UnitOfMeasure is null ? (object?)null : r.UnitCost,
                                         r.NetAmount, r.TaxAmount, r.TotalAmount, r.LineCount })
            .Append(new object?[] { "TOPLAM", "", GrandNet, GrandTax, GrandTotal, Rows.Sum(r => r.LineCount) });
        return CsvExport.ToFile($"Gider_Raporu_{From:yyyyMMdd}-{To:yyyyMMdd}.csv", headers, csvRows);
    }

    public record RowDto(
        Guid? CostCenterId, string? CostCenterName,
        Guid? ExpenseTypeId, string? ExpenseTypeName,
        string? UnitOfMeasure, decimal TotalQuantity, decimal UnitCost,  // metered tüketim + birim-maliyet (kWh/m3 trendi, TL/birim)
        decimal NetAmount, decimal TaxAmount, decimal TotalAmount, int LineCount);
}
