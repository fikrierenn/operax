namespace Operax.Web.Lib;

/// <summary>
/// Türkçe arayüz yardımcısı.
/// Karar (2026-05-28): Operax tamamen Türkçe arayüz olarak çalışır.
/// L.T(tr, en) çağrı imzası geriye uyumluluk için korunmuştur ama
/// her zaman Türkçe metni döndürür. İngilizce parametresi yok sayılır.
/// </summary>
public static class L
{
    /// <summary>
    /// Türkçe metni döner. İngilizce parametre geriye uyumluluk için
    /// alınır ancak kullanılmaz (turkish-ui.md kuralı: arayüz tamamen Türkçe).
    /// </summary>
    public static string T(string tr, string en) => tr ?? string.Empty;

    /// <summary>
    /// Daima false — arayüz tamamen Türkçe.
    /// </summary>
    public static bool IsEn => false;
}
