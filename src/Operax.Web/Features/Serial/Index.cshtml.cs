using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Serial;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<SerialListDto> Serials { get; set; } = [];
    public string? Filter { get; set; }

    public async Task OnGetAsync(string? q, CancellationToken ct)
    {
        // Şirkete ait seri numaralarını ürün ve konum bilgisiyle listeler
        Filter = q;
        using var conn = db.Open();

        Serials = await conn.QueryAsync<SerialListDto>(new CommandDefinition(@"
            SELECT TOP 500
                s.Id, s.SerialNo, s.Status, s.LotNo,
                i.Code AS ItemCode, i.Name AS ItemName,
                w.Code AS WhCode, b.Code AS BinCode,
                s.CreatedAt
            FROM ItemSerial s
            JOIN Item i ON i.Id = s.ItemId
            LEFT JOIN Warehouse w ON w.Id = s.CurrentWarehouseId
            LEFT JOIN Bin b ON b.Id = s.CurrentBinId
            WHERE s.CompanyId = @CompanyId
              AND (@Q IS NULL
                   OR s.SerialNo LIKE '%' + @Q + '%'
                   OR i.Code LIKE '%' + @Q + '%'
                   OR i.Name LIKE '%' + @Q + '%')
            ORDER BY s.CreatedAt DESC",
            new { CompanyId = company.Id, Q = string.IsNullOrWhiteSpace(q) ? null : q }, cancellationToken: ct));
    }

    public record SerialListDto
    {
        public Guid Id { get; set; }
        public string SerialNo { get; set; } = "";
        public string Status { get; set; } = "";
        public string? LotNo { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string? WhCode { get; set; }
        public string? BinCode { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
