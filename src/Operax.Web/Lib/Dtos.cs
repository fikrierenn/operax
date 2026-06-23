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
    public const string Closed         = "CLOSED";          // Sipariş tamamen kapandı
    public const string ClosedPartial  = "CLOSED_PARTIAL";  // Sipariş kısmen kapandı (kalan iptal)
}

/// <summary>Stok hareket tipleri</summary>
public static class MovementType
{
    public const string Receipt    = "RECEIPT";
    public const string Issue      = "ISSUE";
    public const string Transfer   = "TRANSFER";
    public const string CountAdj   = "COUNT_ADJ";
    public const string Production = "PRODUCTION";
    public const string Scrap      = "SCRAP";       // Zayiat çıkışı (sarf fişi ReasonCode set ise)
}

/// <summary>Sözlük tipi kodları (DictionaryType.Code) — vokabüler veri-driven, hardcoded değer yok</summary>
public static class DictType
{
    public const string MaterialIssueReason = "MATERIAL_ISSUE_REASON"; // Sarf/zayiat nedeni (KDV davranışı RequiresKdvAdjustment flag'inde)
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

/// <summary>Fiyat listesi yönü (Plan 30) — magic string yerine kullanılır.</summary>
public static class PriceDirection
{
    public const string Sales    = "SALES";     // Satış (müşteri) fiyat listesi
    public const string Purchase = "PURCHASE";  // Alış (tedarikçi) fiyat listesi
}

/// <summary>Fiyat listesi satır tipi (Plan 30).</summary>
public static class PriceLineType
{
    public const string Fixed    = "FIXED";     // Sabit birim fiyat (zincir iskonto üstüne uygulanır)
    public const string Discount = "DISCOUNT";  // Sadece iskonto kademesi taşıyan satır
}

/// <summary>Finansal hesap tipi (Plan 35) — schema_M11_Finance.AccountType + DB ACCOUNT_TYPE dict ile birebir.</summary>
public static class AccountType
{
    public const string Cash       = "CASH";         // Kasa
    public const string Bank       = "BANK";         // Banka hesabı
    public const string CreditCard = "CREDIT_CARD";  // Kredi kartı
    public const string Loan       = "LOAN";         // Kredi hesabı
    public const string Pos        = "POS";          // POS cihazı
}

/// <summary>Finansal hareket tipi (Plan 35) — schema_M11_Finance.TransactionType + DB TRANSACTION_TYPE dict ile birebir.</summary>
public static class TransactionType
{
    public const string Income      = "INCOME";        // Tahsilat / gelir
    public const string Expense     = "EXPENSE";       // Ödeme / gider
    public const string TransferIn  = "TRANSFER_IN";   // Virman giriş
    public const string TransferOut = "TRANSFER_OUT";  // Virman çıkış
}

/// <summary>Ödeme yöntemi (Plan 35) — DB PAYMENT_METHOD dict ile birebir. Kasa/banka fişi yöntemi (araç tipi değil — onun için InstrumentType).</summary>
public static class PaymentMethod
{
    public const string Cash           = "CASH";             // Nakit
    public const string BankTransfer   = "BANK_TRANSFER";    // Havale / EFT (fiş yöntemi)
    public const string CreditCard     = "CREDIT_CARD";      // Kredi kartı
    public const string Cheque         = "CHEQUE";           // Çek
    public const string PromissoryNote = "PROMISSORY_NOTE";  // Senet
    public const string Offset         = "OFFSET";           // Mahsup
    public const string Other          = "OTHER";            // Diğer
}

// ============================================================
// Plan 41 — Statü/Yön/Tip kapalı kümeleri (VT CHECK ile birebir)
// ============================================================

/// <summary>Finansal araç tipi (Plan 41) — FinancialTransaction.InstrumentType + Partner.DefaultPaymentMethod.
/// PaymentMethod (fiş yöntemi) ile AYRI eksen: bu "hangi araçla" (EFT/Havale/Çek...). HAVALE→GIRO (İngilizce tutarlılık).</summary>
public static class InstrumentType
{
    public const string Cash   = "CASH";    // Nakit
    public const string Eft    = "EFT";     // Elektronik Fon Transferi (bankalar arası)
    public const string Giro   = "GIRO";    // Havale (banka içi virman)
    public const string Cheque = "CHEQUE";  // Çek
    public const string Note   = "NOTE";    // Senet
    public const string Card   = "CARD";    // Kredi/banka kartı
    public const string Loan   = "LOAN";    // Kredi
    public const string Cc     = "CC";      // Kredi kartı ekstre kapama
}

/// <summary>Tahsilat/ödeme yönü (Plan 41) — PaymentPlan.Direction + cari yaşlandırma.</summary>
public static class FinanceDirection
{
    public const string Receivable = "RECEIVABLE";  // Alacak (tahsil edilecek)
    public const string Payable    = "PAYABLE";     // Borç (ödenecek)
}

/// <summary>Çek/senet yönü (Plan 41) — Cheque.Direction / PromissoryNote.Direction.</summary>
public static class ChequeDirection
{
    public const string Received = "RECEIVED";  // Alınan (müşteri çeki — portföy)
    public const string Issued   = "ISSUED";    // Verilen (kendi çekimiz)
}

/// <summary>Çek/senet statüsü (Plan 41) — Cheque.Status + PromissoryNote.Status. document-immutability §2.4 zinciri.</summary>
public static class ChequeStatus
{
    public const string Portfolio = "PORTFOLIO";  // Portföyde (alınan, henüz işlem yok)
    public const string InBank    = "IN_BANK";    // Tahsile/teminata bankada
    public const string Collected = "COLLECTED";  // Tahsil edildi
    public const string Returned  = "RETURNED";   // Karşılıksız / iade
    public const string Paid      = "PAID";       // Ödendi (verilen çek)
    public const string Endorsed  = "ENDORSED";   // Ciro edildi (3. tarafa)
}

/// <summary>Kredi statüsü (Plan 41) — Loan.Status.</summary>
public static class LoanStatus
{
    public const string Active       = "ACTIVE";        // Aktif (taksitler ödeniyor)
    public const string Closed       = "CLOSED";        // Kapandı (tamamı ödendi)
    public const string Restructured = "RESTRUCTURED";  // Yapılandırıldı (yeni krediye taşındı)
}

/// <summary>Ödeme planı statüsü (Plan 41) — PaymentPlan.Status.</summary>
public static class PaymentPlanStatus
{
    public const string Open      = "OPEN";       // Açık (vadesi gelmedi)
    public const string Partial   = "PARTIAL";    // Kısmi ödendi
    public const string Paid      = "PAID";       // Tamamı ödendi
    public const string Overdue   = "OVERDUE";    // Vadesi geçti
    public const string Cancelled = "CANCELLED";  // İptal
}

/// <summary>Kredi taksit hesap yöntemi (Plan 41) — Loan.CalcMethod.</summary>
public static class LoanCalcMethod
{
    public const string Annuity        = "ANUITE";           // Eşit taksit (anüite)
    public const string EqualPrincipal = "EQUAL_PRINCIPAL";  // Eşit anapara
    public const string Balloon        = "BALLOON";          // Balon ödeme
    public const string Spot           = "SPOT";             // Spot (tek ödeme)
    public const string Rotative       = "ROTATIVE";         // Rotatif
    public const string Kmh            = "KMH";              // Kredili mevduat hesabı
    public const string Dbs            = "DBS";              // Doğrudan borçlandırma sistemi
}

/// <summary>Kredi kartı tipi (Plan 41) — CreditCard.CardType.</summary>
public static class CardType
{
    public const string Credit    = "CREDIT";     // Bireysel kredi kartı
    public const string Business  = "BUSINESS";   // Ticari kart
    public const string Corporate = "CORPORATE";  // Kurumsal kart
    public const string Debit     = "DEBIT";      // Banka kartı
}

/// <summary>Seri no statüsü (Plan 41) — ItemSerial.Status.</summary>
public static class SerialStatus
{
    public const string InStock    = "IN_STOCK";    // Stokta
    public const string Shipped    = "SHIPPED";     // Sevk edildi
    public const string Scrapped   = "SCRAPPED";    // Hurda
    public const string Quarantine = "QUARANTINE";  // Karantina
}

/// <summary>Lot statüsü (Plan 41) — ItemLot.Status.</summary>
public static class LotStatus
{
    public const string Available  = "AVAILABLE";   // Kullanılabilir
    public const string Quarantine = "QUARANTINE";  // Karantina
    public const string Blocked    = "BLOCKED";     // Bloke
}

/// <summary>LPN (taşıma birimi) statüsü (Plan 41) — LPN.Status.</summary>
public static class LpnStatus
{
    public const string InUse     = "IN_USE";     // Kullanımda (dolduruluyor)
    public const string Available = "AVAILABLE";  // Boş/hazır
    public const string Loaded    = "LOADED";     // Yüklendi
    public const string Shipped   = "SHIPPED";    // Sevk edildi
}

/// <summary>LPN tipi (Plan 41) — LPN.LpnType.</summary>
public static class LpnType
{
    public const string Pallet = "PALLET";  // Palet
    public const string Box    = "BOX";     // Kutu
    public const string Carton = "CARTON";  // Koli
}

/// <summary>Üretim emri statüsü (Plan 41) — ProductionOrder.Status. NEW yetim→DRAFT, RELEASED yok (SP desteksiz).</summary>
public static class ProductionStatus
{
    public const string Draft      = "DRAFT";        // Taslak
    public const string InProgress = "IN_PROGRESS";  // Üretimde
    public const string Completed  = "COMPLETED";    // Tamamlandı
    public const string Cancelled  = "CANCELLED";    // İptal
}

/// <summary>Toplama görevi statüsü (Plan 41) — PickTask.Status. DocStatus'tan ayrı (görev atama yaşam döngüsü).</summary>
public static class PickTaskStatus
{
    public const string Draft      = "DRAFT";        // Oluşturuldu
    public const string Assigned   = "ASSIGNED";     // Personele atandı
    public const string InProgress = "IN_PROGRESS";  // Toplanıyor
    public const string Completed  = "COMPLETED";    // Tamamlandı
    public const string Cancelled  = "CANCELLED";    // İptal
}

/// <summary>Bütçe statüsü (Plan 41) — Budget.Status.</summary>
public static class BudgetStatus
{
    public const string Draft    = "DRAFT";     // Taslak
    public const string Approved = "APPROVED";  // Onaylı
    public const string Closed   = "CLOSED";    // Kapalı
}

/// <summary>Bütçe tipi (Plan 41) — Budget.Type.</summary>
public static class BudgetType
{
    public const string Operational = "OPERATIONAL";  // Operasyonel
    public const string CashFlow    = "CASH_FLOW";    // Nakit akış
    public const string Investment  = "INVESTMENT";   // Yatırım
}

/// <summary>Cari risk kategorisi (Plan 41) — Partner.RiskCategory.</summary>
public static class RiskCategory
{
    public const string Low     = "LOW";      // Düşük
    public const string Medium  = "MEDIUM";   // Orta
    public const string High    = "HIGH";     // Yüksek
    public const string Blocked = "BLOCKED";  // Bloke
}

/// <summary>Cari ödeme vade politikası (Plan 41) — Partner.PaymentTermPolicy.</summary>
public static class PaymentTermPolicy
{
    public const string Net          = "NET";           // Net gün (fatura + N gün)
    public const string NetEom       = "NET_EOM";       // Ay sonu + N gün
    public const string Installments = "INSTALLMENTS";  // Taksitli
}

/// <summary>Ürün tipi (Plan 41) — Item.ItemType.</summary>
public static class ItemType
{
    public const string Stock      = "STOCK";        // Stok malı
    public const string Consumable = "CONSUMABLE";   // Sarf malzeme
    public const string Service    = "SERVICE";      // Hizmet
    public const string FixedAsset = "FIXED_ASSET";  // Demirbaş
}

/// <summary>Şube tipi (Plan 41) — Branch.BranchType.</summary>
public static class BranchType
{
    public const string Sube    = "SUBE";     // Şube
    public const string Merkez  = "MERKEZ";   // Merkez
    public const string Fabrika = "FABRIKA";  // Fabrika
    public const string Magaza  = "MAGAZA";   // Mağaza
    public const string Ofis    = "OFIS";     // Ofis
}

/// <summary>Kullanıcı tanımlı alan veri tipi (Plan 41) — UserFieldDefinition.FieldType.</summary>
public static class UdfFieldType
{
    public const string Boolean = "BOOLEAN";  // Evet/Hayır
    public const string Date    = "DATE";     // Tarih
    public const string Number  = "NUMBER";   // Sayı
    public const string Select  = "SELECT";   // Seçim listesi
    public const string Text    = "TEXT";     // Metin
}

/// <summary>UDF veri kaynağı tipi (Plan 41) — UserFieldDefinition.DataSourceType.</summary>
public static class UdfDataSourceType
{
    public const string Dictionary = "DICTIONARY";  // Sözlük tanımı
    public const string Static     = "STATIC";      // Sabit liste
    public const string Table      = "TABLE";       // Whitelist tablo lookup
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
