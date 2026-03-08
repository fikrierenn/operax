namespace Operax.Web.Lib;

// ============================================================
// Ortak DTO — tüm modüllerde kullanılır
// ============================================================

/// <summary>Dropdown listesi için genel DTO</summary>
public record DdlDto(Guid Id, string Code, string Name, Guid? BaseUomId = null);

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
