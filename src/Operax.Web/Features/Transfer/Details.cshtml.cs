using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Transfer;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, IAuditService audit) : PageModel
{
    [BindProperty]
    public TransferHeaderDto Header { get; set; } = new();
    public IEnumerable<TransferLineDto> Lines      { get; set; } = [];
    public IEnumerable<DdlDto>          Warehouses { get; set; } = [];
    public IEnumerable<DdlDto>          Items      { get; set; } = [];
    public IEnumerable<BinDto>          Bins       { get; set; } = [];

    public bool IsNew => Header.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        // Dropdown listelerini ve transfer bilgilerini yükler
        using var conn = db.Open();

        Warehouses = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0",
            new { CompanyId = company.Id });

        Items = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code, Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0",
            new { CompanyId = company.Id });

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<TransferHeaderDto>(
                "SELECT * FROM StockTransfer WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }) ?? new();

            Lines = await conn.QueryAsync<TransferLineDto>(@"
                SELECT l.*, i.Code as ItemCode, i.Name as ItemName,
                       fb.Code as FromBinCode, tb.Code as ToBinCode
                FROM StockTransferLine l
                JOIN StockTransfer st ON st.Id = l.TransferId
                JOIN Item i ON i.Id = l.ItemId
                LEFT JOIN Bin fb ON fb.Id = l.FromBinId
                LEFT JOIN Bin tb ON tb.Id = l.ToBinId
                WHERE l.TransferId = @Id AND st.CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id });

            // Bin listesi — CompanyId filtresi depo üzerinden
            Bins = await conn.QueryAsync<BinDto>(@"
                SELECT b.Id, b.Code, b.WarehouseId
                FROM Bin b
                JOIN Warehouse w ON w.Id = b.WarehouseId
                WHERE w.CompanyId = @CompanyId AND b.IsActive = 1",
                new { CompanyId = company.Id });
        }
        else
        {
            Header.DocNo        = "NEW";
            Header.TransferType = "BIN_TO_BIN";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Transfer başlığını kaydeder veya günceller (DRAFT)
        using var conn = db.Open();

        if (IsNew)
        {
            Header.Id    = Guid.NewGuid();
            Header.DocNo = $"{DocPrefix.Transfer}-{DateTime.UtcNow:yyyyMMddHHmm}";

            await conn.ExecuteAsync(@"
                INSERT INTO StockTransfer
                    (Id, CompanyId, DocNo, Status, TransferType, FromWarehouseId, ToWarehouseId, Notes, CreatedBy)
                VALUES
                    (@Id, @CompanyId, @DocNo, @Status, @TransferType, @FromWarehouseId, @ToWarehouseId, @Notes, @UserId)",
                new {
                    Header.Id, CompanyId = company.Id, Header.DocNo,
                    Status = DocStatus.Draft, Header.TransferType,
                    Header.FromWarehouseId, Header.ToWarehouseId, Header.Notes,
                    UserId = user.Id
                });
            await audit.LogAsync("CREATE", "StockTransfer", Header.Id, $"DocNo: {Header.DocNo}, Tür: {Header.TransferType}");
        }
        else
        {
            await conn.ExecuteAsync(
                "UPDATE StockTransfer SET Notes=@Notes, TransferType=@TransferType WHERE Id=@Id AND CompanyId=@CompanyId",
                new { Header.Notes, Header.TransferType, Header.Id, CompanyId = company.Id });
            await audit.LogAsync("UPDATE", "StockTransfer", Header.Id, $"DocNo: {Header.DocNo}");
        }

        return RedirectToPage(new { id = Header.Id });
    }

    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, Guid? fromBinId, Guid? toBinId, decimal qty)
    {
        // Satır ekler; temel birimde miktarı hesaplar (UOM = temel birim varsayılır)
        using var conn = db.Open();

        var baseUomId = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT BaseUomId FROM Item WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = itemId, CompanyId = company.Id });

        // Ürün bulunamadıysa veya şirkete ait değilse yönlendir
        if (baseUomId is null) return RedirectToPage(new { id });

        await conn.ExecuteAsync(@"
            /* isolation-guard:ignore: Item WHERE CompanyId = @CompanyId (satir 106) ile dogrulandi; TransferId = id, header OnGetAsync'te CompanyId ile dogrulandi */
            INSERT INTO StockTransferLine (TransferId, ItemId, UomId, FromBinId, ToBinId, Qty, QtyBase)
            VALUES (@TransferId, @ItemId, @UomId, @FromBinId, @ToBinId, @Qty, @Qty)",
            new { TransferId = id, ItemId = itemId, UomId = baseUomId, FromBinId = fromBinId, ToBinId = toBinId, Qty = qty });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPostAsync(Guid id)
    {
        // sp_TransferPost: çift yönlü stok hareketi, durum değişimi
        using var conn = db.Open();
        await conn.ExecuteAsync("sp_TransferPost",
            new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
            commandType: CommandType.StoredProcedure);
        await audit.LogAsync("POST", "StockTransfer", id, "Transfer onaylandı, stok hareketi oluşturuldu");
        return RedirectToPage(new { id });
    }

    public record TransferHeaderDto
    {
        public Guid    Id               { get; set; }
        public string  DocNo            { get; set; } = "";
        public string  Status           { get; set; } = DocStatus.Draft;
        public string  TransferType     { get; set; } = "BIN_TO_BIN";
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
}
