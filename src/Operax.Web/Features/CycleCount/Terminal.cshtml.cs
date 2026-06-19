using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.CycleCount;

[Authorize]
public class TerminalModel(Db db, ICurrentCompany company, ICurrentUser user) : PageModel
{
    public CountSessionDto? ActiveCount { get; set; }
    public IEnumerable<PendingCountDto> PendingCounts { get; set; } = [];

    public async Task OnGetAsync(Guid? countId)
    {
        // El terminali: sayım belgesi seçilip raf raf fiziksel sayım sonucu girilir
        using var conn = db.Open();

        PendingCounts = await conn.QueryAsync<PendingCountDto>(@"
            SELECT c.Id, c.DocNo,
                   (SELECT COUNT(*) FROM CycleCountLine WHERE CycleCountId = c.Id) AS TotalLines,
                   (SELECT COUNT(*) FROM CycleCountLine WHERE CycleCountId = c.Id AND QtyCounted > 0) AS CountedLines
            FROM CycleCount c
            WHERE c.CompanyId = @CompanyId AND c.Status = @Status
            ORDER BY c.CreatedAt DESC",
            new { CompanyId = company.Id, Status = DocStatus.Counting });

        if (!countId.HasValue) return;

        ActiveCount = await conn.QueryFirstOrDefaultAsync<CountSessionDto>(@"
            SELECT c.Id, c.DocNo, c.Status, w.Code AS WhCode
            FROM CycleCount c
            LEFT JOIN Warehouse w ON w.Id = c.WarehouseId
            WHERE c.Id = @CountId AND c.CompanyId = @CompanyId",
            new { CountId = countId, CompanyId = company.Id });

        if (ActiveCount == null) return;

        // Sayılmamış (QtyCounted = 0) satırlar — önce bunlar
        ActiveCount.Lines = (await conn.QueryAsync<CountLineTermDto>(@"
            SELECT l.Id, l.QtyCounted, l.QtySystem,
                   i.Code AS ItemCode, i.Name AS ItemName,
                   b.Code AS BinCode, l.LotNo
            FROM CycleCountLine l
            JOIN CycleCount c ON c.Id = l.CycleCountId
            JOIN Item i ON i.Id = l.ItemId
            LEFT JOIN Bin b ON b.Id = l.BinId
            WHERE l.CycleCountId = @CountId AND c.CompanyId = @CompanyId
            ORDER BY (CASE WHEN l.QtyCounted = 0 THEN 0 ELSE 1 END), b.Code, i.Code",
            new { CountId = countId, CompanyId = company.Id })).ToList();
    }

    public async Task<IActionResult> OnPostEnterCountAsync(Guid countId, Guid lineId, decimal qtyCounted)
    {
        // Sayım sonucunu kaydeder (günceller), fark hesabı SP'de yapılacak
        using var conn = db.Open();
        await conn.ExecuteAsync(@"
            UPDATE l SET l.QtyCounted = @QtyCounted, l.CountedBy = @UserId, l.CountedAt = GETUTCDATE()
            FROM CycleCountLine l
            JOIN CycleCount c ON c.Id = l.CycleCountId
            WHERE l.Id = @LineId AND l.CycleCountId = @CountId AND c.CompanyId = @CompanyId",
            new { QtyCounted = qtyCounted, UserId = user.Id, LineId = lineId, CountId = countId, CompanyId = company.Id });

        TempData["Success"] = $"Sayım kaydedildi: {qtyCounted}";
        return RedirectToPage(new { countId });
    }

    public record CountSessionDto
    {
        public Guid Id { get; set; }
        public string DocNo { get; set; } = "";
        public string Status { get; set; } = "";
        public string? WhCode { get; set; }
        public List<CountLineTermDto> Lines { get; set; } = [];
        public int TotalLines => Lines.Count;
        public int CountedLines => Lines.Count(l => l.QtyCounted > 0);
    }

    public record CountLineTermDto
    {
        public Guid Id { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string? BinCode { get; set; }
        public string? LotNo { get; set; }
        public decimal QtySystem { get; set; }
        public decimal QtyCounted { get; set; }
        public bool IsCounted => QtyCounted > 0;
    }

    public record PendingCountDto
    {
        public Guid Id { get; set; }
        public string DocNo { get; set; } = "";
        public int TotalLines { get; set; }
        public int CountedLines { get; set; }
    }
}
