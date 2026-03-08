using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Serial;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    public SerialHeaderDto Serial { get; set; } = new();
    public IEnumerable<SerialMovementDto> Movements { get; set; } = [];

    public async Task OnGetAsync(Guid id)
    {
        // Seri numarasının güncel durumunu ve tam yaşam döngüsü geçmişini getirir
        using var conn = db.Open();

        Serial = await conn.QueryFirstOrDefaultAsync<SerialHeaderDto>(@"
            SELECT s.*, i.Code AS ItemCode, i.Name AS ItemName,
                   w.Code AS WhCode, b.Code AS BinCode
            FROM ItemSerial s
            JOIN Item i ON i.Id = s.ItemId
            LEFT JOIN Warehouse w ON w.Id = s.CurrentWarehouseId
            LEFT JOIN Bin b ON b.Id = s.CurrentBinId
            WHERE s.Id = @Id AND s.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }) ?? new();

        if (Serial.Id == Guid.Empty) return;

        // Yaşam döngüsü — bu seri noya ait tüm stok hareketleri
        Movements = await conn.QueryAsync<SerialMovementDto>(@"
            SELECT sm.MovementType, sm.QtyBase, sm.CreatedAt,
                   sm.SourceDoc, sm.LotNo,
                   w.Code AS WhCode, b.Code AS BinCode
            FROM StockMovement sm
            LEFT JOIN Warehouse w ON w.Id = sm.WarehouseId
            LEFT JOIN Bin b ON b.Id = sm.BinId
            WHERE sm.SerialNo = @SerialNo AND sm.ItemId = @ItemId
              AND sm.CompanyId = @CompanyId AND sm.IsCancelled = 0
            ORDER BY sm.CreatedAt ASC",
            new { SerialNo = Serial.SerialNo, ItemId = Serial.ItemId, CompanyId = company.Id });
    }

    public record SerialHeaderDto
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public string SerialNo { get; set; } = "";
        public string Status { get; set; } = "";
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string? LotNo { get; set; }
        public string? WhCode { get; set; }
        public string? BinCode { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public record SerialMovementDto
    {
        public string MovementType { get; set; } = "";
        public decimal QtyBase { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? SourceDoc { get; set; }
        public string? LotNo { get; set; }
        public string? WhCode { get; set; }
        public string? BinCode { get; set; }
    }
}
