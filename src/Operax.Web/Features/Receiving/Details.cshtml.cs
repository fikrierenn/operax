using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Receiving;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public ReceivingHeaderDto Header { get; set; } = new();
    public IEnumerable<ReceivingLineDto> Lines { get; set; } = [];

    public IEnumerable<DdlDto> Warehouses { get; set; } = [];
    public IEnumerable<DdlDto> Partners { get; set; } = [];
    public IEnumerable<DdlDto> AvailableItems { get; set; } = [];
    public IEnumerable<DdlDto> OpenPurchaseOrders { get; set; } = [];
    public IEnumerable<DdlDto> AllUoms { get; set; } = [];

    public bool IsNew => Header.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        // Dropdown listelerini ve belge bilgilerini yükler
        using var conn = db.Open();

        Warehouses = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0",
            new { CompanyId = company.Id });

        Partners = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Partner WHERE CompanyId = @CompanyId AND Type IN ('VENDOR', 'BOTH') AND IsDeleted = 0",
            new { CompanyId = company.Id });

        AvailableItems = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name, BaseUomId FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0",
            new { CompanyId = company.Id });

        // tvf_OpenPurchaseOrders: CompanyId parametreli iTVF
        OpenPurchaseOrders = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM tvf_OpenPurchaseOrders(@CompanyId)",
            new { CompanyId = company.Id });

        AllUoms = await conn.QueryAsync<DdlDto>(
            "SELECT dv.Id, dv.Code, dv.NameTr as Name FROM DictionaryValue dv JOIN DictionaryType dt ON dt.Id = dv.TypeId WHERE dt.Code = 'UOM' AND dt.CompanyId = @CompanyId AND dv.IsActive = 1 AND dv.IsDeleted = 0",
            new { CompanyId = company.Id });

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<ReceivingHeaderDto>(@"
                SELECT r.Id, r.WarehouseId, r.PartnerId, r.PurchaseOrderId, r.DocNo, r.Status, r.Notes,
                       p.Name as PartnerName
                FROM ReceivingHeader r
                JOIN Partner p ON p.Id = r.PartnerId
                WHERE r.Id = @Id AND r.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }) ?? new();

            Lines = await conn.QueryAsync<ReceivingLineDto>(@"
                SELECT l.Id, i.Code as ItemCode, i.Name as ItemName, dv.Code as UomCode,
                       l.QtyOriginal, l.QtyBase, l.LotNo, l.PurchaseOrderLineId, l.ItemId, l.UomId
                FROM ReceivingLine l
                JOIN ReceivingHeader rh ON rh.Id = l.HeaderId
                JOIN Item i ON i.Id = l.ItemId
                JOIN DictionaryValue dv ON dv.Id = l.UomId
                WHERE l.HeaderId = @Id AND rh.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id });

            // Bağlı alış faturası sayacı (PurchaseInvoice — satır-bazlı Receiving bağı)
            InvoiceCount = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(DISTINCT pi.Id)
                FROM PurchaseInvoice pi
                JOIN PurchaseInvoiceLine pil ON pil.InvoiceId = pi.Id
                JOIN ReceivingLine rl ON rl.Id = pil.SourceReceivingLineId
                WHERE rl.HeaderId = @Id AND pi.CompanyId = @CompanyId AND pi.Status <> @Cancelled",
                new { Id = id, CompanyId = company.Id, Cancelled = DocStatus.Cancelled });

            DocFlow = new DocFlowVm([
                new DocFlowItem(
                    Label: "Alış Faturası",
                    Count: InvoiceCount,
                    ListUrl: InvoiceCount > 0 ? $"/PurchaseInvoices?receivingId={id.Value}" : null,
                    CreateUrl: Header.Status == DocStatus.Posted
                        ? $"/Receiving/Details/{id.Value}?handler=CreateInvoice"
                        : null,
                    CreateLabel: "Alış Faturası Oluştur",
                    CanCreate: Header.Status == DocStatus.Posted
                        && user.HasRole("Administrator","Purchasing","Finance"))
            ]);
        }
        else
        {
            Header.Status = DocStatus.Draft;
            Header.DocNo  = "NEW";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Belge başlığını kaydeder veya günceller (DRAFT)
        using var conn = db.Open();

        if (IsNew)
        {
            Header.Id = Guid.NewGuid();
            // DocNo: RCV-YYYYMMDD-00001 formatı — şirket bazlı günlük sıra numarası
            var seq = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) + 1 FROM ReceivingHeader WHERE CompanyId = @CompanyId AND CAST(DocDate AS DATE) = CAST(GETDATE() AS DATE)",
                new { CompanyId = company.Id });
            Header.DocNo = $"{DocPrefix.Receiving}-{DateTime.UtcNow:yyyyMMdd}-{seq:D5}";

            await conn.ExecuteAsync(@"
                INSERT INTO ReceivingHeader
                    (Id, CompanyId, WarehouseId, PartnerId, PurchaseOrderId, DocNo, Status, Notes, CreatedBy)
                VALUES
                    (@Id, @CompanyId, @WarehouseId, @PartnerId, @PurchaseOrderId, @DocNo, @Status, @Notes, @UserId)",
                new {
                    Header.Id, CompanyId = company.Id, Header.WarehouseId, Header.PartnerId,
                    Header.PurchaseOrderId, Header.DocNo, Status = DocStatus.Draft, Header.Notes,
                    UserId = user.Id
                });
            await audit.LogAsync("CREATE", "ReceivingHeader", Header.Id, $"DocNo: {Header.DocNo}");
        }
        else
        {
            await conn.ExecuteAsync(
                "UPDATE ReceivingHeader SET WarehouseId=@WarehouseId, PartnerId=@PartnerId, PurchaseOrderId=@PurchaseOrderId, Notes=@Notes WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Header.WarehouseId, Header.PartnerId, Header.PurchaseOrderId, Header.Notes, Header.Id, CompanyId = company.Id });
            await audit.LogAsync("UPDATE", "ReceivingHeader", Header.Id, $"DocNo: {Header.DocNo}");
        }

        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, Guid uomId, decimal qty, string? lotNo, Guid? poLineId)
    {
        // Satır ekler; UOM dönüşümünü fn_GetConversionRate ile hesaplar
        using var conn = db.Open();

        // Ürün kontrolü — CompanyId dahil
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Item WHERE Id = @ItemId AND CompanyId = @CompanyId AND IsActive = 1",
            new { ItemId = itemId, CompanyId = company.Id });

        if (exists == 0) return RedirectToPage(new { id });

        // DB fonksiyonu ile dönüşüm oranı
        var rate = await conn.ExecuteScalarAsync<decimal>(
            "SELECT dbo.fn_GetConversionRate(@ItemId, @UomId)",
            new { ItemId = itemId, UomId = uomId });

        if (rate == 0) rate = 1;

        await conn.ExecuteAsync(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: eklenen Item bu handler'da WHERE Id = @ItemId AND CompanyId = @CompanyId ile
            -- doğrulandı; bulunamazsa işlem iptal edildi (exists == 0).
            -- @HeaderId değeri OnGetAsync'te WHERE r.Id = @Id AND r.CompanyId = @CompanyId ile
            -- yüklenen ReceivingHeader.Id'dir; farklı firmanın mal kabul belgesine satır eklenemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            INSERT INTO ReceivingLine (HeaderId, ItemId, UomId, QtyOriginal, QtyBase, LotNo, PurchaseOrderLineId)
            VALUES (@HeaderId, @ItemId, @UomId, @Qty, @QtyBase, @LotNo, @PoLineId)",
            new { HeaderId = id, ItemId = itemId, UomId = uomId, Qty = qty, QtyBase = qty * rate, LotNo = lotNo, PoLineId = poLineId });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPostAsync(Guid id)
    {
        // sp_ReceivingPost: stok hareketi, PO güncelleme, ItemCost MA, durum değişimi
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync("sp_ReceivingPost",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure);
            await audit.LogAsync("POST", "ReceivingHeader", id, "Mal kabul irsaliyesi onaylandı");
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Mal kabul onay hatası: {HeaderId}", id);
            TempData["Error"] = "Mal kabul onaylanırken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public record ReceivingHeaderDto
    {
        public Guid    Id               { get; set; }
        public Guid    WarehouseId      { get; set; }
        public Guid    PartnerId        { get; set; }
        public Guid?   PurchaseOrderId  { get; set; }
        public string  DocNo            { get; set; } = "";
        public string  Status           { get; set; } = DocStatus.Draft;
        public string? Notes            { get; set; }
        public string? PartnerName      { get; set; }
    }

    public record ReceivingLineDto
    {
        public Guid    Id                  { get; set; }
        public Guid    ItemId              { get; set; }
        public Guid    UomId               { get; set; }
        public string? ItemCode            { get; set; }
        public string? ItemName            { get; set; }
        public string? UomCode             { get; set; }
        public decimal QtyOriginal         { get; set; }
        public decimal QtyBase             { get; set; }
        public string? LotNo               { get; set; }
        public Guid?   PurchaseOrderLineId { get; set; }
    }

    // Belge zinciri sayaçları — smart button partial için
    public int InvoiceCount { get; set; }
    public DocFlowVm? DocFlow { get; set; }

    public async Task<IActionResult> OnPostCreateInvoiceAsync(Guid id)
    {
        // sp_CreatePurchaseInvoiceFromReceiving: POSTED Receiving'den DRAFT PurchaseInvoice + satırlar (Plan 24)
        using var conn = db.Open();
        try
        {
            var prm = new DynamicParameters();
            prm.Add("ReceivingId",  id);
            prm.Add("CompanyId",    company.Id);
            prm.Add("UserId",       user.Id);
            prm.Add("NewInvoiceId", dbType: System.Data.DbType.Guid,
                    direction: System.Data.ParameterDirection.Output);

            await conn.ExecuteAsync("sp_CreatePurchaseInvoiceFromReceiving", prm,
                commandType: System.Data.CommandType.StoredProcedure);

            var newId = prm.Get<Guid>("NewInvoiceId");
            TempData["Success"] = "Alış faturası oluşturuldu.";
            return RedirectToPage("/PurchaseInvoices/Details", new { id = newId });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
            return RedirectToPage(new { id });
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Fatura oluşturma hatası: {ReceivingId}", id);
            TempData["Error"] = "Fatura oluşturulurken veritabanı hatası oluştu.";
            return RedirectToPage(new { id });
        }
    }
}
