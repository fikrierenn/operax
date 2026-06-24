namespace Operax.Web.Features.MasterData.Items;

// Ürün detay ekranının DTO/record tanımları (Details.cshtml.cs'ten ayrıldı — dosya boyutu disiplini).
// partial class: tip adları DetailsModel.* olarak korunur, view/referans değişmez.
public partial class DetailsModel
{
    public record ItemDto
    {
        public Guid    Id               { get; set; }
        public string  Code             { get; set; } = "";
        public string  Name             { get; set; } = "";
        public string? Description      { get; set; }
        public Guid    BaseUomId        { get; set; }
        public string? BaseUomCode      { get; set; }
        public Guid?   CategoryId       { get; set; }
        public string? CategoryName     { get; set; }
        public decimal TaxRate          { get; set; } = 20;
        public string  ItemType         { get; set; } = Operax.Web.Lib.ItemType.Stock;  // Ürün doğası: STOCK (fiziksel) | SERVICE (hizmet) | FIXED_ASSET (demirbaş) — Plan 52: CONSUMABLE kaldırıldı
        public bool    IsLotTracked     { get; set; }
        public bool    IsSerialTracked  { get; set; }
        public bool    IsActive         { get; set; }

        // Ürün-seviyesi emniyet stok limitleri (Plan 34 — eski Description-JSON MinQty/MaxQty'den terfi)
        public decimal? MinStockLevel    { get; set; }
        public decimal? MaxStockLevel    { get; set; }
        // Dinamik UDF JSON çantası — servisle doldurulur, kullanıcı bind etmez
        public string? AdditionalFields  { get; set; }
    }

    public record UomConversionDto(Guid Id, string UomCode, string UomName, decimal ConversionRate);
    public record BarcodeDto(Guid Id, string Barcode, string UomCode);
}
