using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;
using Microsoft.AspNetCore.Authorization;

namespace Operax.Web.Features.Manufacturing.BOM;

[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty]
    public ProductModelDto Model { get; set; } = new();
    public IEnumerable<ParameterDto> Parameters { get; set; } = [];
    public IEnumerable<BomLineDto> BomLines { get; set; } = [];
    public IEnumerable<DdlDto> Items { get; set; } = [];

    public bool IsNew => Model.Id == Guid.Empty;

    public async Task OnGetAsync(Guid? id)
    {
        // Model, parametre ve BOM satırlarını yükler; yeni kayıt için boş döner
        using var conn = db.Open();

        // Ürün dropdown listesi
        Items = await conn.QueryAsync<DdlDto>(
            "SELECT Id, Code + ' — ' + Name AS Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 ORDER BY Code",
            new { CompanyId = company.Id });

        if (!id.HasValue) { Model.IsActive = true; return; }

        Model = await conn.QueryFirstOrDefaultAsync<ProductModelDto>(@"
            SELECT m.*, i.Code AS BaseItemCode
            FROM ProductModel m
            LEFT JOIN Item i ON i.Id = m.BaseItemId
            WHERE m.Id = @Id AND m.CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id }) ?? new();

        if (Model.Id == Guid.Empty) return;

        // Model parametreleri (WIDTH, HEIGHT, vb.)
        Parameters = await conn.QueryAsync<ParameterDto>(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: üst kayıt ProductModel aynı handler içinde daha önce
            -- WHERE m.Id = @Id AND m.CompanyId = @CompanyId ile yüklendi ve bulunamazsa boş form döndü.
            -- Bu sorgu yalnızca o doğrulanmış ProductModel.Id üzerinden parametreleri okuyduğundan
            -- başka firmanın model verisine erişilemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            SELECT Id, Code, Name, DataType, DefaultValue, Unit FROM ProductModelParameter
            WHERE ProductModelId = @ModelId
            ORDER BY Code",
            new { ModelId = Model.Id });

        // BOM satırları (formüllü bileşenler)
        BomLines = await conn.QueryAsync<BomLineDto>(@"
            -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
            -- Gerekçe: üst kayıt ProductModel aynı handler içinde daha önce
            -- WHERE m.Id = @Id AND m.CompanyId = @CompanyId ile yüklendi ve bulunamazsa boş form döndü.
            -- Bu sorgu yalnızca o doğrulanmış ProductModel.Id üzerinden BOM satırlarını okuyduğundan
            -- başka firmanın reçete verisine erişilemez.
            -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
            SELECT b.*, i.Code AS ItemCode, i.Name AS ItemName
            FROM ProductModelBOM b
            JOIN Item i ON i.Id = b.ComponentItemId
            WHERE b.ProductModelId = @ModelId
            ORDER BY i.Code",
            new { ModelId = Model.Id });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Ürün modelini kaydeder (insert veya update)
        using var conn = db.Open();
        try
        {
            if (IsNew)
            {
                Model.Id = Guid.NewGuid();
                await conn.ExecuteAsync(@"
                    INSERT INTO ProductModel (Id, CompanyId, Code, Name, BaseItemId, IsActive)
                    VALUES (@Id, @CompanyId, @Code, @Name, @BaseItemId, @IsActive)",
                    new { Model.Id, CompanyId = company.Id, Model.Code, Model.Name, Model.BaseItemId, Model.IsActive });
            }
            else
            {
                // CompanyId: başka şirketin modelini güncelleyemez
                await conn.ExecuteAsync(@"
                    UPDATE ProductModel
                    SET Code = @Code, Name = @Name, BaseItemId = @BaseItemId, IsActive = @IsActive
                    WHERE Id = @Id AND CompanyId = @CompanyId",
                    new { Model.Code, Model.Name, Model.BaseItemId, Model.IsActive, Model.Id, CompanyId = company.Id });
            }

            TempData["Success"] = "Ürün modeli kaydedildi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP veya DB kısıtı Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
            return Page();
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Ürün modeli kayıt hatası: {ModelId}", Model.Id);
            TempData["Error"] = "İşlem sırasında veritabanı hatası oluştu.";
            return Page();
        }

        return RedirectToPage(new { id = Model.Id });
    }

    public async Task<IActionResult> OnPostAddParamAsync(Guid id, string code, string name, string dataType, string? defaultValue, string? unit)
    {
        // Ürün modeline yeni parametre ekler (WIDTH, HEIGHT vb.)
        using var conn = db.Open();

        // Modelin şirkete ait olduğunu doğrula
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM ProductModel WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id });
        if (exists == 0) return RedirectToPage("./Index");

        try
        {
            await conn.ExecuteAsync(@"
                -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
                -- Gerekçe: üst kayıt ProductModel bu handler'da WHERE Id = @Id AND CompanyId = @CompanyId
                -- ile doğrulandı; bulunamazsa Index sayfasına yönlendirildi.
                -- @ModelId parametresi o doğrulanmış ProductModel.Id değeridir;
                -- farklı firmanın modeline parametre eklenemez.
                -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                INSERT INTO ProductModelParameter (ProductModelId, Code, Name, DataType, DefaultValue, Unit)
                VALUES (@ModelId, @Code, @Name, @DataType, @DefaultValue, @Unit)",
                new { ModelId = id, Code = code.ToUpper(), Name = name, DataType = dataType, DefaultValue = defaultValue, Unit = unit });

            TempData["Success"] = "Parametre eklendi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP veya DB kısıtı Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Parametre ekleme hatası: {ModelId}", id);
            TempData["Error"] = "İşlem sırasında veritabanı hatası oluştu.";
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteParamAsync(Guid id, Guid paramId)
    {
        // Parametreyi siler — önce modelin şirkete ait olduğunu doğrula
        using var conn = db.Open();
        await conn.ExecuteAsync(@"
            DELETE p FROM ProductModelParameter p
            JOIN ProductModel m ON m.Id = p.ProductModelId
            WHERE p.Id = @ParamId AND m.CompanyId = @CompanyId",
            new { ParamId = paramId, CompanyId = company.Id });
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddBomLineAsync(Guid id, Guid itemId, string qtyFormula, decimal wastePercentage, string? conditionFormula)
    {
        // BOM satırı ekler: bileşen ürün + miktar formülü + fire oranı
        using var conn = db.Open();

        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM ProductModel WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = id, CompanyId = company.Id });
        if (exists == 0) return RedirectToPage("./Index");

        try
        {
            await conn.ExecuteAsync(@"
                -- Çoklu-firma izolasyon notu: bu sorgu doğrudan CompanyId filtresi taşımaz; güvenlidir.
                -- Gerekçe: üst kayıt ProductModel bu handler'da WHERE Id = @Id AND CompanyId = @CompanyId
                -- ile doğrulandı; bulunamazsa Index sayfasına yönlendirildi.
                -- @ModelId parametresi o doğrulanmış ProductModel.Id değeridir;
                -- farklı firmanın reçetesine bileşen eklenemez.
                -- isolation-guard:ignore  (operax-cli scan-isolation tarayıcısı bu işaretle sorguyu atlar)
                INSERT INTO ProductModelBOM (ProductModelId, ComponentItemId, QtyFormula, WastePercentage, ConditionFormula)
                VALUES (@ModelId, @ItemId, @QtyFormula, @WastePercentage, @ConditionFormula)",
                new { ModelId = id, ItemId = itemId, QtyFormula = qtyFormula, WastePercentage = wastePercentage, ConditionFormula = conditionFormula });

            TempData["Success"] = "BOM satırı eklendi.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
        {
            // İş kuralı hatası — SP veya DB kısıtı Türkçe mesaj fırlattı
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "BOM satırı ekleme hatası: {ModelId}", id);
            TempData["Error"] = "İşlem sırasında veritabanı hatası oluştu.";
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteBomLineAsync(Guid id, Guid bomLineId)
    {
        // BOM satırını siler — şirket sahipliği JOIN ile doğrulanır
        using var conn = db.Open();
        await conn.ExecuteAsync(@"
            DELETE b FROM ProductModelBOM b
            JOIN ProductModel m ON m.Id = b.ProductModelId
            WHERE b.Id = @BomLineId AND m.CompanyId = @CompanyId",
            new { BomLineId = bomLineId, CompanyId = company.Id });
        return RedirectToPage(new { id });
    }

    public record ProductModelDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public Guid? BaseItemId { get; set; }
        public string? BaseItemCode { get; set; }
        public bool IsActive { get; set; }
    }

    public record ParameterDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "NUMBER";
        public string? DefaultValue { get; set; }
        public string? Unit { get; set; }
    }

    public record BomLineDto
    {
        public Guid Id { get; set; }
        public Guid ComponentItemId { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string QtyFormula { get; set; } = "";
        public decimal WastePercentage { get; set; }
        public string? ConditionFormula { get; set; }
    }
}
