using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Aging;

/// <summary>
/// Yaşlandırma raporu — tvf_PaymentPlanAging(@CompanyId) iTVF'inden.
/// 0-30 / 31-60 / 61-90 / 90+ kovaları, alacak/borç ayrı sekmeli.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Direction { get; set; } = "RECEIVABLE";

    public List<AgingRowDto> Rows   { get; set; } = [];
    public AgingTotalsDto    Totals { get; set; } = new(0, 0, 0, 0, 0, 0, 0);

    // Yaşlandırma raporunu tvf_PaymentPlanAging'den yükler ve kova toplamlarını hesaplar.
    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            using var conn = db.Open();
            var p = new { CompanyId = company.Id, Direction };

            Rows = (await conn.QueryAsync<AgingRowDto>(new CommandDefinition(@"
                SELECT
                    PartnerId, PartnerName,
                    NotDue, Days1_30, Days31_60, Days61_90, Over90, TotalOpen,
                    OpenOrderAmount
                FROM dbo.tvf_PaymentPlanAging(@CompanyId)
                WHERE Direction = @Direction
                ORDER BY TotalOpen DESC", p, cancellationToken: ct))).ToList();

            Totals = new AgingTotalsDto(
                Rows.Sum(r => r.NotDue),
                Rows.Sum(r => r.Days1_30),
                Rows.Sum(r => r.Days31_60),
                Rows.Sum(r => r.Days61_90),
                Rows.Sum(r => r.Over90),
                Rows.Sum(r => r.TotalOpen),
                Rows.Sum(r => r.OpenOrderAmount));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Yaşlandırma raporu veri yükleme hatası");
            TempData["Error"] = "Veriler yüklenirken bir hata oluştu.";
        }
    }

    // Yaşlandırma tablosunu CSV olarak dışa aktarır (ortak CsvExport — Plan 40). Sayı kolonları
    // tipli hücre olarak gider: negatif/ondalık değer formül guard'ına takılmaz, Excel'de sayı kalır.
    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        await OnGetAsync(ct);
        var noun = Direction == "PAYABLE" ? "Borc" : "Alacak";
        var headers = new[] { "Cari", "Vade Gelmedi", "1-30", "31-60", "61-90", "90+", "Toplam", "Açık Sipariş" };
        var rows = Rows
            .Select(r => new object?[] { r.PartnerName, r.NotDue, r.Days1_30, r.Days31_60,
                                         r.Days61_90, r.Over90, r.TotalOpen, r.OpenOrderAmount })
            .Append(new object?[] { "TOPLAM", Totals.NotDue, Totals.Days1_30, Totals.Days31_60,
                                    Totals.Days61_90, Totals.Over90, Totals.Total, Totals.OpenOrder });
        return CsvExport.ToFile($"Yaslandirma_{noun}_{DateTime.Today:yyyyMMdd}.csv", headers, rows);
    }

    public record AgingRowDto(
        Guid    PartnerId,
        string  PartnerName,
        decimal NotDue,
        decimal Days1_30,
        decimal Days31_60,
        decimal Days61_90,
        decimal Over90,
        decimal TotalOpen,
        decimal OpenOrderAmount);

    public record AgingTotalsDto(
        decimal NotDue, decimal Days1_30, decimal Days31_60,
        decimal Days61_90, decimal Over90, decimal Total,
        decimal OpenOrder);
}
