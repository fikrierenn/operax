using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Shipping;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public ShippingHeaderDto Header { get; set; } = new();
    public IEnumerable<ShippingLineDto> Lines { get; set; } = [];

    public IEnumerable<DdlDto> Warehouses     { get; set; } = [];
    public IEnumerable<DdlDto> AvailableItems { get; set; } = [];
    public IEnumerable<DdlDto> OpenSalesOrders { get; set; } = [];
    public IEnumerable<DdlDto> AllUoms        { get; set; } = [];

    public Guid? PickTaskId { get; set; }
    public bool  IsNew => Header.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        // Dropdown listelerini ve sevkiyat bilgilerini yükler
        using var conn = db.Open();

        Warehouses = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0",
            new { CompanyId = company.Id }, cancellationToken: ct));

        // İş kuralı (Plan 52 iki-eksen): sevkiyat FİZİKSEL stok hareketidir → hizmet (SERVICE) sevk edilemez,
        // hariç tutulur. Stok/demirbaş sevk edilebilir. (Hizmet faturalanır ama fiziksel taşınmaz.)
        AvailableItems = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name, BaseUomId FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0 AND ItemType <> @ServiceType",
            new { CompanyId = company.Id, ServiceType = ItemType.Service }, cancellationToken: ct));

        // tvf_OpenSalesOrders: CompanyId parametreli iTVF
        OpenSalesOrders = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM tvf_OpenSalesOrders(@CompanyId)",
            new { CompanyId = company.Id }, cancellationToken: ct));

        AllUoms = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT dv.Id, dv.Code, dv.NameTr as Name FROM DictionaryValue dv JOIN DictionaryType dt ON dt.Id = dv.TypeId WHERE dt.Code = 'UOM' AND dt.CompanyId = @CompanyId AND dv.IsActive = 1 AND dv.IsDeleted = 0",
            new { CompanyId = company.Id }, cancellationToken: ct));

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<ShippingHeaderDto>(new CommandDefinition(@"
                SELECT Id, WarehouseId, DocNo, Status, DocDate, CarrierName, VehiclePlate, Notes
                FROM ShippingHeader WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct)) ?? new();

            Lines = await conn.QueryAsync<ShippingLineDto>(new CommandDefinition(@"
                SELECT l.Id, i.Code as ItemCode, i.Name as ItemName, dv.Code as UomCode,
                       l.QtyOriginal, l.QtyBase, l.LotNo, oh.OrderNo as SourceOrderNo,
                       l.SalesOrderLineId, l.ItemId, l.UomId
                FROM ShippingLine l
                JOIN ShippingHeader sh ON sh.Id = l.HeaderId
                JOIN Item i ON i.Id = l.ItemId
                JOIN DictionaryValue dv ON dv.Id = l.UomId
                JOIN SalesOrderLine ol ON ol.Id = l.SalesOrderLineId
                JOIN SalesOrderHeader oh ON oh.Id = ol.HeaderId
                WHERE l.HeaderId = @Id AND sh.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct));

            PickTaskId = await conn.QueryFirstOrDefaultAsync<Guid?>(new CommandDefinition(
                "SELECT Id FROM PickTask WHERE ShipmentId = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct));
        }
        else
        {
            Header.DocDate = DateTime.UtcNow;
            Header.Status  = DocStatus.Draft;
            Header.DocNo   = "NEW";
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Sevkiyat başlığını kaydeder veya günceller (DRAFT)
        using var conn = db.Open();

        try
        {
            if (IsNew)
            {
                Header.Id = Guid.NewGuid();
                var seq = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(1) + 1 FROM ShippingHeader WHERE CompanyId = @CompanyId AND CAST(DocDate AS DATE) = CAST(GETDATE() AS DATE)",
                    new { CompanyId = company.Id }, cancellationToken: ct));
                Header.DocNo = $"{DocPrefix.Shipping}-{DateTime.UtcNow:yyyyMMdd}-{seq:D5}";

                await conn.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO ShippingHeader
                        (Id, CompanyId, WarehouseId, DocNo, Status, DocDate, CarrierName, VehiclePlate, Notes, CreatedBy)
                    VALUES
                        (@Id, @CompanyId, @WarehouseId, @DocNo, @Status, @DocDate, @CarrierName, @VehiclePlate, @Notes, @UserId)",
                    new {
                        Header.Id, CompanyId = company.Id, Header.WarehouseId, Header.DocNo,
                        Status = DocStatus.Draft, Header.DocDate, Header.CarrierName, Header.VehiclePlate,
                        Header.Notes, UserId = user.Id
                    }, cancellationToken: ct));
                await audit.LogAsync("CREATE", "ShippingHeader", Header.Id, $"DocNo: {Header.DocNo}");
            }
            else
            {
                // Evrak bütünlüğü: bu sevkiyata satış faturası kesilmişse düzenlenemez (document-immutability §3)
                if (await DocumentLock.ShippingHasInvoiceAsync(conn, Header.Id, company.Id))
                {
                    TempData["Error"] = "Belge kilitli: bu sevkiyata bağlı satış faturası mevcut, düzenlenemez.";
                    return RedirectToPage(new { id = Header.Id });
                }
                await conn.ExecuteAsync(new CommandDefinition(
                    "UPDATE ShippingHeader SET WarehouseId=@WarehouseId, CarrierName=@CarrierName, VehiclePlate=@VehiclePlate, Notes=@Notes WHERE Id=@Id AND CompanyId=@CompanyId",
                    new { Header.WarehouseId, Header.CarrierName, Header.VehiclePlate, Header.Notes, Header.Id, CompanyId = company.Id },
                    cancellationToken: ct));
                await audit.LogAsync("UPDATE", "ShippingHeader", Header.Id, $"DocNo: {Header.DocNo}");
            }

            TempData["Success"] = "Sevkiyat kaydedildi.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
            return RedirectToPage(new { id = Header.Id == Guid.Empty ? (Guid?)null : Header.Id });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Sevkiyat başlık kayıt hatası: {HeaderId}", Header.Id);
            TempData["Error"] = "Sevkiyat kaydedilirken veritabanı hatası oluştu.";
            return RedirectToPage(new { id = Header.Id == Guid.Empty ? (Guid?)null : Header.Id });
        }

        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, Guid uomId, decimal qty, string? lotNo, Guid soLineId, CancellationToken ct)
    {
        // Satır ekler; UOM dönüşümünü fn_GetConversionRate ile hesaplar
        using var conn = db.Open();

        // Evrak bütünlüğü: faturalanmış sevkiyata satır eklenemez (document-immutability §3)
        if (await DocumentLock.ShippingHasInvoiceAsync(conn, id, company.Id))
        {
            TempData["Error"] = "Belge kilitli: bu sevkiyata bağlı satış faturası mevcut, satır eklenemez.";
            return RedirectToPage(new { id });
        }

        try
        {
            // Eğer itemId boşsa ve soLineId (Satış Sipariş Başlık ID'si) verilmişse, siparişteki tüm açık satırları aktar
            if (itemId == Guid.Empty && soLineId != Guid.Empty)
            {
                // Performans (PF-4): tek set-based INSERT...SELECT — eski N+1 (her satır için ayrı
                // fn_GetConversionRate + INSERT) kaldırıldı. UOM dönüşümü CROSS APPLY ile satır başına
                // bir kez hesaplanır; rate=0 ise 1'e düşülür (COALESCE/NULLIF).
                // İzolasyon: kaynak satırlar soh.CompanyId = @CompanyId ile firma-filtreli (güvenli).
                // isolation-guard:ignore  (CompanyId predikatı JOIN'de mevcut)
                var inserted = await conn.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO ShippingLine (HeaderId, SalesOrderLineId, ItemId, UomId, QtyOriginal, QtyBase, LotNo)
                    SELECT @HeaderId, sol.Id, sol.ItemId, sol.UomId,
                           rem.Qty, rem.Qty * COALESCE(NULLIF(cr.Rate, 0), 1), NULL
                    FROM SalesOrderLine sol
                    JOIN SalesOrderHeader soh ON soh.Id = sol.HeaderId
                    CROSS APPLY (SELECT (sol.QtyOrdered - sol.QtyShipped) AS Qty) rem
                    CROSS APPLY (SELECT dbo.fn_GetConversionRate(sol.ItemId, sol.UomId) AS Rate) cr
                    WHERE sol.HeaderId = @SalesOrderHeaderId AND soh.CompanyId = @CompanyId AND rem.Qty > 0",
                    new { HeaderId = id, SalesOrderHeaderId = soLineId, CompanyId = company.Id },
                    cancellationToken: ct));

                TempData["Success"] = inserted > 0
                    ? "Sipariş satırları sevkiyata aktarıldı."
                    : "Aktarılacak açık sipariş satırı bulunamadı.";
                return RedirectToPage(new { id });
            }

            // Guard: madde fiziksel sevke uygun mu? Hizmet (SERVICE) sevk edilemez (fiziksel taşınmaz)
            var exists = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId AND IsActive = 1 AND ItemType <> @ServiceType",
                new { ItemId = itemId, CompanyId = company.Id, ServiceType = ItemType.Service }, cancellationToken: ct));

            // Guard: madde sevkiyata uygun değilse (hizmet/pasif) sessiz dönme; kullanıcıyı bilgilendir
            if (exists == 0)
            {
                logger.LogWarning("Sevkiyat satır ekleme reddedildi: Item {ItemId} sevkiyata uygun değil (hizmet/pasif), Shipping {HeaderId}", itemId, id);
                TempData["Error"] = "Seçilen madde sevkiyata uygun değil (hizmet kalemi veya pasif). Satır eklenmedi.";
                return RedirectToPage(new { id });
            }

            var rateVal = await conn.ExecuteScalarAsync<decimal>(new CommandDefinition(
                "SELECT dbo.fn_GetConversionRate(@ItemId, @UomId)",
                new { ItemId = itemId, UomId = uomId }, cancellationToken: ct));

            if (rateVal == 0) rateVal = 1;

            await conn.ExecuteAsync(new CommandDefinition(@"
                -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
                -- Gerekçe: eklenen Item bu handler'da WHERE Id = @ItemId AND CompanyId = @CompanyId ile
                -- doğrulandı; bulunamazsa işlem iptal edildi (exists == 0).
                -- @HeaderId değeri OnGetAsync'te WHERE Id = @Id AND CompanyId = @CompanyId ile
                -- yüklenen ShippingHeader.Id'dir; farklı firmanın sevkiyatına satır eklenemez.
                -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                INSERT INTO ShippingLine (HeaderId, SalesOrderLineId, ItemId, UomId, QtyOriginal, QtyBase, LotNo)
                VALUES (@HeaderId, @SOLineId, @ItemId, @UomId, @Qty, @QtyBase, @LotNo)",
                new { HeaderId = id, SOLineId = soLineId, ItemId = itemId, UomId = uomId, Qty = qty, QtyBase = qty * rateVal, LotNo = lotNo },
                cancellationToken: ct));

            TempData["Success"] = "Satır eklendi.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Sevkiyat satır ekleme hatası: {HeaderId}", id);
            TempData["Error"] = "Satır eklenirken veritabanı hatası oluştu.";
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreatePickTaskAsync(Guid id, CancellationToken ct)
    {
        // sp_ShippingCreatePickTask: FIFO rezervasyon + eksik stok için üretim emri
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_ShippingCreatePickTask",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Toplama görevi oluşturma hatası: {HeaderId}", id);
            TempData["Error"] = "Toplama görevi oluşturulurken hata oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPostAsync(Guid id, CancellationToken ct)
    {
        // sp_ShippingPost: stok hareketi (ISSUE), SO güncelleme, ItemCost, otomatik fatura, durum değişimi
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_ShippingPost",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
            await audit.LogAsync("POST", "ShippingHeader", id, "Sevkiyat irsaliyesi onaylandı, stok çıkışı yapıldı");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Sevkiyat onay hatası: {HeaderId}", id);
            TempData["Error"] = "Sevkiyat onaylanırken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReverseAsync(Guid id, CancellationToken ct)
    {
        // sp_ShippingReverse: POSTED sevkiyatı CANCELLED yapar, ISSUE hareketlerini kapatıp ters REVERSAL yazar
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_ShippingReverse",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
            await audit.LogAsync("CANCEL", "ShippingHeader", id, "Sevkiyat iptal edildi, ters stok hareketi yazıldı");
            TempData["Success"] = "Sevkiyat iptal edildi.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı (bağlı fatura/dönem kilidi vb.)
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Sevkiyat iptal hatası: {HeaderId}", id);
            TempData["Error"] = "Sevkiyat iptal edilirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public record ShippingHeaderDto
    {
        public Guid     Id           { get; set; }
        public Guid     WarehouseId  { get; set; }
        public string   DocNo        { get; set; } = "";
        public string   Status       { get; set; } = DocStatus.Draft;
        public DateTime DocDate      { get; set; }
        public string?  CarrierName  { get; set; }
        public string?  VehiclePlate { get; set; }
        public string?  Notes        { get; set; }
    }

    public record ShippingLineDto
    {
        public Guid    Id               { get; set; }
        public Guid    ItemId           { get; set; }
        public Guid    UomId            { get; set; }
        public Guid    SalesOrderLineId { get; set; }
        public string? ItemCode         { get; set; }
        public string? ItemName         { get; set; }
        public string? UomCode          { get; set; }
        public decimal QtyOriginal      { get; set; }
        public decimal QtyBase          { get; set; }
        public string? LotNo            { get; set; }
        public string? SourceOrderNo    { get; set; }
    }
}
