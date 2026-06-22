using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Operax.Web.Lib;

/// <summary>
/// Ortak CSV dışa aktarım yardımcısı (Plan 40). Tüm rapor ekranları bunu kullanır — CSV biçimi,
/// güvenlik ve Excel uyumu tek yerde yaşar.
/// Tipli hücre yaklaşımı: sayı/tarih hücresi güvenli biçimlenir (formül guard UYGULANMAZ — aksi halde
/// negatif sayı '-1.234,56' Excel'de metne dönüp toplamları bozardı), metin hücresi ise formula
/// injection'a karşı nötrlenir. Çıktı UTF-8 BOM'lu (Excel Türkçe karakter + ayraç uyumu).
/// </summary>
public static class CsvExport
{
    // Excel-TR varsayılan alan ayracı; tr-TR kültürü ondalık ayracı virgül olduğundan ayraç noktalı virgül olur
    private const char Sep = ';';
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Başlık + satırlardan indirilebilir CSV dosyası (FileContentResult) üretir.</summary>
    public static FileContentResult ToFile(string fileName, IEnumerable<string> headers,
        IEnumerable<IEnumerable<object?>> rows)
        => new(ToBytes(headers, rows), "text/csv") { FileDownloadName = fileName };

    /// <summary>Başlık + satırları UTF-8 BOM'lu CSV bayt dizisine çevirir.</summary>
    public static byte[] ToBytes(IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder();
        // Başlık satırı (UI etiketi sabit metindir ama yine de RFC-4180 kaçışından geçer)
        sb.AppendLine(string.Join(Sep, headers.Select(Field)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(Sep, row.Select(Cell)));
        // UTF-8 BOM ön eki: Excel'in dosyayı UTF-8 algılaması ve Türkçe karakterleri doğru göstermesi için
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    // Hücre tipine göre biçimlenir: sayı/tarih güvenli kabul edilir (guard yok), metin nötrlenir.
    private static string Cell(object? v) => v switch
    {
        null              => "",
        decimal d         => d.ToString("N2", Tr),
        double db         => db.ToString("N2", Tr),
        float f           => f.ToString("N2", Tr),
        int i             => i.ToString(Tr),
        long l            => l.ToString(Tr),
        DateTime dt       => dt.ToString("dd.MM.yyyy", Tr),
        bool b            => b ? "Evet" : "Hayır",
        _                 => Field(v.ToString())
    };

    // Metin hücresi güvenli yazımı: formula injection guard + RFC-4180 kaçış.
    private static string Field(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        // Güvenlik: önde boşluk olsa bile ilk anlamlı karakter formül tetikleyici mi? Excel/LibreOffice
        // " =cmd" gibi baştan-boşluklu payload'u trim edip formül çalıştırabilir → ilk non-boşluğa bak,
        // tetikleyiciyse ORİJİNAL değeri tek tırnakla nötrle (=,+,-,@,| DDE dahil).
        var probe = v.TrimStart();
        if (probe.Length > 0 && probe[0] is '=' or '+' or '-' or '@' or '|')
            v = "'" + v;
        v = v.Replace('\n', ' ').Replace('\r', ' ');
        // Ayraç veya tırnak içeren alanı çift-tırnağa al (RFC-4180 — veri bozulmaz)
        if (v.Contains(Sep) || v.Contains('"'))
            v = "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }
}
