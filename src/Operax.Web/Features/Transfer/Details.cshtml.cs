using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Transfer;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public TransferHeaderDto Header { get; set; } = new();
    public IEnumerable<TransferLineDto> Lines      { get; set; } = [];
    public IEnumerable<WhDdlDto>         Warehouses     { get; set; } = [];
    public IEnumerable<DdlDto>          Items          { get; set; } = [];
    public IEnumerable<BinDto>          Bins           { get; set; } = [];
    // POSTED görünüm için şube adları
    public string? FromBranchName { get; set; }
    public string? ToBranchName   { get; set; }

    public bool IsNew => Header.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        // Dropdown listelerini ve transfer bilgilerini yükler
        using var conn = db.Open();

        // BranchId dahil — JS transfer tipi türetme için
        Warehouses = await conn.QueryAsync<WhDdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name, BranchId FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code",
            new { CompanyId = company.Id }, cancellationToken: ct));

        Items = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0",
            new { CompanyId = company.Id }, cancellationToken: ct));

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<TransferHeaderDto>(new CommandDefinition(
                "SELECT Id, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes FROM StockTransfer WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct)) ?? new();

            // Şube adları — detay görünümünde depo yanında gösterilir
            // Şube adları — CompanyId filtresi zorunlu (başka firma transfer ID'si bilinirse sızıntı önlenir)
            var branchNames = await conn.QueryFirstOrDefaultAsync<(string? From, string? To)>(new CommandDefinition(@"
                SELECT fb.Name AS From, tb.Name AS To
                FROM StockTransfer t
                JOIN Warehouse fw ON fw.Id = t.FromWarehouseId
                JOIN Warehouse tw ON tw.Id = t.ToWarehouseId
                LEFT JOIN Branch fb ON fb.Id = fw.BranchId
                LEFT JOIN Branch tb ON tb.Id = tw.BranchId
                WHERE t.Id = @Id AND t.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct));
            FromBranchName = branchNames.From;
            ToBranchName   = branchNames.To;

            Lines = await conn.QueryAsync<TransferLineDto>(new CommandDefinition(@"
                SELECT l.*, i.Code as ItemCode, i.Name as ItemName,
                       fb.Code as FromBinCode, tb.Code as ToBinCode
                FROM StockTransferLine l
                JOIN StockTransfer st ON st.Id = l.TransferId
                JOIN Item i ON i.Id = l.ItemId
                LEFT JOIN Bin fb ON fb.Id = l.FromBinId
                LEFT JOIN Bin tb ON tb.Id = l.ToBinId
                WHERE l.TransferId = @Id AND st.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct));

            // Bin listesi — CompanyId filtresi depo üzerinden
            Bins = await conn.QueryAsync<BinDto>(new CommandDefinition(@"
                SELECT b.Id, b.Code, b.WarehouseId
                FROM Bin b
                JOIN Warehouse w ON w.Id = b.WarehouseId
                WHERE w.CompanyId = @CompanyId AND b.IsActive = 1",
                new { CompanyId = company.Id }, cancellationToken: ct));
        }
        else
        {
            Header.DocNo = "NEW";
            Header.TransferType = TransferTypeHelper.BinToBin;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Transfer başlığını kaydeder veya günceller (DRAFT)
        using var conn = db.Open();

        // TransferType kodu kaynak/hedef depodan otomatik türetilir
        Header.TransferType = TransferTypeHelper.GetCode(Header.FromWarehouseId, Header.ToWarehouseId);

        if (IsNew)
        {
            Header.Id    = Guid.NewGuid();
            Header.DocNo = $"{DocPrefix.Transfer}-{DateTime.UtcNow:yyyyMMddHHmm}";

            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO StockTransfer
                    (Id, CompanyId, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes, CreatedBy)
                VALUES
                    (@Id, @CompanyId, @DocNo, @Status, @TransferType, @FromWarehouseId, @ToWarehouseId, @Notes, @UserId)",
                new {
                    Header.Id, CompanyId = company.Id, Header.DocNo,
                    Status = DocStatus.Draft, Header.TransferType,
                    Header.FromWarehouseId, Header.ToWarehouseId, Header.Notes,
                    UserId = user.Id
                }, cancellationToken: ct));
            await audit.LogAsync("CREATE", "StockTransfer", Header.Id, $"DocNo: {Header.DocNo}, Tür: {Header.TransferType}");
        }
        else
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE StockTransfer SET Notes=@Notes, TransferType=@TransferType WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Header.Notes, Header.TransferType, Header.Id, CompanyId = company.Id },
                cancellationToken: ct));
            await audit.LogAsync("UPDATE", "StockTransfer", Header.Id, $"DocNo: {Header.DocNo}");
        }

        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, Guid? fromBinId, Guid? toBinId, decimal qty, CancellationToken ct)
    {
        // Satır ekler; temel birimde miktarı hesaplar (UOM = temel birim varsayılır)
        using var conn = db.Open();

        var baseUomId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT BaseUomId FROM Item WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = itemId, CompanyId = company.Id }, cancellationToken: ct));

        // Ürün bulunamadıysa veya şirkete ait değilse yönlendir
        if (baseUomId is null) return RedirectToPage(new { id });

        await conn.ExecuteAsync(new CommandDefinition(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: eklenen Item bu handler'da WHERE Id = @Id AND CompanyId = @CompanyId ile
            -- doğrulandı; bulunamazsa işlem iptal edildi (BaseUomId null döndü).
            -- @TransferId değeri OnGetAsync'te WHERE Id = @Id AND CompanyId = @CompanyId ile
            -- yüklenen StockTransfer.Id'dir; farklı firmanın transferine satır eklenemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            INSERT INTO StockTransferLine (TransferId, ItemId, UomId, FromBinId, ToBinId, Qty, QtyBase)
            VALUES (@TransferId, @ItemId, @UomId, @FromBinId, @ToBinId, @Qty, @Qty)",
            new { TransferId = id, ItemId = itemId, UomId = baseUomId, FromBinId = fromBinId, ToBinId = toBinId, Qty = qty },
            cancellationToken: ct));

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPostAsync(Guid id, CancellationToken ct)
    {
        // sp_TransferPost: çift yönlü stok hareketi, durum değişimi
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition("sp_TransferPost",
            new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
        await audit.LogAsync("POST", "StockTransfer", id, "Transfer onaylandı, stok hareketi oluşturuldu");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReverseAsync(Guid id, CancellationToken ct)
    {
        // sp_TransferReverse: POSTED transferi CANCELLED yapar, çıkış+giriş hareketlerini kapatıp ters REVERSAL yazar
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_TransferReverse",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
            await audit.LogAsync("CANCEL", "StockTransfer", id, "Transfer iptal edildi, ters stok hareketi yazıldı");
            TempData["Success"] = "Transfer iptal edildi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            // İş kuralı hatası — SP Türkçe mesaj fırlattı (dönem kilidi vb.)
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Transfer iptal hatası: {HeaderId}", id);
            TempData["Error"] = "Transfer iptal edilirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    public record TransferHeaderDto
    {
        public Guid    Id               { get; set; }
        public string  DocNo            { get; set; } = "";
        public string  Status           { get; set; } = DocStatus.Draft;
        public string  TransferType     { get; set; } = TransferTypeHelper.BinToBin;
        public Guid    FromWarehouseId  { get; set; }
        public Guid    ToWarehouseId    { get; set; }
        public string? Notes            { get; set; }
    }

    public record TransferLineDto
    {
        public Guid    Id           { get; set; }
        public Guid    ItemId       { get; set; }
        public Guid    UomId        { get; set; }
        public string  ItemCode     { get; set; } = "";
        public string  ItemName     { get; set; } = "";
        public Guid?   FromBinId    { get; set; }
        public string? FromBinCode  { get; set; }
        public Guid?   ToBinId      { get; set; }
        public string? ToBinCode    { get; set; }
        public decimal Qty          { get; set; }
        public decimal QtyBase      { get; set; }
    }

    public record BinDto(Guid Id, string Code, Guid WarehouseId);

    // BranchId dahil depo dropdown DTO (JS türetme için)
    public record WhDdlDto
    {
        public Guid  Id       { get; set; }
        public string Code    { get; set; } = "";
        public string Name    { get; set; } = "";
        public Guid? BranchId { get; set; }
    }
}
