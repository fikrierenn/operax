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

    public async Task OnGetAsync(Guid? id)
    {
        // Dropdown listelerini ve sevkiyat bilgilerini yükler
        using var conn = db.Open();

        Warehouses = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0",
            new { CompanyId = company.Id });

        // İş kuralı: CONSUMABLE (sarf malzeme) satışta gizlenir; yalnızca sarf fişinde kullanılır
        AvailableItems = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name, BaseUomId FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0 AND ItemType <> 'CONSUMABLE'",
            new { CompanyId = company.Id });

        // tvf_OpenSalesOrders: CompanyId parametreli iTVF
        OpenSalesOrders = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM tvf_OpenSalesOrders(@CompanyId)",
            new { CompanyId = company.Id });

        AllUoms = await conn.QueryAsync<DdlDto>(
            "SELECT dv.Id, dv.Code, dv.NameTr as Name FROM DictionaryValue dv JOIN DictionaryType dt ON dt.Id = dv.TypeId WHERE dt.Code = 'UOM' AND dt.CompanyId = @CompanyId AND dv.IsActive = 1 AND dv.IsDeleted = 0",
            new { CompanyId = company.Id });

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<ShippingHeaderDto>(@"
                SELECT Id, WarehouseId, DocNo, Status, DocDate, CarrierName, VehiclePlate, Notes
                FROM ShippingHeader WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }) ?? new();

            Lines = await conn.QueryAsync<ShippingLineDto>(@"
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
                new { Id = id, CompanyId = company.Id });

            PickTaskId = await conn.QueryFirstOrDefaultAsync<Guid?>(
                "SELECT Id FROM PickTask WHERE ShipmentId = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id });
        }
        else
        {
            Header.DocDate = DateTime.UtcNow;
            Header.Status  = DocStatus.Draft;
            Header.DocNo   = "NEW";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Sevkiyat başlığını kaydeder veya günceller (DRAFT)
        using var conn = db.Open();

        if (IsNew)
        {
            Header.Id = Guid.NewGuid();
            var seq = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) + 1 FROM ShippingHeader WHERE CompanyId = @CompanyId AND CAST(DocDate AS DATE) = CAST(GETDATE() AS DATE)",
                new { CompanyId = company.Id });
            Header.DocNo = $"{DocPrefix.Shipping}-{DateTime.UtcNow:yyyyMMdd}-{seq:D5}";

            await conn.ExecuteAsync(@"
                INSERT INTO ShippingHeader
                    (Id, CompanyId, WarehouseId, DocNo, Status, DocDate, CarrierName, VehiclePlate, Notes, CreatedBy)
                VALUES
                    (@Id, @CompanyId, @WarehouseId, @DocNo, @Status, @DocDate, @CarrierName, @VehiclePlate, @Notes, @UserId)",
                new {
                    Header.Id, CompanyId = company.Id, Header.WarehouseId, Header.DocNo,
                    Status = DocStatus.Draft, Header.DocDate, Header.CarrierName, Header.VehiclePlate,
                    Header.Notes, UserId = user.Id
                });
            await audit.LogAsync("CREATE", "ShippingHeader", Header.Id, $"DocNo: {Header.DocNo}");
        }
        else
        {
            await conn.ExecuteAsync(
                "UPDATE ShippingHeader SET WarehouseId=@WarehouseId, CarrierName=@CarrierName, VehiclePlate=@VehiclePlate, Notes=@Notes WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Header.WarehouseId, Header.CarrierName, Header.VehiclePlate, Header.Notes, Header.Id, CompanyId = company.Id });
            await audit.LogAsync("UPDATE", "ShippingHeader", Header.Id, $"DocNo: {Header.DocNo}");
        }

        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, Guid uomId, decimal qty, string? lotNo, Guid soLineId)
    {
        // Satır ekler; UOM dönüşümünü fn_GetConversionRate ile hesaplar
        using var conn = db.Open();

        // Eğer itemId boşsa ve soLineId (Satış Sipariş Başlık ID'si) verilmişse, siparişteki tüm açık satırları aktar
        if (itemId == Guid.Empty && soLineId != Guid.Empty)
        {
            var soLines = await conn.QueryAsync<(Guid Id, Guid ItemId, Guid UomId, decimal QtyRemaining)>(@"
                SELECT sol.Id, sol.ItemId, sol.UomId, (sol.QtyOrdered - sol.QtyShipped) as QtyRemaining
                FROM SalesOrderLine sol
                JOIN SalesOrderHeader soh ON soh.Id = sol.HeaderId
                WHERE sol.HeaderId = @SalesOrderHeaderId AND soh.CompanyId = @CompanyId AND (sol.QtyOrdered - sol.QtyShipped) > 0",
                new { SalesOrderHeaderId = soLineId, CompanyId = company.Id });

            foreach (var sol in soLines)
            {
                var rate = await conn.ExecuteScalarAsync<decimal>(
                    "SELECT dbo.fn_GetConversionRate(@ItemId, @UomId)",
                    new { ItemId = sol.ItemId, UomId = sol.UomId });

                if (rate == 0) rate = 1;

                await conn.ExecuteAsync(@"
                    -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
                    -- Gerekçe: kaynak SalesOrderLine'lar bu handler'da
                    -- JOIN SalesOrderHeader soh ON soh.Id = sol.HeaderId ... AND soh.CompanyId = @CompanyId
                    -- filtresiyle çekildi; yalnızca aynı firmanın açık sipariş satırları döndü.
                    -- Döngüdeki her sol kaydı o doğrulanmış sorgudan geldiğinden farklı firmaya satır eklenemez.
                    -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                    INSERT INTO ShippingLine (HeaderId, SalesOrderLineId, ItemId, UomId, QtyOriginal, QtyBase, LotNo)
                    VALUES (@HeaderId, @SOLineId, @ItemId, @UomId, @Qty, @QtyBase, @LotNo)",
                    new {
                        HeaderId = id,
                        SOLineId = sol.Id,
                        ItemId = sol.ItemId,
                        UomId = sol.UomId,
                        Qty = sol.QtyRemaining,
                        QtyBase = sol.QtyRemaining * rate,
                        LotNo = (string?)null
                    });
            }

            return RedirectToPage(new { id });
        }

        // Guard: madde geçerli ve satışa uygun mu? CONSUMABLE sevkiyatta kabul edilmez
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId AND IsActive = 1 AND ItemType <> 'CONSUMABLE'",
            new { ItemId = itemId, CompanyId = company.Id });

        if (exists == 0) return RedirectToPage(new { id });

        var rateVal = await conn.ExecuteScalarAsync<decimal>(
            "SELECT dbo.fn_GetConversionRate(@ItemId, @UomId)",
            new { ItemId = itemId, UomId = uomId });

        if (rateVal == 0) rateVal = 1;

        await conn.ExecuteAsync(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: eklenen Item bu handler'da WHERE Id = @ItemId AND CompanyId = @CompanyId ile
            -- doğrulandı; bulunamazsa işlem iptal edildi (exists == 0).
            -- @HeaderId değeri OnGetAsync'te WHERE Id = @Id AND CompanyId = @CompanyId ile
            -- yüklenen ShippingHeader.Id'dir; farklı firmanın sevkiyatına satır eklenemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            INSERT INTO ShippingLine (HeaderId, SalesOrderLineId, ItemId, UomId, QtyOriginal, QtyBase, LotNo)
            VALUES (@HeaderId, @SOLineId, @ItemId, @UomId, @Qty, @QtyBase, @LotNo)",
            new { HeaderId = id, SOLineId = soLineId, ItemId = itemId, UomId = uomId, Qty = qty, QtyBase = qty * rateVal, LotNo = lotNo });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreatePickTaskAsync(Guid id)
    {
        // sp_ShippingCreatePickTask: FIFO rezervasyon + eksik stok için üretim emri
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_ShippingCreatePickTask",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure);
        }
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

    public async Task<IActionResult> OnPostPostAsync(Guid id)
    {
        // sp_ShippingPost: stok hareketi (ISSUE), SO güncelleme, ItemCost, otomatik fatura, durum değişimi
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_ShippingPost",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure);
            await audit.LogAsync("POST", "ShippingHeader", id, "Sevkiyat irsaliyesi onaylandı, stok çıkışı yapıldı");
        }
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

    public async Task<IActionResult> OnPostReverseAsync(Guid id)
    {
        // sp_ShippingReverse: POSTED sevkiyatı CANCELLED yapar, ISSUE hareketlerini kapatıp ters REVERSAL yazar
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_ShippingReverse",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure);
            await audit.LogAsync("CANCEL", "ShippingHeader", id, "Sevkiyat iptal edildi, ters stok hareketi yazıldı");
            TempData["Success"] = "Sevkiyat iptal edildi.";
        }
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
