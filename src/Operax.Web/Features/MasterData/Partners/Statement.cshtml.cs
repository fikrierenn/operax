using System.Data;
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
public class StatementModel(Db db, ICurrentCompany company, IDictionaryLabels labels) : PageModel
{
    public Guid PartnerId { get; set; }
    public CompanyInfoDto CompanyInfo { get; set; } = new("", null, null);
    public PartnerInfoDto? Partner { get; set; }

    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }

    // Ekstre tipi: ALL = tüm hareket (devir+yürüyen) · OPEN = açık kalem (FIFO kapanmamış borçlar). Plan 39 Faz 2.
    public string StatementType { get; set; } = "ALL";
    public bool IsOpenItem => StatementType == "OPEN";

    public decimal OpeningBalance { get; set; }
    public List<StatementLineDto> Lines { get; set; } = [];
    public AgingDto Aging { get; set; } = new(0, 0, 0, 0);

    // Kapanış bakiyesi: son satırın yürüyen bakiyesi (hareket yoksa devir)
    public decimal ClosingBalance => Lines.Count > 0 ? Lines[^1].RunningBalance : OpeningBalance;

    public async Task<IActionResult> OnGetAsync(Guid id, DateTime? from, DateTime? to, string? type, CancellationToken ct)
    {
        // İş kuralı: tarih aralığı varsayılan son 90 gün (ekstre tipik dönem)
        DateFrom = from ?? DateTime.Today.AddDays(-90);
        DateTo   = to   ?? DateTime.Today;
        PartnerId = id;
        // Güvenli beyaz-liste: yalnız ALL/OPEN SP'ye gider (geçersiz değer ALL'a düşer)
        StatementType = type == "OPEN" ? "OPEN" : "ALL";

        using var conn = db.Open();
        if (!await LoadAsync(conn, id, ct)) return NotFound();
        return Page();
    }

    /// <summary>Ekstreyi CSV olarak dışa aktarır (Excel uyumlu — UTF-8 BOM).</summary>
    public async Task<IActionResult> OnGetExportAsync(Guid id, DateTime? from, DateTime? to, string? type, CancellationToken ct)
    {
        DateFrom = from ?? DateTime.Today.AddDays(-90);
        DateTo   = to   ?? DateTime.Today;
        StatementType = type == "OPEN" ? "OPEN" : "ALL";

        using var conn = db.Open();
        if (!await LoadAsync(conn, id, ct)) return NotFound();

        var typeSuffix = IsOpenItem ? "_AcikKalem" : "";
        var fileName = $"Ekstre{typeSuffix}_{Partner!.Code}_{DateFrom:yyyyMMdd}-{DateTo:yyyyMMdd}.csv";
        return CsvExport.ToFile(fileName, StatementHeaders, BuildRows());
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
            new { CompanyId = company.Id, PartnerId = partnerId, From = DateFrom, To = DateTo, StatementType },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));

        OpeningBalance = await multi.ReadFirstOrDefaultAsync<decimal>();
        Lines          = (await multi.ReadAsync<StatementLineDto>()).ToList();
        Aging          = await multi.ReadFirstOrDefaultAsync<AgingDto>() ?? new(0, 0, 0, 0);
        return true;
    }

    // Ekstre CSV başlıkları (CsvExport ortak helper'ı escape + BOM uygular)
    private static readonly string[] StatementHeaders =
        ["Tarih", "İşlem", "Belge No", "Açıklama", "Borç", "Alacak", "Bakiye"];

    /// <summary>Ekstre satırlarını CsvExport için tipli hücre dizisine çevirir (devir + hareketler + kapanış).</summary>
    private IEnumerable<object?[]> BuildRows()
    {
        // Devir satırı yalnız ALL'da anlamlı (OPEN'de açılış 0, liste açık kalemin kendisi)
        if (!IsOpenItem)
            yield return ["", "DEVİR", "", "", "", "", OpeningBalance];
        foreach (var r in Lines)
            yield return [r.MovementDate, labels.Label("SOURCE_DOC_TYPE", r.SourceDocType), r.SourceDocNo, r.Description,
                          r.Debit, r.Credit, r.RunningBalance];
        yield return ["", IsOpenItem ? "AÇIK TOPLAM" : "KAPANIŞ", "", "", "", "", ClosingBalance];
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
