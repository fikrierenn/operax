using Microsoft.AspNetCore.Mvc;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.MasterData.Partners;

// Cari detay ekranının lazy tab yükleyicileri + mutabakat POST handler'ları
// (Details.cshtml.cs'ten ayrıldı — dosya boyutu disiplini). partial class: primary
// constructor parametreleri (db/company/user/logger) ve instance property'ler erişilebilir.
public partial class DetailsModel
{
    // Bakiye özeti + devir + tarih aralığı ekstresi + vade analizi — QueryMultiple ile tek round-trip
    private async Task LoadLedgerAsync(System.Data.IDbConnection conn, Guid partnerId, CancellationToken ct)
    {
        // İş kuralı: durum parametreleri DocStatus sabitleriyle beslenir, magic string yok
        var p = new { CompanyId = company.Id, PartnerId = partnerId, From = DateFrom, To = DateTo,
                      StApproved = DocStatus.Approved, StPosted = DocStatus.Posted };
        using var multi = await conn.QueryMultipleAsync(new CommandDefinition(@"
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
            WHERE CompanyId = @CompanyId AND PartnerId = @PartnerId;",
            p, cancellationToken: ct));

        Balance        = await multi.ReadFirstOrDefaultAsync<BalanceSummaryDto>();
        OpeningBalance = await multi.ReadFirstOrDefaultAsync<decimal>();
        Ledger         = (await multi.ReadAsync<LedgerRowDto>()).ToList();
        VadeAnalysis   = await multi.ReadFirstOrDefaultAsync<VadeAnalysisDto>();
    }

    // Cariye ait satış (SO) + satınalma (PO) siparişleri — birleşik liste, satırlardan tutar hesaplanır
    private async Task LoadOrdersAsync(System.Data.IDbConnection conn, Guid partnerId, CancellationToken ct)
    {
        // İş kuralı: durum parametreleri DocStatus sabitleriyle beslenir, magic string yok
        var p = new { CompanyId = company.Id, PartnerId = partnerId, From = DateFrom, To = DateTo, Sf = OrderStatus,
                      StCancelled = DocStatus.Cancelled };
        // Açık tutar = (sipariş - sevk/kabul) * fiyat; sipariş ledger/bakiyeyi etkilemez (bilgi amaçlı)
        // HasInvoice/HasDelivery: belge zinciri durumu (SO→Shipping/SalesInvoice, PO→Receiving)
        // Durum filtresi: @Sf boşsa tümü, doluysa eşleşen Status
        Orders = (await conn.QueryAsync<OrderRowDto>(new CommandDefinition(@"
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
            ORDER BY OrderDate DESC",
            p, cancellationToken: ct))).ToList();
    }

    // Cariye ait satış + alış faturaları (birleşik)
    private async Task LoadInvoicesAsync(System.Data.IDbConnection conn, Guid partnerId, CancellationToken ct)
    {
        var p = new { CompanyId = company.Id, PartnerId = partnerId };
        Invoices = (await conn.QueryAsync<InvoiceRowDto>(new CommandDefinition(@"
            SELECT 'Satış' AS Kind, Id, InvoiceNo AS DocNo, InvoiceDate, GrandTotal, ISNULL(PaidAmount,0) AS PaidAmount, Status
            FROM SalesInvoice
            WHERE PartnerId = @PartnerId AND CompanyId = @CompanyId AND IsDeleted = 0
            UNION ALL
            SELECT 'Alış' AS Kind, Id, DocNo, InvoiceDate, TotalAmount, 0, Status
            FROM ExpenseInvoice
            WHERE PartnerId = @PartnerId AND CompanyId = @CompanyId
            ORDER BY InvoiceDate DESC",
            p, cancellationToken: ct))).ToList();
    }

