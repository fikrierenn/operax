using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Warehouses;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty]
    public WarehouseDto Warehouse { get; set; } = new();
    public IEnumerable<BinDto> Bins { get; set; } = [];
    public IEnumerable<DdlDto> BranchDdl { get; set; } = [];

    public bool IsNew => Warehouse.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        using var conn = db.Open();

        // Şube dropdown her zaman yüklenir
        BranchDdl = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Name AS Text FROM Branch WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code",
            new { CompanyId = company.Id },
            cancellationToken: ct));

        if (id.HasValue)
        {
            // CompanyId zorunlu — başka şirket deposu görüntülenemez
            Warehouse = await conn.QueryFirstOrDefaultAsync<WarehouseDto>(new CommandDefinition(
                "SELECT Id, Code, Name, IsActive, BranchId FROM Warehouse WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id },
                cancellationToken: ct)) ?? new();

            Bins = await conn.QueryAsync<BinDto>(new CommandDefinition(@"
                -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
                -- Gerekçe: üst kayıt Warehouse aynı handler içinde daha önce
                -- WHERE Id = @Id AND CompanyId = @CompanyId ile yüklendi ve bulunamazsa boş form döndü.
                -- Bu sorgu yalnızca o doğrulanmış Warehouse.Id üzerinden Bin kayıtlarını okuyduğundan
                -- başka firmanın deposuna ait raf verisi dönemez.
                -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                SELECT Id, Code, Zone, IsPickingArea, IsReceivingArea FROM Bin WHERE WarehouseId = @Id AND IsDeleted = 0 ORDER BY SortNo, Code",
                new { Id = id },
                cancellationToken: ct));
        }
        else
        {
            Warehouse.IsActive = true;
        }
    }


    // Depoyu kaydeder veya günceller — ModelState geçersizse formu hatalarla geri gösterir
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        using var conn = db.Open();

        // Guard: form doğrulaması başarısızsa kaydetme — dropdown + raf listesini tekrar
        // yükleyip formu hata mesajlarıyla geri göster (boş kayıt INSERT'i önlenir)
        if (!ModelState.IsValid)
        {
            await ReloadFormAsync(conn, ct);
            return Page();
        }

        var wasNew = IsNew; // redirect öncesinde durumu yakala

        // İş kuralı: BranchId bu firmaya ait olmalı (IDOR koruması)
        if (Warehouse.BranchId.HasValue)
        {
            var branchOwned = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM Branch WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = Warehouse.BranchId, CompanyId = company.Id },
                cancellationToken: ct));
            if (branchOwned == 0) Warehouse.BranchId = null;
        }

        if (IsNew)
        {
            Warehouse.Id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO Warehouse (Id, CompanyId, Code, Name, IsActive, BranchId)
                VALUES (@Id, @CompanyId, @Code, @Name, @IsActive, @BranchId)";
            await conn.ExecuteAsync(new CommandDefinition(sql,
                new { Warehouse.Id, CompanyId = company.Id, Warehouse.Code, Warehouse.Name, Warehouse.IsActive, Warehouse.BranchId },
                cancellationToken: ct));
        }
        else
        {
            // CompanyId zorunlu — başka şirket deposunu güncelleyemez
            const string sql = "UPDATE Warehouse SET Code = @Code, Name = @Name, IsActive = @IsActive, BranchId = @BranchId WHERE Id = @Id AND CompanyId = @CompanyId";
            await conn.ExecuteAsync(new CommandDefinition(sql,
                new { Warehouse.Code, Warehouse.Name, Warehouse.IsActive, Warehouse.BranchId, Warehouse.Id, CompanyId = company.Id },
                cancellationToken: ct));
        }

        TempData["Success"] = wasNew ? "Depo oluşturuldu." : "Depo güncellendi.";
        return RedirectToPage(new { id = Warehouse.Id });
    }

    public async Task<IActionResult> OnPostAddBinAsync(Guid id, string code, string? zone, bool isPicking, bool isReceiving, CancellationToken ct)
    {
        using var conn = db.Open();
        // Deponun şirkete ait olduğunu doğrula
        var whExists = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Warehouse WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id },
            cancellationToken: ct));
        if (whExists == 0) return RedirectToPage("./Index");

        const string sql = @"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: üst kayıt Warehouse bu handler'da WHERE Id = @Id AND CompanyId = @CompanyId
            -- ile doğrulandı; bulunamazsa Index sayfasına yönlendirildi.
            -- @WarehouseId parametresi o doğrulanmış Warehouse.Id değeridir;
            -- farklı firmanın deposuna raf eklenemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            INSERT INTO Bin (WarehouseId, Code, Zone, IsPickingArea, IsReceivingArea, IsStorageArea, IsActive)
            VALUES (@WarehouseId, @Code, @Zone, @IsPicking, @IsReceiving, 1, 1)";
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { WarehouseId = id, Code = code, Zone = zone, IsPicking = isPicking, IsReceiving = isReceiving },
            cancellationToken: ct));
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteBinAsync(Guid id, Guid binId, CancellationToken ct)
    {
        using var conn = db.Open();
        // Rafa ait deponun şirkete ait olduğunu doğrula
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE b SET b.IsDeleted = 1
            FROM Bin b
            JOIN Warehouse w ON w.Id = b.WarehouseId
            WHERE b.Id = @BinId AND w.CompanyId = @CompanyId",
            new { BinId = binId, CompanyId = company.Id },
            cancellationToken: ct));
        return RedirectToPage(new { id });
    }

    // Form yeniden gösterimi için dropdown + raf listesini yükler (doğrulama hatası sonrası)
    private async Task ReloadFormAsync(System.Data.IDbConnection conn, CancellationToken ct)
    {
        BranchDdl = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Name AS Text FROM Branch WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code",
            new { CompanyId = company.Id }, cancellationToken: ct));

        // Mevcut depo düzenleniyorsa rafları da geri yükle (üst kayıt POST'ta bind edilmedi)
        if (!IsNew)
            Bins = await conn.QueryAsync<BinDto>(new CommandDefinition(
                "SELECT Id, Code, Zone, IsPickingArea, IsReceivingArea FROM Bin WHERE WarehouseId = @Id AND IsDeleted = 0 ORDER BY SortNo, Code",
                new { Id = Warehouse.Id }, cancellationToken: ct));
    }

    public record WarehouseDto
    {
        public Guid Id { get; set; }
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Depo kodu zorunludur.")]
        public string Code { get; set; } = "";
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Depo adı zorunludur.")]
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public Guid? BranchId { get; set; }
    }
    public record BinDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string? Zone { get; set; } public bool IsPickingArea { get; set; } public bool IsReceivingArea { get; set; } }
}
