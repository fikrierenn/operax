using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.SalesInvoices;

/// <summary>
/// Sevkiyattan satış faturası oluşturma (Plan 21 — N irsaliye → 1 fatura).
/// Faturalanmamış sevkiyatlar (tvf_UninvoicedShipments) cari bazlı listelenir;
/// kullanıcı aynı cariye ait birden çok sevkiyatı seçip tek faturada birleştirir.
/// Birleştirme sp_GenerateSalesInvoiceFromShippings ile atomik yapılır.
/// </summary>
[Authorize]
public class CreateModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<CreateModel> logger) : PageModel
{
    // Seçili cari (?partnerId=...). Boşsa yalnızca cari listesi gösterilir.
    [BindProperty(SupportsGet = true)] public Guid? PartnerId { get; set; }

    public List<PartnerGroupDto>  PartnerGroups { get; set; } = [];
    public List<ShipmentLineDto>  ShipmentLines { get; set; } = [];
    public string?                SelectedPartnerName { get; set; }

    // Faturalanmamış sevkiyatları cari bazlı yükler; cari seçiliyse o carinin satırlarını getirir
    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id, PartnerId };

        // Cari özet: faturalanmamış sevkiyatı olan her cari + sevkiyat/satır sayısı + en eski yaş
        PartnerGroups = (await conn.QueryAsync<PartnerGroupDto>(@"
            SELECT
                PartnerId,
                PartnerName,
                COUNT(DISTINCT ShippingId) AS ShipmentCount,
                COUNT(*)                   AS LineCount,
                SUM(RemainingQty)          AS TotalRemainingQty,
                MAX(AgeDays)               AS MaxAgeDays
            FROM dbo.tvf_UninvoicedShipments(@CompanyId)
            GROUP BY PartnerId, PartnerName
            ORDER BY MAX(AgeDays) DESC, PartnerName", p)).ToList();

        // Cari seçilmemişse satır listesine gerek yok
        if (PartnerId is null) return;

        // Seçili carinin faturalanmamış sevkiyat satırları (sevkiyat bazlı gruplanır view'da)
        ShipmentLines = (await conn.QueryAsync<ShipmentLineDto>(@"
            SELECT
                ShippingId, ShippingNo, ShipDate,
                ShippingLineId, ItemName,
                QtyBase, InvoicedQty, RemainingQty,
                LineStatus, AgeDays
            FROM dbo.tvf_UninvoicedShipments(@CompanyId)
            WHERE PartnerId = @PartnerId
            ORDER BY ShipDate, ShippingNo", p)).ToList();

        SelectedPartnerName = PartnerGroups.FirstOrDefault(g => g.PartnerId == PartnerId)?.PartnerName;
    }

    // Seçilen sevkiyatları tek faturada birleştirir (sp_GenerateSalesInvoiceFromShippings)
    public async Task<IActionResult> OnPostMergeAsync(Guid partnerId, Guid[] shippingIds)
    {
        // İş kuralı: en az bir sevkiyat seçilmeli
        if (shippingIds is null || shippingIds.Length == 0)
        {
            TempData["Error"] = "Birleştirmek için en az bir sevkiyat seçin.";
            return RedirectToPage(new { partnerId });
        }

        using var conn = db.Open();
        try
        {
            var prm = new DynamicParameters();
            prm.Add("CompanyId",       company.Id);
            prm.Add("PartnerId",       partnerId);
            prm.Add("ShippingIdsJson", JsonSerializer.Serialize(shippingIds));
            prm.Add("UserId",          user.Id);
            prm.Add("NewInvoiceId",    dbType: DbType.Guid, direction: ParameterDirection.Output);

            await conn.ExecuteAsync("sp_GenerateSalesInvoiceFromShippings", prm,
                commandType: CommandType.StoredProcedure);

            var newId = prm.Get<Guid>("NewInvoiceId");
            TempData["Success"] = $"{shippingIds.Length} sevkiyat tek faturada birleştirildi.";
            return RedirectToPage("/SalesInvoices/Details", new { id = newId });
        }
        // SP iş kuralı hatası (50000-59999) — Türkçe mesaj kullanıcıya gösterilebilir
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        // Sistem hatası — detay log'a, kullanıcıya generic mesaj
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Sevkiyat birleştirme fatura hatası: {PartnerId}", partnerId);
            TempData["Error"] = "Fatura oluşturulurken veritabanı hatası oluştu.";
        }

        return RedirectToPage(new { partnerId });
    }

    // Cari özeti — faturalanmamış sevkiyatı olan müşteri
    public record PartnerGroupDto(
        Guid    PartnerId,
        string  PartnerName,
        int     ShipmentCount,
        int     LineCount,
        decimal TotalRemainingQty,
        int     MaxAgeDays);

    // Sevkiyat satırı — faturalanmamış (kalan miktarlı)
    public record ShipmentLineDto(
        Guid     ShippingId,
        string   ShippingNo,
        DateTime ShipDate,
        Guid     ShippingLineId,
        string   ItemName,
        decimal  QtyBase,
        decimal  InvoicedQty,
        decimal  RemainingQty,
        string   LineStatus,
        int      AgeDays);
}
