using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Branches;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty]
    public BranchDto Branch { get; set; } = new();

    // Şubeye bağlı depolar (görüntüleme)
    public IEnumerable<WarehouseDto> Warehouses { get; set; } = [];

    // İade deposu dropdown için
    public IEnumerable<DdlDto> WarehouseDdl { get; set; } = [];

    public bool IsNew => Branch.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        using var conn = db.Open();

        // Dropdown her zaman yüklenir
        WarehouseDdl = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Name AS Text FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code",
            new { CompanyId = company.Id });

        if (id.HasValue)
        {
            // CompanyId zorunlu — başka şirket şubesi görüntülenemez
            Branch = await conn.QueryFirstOrDefaultAsync<BranchDto>(
                "SELECT * FROM Branch WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }) ?? new();

            // Şubeye bağlı depolar
            Warehouses = await conn.QueryAsync<WarehouseDto>(
                "SELECT Id, Code, Name, IsActive FROM Warehouse WHERE BranchId = @BranchId AND IsDeleted = 0 ORDER BY Code",
                new { BranchId = id });
        }
        else
        {
            Branch.IsActive = true;
            Branch.BranchType = "SUBE";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        using var conn = db.Open();

        if (IsNew)
        {
            Branch.Id = Guid.NewGuid();
            await conn.ExecuteAsync(@"
                INSERT INTO Branch (Id, CompanyId, Code, Name, City, Address, Phone, BranchType, ReturnWarehouseId, IsActive)
                VALUES (@Id, @CompanyId, @Code, @Name, @City, @Address, @Phone, @BranchType, @ReturnWarehouseId, @IsActive)",
                new
                {
                    Branch.Id,
                    CompanyId = company.Id,
                    Branch.Code, Branch.Name, Branch.City, Branch.Address,
                    Branch.Phone, Branch.BranchType, Branch.ReturnWarehouseId, Branch.IsActive
                });
        }
        else
        {
            // CompanyId zorunlu — başka şirket şubesi güncellenemez
            await conn.ExecuteAsync(@"
                UPDATE Branch
                SET Code = @Code, Name = @Name, City = @City, Address = @Address,
                    Phone = @Phone, BranchType = @BranchType, ReturnWarehouseId = @ReturnWarehouseId,
                    IsActive = @IsActive, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND CompanyId = @CompanyId",
                new
                {
                    Branch.Code, Branch.Name, Branch.City, Branch.Address,
                    Branch.Phone, Branch.BranchType, Branch.ReturnWarehouseId,
                    Branch.IsActive, Branch.Id, CompanyId = company.Id
                });
        }

        TempData["Success"] = "Şube kaydedildi.";
        return RedirectToPage(new { id = Branch.Id });
    }

    public record BranchDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string BranchType { get; set; } = "SUBE";
        public Guid? ReturnWarehouseId { get; set; }
        public bool IsActive { get; set; }
    }

    public record WarehouseDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }
}
