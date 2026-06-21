using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Manufacturing.WorkCenters;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company, ILogger<IndexModel> logger) : PageModel
{
    public IEnumerable<WorkCenterDto> WorkCenters { get; set; } = [];

    [BindProperty]
    public WorkCenterFormDto Form { get; set; } = new();

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Şirkete ait iş merkezlerini / makineleri listeler
        using var conn = db.Open();
        var page = Page < 1 ? 1 : Page;
        const string sql = @"
            SELECT Id, Code, Name,
                   LaborRatePerSec * 3600 AS LaborRatePerHour,
                   MachineRatePerSec * 3600 AS MachineRatePerHour,
                   ElectricityRatePerSec * 3600 AS ElectricityRatePerHour,
                   IsActive
            FROM WorkCenter
            WHERE CompanyId = @CompanyId
            ORDER BY Code
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM WorkCenter WHERE CompanyId = @CompanyId;";
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { CompanyId = company.Id, Page = page, PageSize }, cancellationToken: ct));
        WorkCenters = (await grid.ReadAsync<WorkCenterDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Yeni iş merkezi ekler veya mevcut iş merkezini günceller
        using var conn = db.Open();
        try
        {
            if (Form.Id == Guid.Empty)
            {
                await conn.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO WorkCenter (Id, CompanyId, Code, Name,
                        LaborRatePerSec, MachineRatePerSec, ElectricityRatePerSec, IsActive)
                    VALUES (NEWID(), @CompanyId, @Code, @Name,
                        @LaborRate / 3600.0, @MachineRate / 3600.0, @ElectricRate / 3600.0, @IsActive)",
                    new
                    {
                        CompanyId = company.Id,
                        Form.Code, Form.Name,
                        LaborRate = Form.LaborRatePerHour,
                        MachineRate = Form.MachineRatePerHour,
                        ElectricRate = Form.ElectricityRatePerHour,
                        Form.IsActive
                    }, cancellationToken: ct));
            }
            else
            {
                // CompanyId: başka şirketin iş merkezini güncelleyemez
                await conn.ExecuteAsync(new CommandDefinition(@"
                    UPDATE WorkCenter
                    SET Code = @Code, Name = @Name,
                        LaborRatePerSec = @LaborRate / 3600.0,
                        MachineRatePerSec = @MachineRate / 3600.0,
                        ElectricityRatePerSec = @ElectricRate / 3600.0,
                        IsActive = @IsActive
                    WHERE Id = @Id AND CompanyId = @CompanyId",
                    new
                    {
                        Form.Id, CompanyId = company.Id,
                        Form.Code, Form.Name,
                        LaborRate = Form.LaborRatePerHour,
                        MachineRate = Form.MachineRatePerHour,
                        ElectricRate = Form.ElectricityRatePerHour,
                        Form.IsActive
                    }, cancellationToken: ct));
            }

            TempData["Success"] = "İş merkezi kaydedildi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP veya DB kısıtı Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "İş merkezi kayıt hatası: {FormId}", Form.Id);
            TempData["Error"] = "İşlem sırasında veritabanı hatası oluştu.";
        }

        return RedirectToPage();
    }

    public record WorkCenterDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal LaborRatePerHour { get; set; }
        public decimal MachineRatePerHour { get; set; }
        public decimal ElectricityRatePerHour { get; set; }
        public bool IsActive { get; set; }
    }

    public record WorkCenterFormDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal LaborRatePerHour { get; set; }
        public decimal MachineRatePerHour { get; set; }
        public decimal ElectricityRatePerHour { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
