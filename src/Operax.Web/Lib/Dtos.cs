namespace Operax.Web.Lib;

// ============================================================
// Ortak DTO — tüm modüllerde kullanılır
// ============================================================

/// <summary>Dropdown listesi için genel DTO — Dapper property mapping ile kullanılır</summary>
public class DdlDto
{
    public Guid    Id        { get; set; }
    public string  Code      { get; set; } = "";
    public string  Name      { get; set; } = "";
    public Guid?   BaseUomId { get; set; }
}

// ============================================================
// Sabitler — magic string yasak, buradan kullanılır
// ============================================================

/// <summary>Belge durum sabitleri</summary>
public static class DocStatus
{
    public const string Draft      = "DRAFT";
    public const string Approved   = "APPROVED";
    public const string Posted     = "POSTED";
    public const string Cancelled  = "CANCELLED";
    public const string Received   = "RECEIVED";
    public const string Completed  = "COMPLETED";
    public const string Counting   = "COUNTING";
    public const string InProgress = "IN_PROGRESS";
    public const string Assigned   = "ASSIGNED";
}

/// <summary>Stok hareket tipleri</summary>
public static class MovementType
{
    public const string Receipt    = "RECEIPT";
    public const string Issue      = "ISSUE";
    public const string Transfer   = "TRANSFER";
    public const string CountAdj   = "COUNT_ADJ";
    public const string Production = "PRODUCTION";
}

/// <summary>Kaynak belge tipleri (SourceDocType kolonu)</summary>
public static class SourceDoc
{
    public const string Receiving  = "RECEIVING";
    public const string Shipping   = "SHIPPING";
    public const string Transfer   = "TRANSFER";
    public const string Count      = "COUNT";
    public const string Production = "PRODUCTION";
    public const string Picking    = "PICKING";
}

/// <summary>Cari hesap defteri hareket kaynak tipleri (AccountMovement.SourceDocType)</summary>
public static class AccountMovementType
{
    public const string SalesInvoice    = "SALES_INVOICE";    // Satış faturası → Borç
    public const string PurchaseInvoice = "PURCHASE_INVOICE"; // Alış faturası → Alacak
    public const string Payment         = "PAYMENT";          // Tedarikçiye ödeme → Borç
    public const string Collection      = "COLLECTION";       // Müşteriden tahsilat → Alacak
    public const string ChequeIn        = "CHEQUE_IN";        // Müşteri çeki giriş
    public const string ChequeOut       = "CHEQUE_OUT";       // Tedarikçiye çek/ciro
    public const string Opening         = "OPENING";          // Açılış/devir bakiyesi
    public const string Variance        = "VARIANCE";         // Fiyat/tutar farkı düzeltmesi
    public const string Reversal        = "REVERSAL";         // İptal ters kaydı
}

/// <summary>Evrak numarası önekleri</summary>
public static class DocPrefix
{
    public const string Receiving     = "RCV";
    public const string Shipping      = "SHP";
    public const string Transfer      = "TRF";
    public const string CycleCount    = "CNT";
    public const string Replenishment = "REP";
    public const string PurchaseOrder = "PO";
    public const string SalesOrder    = "SO";
    public const string Production    = "PRD";
    public const string Picking       = "PCK";
}
