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
    public string  Text      { get; set; } = "";  // Dropdown gösteriş metni (Name'e fallback)
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
    public const string Paid       = "PAID";       // Gider faturası ödendi
    public const string Partial    = "PARTIAL";    // Kısmi ödeme/tahsilat
    public const string Rejected   = "REJECTED";   // Fiyat farkı reddedildi
    public const string Pending    = "PENDING";    // Onay bekliyor
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
    public const string CycleCount = "CYCLE_COUNT";
    public const string Production = "PRODUCTION";
}

/// <summary>Cari (Partner) tip sabitleri — müşteri / tedarikçi / her ikisi</summary>
public static class PartnerType
{
    public const string Customer = "CUSTOMER";  // Müşteri
    public const string Vendor   = "VENDOR";    // Tedarikçi
    public const string Both     = "BOTH";      // Hem müşteri hem tedarikçi
}

/// <summary>Mal kabul modu sabitleri (Plan 28) — terminal/masaüstü kabul akışı</summary>
public static class ReceivingMode
{
    public const string SinglePo      = "SINGLE_PO";      // Tek siparişe karşı kontrollü kabul
    public const string BulkSupplier  = "BULK_SUPPLIER";  // Tedarikçinin tüm açık siparişleri — toplu/kör kabul
    public const string Free          = "FREE";           // Siparişsiz serbest kabul (yetkili)
}

/// <summary>Belge ön ek sabitleri</summary>
public static class DocPrefix
{
    public const string PurchaseOrder  = "PO";
    public const string Receiving      = "RCV";
    public const string SalesOrder     = "SO";
    public const string Shipping       = "SHP";
    public const string Transfer       = "TRN";
    public const string Replenishment  = "RPL";
    public const string CycleCount     = "CYC";
}

/// <summary>Mal Kabulü (Receiving) detay DTO</summary>
public class ReceivingLineDto
{
    public Guid Id { get; set; }
    public Guid ReceivingHeaderId { get; set; }
    public Guid ItemId { get; set; }
    public Guid UomId { get; set; }
    public decimal QtyReceived { get; set; }
    public decimal QtyBase { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Sevkiyat (Shipping) detay DTO</summary>
public class ShippingLineDto
{
    public Guid Id { get; set; }
    public Guid ShippingHeaderId { get; set; }
    public Guid ItemId { get; set; }
    public Guid UomId { get; set; }
    public decimal QtyShipped { get; set; }
    public decimal QtyBase { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Transfer detay DTO</summary>
public class TransferLineDto
{
    public Guid Id { get; set; }
    public Guid TransferHeaderId { get; set; }
    public Guid ItemId { get; set; }
    public Guid UomId { get; set; }
    public decimal Qty { get; set; }
    public decimal QtyBase { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Sayım (CycleCount) detay DTO</summary>
public class CycleCountLineDto
{
    public Guid Id { get; set; }
    public Guid CycleCountHeaderId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BinId { get; set; }
    public Guid UomId { get; set; }
    public decimal SystemQty { get; set; }
    public decimal? CountedQty { get; set; }
    public decimal CountedQtyBase { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Siparişin açık miktarını gösteren view model</summary>
public class OpenOrderViewModel
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public string PartnerId { get; set; } = "";
    public string PartnerName { get; set; } = "";
    public int TotalLines { get; set; }
    public int FulfilledLines { get; set; }
    public int OpenLines => TotalLines - FulfilledLines;
}
