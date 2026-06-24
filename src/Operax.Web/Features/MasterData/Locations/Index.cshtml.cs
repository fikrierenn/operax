using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Locations;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    public IEnumerable<WarehouseDto> Warehouses { get; set; } = [];
    public IEnumerable<BinDto> Bins { get; set; } = [];
    public Guid? SelectedWhId { get; set; }

    public async Task OnGetAsync(Guid? whId, CancellationToken ct)
    {
        SelectedWhId = whId;
        using var conn = db.Open();

        // Depoları getir
        Warehouses = await conn.QueryAsync<WarehouseDto>(new CommandDefinition(@"
            SELECT Id, Code, Name FROM Warehouse
            WHERE CompanyId = @CompanyId AND IsDeleted = 0
            ORDER BY Code",
            new { CompanyId = company.Id }, cancellationToken: ct));

        // Depo seçilmediyse ilk depoyu varsayılan seç
        if (whId == null && Warehouses.Any())
        {
            SelectedWhId = Warehouses.First().Id;
        }

        // Seçili depoya ait hücreleri getir
        if (SelectedWhId != null)
        {
            // CompanyId JOIN: URL manipülasyonuyla yabancı depo rafları görülemesin
            Bins = await conn.QueryAsync<BinDto>(new CommandDefinition(@"
                SELECT b.Id, b.Code, b.Zone, b.IsPickingArea, b.IsReceivingArea
                FROM Bin b
                JOIN Warehouse w ON w.Id = b.WarehouseId
                WHERE b.WarehouseId = @WarehouseId AND w.CompanyId = @CompanyId AND b.IsDeleted = 0
                ORDER BY b.SortNo, b.Code",
                new { WarehouseId = SelectedWhId, CompanyId = company.Id }, cancellationToken: ct));
        }
    }

    // Seçili depoya yeni hücre ekler — depo bu firmaya ait olmalı (IDOR)
    public async Task<IActionResult> OnPostAddBinAsync(Guid whId, string code, string? zone, bool isPicking, bool isReceiving, CancellationToken ct)
    {
        try
        {
            using var conn = db.Open();
            // Guard: depo bu firmaya ait değilse işlem yapılmaz
            var owned = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM Warehouse WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0",
                new { Id = whId, CompanyId = company.Id }, cancellationToken: ct));
            if (owned == 0) return RedirectToPage(new { whId });

            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO Bin (WarehouseId, Code, Zone, IsPickingArea, IsReceivingArea, IsStorageArea, IsActive)
                VALUES (@WarehouseId, @Code, @Zone, @IsPicking, @IsReceiving, 1, 1)",
                new { WarehouseId = whId, Code = code, Zone = zone, IsPicking = isPicking, IsReceiving = isReceiving }, cancellationToken: ct));
            TempData["Success"] = "Hücre eklendi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Sistem hatası — ham mesaj gösterilmez, detay log'a
            logger.LogError(sqlEx, "Hücre ekleme DB hatası. Depo {WarehouseId}", whId);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { whId });
    }

    // Hücreyi günceller — hücrenin deposu bu firmaya ait olmalı (IDOR)
    public async Task<IActionResult> OnPostEditBinAsync(Guid whId, Guid binId, string code, string? zone, bool isPicking, bool isReceiving, CancellationToken ct)
    {
        try
        {
            using var conn = db.Open();
            var affected = await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE b SET b.Code = @Code, b.Zone = @Zone, b.IsPickingArea = @IsPicking, b.IsReceivingArea = @IsReceiving
                FROM Bin b JOIN Warehouse w ON w.Id = b.WarehouseId
                WHERE b.Id = @BinId AND w.CompanyId = @CompanyId AND b.IsDeleted = 0",
                new { BinId = binId, Code = code, Zone = zone, IsPicking = isPicking, IsReceiving = isReceiving, CompanyId = company.Id }, cancellationToken: ct));
            TempData[affected > 0 ? "Success" : "Error"] = affected > 0 ? "Hücre güncellendi." : "Hücre bulunamadı.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Hücre güncelleme DB hatası. Hücre {BinId}", binId);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { whId });
    }

    // Hücreyi soft-delete eder — içinde stok varsa engellenir
    public async Task<IActionResult> OnPostDeleteBinAsync(Guid whId, Guid binId, CancellationToken ct)
    {
        try
        {
            using var conn = db.Open();
            // İş kuralı: hücrede stok bakiyesi varsa silinemez
            var qty = await conn.ExecuteScalarAsync<decimal>(new CommandDefinition(
                "SELECT ISNULL(SUM(QtyBalance), 0) FROM tvf_InventoryBalance(@CompanyId) WHERE BinId = @BinId",
                new { BinId = binId, CompanyId = company.Id }, cancellationToken: ct));
            if (qty != 0)
            {
                TempData["Error"] = "Hücre silinemez: stok bakiyesi mevcut.";
                return RedirectToPage(new { whId });
            }

            // Soft-delete — yalnızca bu firmanın deposuna ait hücre
            var affected = await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE b SET b.IsDeleted = 1
                FROM Bin b JOIN Warehouse w ON w.Id = b.WarehouseId
                WHERE b.Id = @BinId AND w.CompanyId = @CompanyId AND b.IsDeleted = 0",
                new { BinId = binId, CompanyId = company.Id }, cancellationToken: ct));
            TempData[affected > 0 ? "Success" : "Error"] = affected > 0 ? "Hücre silindi." : "Hücre bulunamadı.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Hücre silme DB hatası. Hücre {BinId}", binId);
            TempData["Error"] = "Veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { whId });
    }

    public record WarehouseDto(Guid Id, string Code, string Name);
    public record BinDto(Guid Id, string Code, string? Zone, bool IsPickingArea, bool IsReceivingArea);
}
