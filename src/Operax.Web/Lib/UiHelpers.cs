// Operax Arayüz Yardımcı Metotları (Razor view'lardan @Html.Raw ile çağrılır).
// Status badge ve sayısal/parasal formatlama gibi tekrarlı işlerin tek yeri.

using System.Globalization;

namespace Operax.Web.Lib;

/// <summary>
/// Razor sayfalarında tekrarlı kullanılan görsel yardımcılar.
/// </summary>
public static class UiHelpers
{
    /// <summary>
    /// Verilen evrak durum koduna göre uygun .badge HTML'i üretir.
    /// Magic string yerine DocStatus sabitleri kullanılır.
    /// </summary>
    public static string StatusBadge(string? statusCode)
    {
        // İş kuralı: Boş değer için nötr rozet döner
        if (string.IsNullOrWhiteSpace(statusCode))
            return "<span class=\"badge badge-neutral\"><span class=\"badge-dot\"></span>—</span>";

        return statusCode switch
        {
            DocStatus.Draft     => "<span class=\"badge badge-warn\"><span class=\"badge-dot\"></span>TASLAK</span>",
            DocStatus.Posted    => "<span class=\"badge badge-success\"><span class=\"badge-dot\"></span>ONAYLI</span>",
            DocStatus.Cancelled => "<span class=\"badge badge-danger\"><span class=\"badge-dot\"></span>İPTAL</span>",
            _                   => $"<span class=\"badge badge-neutral\"><span class=\"badge-dot\"></span>{statusCode}</span>",
        };
    }

    /// <summary>
    /// Türk Lirası para birimi formatı: 12.345 ₺
    /// </summary>
    public static string FmtTL(decimal amount)
    {
        // İş kuralı: Para birimi ondalıksız gösterilir (anasayfa metrikleri)
        var tr = new CultureInfo("tr-TR");
        return string.Format(tr, "{0:N0} ₺", amount);
    }

    /// <summary>
    /// Ondalıklı sayı formatı: 12.345,67
    /// </summary>
    public static string FmtNum(decimal n, int decimals = 0)
    {
        var tr = new CultureInfo("tr-TR");
        return n.ToString($"N{decimals}", tr);
    }

    /// <summary>
    /// Kısa tarih formatı: 27 May 2026
    /// </summary>
    public static string FmtDateShort(System.DateTime dt)
    {
        var tr = new CultureInfo("tr-TR");
        return dt.ToString("d MMM yyyy", tr);
    }

    /// <summary>
    /// Verilen bir isim için ilk iki harfin baş harfi (avatar için).
    /// </summary>
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Trim().Split(new[] { ' ', '.', '-', '_' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
        return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
    }
}
