using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.PurchaseOrders;

/// <summary>
/// Fiyat Farkı Onay Listesi — tedarikçi liste fiyatından sapan PO satırları.
/// Yönetici onaylar (PO fiyatı + ItemCost güncellenir) veya reddeder.
/// </summary>
[Authorize]
public class PriceVariancesModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, ILogger<PriceVariancesModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = DocStatus.Draft;

    public List<VarianceDto> Variances { get; set; } = [];
    public int DraftCount    { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        await LoadCountsAsync(conn, p, ct);
        await LoadVariancesAsync(conn, ct);
    }

    private async Task LoadCountsAsync(System.Data.IDbConnection conn, object p, CancellationToken ct)
    {
        // İş kuralı: durum/kaynak parametreleri DocStatus + SourceDoc sabitleriyle beslenir, magic string yok
        var counts = await conn.QuerySingleAsync<(int Draft, int Approved, int Rejected)>(new CommandDefinition(@"
            SELECT
                SUM(CASE WHEN Status = @StDraft    THEN 1 ELSE 0 END) AS Draft,
                SUM(CASE WHEN Status = @StApproved THEN 1 ELSE 0 END) AS Approved,
                SUM(CASE WHEN Status = @StRejected THEN 1 ELSE 0 END) AS Rejected
            FROM PriceVariance
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND SourceDocType = @SrcPo",
            new { CompanyId = company.Id, StDraft = DocStatus.Draft, StApproved = DocStatus.Approved,
                  StRejected = DocStatus.Rejected, SrcPo = SourceDoc.PurchaseOrder }, cancellationToken: ct));
        DraftCount = counts.Draft;
        ApprovedCount = counts.Approved;
        RejectedCount = counts.Rejected;
    }

    // Aktif sekme (Status) filtresine göre PO fiyat farkı satırlarını yükler
    private async Task LoadVariancesAsync(System.Data.IDbConnection conn, CancellationToken ct)
    {
        var sql = @"
            SELECT
                v.Id, v.SourceDocId AS PoHeaderId,
                h.OrderNo,
                i.Code AS ItemCode, i.Name AS ItemName,
                p.Name AS PartnerName,
                v.ExpectedPrice, v.ActualPrice, v.Variance, v.VariancePercent,
                v.Status, v.CreatedAt
            FROM PriceVariance v
            JOIN Item i ON i.Id = v.ItemId
            LEFT JOIN Partner p ON p.Id = v.PartnerId
            LEFT JOIN PurchaseOrderHeader h ON h.Id = v.SourceDocId
            WHERE v.CompanyId = @CompanyId AND v.IsDeleted = 0
              AND v.SourceDocType = @SrcPo
              AND v.Status = @Tab
            ORDER BY v.CreatedAt DESC";
        Variances = (await conn.QueryAsync<VarianceDto>(new CommandDefinition(sql,
            new { CompanyId = company.Id, Tab, SrcPo = SourceDoc.PurchaseOrder }, cancellationToken: ct))).ToList();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct)
    {
        // İş kuralı: Fiyat farkı onaylanır — PO satır fiyatı + maliyet motoru güncellenir
        using var conn = db.Open();
        try
        {
            // CompanyId: SP yalnızca bu firmanın fiyat farkını onaylar (IDOR koruması)
            await conn.ExecuteAsync(new CommandDefinition("sp_ApprovePriceVariance",
                new { VarianceId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));
            await audit.LogAsync("APPROVE_VARIANCE", "PriceVariance", id, "Fiyat farkı onaylandı");
            TempData["Success"] = "Fiyat farkı onaylandı.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP Türkçe yazdı, kullanıcıya gösterilebilir
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Sistem hatası — ham mesaj gösterilmez, detay log'a
            logger.LogError(sqlEx, "Fiyat farkı onay DB hatası. Variance {VarianceId}", id);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { tab = DocStatus.Draft });
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, CancellationToken ct)
    {
        // İş kuralı: Reddedildiğinde PO satır fiyatı değişmez, kayıt REJECTED işaretlenir
        using var conn = db.Open();
        try
        {
            // İş kuralı: yalnızca DRAFT fiyat farkı reddedilebilir (CompanyId scope — IDOR)
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE PriceVariance
                SET Status = @StRejected, ApprovedBy = @UserId, ApprovedAt = GETUTCDATE()
                WHERE Id = @Id AND CompanyId = @CompanyId AND Status = @StDraft",
                new { Id = id, UserId = user.Id, CompanyId = company.Id,
                      StDraft = DocStatus.Draft, StRejected = DocStatus.Rejected }, cancellationToken: ct));
            await audit.LogAsync("REJECT_VARIANCE", "PriceVariance", id, "Fiyat farkı reddedildi");
            TempData["Success"] = "Fiyat farkı reddedildi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — Türkçe mesaj kullanıcıya gösterilebilir (approve ile simetri)
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Sistem hatası — ham mesaj gösterilmez, detay log'a
            logger.LogError(sqlEx, "Fiyat farkı red DB hatası. Variance {VarianceId}", id);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { tab = DocStatus.Draft });
    }

    public record VarianceDto(
        Guid     Id,
        Guid     PoHeaderId,
        string?  OrderNo,
        string   ItemCode,
        string   ItemName,
        string?  PartnerName,
        decimal  ExpectedPrice,
        decimal  ActualPrice,
        decimal  Variance,
        decimal  VariancePercent,
        string   Status,
        DateTime CreatedAt);
}
