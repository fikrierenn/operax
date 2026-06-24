using Operax.Web.Lib;

namespace Operax.Web.Features.PurchaseOrders;

// Satınalma siparişi detay ekranının DTO/record tanımları (Details.cshtml.cs'ten ayrıldı —
// dosya boyutu disiplini). partial class: tip adları DetailsModel.* olarak korunur.
public partial class DetailsModel
{
    // Fiyat farkı kontrolü için belge bağlamı (tedarikçi + şube). Plan 30.
    private sealed record PriceCheckCtx(Guid PartnerId, Guid? BranchId);

    public record PurchaseOrderHeaderDto
    {
        public Guid     Id                 { get; set; }
        public Guid     WarehouseId        { get; set; }
        public Guid     PartnerId          { get; set; }
        public string   OrderNo            { get; set; } = "";
        public string   Status             { get; set; } = DocStatus.Draft;
        public DateTime OrderDate          { get; set; }
        public DateTime? CreatedAt         { get; set; }
        public DateTime? UpdatedAt         { get; set; }
        public DateTime? DueDate           { get; set; }
        public int      PaymentTermDays    { get; set; } = 30;
        public string?  Notes              { get; set; }
        public string?  PartnerName        { get; set; }
        public string?  PartnerCode        { get; set; }
        public string?  PartnerTaxNumber   { get; set; }
        public string?  PartnerCity        { get; set; }
        public string?  WarehouseName      { get; set; }
    }

    public record PurchaseOrderLineDto
    {
        public Guid     Id          { get; set; }
        public string?  ItemCode    { get; set; }
        public string?  ItemName    { get; set; }
        public string?  UomCode     { get; set; }
        public decimal  QtyOrdered  { get; set; }
        public decimal  QtyReceived { get; set; }
        public decimal? Price       { get; set; }
        public decimal  TaxRate     { get; set; } = 20;
        public decimal  LineTotal   => QtyOrdered * (Price ?? 0);
        public decimal  LineTax     => System.Math.Round(LineTotal * TaxRate / 100m, 2);
    }

    // Denetim izi satırı — UserName NULL ise view 'Sistem' fallback uygular, etiket UiHelpers.AuditActionLabel'dan gelir.
    public record ActivityDto(DateTime CreatedAt, string? UserName, string Action, string? Notes);
}
