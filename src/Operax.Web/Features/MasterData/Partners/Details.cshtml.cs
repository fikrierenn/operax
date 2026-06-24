using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Partners;

// Cari detay ekranı — ana parça (property + OnGet + OnPost). Lazy tab yükleyiciler
// Details.Loaders.cs, DTO/record'lar Details.Dtos.cs partial dosyalarında (dosya boyutu disiplini).
[Authorize]
public partial class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, INumberSeriesService numberSeries, ParameterStore parameters, UdfService udfSvc, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public PartnerDto Partner { get; set; } = new();

    // Dinamik kullanıcı tanımlı alanlar paneli (Plan 34 Faz 2 — Partner entity)
    public CustomFieldsVm UdfPanel { get; set; } = new("Partner", [], new());

    // Aktif tab (?tab=genel|ekstre|...). Lazy yükleme: yalnızca aktif tab verisi çekilir.
    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "genel";

    // Düzenleme modu (?edit=true). Mevcut kayıt varsayılan görüntüleme; yeni kayıt her zaman edit.
    [BindProperty(SupportsGet = true)]
    public bool Edit { get; set; }

    // İş kuralı: yeni kayıt veya açıkça düzenleme istendiğinde alanlar editlenebilir
    public bool IsEditable => IsNew || Edit;

    // Tarih aralığı filtresi — Ekstre + Siparişler tabları (default: son 30 gün)
    [BindProperty(SupportsGet = true, Name = "df")]
    public DateTime? DateFrom { get; set; }
    [BindProperty(SupportsGet = true, Name = "dt")]
    public DateTime? DateTo { get; set; }

    // Siparişler tabı durum filtresi (boş = tümü)
    [BindProperty(SupportsGet = true, Name = "sf")]
    public string? OrderStatus { get; set; }

    public bool IsNew => Partner.Id == Guid.Empty;

    // Sorumlu temsilci dropdown'ları için aktif kullanıcılar
    public List<UserDdl> Users { get; set; } = [];

    // Cari bakiye ve vade bilgileri (sadece Ekstre tabı aktifken yüklenir)
    public BalanceSummaryDto?      Balance      { get; set; }
    public VadeAnalysisDto?        VadeAnalysis { get; set; }

    // Siparişler tabı (lazy) — cariye ait SO + PO birleşik liste
    public List<OrderRowDto>       Orders       { get; set; } = [];

    // Ekstre hareket listesi (lazy) — fatura + ödeme birleşik; devir + yürüyen bakiye
    public List<LedgerRowDto>      Ledger         { get; set; } = [];
    public decimal                 OpeningBalance { get; set; }

    // Mutabakat tabı (lazy) — geçmiş turlar + güncel hazırlık özeti (Plan 19)
    public List<ReconciliationRowDto> ReconciliationLog { get; set; } = [];
    public ReconciliationPrepDto?     ReconciliationPrep { get; set; }

    // Faturalar / Çek-Senet / Fiyatlar tabları (lazy)
    public List<InvoiceRowDto>     Invoices    { get; set; } = [];
    public List<InstrumentRowDto>  Instruments { get; set; } = [];
    public List<PriceListRowDto>   PriceLists  { get; set; } = [];

    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        using var conn = db.Open();

        // İş kuralı: tarih filtresi boşsa varsayılan son 30 gün
        DateFrom ??= DateTime.Today.AddDays(-30);
        DateTo   ??= DateTime.Today;

        // İş kuralı: temsilci dropdown'ı her durumda gerekir (Genel tabı düzenleme formu)
        Users = (await conn.QueryAsync<UserDdl>(new CommandDefinition(
            "SELECT Id, UserName FROM AspNetUsers ORDER BY UserName", cancellationToken: ct))).ToList();

        if (id.HasValue)
        {
            var p = new { Id = id, CompanyId = company.Id };

            Partner = await conn.QueryFirstOrDefaultAsync<PartnerDto>(new CommandDefinition(
                @"SELECT Id, Code, Name, Type, TaxNumber, Email, Phone, Address,
                         IsActive, Notes,
                         PaymentTermDays, PaymentTermPolicy, CreditLimit, BlockOnLimitExceed,
                         RiskScore, RiskCategory, MaxOverdueDays,
                         DefaultPaymentMethod,
                         EFaturaMukellef, EFaturaAlias, IbanForRefund,
                         SalesRepUserId, PurchaseRepUserId, AdditionalFields
                  FROM Partner WHERE Id = @Id AND CompanyId = @CompanyId",
                p, cancellationToken: ct)) ?? new();

            // Dinamik UDF paneli: tanımları yükle + kayıtlı değerleri çöz (Plan 34 Faz 2)
            var udfDefs = await udfSvc.LoadDefinitionsAsync("Partner");
            var udfOpts = await udfSvc.ResolveAllAsync(udfDefs);
            UdfPanel = new CustomFieldsVm("Partner", udfDefs, udfSvc.ReadValues(Partner.AdditionalFields), ReadOnly: !IsEditable, Options: udfOpts);

            // İş kuralı: eski/eksik veride null sayısal alanlar 0 olarak gelir; form min kısıtını
            // ihlal edip kaydetmeyi engeller (örn. RiskScore=0 < min 1). Geçerli varsayılana çek.
            if (Partner.RiskScore is < 1 or > 5)               Partner.RiskScore = 3;
            if (string.IsNullOrWhiteSpace(Partner.RiskCategory)) Partner.RiskCategory = RiskCategory.Medium;
            if (string.IsNullOrWhiteSpace(Partner.DefaultPaymentMethod)) Partner.DefaultPaymentMethod = InstrumentType.Eft;
            if (string.IsNullOrWhiteSpace(Partner.PaymentTermPolicy)) Partner.PaymentTermPolicy = PaymentTermPolicy.Net;

            // İş kuralı: ağır tab verisi yalnızca ilgili tab seçiliyse çekilir (lazy)
            if (Partner.Id != Guid.Empty)
            {
                if (Tab == "ekstre")     await LoadLedgerAsync(conn, Partner.Id, ct);
                if (Tab == "siparisler") await LoadOrdersAsync(conn, Partner.Id, ct);
                if (Tab == "faturalar")  await LoadInvoicesAsync(conn, Partner.Id, ct);
                if (Tab == "cekssenet")  await LoadInstrumentsAsync(conn, Partner.Id, ct);
                if (Tab == "fiyatlar")   await LoadPriceListsAsync(conn, Partner.Id, ct);
                if (Tab == "mutabakat")  await LoadReconciliationAsync(conn, Partner.Id, ct);
            }
        }
        else
        {
            Partner.IsActive             = true;
            Partner.Type                 = PartnerType.Both;
            Partner.RiskScore            = 3;
            Partner.RiskCategory         = RiskCategory.Medium;
            // Yeni cari varsayılan ödeme vadesi parametreden (Plan 29)
            var termDays                 = await parameters.GetIntAsync("DEFAULT_PAYMENT_TERM_DAYS", 30);
            Partner.MaxOverdueDays       = termDays;
            Partner.PaymentTermDays      = termDays;
            Partner.DefaultPaymentMethod = InstrumentType.Eft;
        }
    }

    // Cari kodu otomatik üretir: belge seri yönetiminden (NumberSeries, ayarlardan) — tip'e göre seri
    private async Task<string> GenerateCodeAsync(string? type)
    {
        var docType = type switch
        {
            PartnerType.Customer => NumberSeriesType.PartnerCustomer,
            PartnerType.Vendor   => NumberSeriesType.PartnerVendor,
            _          => NumberSeriesType.PartnerBoth
        };
        return await numberSeries.NextAsync(company.Id, docType);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // İş kuralı: Tab navigasyon paramı + Code (otomatik atanır) form-zorunlu değil.
        // NRT (non-nullable string) implicit [Required] üretiyor → bu alanlar için temizle.
        ModelState.Remove("Tab");
        ModelState.Remove("Partner.Code");
        if (!ModelState.IsValid) return Page();

        // Dinamik UDF: form gönderimini tanımlara göre doğrula + güvenli JSON üret (Plan 34 Faz 2)
        var udfDefs = await udfSvc.LoadDefinitionsAsync("Partner");
        var (udfJson, udfErrors) = await udfSvc.BuildValidatedJsonAsync(Request.Form, udfDefs);
        if (udfErrors.Count > 0)
        {
            TempData["Error"] = string.Join(" ", udfErrors);
            UdfPanel = new CustomFieldsVm("Partner", udfDefs, udfSvc.ReadValues(udfJson), Options: await udfSvc.ResolveAllAsync(udfDefs));
            return Page();
        }
        Partner.AdditionalFields = udfJson;

        using var conn = db.Open();

        if (IsNew)
        {
            Partner.Id = Guid.NewGuid();
            // İş kuralı: cari kodu otomatik atanır (kullanıcı giremez). Tip'e göre önek + sıra no.
            Partner.Code = await GenerateCodeAsync(Partner.Type);
            const string sql = @"
                INSERT INTO Partner
                    (Id, CompanyId, Code, Name, Type, TaxNumber, Email, Phone, Address,
                     IsActive, Notes,
                     PaymentTermDays, PaymentTermPolicy, CreditLimit, BlockOnLimitExceed,
                     RiskScore, RiskCategory, MaxOverdueDays,
                     DefaultPaymentMethod,
                     EFaturaMukellef, EFaturaAlias, IbanForRefund,
                     SalesRepUserId, PurchaseRepUserId, AdditionalFields)
                VALUES
                    (@Id, @CompanyId, @Code, @Name, @Type, @TaxNumber, @Email, @Phone, @Address,
                     @IsActive, @Notes,
                     @PaymentTermDays, @PaymentTermPolicy, @CreditLimit, @BlockOnLimitExceed,
                     @RiskScore, @RiskCategory, @MaxOverdueDays,
                     @DefaultPaymentMethod,
                     @EFaturaMukellef, @EFaturaAlias, @IbanForRefund,
                     @SalesRepUserId, @PurchaseRepUserId, @AdditionalFields)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Partner.Id, CompanyId = company.Id,
                Partner.Code, Partner.Name, Partner.Type, Partner.TaxNumber,
                Partner.Email, Partner.Phone, Partner.Address, Partner.IsActive, Partner.Notes,
                Partner.PaymentTermDays, Partner.PaymentTermPolicy, Partner.CreditLimit, Partner.BlockOnLimitExceed,
                Partner.RiskScore, Partner.RiskCategory, Partner.MaxOverdueDays,
                Partner.DefaultPaymentMethod,
                Partner.EFaturaMukellef, Partner.EFaturaAlias, Partner.IbanForRefund,
                SalesRepUserId    = string.IsNullOrEmpty(Partner.SalesRepUserId)    ? null : Partner.SalesRepUserId,
                PurchaseRepUserId = string.IsNullOrEmpty(Partner.PurchaseRepUserId) ? null : Partner.PurchaseRepUserId,
                Partner.AdditionalFields
            }, cancellationToken: ct));
        }
        else
        {
            const string sql = @"
                UPDATE Partner SET
                    Name = @Name, Type = @Type,
                    TaxNumber = @TaxNumber, Email = @Email, Phone = @Phone, Address = @Address,
                    IsActive = @IsActive, Notes = @Notes,
                    PaymentTermDays = @PaymentTermDays, PaymentTermPolicy = @PaymentTermPolicy, CreditLimit = @CreditLimit,
                    BlockOnLimitExceed = @BlockOnLimitExceed,
                    RiskScore = @RiskScore, RiskCategory = @RiskCategory,
                    MaxOverdueDays = @MaxOverdueDays,
                    DefaultPaymentMethod = @DefaultPaymentMethod,
                    EFaturaMukellef = @EFaturaMukellef, EFaturaAlias = @EFaturaAlias,
                    IbanForRefund = @IbanForRefund,
                    SalesRepUserId = @SalesRepUserId, PurchaseRepUserId = @PurchaseRepUserId,
                    AdditionalFields = @AdditionalFields,
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND CompanyId = @CompanyId";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Partner.Code, Partner.Name, Partner.Type,
                Partner.TaxNumber, Partner.Email, Partner.Phone, Partner.Address,
                Partner.IsActive, Partner.Notes,
                Partner.PaymentTermDays, Partner.PaymentTermPolicy, Partner.CreditLimit, Partner.BlockOnLimitExceed,
                Partner.RiskScore, Partner.RiskCategory, Partner.MaxOverdueDays,
                Partner.DefaultPaymentMethod,
                Partner.EFaturaMukellef, Partner.EFaturaAlias, Partner.IbanForRefund,
                SalesRepUserId    = string.IsNullOrEmpty(Partner.SalesRepUserId)    ? null : Partner.SalesRepUserId,
                PurchaseRepUserId = string.IsNullOrEmpty(Partner.PurchaseRepUserId) ? null : Partner.PurchaseRepUserId,
                Partner.AdditionalFields,
                Partner.Id, CompanyId = company.Id
            }, cancellationToken: ct));
        }

        TempData["Success"] = "Cari kart kaydedildi.";
        return RedirectToPage("./Index");
    }
}
