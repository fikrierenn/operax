using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Manufacturing.WorkCenters;

[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public IEnumerable<WorkCenterDto> WorkCenters { get; set; } = [];

    [BindProperty]
    public WorkCenterFormDto Form { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Şirkete ait iş merkezlerini / makineleri listeler
        using var conn = db.Open();
        WorkCenters = await conn.QueryAsync<WorkCenterDto>(@"
            SELECT Id, Code, Name,
                   LaborRatePerSec * 3600 AS LaborRatePerHour,
                   MachineRatePerSec * 3600 AS MachineRatePerHour,
                   ElectricityRatePerSec * 3600 AS ElectricityRatePerHour,
                   IsActive
            FROM WorkCenter
            WHERE CompanyId = @CompanyId
            ORDER BY Code",
            new { CompanyId = company.Id });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Yeni iş merkezi ekler veya mevcut iş merkezini günceller
        using var conn = db.Open();

        if (Form.Id == Guid.Empty)
        {
            await conn.ExecuteAsync(@"
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
                });
        }
        else
        {
            // CompanyId: başka şirketin iş merkezini güncelleyemez
            await conn.ExecuteAsync(@"
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
                });
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
