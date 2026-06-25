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
    /// Statü kodu → .badge CSS sınıfı (RENK = sunum, kodda kalır — Plan 42). Etiket sözlükten gelir.
    /// Belge + finansal araç + stok statülerini tek haritada toplar (kodlar tekil).
    /// </summary>
    public static string BadgeClass(string? code) => code switch
    {
        DocStatus.Draft or BudgetStatus.Draft                          => "badge-warn",
        DocStatus.Approved or DocStatus.Posted or DocStatus.Completed
            or DocStatus.Received or DocStatus.Paid                    => "badge-success",
        DocStatus.Cancelled or DocStatus.Rejected                      => "badge-danger",
        DocStatus.Pending or DocStatus.Partial                         => "badge-warn",
        DocStatus.Closed                                               => "badge-neutral",
        DocStatus.ClosedPartial or DocStatus.Counting
            or DocStatus.InProgress or DocStatus.Assigned
            or DocStatus.Picking or DocStatus.Picked
            or PickTaskStatus.Released                                 => "badge-info",
        // Finansal araç / ödeme planı
        ChequeStatus.Portfolio or PaymentPlanStatus.Open or LoanStatus.Active => "badge-info",
        ChequeStatus.InBank                                            => "badge-warn",
        ChequeStatus.Collected or ChequeStatus.Paid                    => "badge-success",
        ChequeStatus.Returned or PaymentPlanStatus.Overdue            => "badge-danger",
        ChequeStatus.Endorsed or LoanStatus.Restructured              => "badge-neutral",
        // Stok (seri/lot/lpn)
        SerialStatus.InStock or LotStatus.Available                    => "badge-success",
        SerialStatus.Scrapped or LotStatus.Blocked                     => "badge-danger",
        SerialStatus.Quarantine                                        => "badge-warn",
        // LPN (palet/kap) statüsü — Available zaten LotStatus.Available (success) ile aynı kod, tekrar etme
        LpnStatus.InUse                                                => "badge-success",
        LpnStatus.Loaded                                               => "badge-warn",
        LpnStatus.Shipped                                              => "badge-neutral",
        // Finansal hesap tipi (Accounts listesi rozeti)
        AccountType.Cash                                               => "badge-success",
        AccountType.Bank                                               => "badge-info",
        AccountType.CreditCard                                         => "badge-warn",
        AccountType.Loan                                               => "badge-danger",
        // Cari risk kategorisi (BLOCKED zaten LotStatus.Blocked ile danger)
        RiskCategory.Low                                               => "badge-success",
        RiskCategory.High                                              => "badge-warn",
        _                                                              => "badge-neutral",
    };

    /// <summary>
    /// Statü rozeti HTML'i — RENK koddan (BadgeClass), ETİKET çağrandan (sözlük). Plan 42 tek-kaynak.
    /// Genelde IDictionaryLabels.StatusBadge uzantısı üzerinden çağrılır; label boşsa koda düşer.
    /// </summary>
    public static string StatusBadgeHtml(string? code, string? label)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "<span class=\"badge badge-neutral\"><span class=\"badge-dot\"></span>—</span>";
        // Güvenlik: sözlük metni de olsa HTML encode (XSS savunması)
        var text = System.Net.WebUtility.HtmlEncode(string.IsNullOrEmpty(label) ? code : label);
        return $"<span class=\"badge {BadgeClass(code)}\"><span class=\"badge-dot\"></span>{text}</span>";
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
    /// Guid'in kısa gösterimi: ilk 8 karakter, büyük harf (UUID/TX kısaltması için).
    /// </summary>
    public static string ShortGuid(System.Guid id) => id.ToString()[..8].ToUpperInvariant();

    /// <summary>
    /// Breadcrumb etiketi → index sayfası rotası. Bilinen navigasyon hedefleri tıklanabilir
    /// olur; index sayfası olmayan grup etiketleri (örn. "Master Veri") null döner → metin kalır.
    /// </summary>
    public static string? CrumbHref(string? label) => label switch
    {
        "Anasayfa"      => "/Dashboard",
        "Cari Kartlar"  => "/MasterData/Partners",
        "Ürünler"       => "/MasterData/Items",
        "Depolar"       => "/MasterData/Warehouses",
        "Satınalma"     => "/PurchaseOrders",
        "Satış"         => "/SalesOrders",
        "Hesaplar"      => "/Finance/Accounts",
        "Çek & Senet"   => "/Finance/Cheques",
        "Krediler"      => "/Finance/Loans",
        "Kredi Kartları"=> "/Finance/CreditCards",
        "Ödeme Planı"   => "/Finance/PaymentPlan",
        "Yaşlandırma"   => "/Finance/Aging",
        _ => null
    };

    /// <summary>
    /// AuditLog aksiyon kodu → Türkçe etiket (sipariş denetim izi). PO/SO Details ortak.
    /// </summary>
    public static string AuditActionLabel(string? action) => action switch
    {
        "CREATE"   => "Taslak oluşturdu",
        "UPDATE"   => "Bilgileri güncelledi",
        "ADD_LINE" => "Kalem ekledi",
        "POST"     => "Siparişi onayladı",
        "APPROVE"  => "Siparişi onayladı",
        "CANCEL"   => "Siparişi iptal etti",
        _          => action ?? "—",
    };

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
