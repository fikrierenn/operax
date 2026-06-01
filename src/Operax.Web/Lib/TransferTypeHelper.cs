namespace Operax.Web.Lib;

/// <summary>
/// Transfer tipi türetme: kaynak/hedef deponun şube bilgisinden
/// Türkçe etiket + saklanan TransferType kodu hesaplar.
/// </summary>
public static class TransferTypeHelper
{
    // Saklanan TransferType kod sabitleri (DB değerleri)
    public const string BinToBin = "BIN_TO_BIN";
    public const string WhToWh   = "WH_TO_WH";

    /// <summary>
    /// Kaynak ve hedef depo BranchId'sine göre Türkçe görüntü etiketi döner.
    /// </summary>
    public static string GetLabel(Guid? fromBranchId, Guid? toBranchId, Guid fromWhId, Guid toWhId)
    {
        // Aynı depo — raf/hücre transferi
        if (fromWhId == toWhId)
            return "Raf / Hücre Arası";

        // Her iki depo da şubeye bağlı ve şubeler farklı → şubeler arası
        if (fromBranchId.HasValue && toBranchId.HasValue && fromBranchId != toBranchId)
            return "Şubeler Arası";

        // Her iki depo da aynı şubede
        if (fromBranchId.HasValue && fromBranchId == toBranchId)
            return "Depo Arası (Aynı Şube)";

        // Şubesiz depo veya karma durum → genel depolar arası
        return "Depolar Arası";
    }

    /// <summary>
    /// WH_TO_WH tipinde şube karşılaştırmasından Türkçe etiket döner (BIN_TO_BIN kontrolü dışarıda yapılmış).
    /// </summary>
    public static string GetWhLabel(Guid? fromBranchId, Guid? toBranchId)
    {
        if (fromBranchId.HasValue && toBranchId.HasValue && fromBranchId != toBranchId)
            return "Şubeler Arası";
        if (fromBranchId.HasValue && fromBranchId == toBranchId)
            return "Depo Arası (Aynı Şube)";
        return "Depolar Arası";
    }

    /// <summary>
    /// DB'ye kaydedilecek TransferType kodunu döner.
    /// Şube farkı DB'ye yansımaz; görüntü türetme BranchId üzerinden yapılır.
    /// </summary>
    public static string GetCode(Guid fromWhId, Guid toWhId)
        => fromWhId == toWhId ? BinToBin : WhToWh;
}
