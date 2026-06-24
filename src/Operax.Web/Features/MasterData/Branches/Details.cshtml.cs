using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.MasterData.Branches;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public BranchDto Branch { get; set; } = new();

    // Şubeye bağlı depolar (görüntüleme)
    public IEnumerable<WarehouseDto> Warehouses { get; set; } = [];

    // İade deposu dropdown için
    public IEnumerable<DdlDto> WarehouseDdl { get; set; } = [];

    public bool IsNew => Branch.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        using var conn = db.Open();

        // Dropdown her zaman yüklenir
        WarehouseDdl = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Name AS Text FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code",
            new { CompanyId = company.Id }, cancellationToken: ct));

        if (id.HasValue)
        {
            // CompanyId zorunlu — başka şirket şubesi görüntülenemez
            Branch = await conn.QueryFirstOrDefaultAsync<BranchDto>(new CommandDefinition(
                "SELECT Id, Code, Name, City, Address, Phone, BranchType, ReturnWarehouseId, IsActive FROM Branch WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct)) ?? new();

            // Şubeye bağlı depolar — CompanyId zorunlu (izolasyon: başka şirket deposu sızmaz)
            Warehouses = await conn.QueryAsync<WarehouseDto>(new CommandDefinition(
                "SELECT Id, Code, Name, IsActive FROM Warehouse WHERE BranchId = @BranchId AND CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code",
                new { BranchId = id, CompanyId = company.Id }, cancellationToken: ct));
        }
        else
        {
            Branch.IsActive = true;
            Branch.BranchType = BranchType.Sube;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        using var conn = db.Open();

        // Guard: form doğrulaması başarısızsa dropdown'ı tekrar yükleyip formu geri göster
        if (!ModelState.IsValid)
        {
            WarehouseDdl = await conn.QueryAsync<DdlDto>(new CommandDefinition(
                "SELECT Id, Name AS Text FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code",
                new { CompanyId = company.Id }, cancellationToken: ct));
            return Page();
        }

        try
        {
            // İş kuralı: ReturnWarehouseId bu firmaya ait olmalı (IDOR koruması)
            if (Branch.ReturnWarehouseId.HasValue)
            {
                var whOwned = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(1) FROM Warehouse WHERE Id = @Id AND CompanyId = @CompanyId",
                    new { Id = Branch.ReturnWarehouseId, CompanyId = company.Id }, cancellationToken: ct));
                if (whOwned == 0) Branch.ReturnWarehouseId = null;
            }

            if (IsNew)
            {
                Branch.Id = Guid.NewGuid();
                await conn.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO Branch (Id, CompanyId, Code, Name, City, Address, Phone, BranchType, ReturnWarehouseId, IsActive)
                    VALUES (@Id, @CompanyId, @Code, @Name, @City, @Address, @Phone, @BranchType, @ReturnWarehouseId, @IsActive)",
                    new
                    {
                        Branch.Id,
                        CompanyId = company.Id,
                        Branch.Code, Branch.Name, Branch.City, Branch.Address,
                        Branch.Phone, Branch.BranchType, Branch.ReturnWarehouseId, Branch.IsActive
                    }, cancellationToken: ct));
            }
            else
            {
                // CompanyId zorunlu — başka şirket şubesi güncellenemez
                await conn.ExecuteAsync(new CommandDefinition(@"
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
                    }, cancellationToken: ct));
            }
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Sistem hatası (örn. BranchType CHECK ihlali) — ham mesaj gösterilmez, detay log'a
            logger.LogError(sqlEx, "Şube kaydetme DB hatası. Şube {BranchId}", Branch.Id);
            TempData["Error"] = "Veritabanı hatası oluştu.";
            // Dropdown'ı tekrar yükle (form yeniden gösterilecek)
            WarehouseDdl = await conn.QueryAsync<DdlDto>(new CommandDefinition(
                "SELECT Id, Name AS Text FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code",
                new { CompanyId = company.Id }, cancellationToken: ct));
            return Page();
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
        public string BranchType { get; set; } = Operax.Web.Lib.BranchType.Sube;
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
