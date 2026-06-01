using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Transfer;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<TransferDto> Transfers { get; set; } = [];
    public IEnumerable<DdlDto>     Branches  { get; set; } = [];
    
    public int DraftCount { get; set; }
    public int PostedCount { get; set; }
    public decimal TotalTransferQty { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        
        Transfers = await conn.QueryAsync<TransferDto>(@"
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
            ORDER BY t.CreatedAt DESC", new { CompanyId = company.Id });

        // Şube filtresi dropdown için
        Branches = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Name AS Text FROM Branch WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Name",
            new { CompanyId = company.Id });

        DraftCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM StockTransfer WHERE CompanyId = @CompanyId AND Status = @StDraft",
            new { CompanyId = company.Id, StDraft = DocStatus.Draft });

        PostedCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM StockTransfer WHERE CompanyId = @CompanyId AND Status = @StPosted",
            new { CompanyId = company.Id, StPosted = DocStatus.Posted });

        TotalTransferQty = await conn.ExecuteScalarAsync<decimal>(@"
            SELECT ISNULL(SUM(l.QtyBase), 0)
            FROM StockTransferLine l
            JOIN StockTransfer t ON t.Id = l.TransferId
            WHERE t.CompanyId = @CompanyId AND t.Status = @StPosted",
            new { CompanyId = company.Id, StPosted = DocStatus.Posted });
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