    // Cariye ait çek + senet portföyü (birleşik)
    private async Task LoadInstrumentsAsync(System.Data.IDbConnection conn, Guid partnerId, CancellationToken ct)
    {
        var p = new { CompanyId = company.Id, PartnerId = partnerId };
        Instruments = (await conn.QueryAsync<InstrumentRowDto>(new CommandDefinition(@"
            SELECT Id, 'Çek' AS Kind, Direction, ChequeNo AS No, Amount, DueDate, Status
            FROM Cheque
            WHERE PartnerId = @PartnerId AND CompanyId = @CompanyId
            UNION ALL
            SELECT Id, 'Senet' AS Kind, Direction, NoteNo, Amount, DueDate, Status
            FROM PromissoryNote
            WHERE PartnerId = @PartnerId AND CompanyId = @CompanyId
            ORDER BY DueDate DESC", p, cancellationToken: ct))).ToList();
    }

    // Cariye özel fiyat listeleri (satır sayısıyla)
    private async Task LoadPriceListsAsync(System.Data.IDbConnection conn, Guid partnerId, CancellationToken ct)
    {
        var p = new { CompanyId = company.Id, PartnerId = partnerId };
        PriceLists = (await conn.QueryAsync<PriceListRowDto>(new CommandDefinition(@"
            SELECT pl.Id, pl.Code, pl.Name, pl.Direction, pl.Currency, pl.ValidFrom, pl.ValidTo, pl.IsActive,
                   (SELECT COUNT(*) FROM PriceListLine pll WHERE pll.PriceListId = pl.Id) AS LineCount
            FROM PriceList pl
            WHERE pl.PartnerId = @PartnerId AND pl.CompanyId = @CompanyId
            ORDER BY pl.IsActive DESC, pl.ValidFrom DESC", p, cancellationToken: ct))).ToList();
    }

    // Cari mutabakat geçmişi + güncel açık kalem özeti (Plan 19)
    private async Task LoadReconciliationAsync(System.Data.IDbConnection conn, Guid partnerId, CancellationToken ct)
    {
        var p = new { CompanyId = company.Id, PartnerId = partnerId };
        // Mutabakat turu geçmişi (en yeni üstte)
        ReconciliationLog = (await conn.QueryAsync<ReconciliationRowDto>(new CommandDefinition(@"
            SELECT Id, StatementDate, BalanceSnapshot, Status, SentChannel,
                   SentAt, DeadlineAt, ResponseAt, ResponseNote
            FROM PartnerReconciliationLog
            WHERE CompanyId = @CompanyId AND PartnerId = @PartnerId
            ORDER BY StatementDate DESC, CreatedAt DESC", p, cancellationToken: ct))).ToList();
        // Bugün itibarıyla mutabakat hazırlık özeti (muhasebe tarih girince yenilenir)
        ReconciliationPrep = await conn.QueryFirstOrDefaultAsync<ReconciliationPrepDto>(new CommandDefinition(@"
            SELECT NetBalance, MovementCount, OpenItemCount, OpenItemTotal
            FROM dbo.tvf_ReconciliationPrep(@CompanyId, @PartnerId, @AsOf)",
            new { CompanyId = company.Id, PartnerId = partnerId, AsOf = DateTime.Today }, cancellationToken: ct));
    }

    // Mutabakat turu başlat — muhasebe kesim tarihi + kanal girer, bakiye snapshot alınır
    public async Task<IActionResult> OnPostCreateReconciliationAsync(Guid id, DateTime statementDate, string channel, CancellationToken ct)
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
            await conn.ExecuteAsync(new CommandDefinition("sp_CreateReconciliationStatement", prm,
                commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));
            TempData["Success"] = "Mutabakat oluşturuldu ve gönderim için işaretlendi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP Türkçe yazdı, kullanıcıya gösterilebilir
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Sistem hatası — ham mesaj gösterilmez, detay log'a
            logger.LogError(sqlEx, "Mutabakat oluşturma DB hatası. Cari {PartnerId}", id);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id, tab = "mutabakat" });
    }

    // Mutabakata yanıt — onay (CONFIRMED) veya itiraz (DISPUTED, gerekçe zorunlu)
    public async Task<IActionResult> OnPostRespondReconciliationAsync(Guid id, Guid reconciliationId, bool confirmed, string? note, CancellationToken ct)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_RespondReconciliation",
                new { ReconciliationId = reconciliationId, CompanyId = company.Id,
                      Confirmed = confirmed, ResponseNote = note, UserId = user.Id.ToString() },
                commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));
            TempData["Success"] = confirmed ? "Mutabakat onaylandı." : "Mutabakat itirazı kaydedildi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP Türkçe yazdı, kullanıcıya gösterilebilir
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Sistem hatası — ham mesaj gösterilmez, detay log'a
            logger.LogError(sqlEx, "Mutabakat yanıt DB hatası. Mutabakat {ReconciliationId}", reconciliationId);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id, tab = "mutabakat" });
    }
}
