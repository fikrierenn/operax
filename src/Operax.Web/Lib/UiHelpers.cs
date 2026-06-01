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
            DocStatus.Approved  => "<span class=\"badge badge-success\"><span class=\"badge-dot\"></span>ONAYLI</span>",
            DocStatus.Posted    => "<span class=\"badge badge-success\"><span class=\"badge-dot\"></span>İŞLENDİ</span>",
            DocStatus.Cancelled => "<span class=\"badge badge-danger\"><span class=\"badge-dot\"></span>İPTAL</span>",
            "PENDING"           => "<span class=\"badge badge-warn\"><span class=\"badge-dot\"></span>BEKLİYOR</span>",
            "CLOSED"            => "<span class=\"badge badge-neutral\"><span class=\"badge-dot\"></span>KAPANDI</span>",
            "CLOSED_PARTIAL"    => "<span class=\"badge badge-info\"><span class=\"badge-dot\"></span>KISMİ KAPANDI</span>",
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
    /// Finansal araç durum kodu → Türkçe rozet (çek/senet/kredi statüleri).
    /// </summary>
    public static string FinanceStatusBadge(string? code) => code switch
    {
        "PORTFOLIO"  => "<span class=\"badge badge-info\"><span class=\"badge-dot\"></span>PORTFÖYDE</span>",
        "IN_BANK"    => "<span class=\"badge badge-warn\"><span class=\"badge-dot\"></span>BANKADA</span>",
        "COLLECTED"  => "<span class=\"badge badge-success\"><span class=\"badge-dot\"></span>TAHSİL EDİLDİ</span>",
        "RETURNED"   => "<span class=\"badge badge-danger\"><span class=\"badge-dot\"></span>KARŞILIKSIZ</span>",
        "ENDORSED"   => "<span class=\"badge badge-neutral\"><span class=\"badge-dot\"></span>CİROLANDI</span>",
        "PAID"       => "<span class=\"badge badge-success\"><span class=\"badge-dot\"></span>ÖDENDİ</span>",
        "ACTIVE"     => "<span class=\"badge badge-info\"><span class=\"badge-dot\"></span>AKTİF</span>",
        "CLOSED"     => "<span class=\"badge badge-success\"><span class=\"badge-dot\"></span>KAPANDI</span>",
        "OVERDUE"    => "<span class=\"badge badge-danger\"><span class=\"badge-dot\"></span>GECİKMİŞ</span>",
        _            => $"<span class=\"badge badge-neutral\"><span class=\"badge-dot\"></span>{code}</span>",
    };

    /// <summary>
    /// Kredi hesap yöntemi kodu → Türkçe etiket.
    /// </summary>
    public static string LoanMethodLabel(string? code) => code switch
    {
        "ANUITE"          => "Anüite",
        "EQUAL_PRINCIPAL" => "Eşit Anapara",
        "BALLOON"         => "Balon Ödemeli",
        "SPOT"            => "Spot",
        "ROTATIVE"        => "Rotatif",
        "KMH"             => "KMH",
        "DBS"             => "DBS",
        _                 => code ?? "—",
    };

    /// <summary>
    /// Finansal hesap tipi kodu → Türkçe etiket.
    /// </summary>
    public static string AccountTypeLabel(string? code) => code switch
    {
        "CASH"        => "Kasa",
        "BANK"        => "Banka",
        "CREDIT_CARD" => "Kredi Kartı",
        "LOAN"        => "Kredi",
        "POS"         => "POS",
        _             => code ?? "—",
    };

    /// <summary>
    /// Ürün tipi kodu → Türkçe etiket.
    /// </summary>
    public static string ItemTypeLabel(string? code) => code switch
    {
        "STOCK"       => "Stok",
        "CONSUMABLE"  => "Sarf Malzeme",
        "SERVICE"     => "Hizmet",
        "EXPENSE"     => "Gider",
        "FIXED_ASSET" => "Sabit Kıymet",
        _             => code ?? "—",
    };

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
