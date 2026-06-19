using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Inventory.Movements;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    // Filtre parametreleri — GET sorgu dizesinden bağlanır
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }

    public IEnumerable<MovementDto> Movements { get; set; } = [];

    public int Last24hMovements { get; set; }
    public decimal InflowVolume { get; set; }
    public decimal OutflowVolume { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        // TOP 500 + filtre: büyük tablolarda performans koruması
        // Tarih karşılaştırması SARGable: aralık kullanılır, YEAR()/MONTH() fonksiyon yasak
        const string sql = @"
            SELECT TOP 500
                   sm.CreatedAt, sm.MovementType, i.Name as ItemName, i.Code as ItemCode,
                   wh.Code as WarehouseCode, b.Code as BinCode, sm.QtyBase,
                   sm.SourceDocType, sm.SourceDocNo, u.UserName as OperatorName
            FROM StockMovement sm
            JOIN Item i ON i.Id = sm.ItemId
            JOIN Warehouse wh ON wh.Id = sm.WarehouseId
            LEFT JOIN Bin b ON b.Id = sm.BinId
            LEFT JOIN AspNetUsers u ON u.Id = CAST(sm.CreatedBy AS NVARCHAR(450))
            WHERE sm.CompanyId = @CompanyId
              AND (@Type IS NULL OR sm.MovementType = @Type)
              AND (@From IS NULL OR sm.CreatedAt >= @From)
              AND (@To   IS NULL OR sm.CreatedAt <  DATEADD(day, 1, @To))
              AND (@Q    IS NULL
                   OR i.Code LIKE '%' + @Q + '%'
                   OR i.Name LIKE '%' + @Q + '%'
                   OR sm.SourceDocNo LIKE '%' + @Q + '%')
            ORDER BY sm.CreatedAt DESC";

        Movements = await conn.QueryAsync<MovementDto>(sql,
            new { CompanyId = company.Id, Type, From, To, Q });

        Last24hMovements = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM StockMovement WHERE CompanyId = @CompanyId AND CreatedAt >= DATEADD(hour, -24, GETDATE())",
            new { CompanyId = company.Id });

        InflowVolume = await conn.ExecuteScalarAsync<decimal>(
            "SELECT ISNULL(SUM(QtyBase), 0) FROM StockMovement WHERE CompanyId = @CompanyId AND QtyBase > 0",
            new { CompanyId = company.Id });

        OutflowVolume = await conn.ExecuteScalarAsync<decimal>(
            "SELECT ISNULL(SUM(ABS(QtyBase)), 0) FROM StockMovement WHERE CompanyId = @CompanyId AND QtyBase < 0",
            new { CompanyId = company.Id });
    }

    public record MovementDto(DateTime CreatedAt, string MovementType, string ItemName, string ItemCode, string WarehouseCode, string BinCode, decimal QtyBase, string SourceDocType, string SourceDocNo, string? OperatorName);
}
