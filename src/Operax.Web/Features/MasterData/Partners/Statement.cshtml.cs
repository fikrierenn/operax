using System.Data;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.MasterData.Partners;

/// <summary>
/// Yazdırılabilir Cari Hesap Ekstresi (Plan 39 Faz 1). sp_PartnerStatement'ten devir + dönem
/// hareketleri (yürüyen bakiye) + yaşlandırma kovalarını okur. Salt-okuma rapor — ledger'a dokunmaz.
/// </summary>
[Authorize]
public class StatementModel(Db db, ICurrentCompany company) : PageModel
{
    public Guid PartnerId { get; set; }
    public CompanyInfoDto CompanyInfo { get; set; } = new("", null, null);
    public PartnerInfoDto? Partner { get; set; }

    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }

    public decimal OpeningBalance { get; set; }
    public List<StatementLineDto> Lines { get; set; } = [];
    public AgingDto Aging { get; set; } = new(0, 0, 0, 0);

    // Kapanış bakiyesi: son satırın yürüyen bakiyesi (hareket yoksa devir)
    public decimal ClosingBalance => Lines.Count > 0 ? Lines[^1].RunningBalance : OpeningBalance;

    public async Task<IActionResult> OnGetAsync(Guid id, DateTime? from, DateTime? to, CancellationToken ct)
    {
        // İş kuralı: tarih aralığı varsayılan son 90 gün (ekstre tipik dönem)
        DateFrom = from ?? DateTime.Today.AddDays(-90);
        DateTo   = to   ?? DateTime.Today;
        PartnerId = id;

        using var conn = db.Open();
        if (!await LoadAsync(conn, id, ct)) return NotFound();
        return Page();
    }

    /// <summary>Ekstreyi CSV olarak dışa aktarır (Excel uyumlu — UTF-8 BOM).</summary>
    public async Task<IActionResult> OnGetExportAsync(Guid id, DateTime? from, DateTime? to, CancellationToken ct)
    {
        DateFrom = from ?? DateTime.Today.AddDays(-90);
        DateTo   = to   ?? DateTime.Today;

        using var conn = db.Open();
        if (!await LoadAsync(conn, id, ct)) return NotFound();

        var csv = BuildCsv();
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        var fileName = $"Ekstre_{Partner!.Code}_{DateFrom:yyyyMMdd}-{DateTo:yyyyMMdd}.csv";
        return File(bytes, "text/csv", fileName);
    }

    /// <summary>Firma + cari bilgisi ve sp_PartnerStatement 3 sonuç kümesini yükler.</summary>
    private async Task<bool> LoadAsync(IDbConnection conn, Guid partnerId, CancellationToken ct)
    {
        // Firma başlık bilgisi (ekstre üst kısmı)
        CompanyInfo = await conn.QueryFirstOrDefaultAsync<CompanyInfoDto>(new CommandDefinition(
            "SELECT Name, TaxNumber, CAST(NULL AS NVARCHAR(500)) AS Address FROM Company WHERE Id = @CompanyId",
            new { CompanyId = company.Id }, cancellationToken: ct)) ?? new(company.Name, null, null);

        // Cari bilgisi — CompanyId zorunlu (IDOR koruması)
        Partner = await conn.QueryFirstOrDefaultAsync<PartnerInfoDto>(new CommandDefinition(
            @"SELECT Code, Name, TaxNumber, TaxOffice, Phone, Address
              FROM Partner WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0",
            new { Id = partnerId, CompanyId = company.Id }, cancellationToken: ct));
        if (Partner is null) return false;

        using var multi = await conn.QueryMultipleAsync(new CommandDefinition("sp_PartnerStatement",
            new { CompanyId = company.Id, PartnerId = partnerId, From = DateFrom, To = DateTo },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));

        OpeningBalance = await multi.ReadFirstOrDefaultAsync<decimal>();
        Lines          = (await multi.ReadAsync<StatementLineDto>()).ToList();
        Aging          = await multi.ReadFirstOrDefaultAsync<AgingDto>() ?? new(0, 0, 0, 0);
        return true;
    }

    /// <summary>Ekstre satırlarını CSV metnine dönüştürür (devir + hareketler + kapanış).</summary>
    private string BuildCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Tarih;İşlem;Belge No;Açıklama;Borç;Alacak;Bakiye");
        sb.AppendLine($";DEVİR;;;;;{OpeningBalance:N2}");
        foreach (var r in Lines)
            sb.AppendLine(string.Join(';',
                r.MovementDate.ToString("dd.MM.yyyy"),
                Csv(r.SourceDocType), Csv(r.SourceDocNo), Csv(r.Description),
                r.Debit.ToString("N2"), r.Credit.ToString("N2"), r.RunningBalance.ToString("N2")));
        sb.AppendLine($";KAPANIŞ;;;;;{ClosingBalance:N2}");
        return sb.ToString();
    }

    // CSV alanı güvenli yazımı: formula injection koruması + RFC-4180 kaçış (ayraç = noktalı virgül)
    private static string Csv(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        // Güvenlik: =,+,-,@,TAB,CR ile başlayan hücreyi Excel/LibreOffice formül sanır → tek tırnakla nötrle
        if (v[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            v = "'" + v;
        v = v.Replace('\n', ' ').Replace('\r', ' ');
        // Ayraç veya tırnak içeren alanı çift-tırnağa al (RFC-4180 — veri bozulmaz)
        if (v.Contains(';') || v.Contains('"'))
            v = "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }

    public record CompanyInfoDto(string Name, string? TaxNumber, string? Address);
    public record PartnerInfoDto(string Code, string Name, string? TaxNumber, string? TaxOffice, string? Phone, string? Address);
    public record StatementLineDto(Guid Id, DateTime MovementDate, string SourceDocType, string? SourceDocNo,
        string? Description, decimal Debit, decimal Credit, decimal RunningBalance);
    public record AgingDto(decimal B0_30, decimal B31_60, decimal B61_90, decimal B90Plus)
    {
        public decimal Total => B0_30 + B31_60 + B61_90 + B90Plus;
    }
}
