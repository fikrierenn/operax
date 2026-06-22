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

    // Sayfalama (PF-1) — Items/Index template'i (eski TOP 500 yerine gerçek sayfalama)
    [BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;

        // Tarih karşılaştırması SARGable (aralık); sayfa satırları + filtreli toplam tek round-trip (PF-1)
        const string sql = @"
            SELECT sm.CreatedAt, sm.MovementType, i.Name as ItemName, i.Code as ItemCode,
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
            ORDER BY sm.CreatedAt DESC
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM StockMovement sm
            JOIN Item i ON i.Id = sm.ItemId
            WHERE sm.CompanyId = @CompanyId
              AND (@Type IS NULL OR sm.MovementType = @Type)
              AND (@From IS NULL OR sm.CreatedAt >= @From)
              AND (@To   IS NULL OR sm.CreatedAt <  DATEADD(day, 1, @To))
              AND (@Q    IS NULL
                   OR i.Code LIKE '%' + @Q + '%'
                   OR i.Name LIKE '%' + @Q + '%'
                   OR sm.SourceDocNo LIKE '%' + @Q + '%');";

        using var grid = await conn.QueryMultipleAsync(
            new CommandDefinition(sql,
                new { CompanyId = company.Id, Type, From, To, Q, Page = page, PageSize },
                cancellationToken: ct));
        Movements = (await grid.ReadAsync<MovementDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();

        Last24hMovements = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM StockMovement WHERE CompanyId = @CompanyId AND CreatedAt >= DATEADD(hour, -24, GETDATE())",
                new { CompanyId = company.Id },
                cancellationToken: ct));

        InflowVolume = await conn.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                "SELECT ISNULL(SUM(QtyBase), 0) FROM StockMovement WHERE CompanyId = @CompanyId AND QtyBase > 0",
                new { CompanyId = company.Id },
                cancellationToken: ct));

        OutflowVolume = await conn.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                "SELECT ISNULL(SUM(ABS(QtyBase)), 0) FROM StockMovement WHERE CompanyId = @CompanyId AND QtyBase < 0",
                new { CompanyId = company.Id },
                cancellationToken: ct));
    }

    public record MovementDto(DateTime CreatedAt, string MovementType, string ItemName, string ItemCode, string WarehouseCode, string BinCode, decimal QtyBase, string SourceDocType, string SourceDocNo, string? OperatorName);
}
