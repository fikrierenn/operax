using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Lot;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    public LotHeaderDto Lot { get; set; } = new();
    public IEnumerable<LotMovementDto> Movements { get; set; } = [];
    public IEnumerable<LotLocationDto> Locations { get; set; } = [];

    public async Task OnGetAsync(Guid id)
    {
        // Lot başlık bilgilerini, hareket geçmişini ve konum dağılımını getirir
        using var conn = db.Open();

        // Lot master data + anlık bakiye
        Lot = await conn.QueryFirstOrDefaultAsync<LotHeaderDto>(@"
            SELECT l.*, i.Code AS ItemCode, i.Name AS ItemName,
                   ISNULL((SELECT SUM(QtyBase) FROM StockMovement
                            WHERE CompanyId = @CompanyId AND ItemId = l.ItemId
                              AND LotNo = l.LotNo AND IsCancelled = 0), 0) AS QtyOnHand
            FROM ItemLot l
            JOIN Item i ON i.Id = l.ItemId
            WHERE l.Id = @Id AND l.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }) ?? new();

        if (Lot.Id == Guid.Empty) return;

        // Hareket geçmişi — en yeni hareketten eskiye doğru, lot bazlı tüm hareketler
        Movements = await conn.QueryAsync<LotMovementDto>(@"
            SELECT TOP 200
                sm.Id, sm.MovementType, sm.QtyBase, sm.CreatedAt,
                sm.SourceDoc, sm.SourceDocId,
                w.Code AS WhCode, b.Code AS BinCode
            FROM StockMovement sm
            LEFT JOIN Warehouse w ON w.Id = sm.WarehouseId
            LEFT JOIN Bin b ON b.Id = sm.BinId
            WHERE sm.ItemId = @ItemId AND sm.LotNo = @LotNo
              AND sm.CompanyId = @CompanyId AND sm.IsCancelled = 0
            ORDER BY sm.CreatedAt DESC",
            new { ItemId = Lot.ItemId, LotNo = Lot.LotNo, CompanyId = company.Id });

        // Mevcut konum dağılımı — hangi rafta ne kadar var
        Locations = await conn.QueryAsync<LotLocationDto>(@"
            SELECT w.Code AS WhCode, b.Code AS BinCode, SUM(sm.QtyBase) AS Qty
            FROM StockMovement sm
            LEFT JOIN Warehouse w ON w.Id = sm.WarehouseId
            LEFT JOIN Bin b ON b.Id = sm.BinId
            WHERE sm.ItemId = @ItemId AND sm.LotNo = @LotNo
              AND sm.CompanyId = @CompanyId AND sm.IsCancelled = 0
            GROUP BY w.Code, b.Code
            HAVING SUM(sm.QtyBase) <> 0
            ORDER BY w.Code, b.Code",
            new { ItemId = Lot.ItemId, LotNo = Lot.LotNo, CompanyId = company.Id });
    }

    public record LotHeaderDto
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public string LotNo { get; set; } = "";
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? ProductionDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Notes { get; set; }
        public decimal QtyOnHand { get; set; }
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate < DateTime.UtcNow;
        public int DaysToExpiry => ExpiryDate.HasValue
            ? (int)(ExpiryDate.Value - DateTime.UtcNow).TotalDays
            : int.MaxValue;
    }

    public record LotMovementDto
    {
        public Guid Id { get; set; }
        public string MovementType { get; set; } = "";
        public decimal QtyBase { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? SourceDoc { get; set; }
        public Guid? SourceDocId { get; set; }
        public string? WhCode { get; set; }
        public string? BinCode { get; set; }
    }

    public record LotLocationDto
    {
        public string? WhCode { get; set; }
        public string? BinCode { get; set; }
        public decimal Qty { get; set; }
    }
}
