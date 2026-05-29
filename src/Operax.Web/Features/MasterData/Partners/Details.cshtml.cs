using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Partners;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty]
    public PartnerDto Partner { get; set; } = new();

    // Aktif tab (?tab=genel|ekstre|...). Lazy yükleme: yalnızca aktif tab verisi çekilir.
    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "genel";

    // Tarih aralığı filtresi — Ekstre + Siparişler tabları (default: son 30 gün)
    [BindProperty(SupportsGet = true, Name = "df")]
    public DateTime? DateFrom { get; set; }
    [BindProperty(SupportsGet = true, Name = "dt")]
    public DateTime? DateTo { get; set; }

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
                         PaymentTermDays, CreditLimit, BlockOnLimitExceed,
                         RiskScore, RiskCategory, MaxOverdueDays,
                         DefaultPaymentMethod,
                         EFaturaMukellef, EFaturaAlias, IbanForRefund,
                         SalesRepUserId, PurchaseRepUserId
                  FROM Partner WHERE Id = @Id AND CompanyId = @CompanyId", p) ?? new();

            // İş kuralı: ağır tab verisi yalnızca ilgili tab seçiliyse çekilir (lazy)
            if (Partner.Id != Guid.Empty)
            {
                if (Tab == "ekstre")    await LoadLedgerAsync(conn, Partner.Id);
                if (Tab == "siparisler") await LoadOrdersAsync(conn, Partner.Id);
            }
        }
        else
        {
            Partner.IsActive             = true;
            Partner.Type                 = "BOTH";
            Partner.RiskScore            = 3;
            Partner.RiskCategory         = "MEDIUM";
            Partner.MaxOverdueDays       = 30;
            Partner.PaymentTermDays      = 30;
            Partner.DefaultPaymentMethod = "EFT";
        }
    }

    // Bakiye özeti + devir + tarih aralığı ekstresi + vade analizi — QueryMultiple ile tek round-trip
    private async Task LoadLedgerAsync(System.Data.IDbConnection conn, Guid partnerId)
    {
        var p = new { CompanyId = company.Id, PartnerId = partnerId, From = DateFrom, To = DateTo };
        using var multi = await conn.QueryMultipleAsync(@"
            -- 1) Bakiye özeti — cari hesap defteri (AccountMovement). Borç/Alacak GROSS toplam.
            --    NetBakiye = SUM(Borc) - SUM(Alacak). + = cari bize borçlu, - = biz cariye borçluyuz.
            SELECT
                ISNULL(SUM(am.Borc), 0)            AS TotalBorc,
                ISNULL(SUM(am.Alacak), 0)          AS TotalAlacak,
                ISNULL(SUM(am.Borc - am.Alacak), 0) AS NetBalance,
                ISNULL((SELECT SUM((sol.QtyOrdered - sol.QtyShipped) * sol.Price)
                        FROM SalesOrderHeader soh
                        JOIN SalesOrderLine sol ON sol.HeaderId = soh.Id
                        WHERE soh.PartnerId = @PartnerId AND soh.CompanyId = @CompanyId
                          AND soh.Status IN ('APPROVED', 'POSTED') AND soh.IsDeleted = 0
                          AND sol.QtyOrdered > sol.QtyShipped), 0) AS OpenSalesOrder,
                ISNULL((SELECT SUM((pol.QtyOrdered - pol.QtyReceived) * pol.Price)
                        FROM PurchaseOrderHeader poh
                        JOIN PurchaseOrderLine pol ON pol.HeaderId = poh.Id
                        WHERE poh.PartnerId = @PartnerId AND poh.CompanyId = @CompanyId
                          AND poh.Status IN ('APPROVED', 'POSTED') AND poh.IsDeleted = 0
                          AND pol.QtyOrdered > pol.QtyReceived), 0) AS OpenPurchaseOrder
            FROM AccountMovement am
            WHERE am.CompanyId = @CompanyId AND am.PartnerId = @PartnerId AND am.IsDeleted = 0;

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
                l.SourceDocNo AS DocNo, l.Borc, l.Alacak
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
        var p = new { CompanyId = company.Id, PartnerId = partnerId, From = DateFrom, To = DateTo };
        // Açık tutar = (sipariş - sevk/kabul) * fiyat; sipariş ledger/bakiyeyi etkilemez (bilgi amaçlı)
        // Tarih filtresi OrderDate üzerinden [From..To] (To günü dahil)
        Orders = (await conn.QueryAsync<OrderRowDto>(@"
            SELECT 'Satış' AS Kind, soh.Id, soh.OrderNo, soh.OrderDate, soh.Status,
                   ISNULL((SELECT SUM(sol.QtyOrdered * sol.Price)
                           FROM SalesOrderLine sol WHERE sol.HeaderId = soh.Id), 0) AS Total,
                   ISNULL((SELECT SUM((sol.QtyOrdered - sol.QtyShipped) * sol.Price)
                           FROM SalesOrderLine sol WHERE sol.HeaderId = soh.Id AND sol.QtyOrdered > sol.QtyShipped), 0) AS OpenAmount
            FROM SalesOrderHeader soh
            WHERE soh.PartnerId = @PartnerId AND soh.CompanyId = @CompanyId AND soh.IsDeleted = 0
              AND soh.OrderDate >= @From AND soh.OrderDate < DATEADD(DAY, 1, @To)
            UNION ALL
            SELECT 'Alış' AS Kind, poh.Id, poh.OrderNo, poh.OrderDate, poh.Status,
                   ISNULL((SELECT SUM(pol.QtyOrdered * pol.Price)
                           FROM PurchaseOrderLine pol WHERE pol.HeaderId = poh.Id), 0) AS Total,
                   ISNULL((SELECT SUM((pol.QtyOrdered - pol.QtyReceived) * pol.Price)
                           FROM PurchaseOrderLine pol WHERE pol.HeaderId = poh.Id AND pol.QtyOrdered > pol.QtyReceived), 0) AS OpenAmount
            FROM PurchaseOrderHeader poh
            WHERE poh.PartnerId = @PartnerId AND poh.CompanyId = @CompanyId AND poh.IsDeleted = 0
              AND poh.OrderDate >= @From AND poh.OrderDate < DATEADD(DAY, 1, @To)
            ORDER BY OrderDate DESC", p)).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        using var conn = db.Open();

        if (IsNew)
        {
            Partner.Id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO Partner
                    (Id, CompanyId, Code, Name, Type, TaxNumber, Email, Phone, Address,
                     IsActive, Notes,
                     PaymentTermDays, CreditLimit, BlockOnLimitExceed,
                     RiskScore, RiskCategory, MaxOverdueDays,
                     DefaultPaymentMethod,
                     EFaturaMukellef, EFaturaAlias, IbanForRefund,
                     SalesRepUserId, PurchaseRepUserId)
                VALUES
                    (@Id, @CompanyId, @Code, @Name, @Type, @TaxNumber, @Email, @Phone, @Address,
                     @IsActive, @Notes,
                     @PaymentTermDays, @CreditLimit, @BlockOnLimitExceed,
                     @RiskScore, @RiskCategory, @MaxOverdueDays,
                     @DefaultPaymentMethod,
                     @EFaturaMukellef, @EFaturaAlias, @IbanForRefund,
                     @SalesRepUserId, @PurchaseRepUserId)";
            await conn.ExecuteAsync(sql, new
            {
                Partner.Id, CompanyId = company.Id,
                Partner.Code, Partner.Name, Partner.Type, Partner.TaxNumber,
                Partner.Email, Partner.Phone, Partner.Address, Partner.IsActive, Partner.Notes,
                Partner.PaymentTermDays, Partner.CreditLimit, Partner.BlockOnLimitExceed,
                Partner.RiskScore, Partner.RiskCategory, Partner.MaxOverdueDays,
                Partner.DefaultPaymentMethod,
                Partner.EFaturaMukellef, Partner.EFaturaAlias, Partner.IbanForRefund,
                SalesRepUserId    = string.IsNullOrEmpty(Partner.SalesRepUserId)    ? null : Partner.SalesRepUserId,
                PurchaseRepUserId = string.IsNullOrEmpty(Partner.PurchaseRepUserId) ? null : Partner.PurchaseRepUserId
            });
        }
        else
        {
            const string sql = @"
                UPDATE Partner SET
                    Code = @Code, Name = @Name, Type = @Type,
                    TaxNumber = @TaxNumber, Email = @Email, Phone = @Phone, Address = @Address,
                    IsActive = @IsActive, Notes = @Notes,
                    PaymentTermDays = @PaymentTermDays, CreditLimit = @CreditLimit,
                    BlockOnLimitExceed = @BlockOnLimitExceed,
                    RiskScore = @RiskScore, RiskCategory = @RiskCategory,
                    MaxOverdueDays = @MaxOverdueDays,
                    DefaultPaymentMethod = @DefaultPaymentMethod,
                    EFaturaMukellef = @EFaturaMukellef, EFaturaAlias = @EFaturaAlias,
                    IbanForRefund = @IbanForRefund,
                    SalesRepUserId = @SalesRepUserId, PurchaseRepUserId = @PurchaseRepUserId,
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND CompanyId = @CompanyId";
            await conn.ExecuteAsync(sql, new
            {
                Partner.Code, Partner.Name, Partner.Type,
                Partner.TaxNumber, Partner.Email, Partner.Phone, Partner.Address,
                Partner.IsActive, Partner.Notes,
                Partner.PaymentTermDays, Partner.CreditLimit, Partner.BlockOnLimitExceed,
                Partner.RiskScore, Partner.RiskCategory, Partner.MaxOverdueDays,
                Partner.DefaultPaymentMethod,
                Partner.EFaturaMukellef, Partner.EFaturaAlias, Partner.IbanForRefund,
                SalesRepUserId    = string.IsNullOrEmpty(Partner.SalesRepUserId)    ? null : Partner.SalesRepUserId,
                PurchaseRepUserId = string.IsNullOrEmpty(Partner.PurchaseRepUserId) ? null : Partner.PurchaseRepUserId,
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
        public int     PaymentTermDays       { get; set; } = 30;
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
        decimal  OpenAmount);

    public record BalanceSummaryDto(
        decimal TotalBorc, decimal TotalAlacak, decimal NetBalance,
        decimal OpenSalesOrder, decimal OpenPurchaseOrder);

    // Ekstre hareket satırı — fatura + ödeme birleşik (Borç/Alacak)
    public record LedgerRowDto(
        DateTime Date,
        string   Type,
        string?  DocNo,
        decimal  Borc,
        decimal  Alacak);

    // Tarih filtresi formu için (Ekstre + Siparişler tabları)
    public record DateFilterVm(Guid PartnerId, string Tab, DateTime DateFrom, DateTime DateTo);

    public record VadeAnalysisDto(
        string   Direction,
        int      PaidCount,
        decimal? AvgDelayDays,
        decimal? AvgInvoiceAmount,
        decimal  TotalPaidAmount,
        DateTime? LastPayment);
}
