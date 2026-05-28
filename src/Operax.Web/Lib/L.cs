using System;
using System.Globalization;
using System.Threading;

namespace Operax.Web.Lib;

/// <summary>
/// Çift dil (Türkçe/İngilizce) yerelleştirme yardımcısı.
/// Arayüzdeki metinlerin hem Türkçe hem İngilizce olmasını sağlar.
/// </summary>
public static class L
{
    /// <summary>
    /// Aktif kültüre göre Türkçe veya İngilizce metni döner.
    /// Tarayıcı dili veya seçili kültüre göre çalışır.
    /// </summary>
    public static string T(string tr, string en)
    {
        if (string.IsNullOrEmpty(tr)) return en ?? string.Empty;
        if (string.IsNullOrEmpty(en)) return tr;

        var culture = Thread.CurrentThread.CurrentUICulture.Name;
        return culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? en : tr;
    }

    /// <summary>
    /// Aktif dilin İngilizce olup olmadığını döner.
    /// </summary>
    public static bool IsEn => Thread.CurrentThread.CurrentUICulture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase);
}
