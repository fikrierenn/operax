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
    
    public int DraftCount { get; set; }
    public int PostedCount { get; set; }
    public decimal TotalTransferQty { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        
        Transfers = await conn.QueryAsync<TransferDto>(@"
            SELECT t.*, fw.Code as FromWhCode, tw.Code as ToWhCode,
                   (SELECT COUNT(*) FROM StockTransferLine WHERE TransferId = t.Id) as LineCount
            FROM StockTransfer t
            JOIN Warehouse fw ON fw.Id = t.FromWarehouseId
            JOIN Warehouse tw ON tw.Id = t.ToWarehouseId
            WHERE t.CompanyId = @CompanyId
            ORDER BY t.CreatedAt DESC", new { CompanyId = company.Id });

        DraftCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM StockTransfer WHERE CompanyId = @CompanyId AND Status = 'DRAFT'",
            new { CompanyId = company.Id });

        PostedCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM StockTransfer WHERE CompanyId = @CompanyId AND Status = 'POSTED'",
            new { CompanyId = company.Id });

        TotalTransferQty = await conn.ExecuteScalarAsync<decimal>(@"
            SELECT ISNULL(SUM(l.QtyBase), 0)
            FROM StockTransferLine l
            JOIN StockTransfer t ON t.Id = l.TransferId
            WHERE t.CompanyId = @CompanyId AND t.Status = 'POSTED'",
            new { CompanyId = company.Id });
    }

    public record TransferDto { public Guid Id { get; set; } public string DocNo { get; set; } = ""; public string Status { get; set; } = ""; public string FromWhCode { get; set; } = ""; public string ToWhCode { get; set; } = ""; public int LineCount { get; set; } public DateTime CreatedAt { get; set; } }
}
