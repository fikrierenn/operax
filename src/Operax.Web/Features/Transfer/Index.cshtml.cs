using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Transfer;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    // Transfer no araması
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    // Durum filtresi — DocStatus sabitleri veya boş
    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    // Şube filtresi — Guid veya null
    [BindProperty(SupportsGet = true)]
    public Guid? BranchFilter { get; set; }

    public IEnumerable<TransferDto> Transfers { get; set; } = [];
    public IEnumerable<DdlDto>     Branches  { get; set; } = [];

    public int DraftCount { get; set; }
    public int PostedCount { get; set; }
    public decimal TotalTransferQty { get; set; }

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        // İş kuralı: arama terimi varsa DocNo LIKE filtresi uygulanır
        var search = string.IsNullOrWhiteSpace(Q) ? null : $"%{Q.Trim()}%";

        // İş kuralı: durum filtresi DocStatus sabitlerinden biri değilse tümü gösterilir
        var statusParam = StatusFilter is DocStatus.Draft or DocStatus.Posted
            ? StatusFilter : null;

        using var conn = db.Open();

        var page = Page < 1 ? 1 : Page;
        // Sayfa satırları + aynı filtrenin toplam sayısı tek round-trip (PF-1)
        const string sql = @"
            SELECT t.Id, t.DocNo, t.Status, t.TransferType, t.CreatedAt,
                   fw.Code AS FromWhCode, fw.BranchId AS FromBranchId, fb.Name AS FromBranchName,
                   tw.Code AS ToWhCode,   tw.BranchId AS ToBranchId,   tb.Name AS ToBranchName,
                   (SELECT COUNT(*) FROM StockTransferLine WHERE TransferId = t.Id) AS LineCount
            FROM StockTransfer t
            JOIN Warehouse fw ON fw.Id = t.FromWarehouseId
            JOIN Warehouse tw ON tw.Id = t.ToWarehouseId
            LEFT JOIN Branch fb ON fb.Id = fw.BranchId
            LEFT JOIN Branch tb ON tb.Id = tw.BranchId
            WHERE t.CompanyId = @CompanyId
              AND (@Search     IS NULL OR t.DocNo LIKE @Search)
              AND (@Status     IS NULL OR t.Status = @Status)
              AND (@BranchId   IS NULL OR fw.BranchId = @BranchId OR tw.BranchId = @BranchId)
            ORDER BY t.CreatedAt DESC
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM StockTransfer t
            JOIN Warehouse fw ON fw.Id = t.FromWarehouseId
            JOIN Warehouse tw ON tw.Id = t.ToWarehouseId
            WHERE t.CompanyId = @CompanyId
              AND (@Search     IS NULL OR t.DocNo LIKE @Search)
              AND (@Status     IS NULL OR t.Status = @Status)
              AND (@BranchId   IS NULL OR fw.BranchId = @BranchId OR tw.BranchId = @BranchId);";

        using (var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql,
            new { CompanyId = company.Id, Search = search, Status = statusParam, BranchId = BranchFilter, Page = page, PageSize },
            cancellationToken: ct)))
        {
            Transfers = (await grid.ReadAsync<TransferDto>()).ToList();
            FilteredCount = await grid.ReadSingleAsync<int>();
        }

        // Şube filtresi dropdown için
        Branches = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Name AS Text FROM Branch WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Name",
            new { CompanyId = company.Id }, cancellationToken: ct));

        DraftCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM StockTransfer WHERE CompanyId = @CompanyId AND Status = @StDraft",
            new { CompanyId = company.Id, StDraft = DocStatus.Draft }, cancellationToken: ct));

        PostedCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM StockTransfer WHERE CompanyId = @CompanyId AND Status = @StPosted",
            new { CompanyId = company.Id, StPosted = DocStatus.Posted }, cancellationToken: ct));

        TotalTransferQty = await conn.ExecuteScalarAsync<decimal>(new CommandDefinition(@"
            SELECT ISNULL(SUM(l.QtyBase), 0)
            FROM StockTransferLine l
            JOIN StockTransfer t ON t.Id = l.TransferId
            WHERE t.CompanyId = @CompanyId AND t.Status = @StPosted",
            new { CompanyId = company.Id, StPosted = DocStatus.Posted }, cancellationToken: ct));
    }

    public record TransferDto
    {
        public Guid    Id           { get; set; }
        public string  DocNo        { get; set; } = "";
        public string  Status       { get; set; } = "";
        public string  TransferType { get; set; } = "";
        public string  FromWhCode   { get; set; } = "";
        public Guid?   FromBranchId   { get; set; }
        public string? FromBranchName { get; set; }
        public string  ToWhCode       { get; set; } = "";
        public Guid?   ToBranchId     { get; set; }
        public string? ToBranchName   { get; set; }
        public int     LineCount    { get; set; }
        public DateTime CreatedAt   { get; set; }

        // Türetilmiş Türkçe etiket — BIN_TO_BIN kodu önce kontrol edilir, sonra şube karşılaştırması
        public string TypeLabel => TransferType == TransferTypeHelper.BinToBin
            ? "Raf / Hücre Arası"
            : TransferTypeHelper.GetWhLabel(FromBranchId, ToBranchId);
    }
}
