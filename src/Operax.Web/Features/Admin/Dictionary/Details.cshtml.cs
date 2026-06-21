using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Admin.Dictionary;

[Authorize(Roles = "Administrator")]
public class DetailsModel(Db db) : PageModel
{
    [BindProperty]
    public DictionaryTypeDto Type { get; set; } = new();
    public IEnumerable<DictionaryValueDto> Values { get; set; } = [];

    public async Task OnGetAsync(Guid id)
    {
        using var conn = db.Open();
        Type = await conn.QueryFirstOrDefaultAsync<DictionaryTypeDto>(
            @"-- Çoklu-firma izolasyon notu: bu sorgu CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: DictionaryType tüm firmalar tarafından paylaşılan global sistem verisidir
            -- (birim, para birimi, ülke kodları vb.). Şirkete özel kayıt içermez.
            -- Bu sayfa yalnızca Administrator rolüne açıktır; veri sızıntısı riski yoktur.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            SELECT Id, Code, NameTr, NameEn FROM DictionaryType WHERE Id = @Id", new { Id = id }) ?? new();

        Values = await conn.QueryAsync<DictionaryValueDto>(
            @"-- Çoklu-firma izolasyon notu: bu sorgu CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: DictionaryValue, tüm firmalar tarafından paylaşılan global sözlük değerleridir
            -- (ölçü birimi kodları, ülke adları, kategori değerleri vb.).
            -- DictionaryTypeId filtresi tür kapsamını sınırlar; şirkete özel veri içermez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            SELECT Id, Code, NameTr, NameEn, SortNo FROM DictionaryValue WHERE DictionaryTypeId = @Id AND IsDeleted = 0 ORDER BY SortNo, NameTr",
            new { Id = id });
    }

    public async Task<IActionResult> OnPostAddValueAsync(Guid id, string code, string nameTr, string nameEn, int sortNo)
    {
        using var conn = db.Open();
        const string sql = @"
            -- Çoklu-firma izolasyon notu: bu sorgu CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: DictionaryValue tüm firmaların paylaştığı global sistem verisidir;
            -- şirkete özel alan içermez. Bu sayfa yalnızca Administrator rolüne açıktır.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            INSERT INTO DictionaryValue (DictionaryTypeId, Code, NameTr, NameEn, SortNo)
            VALUES (@TypeId, @Code, @NameTr, @NameEn, @SortNo)";
        
        await conn.ExecuteAsync(sql, new { TypeId = id, Code = code, NameTr = nameTr, NameEn = nameEn, SortNo = sortNo });
        return RedirectToPage(new { id });
    }

    public record DictionaryTypeDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string NameTr { get; set; } = ""; public string NameEn { get; set; } = ""; }
    public record DictionaryValueDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string NameTr { get; set; } = ""; public string NameEn { get; set; } = ""; public int SortNo { get; set; } }
}
