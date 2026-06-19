using System.Data;
using System.Globalization;
using Dapper;

namespace Operax.Web.Lib;

/// <summary>
/// Şirket bazlı sistem parametrelerini (Parameter tablosu) tipli okur.
/// Magic-number yerine: eşik/oran/gün gibi iş ayarları DB'den gelir, kod içinde sabit tutulmaz.
/// Parametre yoksa verilen fallback döner (ekran çökmez — kayıtsız kurulumda güvenli varsayılan).
/// </summary>
public sealed class ParameterStore(Db db, ICurrentCompany company)
{
    /// <summary>Parametre değerini metin olarak okur; kayıt yoksa fallback.</summary>
    public async Task<string> GetStringAsync(string code, string fallback)
    {
        using var conn = db.Open();
        var value = await conn.ExecuteScalarAsync<string?>(
            "SELECT Value FROM Parameter WHERE CompanyId = @CompanyId AND Code = @Code AND IsDeleted = 0",
            new { CompanyId = company.Id, Code = code });
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    /// <summary>Parametreyi tam sayı olarak okur; kayıt yoksa veya geçersizse fallback.</summary>
    public async Task<int> GetIntAsync(string code, int fallback)
    {
        var raw = await GetStringAsync(code, fallback.ToString(CultureInfo.InvariantCulture));
        // İş kuralı: bozuk değer kaydedilmişse sessiz fallback (parametre ekranı doğrulamalı)
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    /// <summary>Parametreyi ondalık olarak okur; kayıt yoksa veya geçersizse fallback.
    /// Değer InvariantCulture'da saklanır (nokta ondalık) — kültür bağımsız çözümleme.</summary>
    public async Task<decimal> GetDecimalAsync(string code, decimal fallback)
    {
        var raw = await GetStringAsync(code, fallback.ToString(CultureInfo.InvariantCulture));
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : fallback;
    }
}
