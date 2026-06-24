using Operax.Web.Lib;

namespace Operax.Web.Features.MasterData.Partners;

// Cari detay ekranının DTO/record tanımları (Details.cshtml.cs'ten ayrıldı — dosya boyutu disiplini).
// partial class: tip adları DetailsModel.* olarak korunur, view/referans değişmez.
public partial class DetailsModel
{
    public record PartnerDto
    {
        public Guid    Id                    { get; set; }
        public string  Code                  { get; set; } = "";
        public string  Name                  { get; set; } = "";
        public string  Type                  { get; set; } = PartnerType.Both;
        public string? TaxNumber             { get; set; }
        public string? Email                 { get; set; }
        public string? Phone                 { get; set; }
        public string? Address               { get; set; }
        public bool    IsActive              { get; set; } = true;
        public string? Notes                 { get; set; }
        public string? AdditionalFields      { get; set; }   // Dinamik UDF JSON çantası (servisle doldurulur)
        public int     PaymentTermDays       { get; set; } = 30;
        public string  PaymentTermPolicy     { get; set; } = Operax.Web.Lib.PaymentTermPolicy.Net;
        public decimal CreditLimit           { get; set; }
        public bool    BlockOnLimitExceed    { get; set; }
        public byte    RiskScore             { get; set; } = 3;
        public string  RiskCategory          { get; set; } = Operax.Web.Lib.RiskCategory.Medium;
        public int     MaxOverdueDays        { get; set; } = 30;
        public string  DefaultPaymentMethod  { get; set; } = InstrumentType.Eft;
        public bool    EFaturaMukellef       { get; set; }
        public string? EFaturaAlias          { get; set; }
        public string? IbanForRefund         { get; set; }
        public string? SalesRepUserId        { get; set; }
        public string? PurchaseRepUserId     { get; set; }
    }

    // Sorumlu temsilci dropdown satırı (AspNetUsers — string PK)
    public record UserDdl(string Id, string UserName);

    // Siparişler tabı satırı — SO/PO birleşik (Kind: Satış/Alış)
    public record OrderRowDto(
        string   Kind,
        Guid     Id,
        string   OrderNo,
        DateTime OrderDate,
        string   Status,
        decimal  Total,
        decimal  OpenAmount,
        bool     HasInvoice,    // satış faturası/alış faturası kesildi mi
        bool     HasDelivery);  // SO: sevkiyat var mı · PO: mal kabul var mı

    // Faturalar tabı satırı — satış/alış birleşik
    public record InvoiceRowDto(
        string    Kind,
        Guid      Id,
        string    DocNo,
        DateTime? InvoiceDate,
        decimal   GrandTotal,
        decimal   PaidAmount,
        string?   Status);

    // Çek/Senet tabı satırı
    public record InstrumentRowDto(
        Guid      Id,
        string    Kind,
        string?   Direction,
        string    No,
        decimal   Amount,
        DateTime? DueDate,
        string?   Status);

    // Fiyat listesi tabı satırı
    public record PriceListRowDto(
        Guid      Id,
        string    Code,
        string?   Name,
        string?   Direction,
        string?   Currency,
        DateTime? ValidFrom,
        DateTime? ValidTo,
        bool      IsActive,
        int       LineCount);

    public record BalanceSummaryDto(
        decimal TotalDebit, decimal TotalCredit, decimal NetBalance,
        decimal OpenSalesOrder, decimal OpenPurchaseOrder);

    // Ekstre hareket satırı — fatura + ödeme birleşik (Debit/Credit)
    public record LedgerRowDto(
        DateTime Date,
        string   Type,
        string?  DocNo,
        decimal  Debit,
        decimal  Credit);

    // Tarih filtresi formu için (Ekstre + Siparişler tabları)
    public record DateFilterVm(Guid PartnerId, string Tab, DateTime DateFrom, DateTime DateTo);

    public record VadeAnalysisDto(
        string   Direction,
        int      PaidCount,
        decimal? AvgDelayDays,
        decimal? AvgInvoiceAmount,
        decimal  TotalPaidAmount,
        DateTime? LastPayment);

    // Mutabakat turu satırı (geçmiş)
    public record ReconciliationRowDto(
        Guid      Id,
        DateTime  StatementDate,
        decimal   BalanceSnapshot,
        string    Status,
        string?   SentChannel,
        DateTime? SentAt,
        DateTime? DeadlineAt,
        DateTime? ResponseAt,
        string?   ResponseNote);

    // Mutabakat hazırlık özeti (kesim tarihine kadar)
    public record ReconciliationPrepDto(
        decimal NetBalance,
        int     MovementCount,
        int     OpenItemCount,
        decimal OpenItemTotal);
}
