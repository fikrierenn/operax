using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Partners;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, INumberSeriesService numberSeries, ParameterStore parameters, UdfService udfSvc) : PageModel
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

    public async Task OnGetAsync(Guid? id)
    {
        using var conn = db.Open();

        // İş kuralı: tarih filtresi boşsa varsayılan son 30 gün
        DateFrom ??= DateTime.Today.AddDays(-30);
        DateTo   ??= DateTime.Today;

        // İş kuralı: temsilci dropdown'ı her durumda gerekir (Genel tabı düzenleme formu)
        Users = (await conn.QueryAsync<UserDdl>(
            "SELECT Id, UserName FROM AspNetUsers ORDER BY UserName")).ToList();

        if (id.HasValue)
        {
            var p = new { Id = id, CompanyId = company.Id };

            Partner = await conn.QueryFirstOrDefaultAsync<PartnerDto>(
                @"SELECT Id, Code, Name, Type, TaxNumber, Email, Phone, Address,
                         IsActive, Notes,
                         PaymentTermDays, PaymentTermPolicy, CreditLimit, BlockOnLimitExceed,
                         RiskScore, RiskCategory, MaxOverdueDays,
                         DefaultPaymentMethod,
                         EFaturaMukellef, EFaturaAlias, IbanForRefund,
                         SalesRepUserId, PurchaseRepUserId, AdditionalFields
                  FROM Partner WHERE Id = @Id AND CompanyId = @CompanyId", p) ?? new();

            // Dinamik UDF paneli: tanımları yükle + kayıtlı değerleri çöz (Plan 34 Faz 2)
            var udfDefs = await udfSvc.LoadDefinitionsAsync("Partner");
            var udfOpts = await udfSvc.ResolveAllAsync(udfDefs);
            UdfPanel = new CustomFieldsVm("Partner", udfDefs, udfSvc.ReadValues(Partner.AdditionalFields), ReadOnly: !IsEditable, Options: udfOpts);

            // İş kuralı: eski/eksik veride null sayısal alanlar 0 olarak gelir; form min kısıtını
            // ihlal edip kaydetmeyi engeller (örn. RiskScore=0 < min 1). Geçerli varsayılana çek.
            if (Partner.RiskScore is < 1 or > 5)               Partner.RiskScore = 3;
            if (string.IsNullOrWhiteSpace(Partner.RiskCategory)) Partner.RiskCategory = "MEDIUM";
            if (string.IsNullOrWhiteSpace(Partner.DefaultPaymentMethod)) Partner.DefaultPaymentMethod = "EFT";
            if (string.IsNullOrWhiteSpace(Partner.PaymentTermPolicy)) Partner.PaymentTermPolicy = "NET";

            // İş kuralı: ağır tab verisi yalnızca ilgili tab seçiliyse çekilir (lazy)
            if (Partner.Id != Guid.Empty)
            {
                if (Tab == "ekstre")     await LoadLedgerAsync(conn, Partner.Id);
                if (Tab == "siparisler") await LoadOrdersAsync(conn, Partner.Id);
                if (Tab == "faturalar")  await LoadInvoicesAsync(conn, Partner.Id);
                if (Tab == "cekssenet")  await LoadInstrumentsAsync(conn, Partner.Id);
                if (Tab == "fiyatlar")   await LoadPriceListsAsync(conn, Partner.Id);
                if (Tab == "mutabakat")  await LoadReconciliationAsync(conn, Partner.Id);
            }
        }
        else
        {
            Partner.IsActive             = true;
            Partner.Type                 = "BOTH";
            Partner.RiskScore            = 3;
            Partner.RiskCategory         = "MEDIUM";
            // Yeni cari varsayılan ödeme vadesi parametreden (Plan 29)
            var termDays                 = await parameters.GetIntAsync("DEFAULT_PAYMENT_TERM_DAYS", 30);
            Partner.MaxOverdueDays       = termDays;
            Partner.PaymentTermDays      = termDays;
            Partner.DefaultPaymentMethod = "EFT";
        }
    }

    // Bakiye özeti + devir + tarih aralığı ekstresi + vade analizi — QueryMultiple ile tek round-trip
    private async Task LoadLedgerAsync(System.Data.IDbConnection conn, Guid partnerId)
    {
        // İş kuralı: durum parametreleri DocStatus sabitleriyle beslenir, magic string yok
        var p = new { CompanyId = company.Id, PartnerId = partnerId, From = DateFrom, To = DateTo,
                      StApproved = DocStatus.Approved, StPosted = DocStatus.Posted };
        using var multi = await conn.QueryMultipleAsync(@"
            -- 1) Bakiye özeti — cari hesap defteri (AccountMovement). Debit/Credit GROSS toplam.
            --    NetBakiye = SUM(Debit) - SUM(Credit). + = cari bize borçlu, - = biz cariye borçluyuz.
            SELECT
                ISNULL(SUM(am.Debit), 0)            AS TotalDebit,
                ISNULL(SUM(am.Credit), 0)          AS TotalCredit,
                ISNULL(SUM(am.Debit - am.Credit), 0) AS NetBalance,
                ISNULL((SELECT SUM((sol.QtyOrdered - sol.QtyShipped) * sol.Price)
                        FROM SalesOrderHeader soh
                        JOIN SalesOrderLine sol ON sol.HeaderId = soh.Id
                        WHERE soh.PartnerId = @PartnerId AND soh.CompanyId = @CompanyId
                          AND soh.Status IN (@StApproved, @StPosted) AND soh.IsDeleted = 0
                          AND sol.QtyOrdered > sol.QtyShipped), 0) AS OpenSalesOrder,
                ISNULL((SELECT SUM((pol.QtyOrdered - pol.QtyReceived) * pol.Price)
                        FROM PurchaseOrderHeader poh
                        JOIN PurchaseOrderLine pol ON pol.HeaderId = poh.Id
                        WHERE poh.PartnerId = @PartnerId AND poh.CompanyId = @CompanyId
                          AND poh.Status IN (@StApproved, @StPosted) AND poh.IsDeleted = 0
                          AND pol.QtyOrdered > pol.QtyReceived), 0) AS OpenPurchaseOrder
            FROM AccountMovement am
            WHERE am.CompanyId = @CompanyId AND am.PartnerId = @PartnerId;

            -- 2) Devir: tarih aralığı başlangıcından önceki net bakiye (defterden)
            SELECT dbo.fn_PartnerBalanceAsOf(@CompanyId, @PartnerId, @From);

            -- 3) Hareketler [From..To] — defterden, kaynak tipi Türkçeye çevrilir
            SELECT
                l.MovementDate AS [Date],
                CASE l.SourceDocType
                    WHEN 'SALES_INVOICE'    THEN N'Satış Faturası'
                    WHEN 'PURCHASE_INVOICE' THEN N'Alış Faturası'
                    WHEN 'PAYMENT'          THEN N'Ödeme'
                    WHEN 'COLLECTION'       THEN N'Tahsilat'
                    WHEN 'CHEQUE_IN'        THEN N'Çek Girişi'
                    WHEN 'CHEQUE_OUT'       THEN N'Çek Çıkışı'
                    WHEN 'OPENING'          THEN N'Devir'
                    WHEN 'VARIANCE'         THEN N'Fark'
                    WHEN 'REVERSAL'         THEN N'İptal'
                    ELSE l.SourceDocType
                END AS [Type],
                l.SourceDocNo AS DocNo, l.Debit, l.Credit
            FROM dbo.tvf_AccountLedger(@CompanyId, @PartnerId, @From, @To) l
            ORDER BY l.MovementDate, l.SourceDocType;

            -- 4) Ödeme davranışı analizi
            SELECT Direction, PaidCount, AvgDelayDays, AvgInvoiceAmount, TotalPaidAmount, LastPayment
            FROM v_PartnerVadeAnalysis
            WHERE CompanyId = @CompanyId AND PartnerId = @PartnerId;", p);

        Balance        = await multi.ReadFirstOrDefaultAsync<BalanceSummaryDto>();
        OpeningBalance = await multi.ReadFirstOrDefaultAsync<decimal>();
        Ledger         = (await multi.ReadAsync<LedgerRowDto>()).ToList();
        VadeAnalysis   = await multi.ReadFirstOrDefaultAsync<VadeAnalysisDto>();
    }

    // Cariye ait satış (SO) + satınalma (PO) siparişleri — birleşik liste, satırlardan tutar hesaplanır
    private async Task LoadOrdersAsync(System.Data.IDbConnection conn, Guid partnerId)
    {
        // İş kuralı: durum parametreleri DocStatus sabitleriyle beslenir, magic string yok
        var p = new { CompanyId = company.Id, PartnerId = partnerId, From = DateFrom, To = DateTo, Sf = OrderStatus,
                      StCancelled = DocStatus.Cancelled };
        // Açık tutar = (sipariş - sevk/kabul) * fiyat; sipariş ledger/bakiyeyi etkilemez (bilgi amaçlı)
        // HasInvoice/HasDelivery: belge zinciri durumu (SO→Shipping/SalesInvoice, PO→Receiving)
        // Durum filtresi: @Sf boşsa tümü, doluysa eşleşen Status
        Orders = (await conn.QueryAsync<OrderRowDto>(@"
            SELECT 'Satış' AS Kind, soh.Id, soh.OrderNo, soh.OrderDate, soh.Status,
                   ISNULL((SELECT SUM(sol.QtyOrdered * sol.Price)
                           FROM SalesOrderLine sol WHERE sol.HeaderId = soh.Id), 0) AS Total,
                   ISNULL((SELECT SUM((sol.QtyOrdered - sol.QtyShipped) * sol.Price)
                           FROM SalesOrderLine sol WHERE sol.HeaderId = soh.Id AND sol.QtyOrdered > sol.QtyShipped), 0) AS OpenAmount,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM SalesInvoice si WHERE si.SalesOrderId = soh.Id AND si.IsDeleted = 0 AND si.Status <> @StCancelled) THEN 1 ELSE 0 END AS BIT) AS HasInvoice,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM ShippingHeader sh WHERE sh.SalesOrderId = soh.Id AND sh.IsDeleted = 0) THEN 1 ELSE 0 END AS BIT) AS HasDelivery
            FROM SalesOrderHeader soh
            WHERE soh.PartnerId = @PartnerId AND soh.CompanyId = @CompanyId AND soh.IsDeleted = 0
              AND soh.OrderDate >= @From AND soh.OrderDate < DATEADD(DAY, 1, @To)
              AND (@Sf IS NULL OR soh.Status = @Sf)
            UNION ALL
            SELECT 'Alış' AS Kind, poh.Id, poh.OrderNo, poh.OrderDate, poh.Status,
                   ISNULL((SELECT SUM(pol.QtyOrdered * pol.Price)
                           FROM PurchaseOrderLine pol WHERE pol.HeaderId = poh.Id), 0) AS Total,
                   ISNULL((SELECT SUM((pol.QtyOrdered - pol.QtyReceived) * pol.Price)
                           FROM PurchaseOrderLine pol WHERE pol.HeaderId = poh.Id AND pol.QtyOrdered > pol.QtyReceived), 0) AS OpenAmount,
                   CAST(0 AS BIT) AS HasInvoice,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM ReceivingHeader rh WHERE rh.PurchaseOrderId = poh.Id AND rh.IsDeleted = 0) THEN 1 ELSE 0 END AS BIT) AS HasDelivery
            FROM PurchaseOrderHeader poh
            WHERE poh.PartnerId = @PartnerId AND poh.CompanyId = @CompanyId AND poh.IsDeleted = 0
              AND poh.OrderDate >= @From AND poh.OrderDate < DATEADD(DAY, 1, @To)
              AND (@Sf IS NULL OR poh.Status = @Sf)
            ORDER BY OrderDate DESC", p)).ToList();
    }

    // Cariye ait satış + alış faturaları (birleşik)
    private async Task LoadInvoicesAsync(System.Data.IDbConnection conn, Guid partnerId)
    {
        var p = new { CompanyId = company.Id, PartnerId = partnerId };
        Invoices = (await conn.QueryAsync<InvoiceRowDto>(@"
            SELECT 'Satış' AS Kind, Id, InvoiceNo AS DocNo, InvoiceDate, GrandTotal, ISNULL(PaidAmount,0) AS PaidAmount, Status
            FROM SalesInvoice
            WHERE PartnerId = @PartnerId AND CompanyId = @CompanyId AND IsDeleted = 0
            UNION ALL
            SELECT 'Alış' AS Kind, Id, DocNo, InvoiceDate, TotalAmount, 0, Status
            FROM ExpenseInvoice
            WHERE PartnerId = @PartnerId AND CompanyId = @CompanyId
            ORDER BY InvoiceDate DESC", p)).ToList();
    }

    // Cariye ait çek + senet portföyü (birleşik)
    private async Task LoadInstrumentsAsync(System.Data.IDbConnection conn, Guid partnerId)
    {
        var p = new { CompanyId = company.Id, PartnerId = partnerId };
        Instruments = (await conn.QueryAsync<InstrumentRowDto>(@"
            SELECT Id, 'Çek' AS Kind, Direction, ChequeNo AS No, Amount, DueDate, Status
            FROM Cheque
            WHERE PartnerId = @PartnerId AND CompanyId = @CompanyId
            UNION ALL
            SELECT Id, 'Senet' AS Kind, Direction, NoteNo, Amount, DueDate, Status
            FROM PromissoryNote
            WHERE PartnerId = @PartnerId AND CompanyId = @CompanyId
            ORDER BY DueDate DESC", p)).ToList();
    }

    // Cariye özel fiyat listeleri (satır sayısıyla)
    private async Task LoadPriceListsAsync(System.Data.IDbConnection conn, Guid partnerId)
    {
        var p = new { CompanyId = company.Id, PartnerId = partnerId };
        PriceLists = (await conn.QueryAsync<PriceListRowDto>(@"
            SELECT pl.Id, pl.Code, pl.Name, pl.Direction, pl.Currency, pl.ValidFrom, pl.ValidTo, pl.IsActive,
                   (SELECT COUNT(*) FROM PriceListLine pll WHERE pll.PriceListId = pl.Id) AS LineCount
            FROM PriceList pl
            WHERE pl.PartnerId = @PartnerId AND pl.CompanyId = @CompanyId
            ORDER BY pl.IsActive DESC, pl.ValidFrom DESC", p)).ToList();
    }

    // Cari mutabakat geçmişi + güncel açık kalem özeti (Plan 19)
    private async Task LoadReconciliationAsync(System.Data.IDbConnection conn, Guid partnerId)
    {
        var p = new { CompanyId = company.Id, PartnerId = partnerId };
        // Mutabakat turu geçmişi (en yeni üstte)
        ReconciliationLog = (await conn.QueryAsync<ReconciliationRowDto>(@"
            SELECT Id, StatementDate, BalanceSnapshot, Status, SentChannel,
                   SentAt, DeadlineAt, ResponseAt, ResponseNote
            FROM PartnerReconciliationLog
            WHERE CompanyId = @CompanyId AND PartnerId = @PartnerId
            ORDER BY StatementDate DESC, CreatedAt DESC", p)).ToList();
        // Bugün itibarıyla mutabakat hazırlık özeti (muhasebe tarih girince yenilenir)
        ReconciliationPrep = await conn.QueryFirstOrDefaultAsync<ReconciliationPrepDto>(@"
            SELECT NetBalance, MovementCount, OpenItemCount, OpenItemTotal
            FROM dbo.tvf_ReconciliationPrep(@CompanyId, @PartnerId, @AsOf)",
            new { CompanyId = company.Id, PartnerId = partnerId, AsOf = DateTime.Today });
    }

    // Mutabakat turu başlat — muhasebe kesim tarihi + kanal girer, bakiye snapshot alınır
    public async Task<IActionResult> OnPostCreateReconciliationAsync(Guid id, DateTime statementDate, string channel)
    {
        using var conn = db.Open();
        try
        {
            var prm = new DynamicParameters();
            prm.Add("@CompanyId", company.Id);
            prm.Add("@PartnerId", id);
            prm.Add("@StatementDate", statementDate);
            prm.Add("@SentChannel", channel);
            prm.Add("@UserId", user.Id.ToString());
            prm.Add("@NewId", dbType: System.Data.DbType.Guid, direction: System.Data.ParameterDirection.Output);
            await conn.ExecuteAsync("sp_CreateReconciliationStatement", prm,
                commandType: System.Data.CommandType.StoredProcedure);
            TempData["Success"] = "Mutabakat oluşturuldu ve gönderim için işaretlendi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        return RedirectToPage(new { id, tab = "mutabakat" });
    }

    // Mutabakata yanıt — onay (CONFIRMED) veya itiraz (DISPUTED, gerekçe zorunlu)
    public async Task<IActionResult> OnPostRespondReconciliationAsync(Guid id, Guid reconciliationId, bool confirmed, string? note)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_RespondReconciliation",
                new { ReconciliationId = reconciliationId, CompanyId = company.Id,
                      Confirmed = confirmed, ResponseNote = note, UserId = user.Id.ToString() },
                commandType: System.Data.CommandType.StoredProcedure);
            TempData["Success"] = confirmed ? "Mutabakat onaylandı." : "Mutabakat itirazı kaydedildi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        return RedirectToPage(new { id, tab = "mutabakat" });
    }

    // Cari kodu otomatik üretir: belge seri yönetiminden (NumberSeries, ayarlardan) — tip'e göre seri
    private async Task<string> GenerateCodeAsync(string? type)
    {
        var docType = type switch
        {
            "CUSTOMER" => NumberSeriesType.PartnerCustomer,
            "VENDOR"   => NumberSeriesType.PartnerVendor,
            _          => NumberSeriesType.PartnerBoth
        };
        return await numberSeries.NextAsync(company.Id, docType);
    }

    public async Task<IActionResult> OnPostAsync()
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
            await conn.ExecuteAsync(sql, new
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
            });
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
            await conn.ExecuteAsync(sql, new
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
            });
        }

        TempData["Success"] = "Cari kart kaydedildi.";
        return RedirectToPage("./Index");
    }

    public record PartnerDto
    {
        public Guid    Id                    { get; set; }
        public string  Code                  { get; set; } = "";
        public string  Name                  { get; set; } = "";
        public string  Type                  { get; set; } = "BOTH";
        public string? TaxNumber             { get; set; }
        public string? Email                 { get; set; }
        public string? Phone                 { get; set; }
        public string? Address               { get; set; }
        public bool    IsActive              { get; set; } = true;
        public string? Notes                 { get; set; }
        public string? AdditionalFields      { get; set; }   // Dinamik UDF JSON çantası (servisle doldurulur)
        public int     PaymentTermDays       { get; set; } = 30;
        public string  PaymentTermPolicy     { get; set; } = "NET";
        public decimal CreditLimit           { get; set; }
        public bool    BlockOnLimitExceed    { get; set; }
        public byte    RiskScore             { get; set; } = 3;
        public string  RiskCategory          { get; set; } = "MEDIUM";
        public int     MaxOverdueDays        { get; set; } = 30;
        public string  DefaultPaymentMethod  { get; set; } = "EFT";
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
