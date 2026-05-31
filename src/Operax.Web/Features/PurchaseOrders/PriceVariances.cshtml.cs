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
public class PriceVariancesModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = DocStatus.Draft;

    public List<VarianceDto> Variances { get; set; } = [];
    public int DraftCount    { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        await LoadCountsAsync(conn, p);
        await LoadVariancesAsync(conn);
    }

    private async Task LoadCountsAsync(System.Data.IDbConnection conn, object p)
    {
        // İş kuralı: durum parametreleri DocStatus sabitleriyle beslenir, magic string yok
        var counts = await conn.QuerySingleAsync<(int Draft, int Approved, int Rejected)>(@"
            SELECT
                SUM(CASE WHEN Status = @StDraft    THEN 1 ELSE 0 END) AS Draft,
                SUM(CASE WHEN Status = @StApproved THEN 1 ELSE 0 END) AS Approved,
                SUM(CASE WHEN Status = 'REJECTED'  THEN 1 ELSE 0 END) AS Rejected
            FROM PriceVariance
            WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND SourceDocType = 'PURCHASE_ORDER'",
            new { ((dynamic)p).CompanyId, StDraft = DocStatus.Draft, StApproved = DocStatus.Approved });
        DraftCount = counts.Draft;
        ApprovedCount = counts.Approved;
        RejectedCount = counts.Rejected;
    }

    private async Task LoadVariancesAsync(System.Data.IDbConnection conn)
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
              AND v.SourceDocType = 'PURCHASE_ORDER'
              AND v.Status = @Tab
            ORDER BY v.CreatedAt DESC";
        Variances = (await conn.QueryAsync<VarianceDto>(sql,
            new { CompanyId = company.Id, Tab })).ToList();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        // İş kuralı: Fiyat farkı onaylanır — PO satır fiyatı + maliyet motoru güncellenir
        using var conn = db.Open();
        await conn.ExecuteAsync("sp_ApprovePriceVariance",
            new { VarianceId = id, UserId = user.Id },
            commandType: System.Data.CommandType.StoredProcedure);
        await audit.LogAsync("APPROVE_VARIANCE", "PriceVariance", id, "Fiyat farkı onaylandı");
        return RedirectToPage(new { tab = DocStatus.Draft });
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        // İş kuralı: Reddedildiğinde PO satır fiyatı değişmez, kayıt REJECTED işaretlenir
        using var conn = db.Open();
        // İş kuralı: yalnızca DRAFT fiyat farkı reddedilebilir; REJECTED DocStatus dışı sabit
        await conn.ExecuteAsync(@"
            UPDATE PriceVariance
            SET Status = 'REJECTED', ApprovedBy = @UserId, ApprovedAt = GETUTCDATE()
            WHERE Id = @Id AND CompanyId = @CompanyId AND Status = @StDraft",
            new { Id = id, UserId = user.Id, CompanyId = company.Id, StDraft = DocStatus.Draft });
        await audit.LogAsync("REJECT_VARIANCE", "PriceVariance", id, "Fiyat farkı reddedildi");
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
