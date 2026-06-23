using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.MaterialIssue;

/// <summary>
/// Sarf fişi detayı: başlık + kalemler + satır ekle/sil + Onayla/İptal.
/// Onay → StockMovement ISSUE (stok düşer); AccountMovement yazılmaz (iç tüketim).
/// </summary>
[Authorize]
public class DetailsModel(Db db, ICurrentCompany company, ICurrentUser user, ILogger<DetailsModel> logger) : PageModel
{
    [BindProperty] public HeaderDto Header { get; set; } = new();
    public List<LineDto> Lines { get; set; } = [];
    public IEnumerable<DdlDto> Warehouses { get; set; } = [];
    public IEnumerable<DdlDto> CostCenters { get; set; } = [];
    public IEnumerable<DdlDto> Items { get; set; } = [];
    public IEnumerable<ReasonDto> Reasons { get; set; } = [];   // Sarf/zayiat nedenleri — sözlükten (MATERIAL_ISSUE_REASON)

    public bool IsNew  => Header.Id == Guid.Empty;
    public bool IsDraft => Header.Status == DocStatus.Draft;

    // Dropdown'lar + başlık + kalemleri yükler
    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        using var conn = db.Open();
        // ServiceType yalnız Items sorgusunda kullanılır; Dapper diğer sorgularda yok sayar.
        // Sarf fiziksel stok düşürür → hizmet (SERVICE) hariç tüm item (Plan 52 iki-eksen: stok evrağı = fiziksel)
        var p = new { CompanyId = company.Id, ServiceType = ItemType.Service };

        Warehouses = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Warehouse WHERE CompanyId = @CompanyId AND IsDeleted = 0 ORDER BY Code", p, cancellationToken: ct));
        CostCenters = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM CostCenter WHERE CompanyId = @CompanyId AND IsActive = 1 ORDER BY Code", p, cancellationToken: ct));
        // Sarf edilebilir ürünler: fiziksel olan (SERVICE hariç) — stok düşeceği için hizmet seçilemez
        Items = await conn.QueryAsync<DdlDto>(new CommandDefinition(
            "SELECT Id, Code, Name FROM Item WHERE CompanyId = @CompanyId AND IsActive = 1 AND IsDeleted = 0 AND ItemType <> @ServiceType ORDER BY Code", p, cancellationToken: ct));
        // Sarf/zayiat nedenleri sözlükten gelir (hardcoded liste DEĞİL — ui-standard §1.5 sıfır-hardcoded-veri).
        // Code=İngilizce sistem-çapa (SP'nin baktığı), NameTr=Türkçe etiket (kullanıcının gördüğü),
        // RequiresKdvAdjustment=KDV-davranışı flag'i (view'da mevzuat açıklamasını seçmek için taşınır).
        // OrderNo ile sıralanır ki dropdown sırası admin'in belirlediği gibi olsun. Yalnız aktif+silinmemiş.
        Reasons = await conn.QueryAsync<ReasonDto>(new CommandDefinition(@"
            SELECT dv.Code AS Code, dv.NameTr AS Name, ISNULL(dv.RequiresKdvAdjustment, 0) AS RequiresKdv
            FROM DictionaryValue dv JOIN DictionaryType dt ON dt.Id = dv.TypeId
            WHERE dt.Code = @ReasonType AND dt.CompanyId = @CompanyId AND dv.IsActive = 1 AND dv.IsDeleted = 0
            ORDER BY dv.OrderNo",
            new { ReasonType = DictType.MaterialIssueReason, CompanyId = company.Id }, cancellationToken: ct));

        if (id.HasValue)
        {
            Header = await conn.QueryFirstOrDefaultAsync<HeaderDto>(new CommandDefinition(
                "SELECT Id, WarehouseId, DocNo, IssueDate, CostCenterId, ReasonCode, IsForceMajeure, KdvAdjustmentAmount, LegalDocRef, Notes, Status FROM MaterialIssueHeader WHERE Id = @Id AND CompanyId = @CompanyId",
                new { Id = id, CompanyId = company.Id }, cancellationToken: ct)) ?? new();

            Lines = (await conn.QueryAsync<LineDto>(new CommandDefinition(@"
                /* isolation-guard:ignore: üst belge yukarıda CompanyId ile doğrulandı */
                SELECT l.Id, i.Code AS ItemCode, i.Name AS ItemName, dv.Code AS UomCode, l.Qty
                FROM MaterialIssueLine l
                JOIN Item i ON i.Id = l.ItemId
                LEFT JOIN DictionaryValue dv ON dv.Id = l.UomId
                WHERE l.HeaderId = @Id ORDER BY l.CreatedAt", new { Id = id }, cancellationToken: ct))).ToList();
        }
        else
        {
            Header.Status = DocStatus.Draft;
            Header.IssueDate = DateTime.Today;
        }
    }

    // Başlık kaydet (yeni/düzenle — yalnızca DRAFT)
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        using var conn = db.Open();
        if (IsNew)
        {
            // Yeni fiş: DocNo SARF-{UTC zaman damgası} ile benzersiz üretilir
            Header.Id = Guid.NewGuid();
            var reason = await ValidReasonAsync(conn, Header.ReasonCode, ct);
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO MaterialIssueHeader (Id, CompanyId, WarehouseId, DocNo, IssueDate, CostCenterId, ReasonCode, IsForceMajeure, LegalDocRef, Notes, Status, CreatedBy)
                VALUES (@Id, @CompanyId, @WarehouseId, @DocNo, @IssueDate, @CostCenterId, @ReasonCode, @IsForceMajeure, @LegalDocRef, @Notes, @St, @UserId)",
                new { Header.Id, CompanyId = company.Id, Header.WarehouseId,
                      DocNo = $"SARF-{DateTime.UtcNow:yyyyMMddHHmm}", Header.IssueDate,
                      Header.CostCenterId, ReasonCode = reason, IsForceMajeure = NormalizeForceMajeure(reason, Header.IsForceMajeure),
                      LegalDocRef = Header.LegalDocRef, Header.Notes, St = DocStatus.Draft, UserId = user.Id },
                cancellationToken: ct));
        }
        else
        {
            // İş kuralı: yalnızca DRAFT belge güncellenebilir (WHERE Status = @St)
            var reason = await ValidReasonAsync(conn, Header.ReasonCode, ct);
            await conn.ExecuteAsync(new CommandDefinition(@"
                UPDATE MaterialIssueHeader
                SET WarehouseId = @WarehouseId, IssueDate = @IssueDate, CostCenterId = @CostCenterId,
                    ReasonCode = @ReasonCode, IsForceMajeure = @IsForceMajeure, LegalDocRef = @LegalDocRef,
                    Notes = @Notes, UpdatedAt = GETUTCDATE(), UpdatedBy = @UserId
                WHERE Id = @Id AND CompanyId = @CompanyId AND Status = @St",
                new { Header.WarehouseId, Header.IssueDate, Header.CostCenterId,
                      ReasonCode = reason, IsForceMajeure = NormalizeForceMajeure(reason, Header.IsForceMajeure),
                      LegalDocRef = Header.LegalDocRef, Header.Notes,
                      UserId = user.Id, Header.Id, CompanyId = company.Id, St = DocStatus.Draft },
                cancellationToken: ct));
        }
        return RedirectToPage(new { id = Header.Id });
    }

    // Satır ekle (yalnızca DRAFT) — UOM ürünün temel birimi
    public async Task<IActionResult> OnPostAddLineAsync(Guid id, Guid itemId, decimal qty, CancellationToken ct)
    {
        using var conn = db.Open();
        // İş kuralı: ürün bu firmaya ait + temel birim alınır
        var baseUom = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT BaseUomId FROM Item WHERE Id = @Id AND CompanyId = @CompanyId",
            new { Id = itemId, CompanyId = company.Id }, cancellationToken: ct));
        if (baseUom is null)
        {
            TempData["Error"] = "Ürün bulunamadı veya bu firmaya ait değil.";
            return RedirectToPage(new { id });
        }
        if (!await IsDraftAsync(conn, id, ct))
        {
            TempData["Error"] = "Yalnızca taslak fişe kalem eklenebilir.";
            return RedirectToPage(new { id });
        }

        await conn.ExecuteAsync(new CommandDefinition(@"
            /* isolation-guard:ignore: HeaderId OnGet'te CompanyId ile doğrulandı; ürün yukarıda firma-ait */
            INSERT INTO MaterialIssueLine (HeaderId, ItemId, UomId, Qty, QtyBase)
            VALUES (@HeaderId, @ItemId, @UomId, @Qty, @Qty)",
            new { HeaderId = id, ItemId = itemId, UomId = baseUom, Qty = qty },
            cancellationToken: ct));
        return RedirectToPage(new { id });
    }

    // Satır sil (yalnızca DRAFT)
    public async Task<IActionResult> OnPostDeleteLineAsync(Guid id, Guid lineId, CancellationToken ct)
    {
        using var conn = db.Open();
        if (!await IsDraftAsync(conn, id, ct)) return RedirectToPage(new { id });
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM MaterialIssueLine WHERE Id = @LineId AND HeaderId IN (SELECT Id FROM MaterialIssueHeader WHERE CompanyId = @CompanyId)",
            new { LineId = lineId, CompanyId = company.Id }, cancellationToken: ct));
        return RedirectToPage(new { id });
    }

    // Onayla → sp_MaterialIssuePost (StockMovement ISSUE)
    public async Task<IActionResult> OnPostPostAsync(Guid id, CancellationToken ct)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_MaterialIssuePost",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
            TempData["Success"] = "Sarf fişi onaylandı, stok düşüldü.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Sarf fişi onay hatası: {HeaderId}", id);
            TempData["Error"] = "Sarf fişi onaylanırken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    // İptal → sp_MaterialIssueReverse (flag-only)
    public async Task<IActionResult> OnPostReverseAsync(Guid id, CancellationToken ct)
    {
        using var conn = db.Open();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("sp_MaterialIssueReverse",
                new { HeaderId = id, CompanyId = company.Id, UserId = user.Id },
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
            TempData["Success"] = "Sarf fişi iptal edildi, stok geri alındı.";
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number >= 50000)
        {
            TempData["Error"] = sqlEx.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            logger.LogError(sqlEx, "Sarf fişi iptal hatası: {HeaderId}", id);
            TempData["Error"] = "Sarf fişi iptal edilirken veritabanı hatası oluştu.";
        }
        return RedirectToPage(new { id });
    }

    // Sarf nedeni doğrulama: boş veya sözlükte (MATERIAL_ISSUE_REASON) aktif değilse → NULL (normal tüketim).
    // Hardcoded liste yok — vokabüler veri-driven; geçersiz/tampering kod sessizce normal tüketime düşer.
    private async Task<string?> ValidReasonAsync(IDbConnection conn, string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var ok = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(1) FROM DictionaryValue dv JOIN DictionaryType dt ON dt.Id = dv.TypeId
            WHERE dt.Code = @ReasonType AND dt.CompanyId = @CompanyId AND dv.Code = @Code AND dv.IsActive = 1 AND dv.IsDeleted = 0",
            new { ReasonType = DictType.MaterialIssueReason, CompanyId = company.Id, Code = code }, cancellationToken: ct));
        return ok > 0 ? code : null;
    }

    // Mücbir sebep yalnız bir zayiat nedeni seçiliyse anlamlı; SP, KDV-düzeltme nedeninde dikkate alır (Maliye-ilan afet)
    private static bool NormalizeForceMajeure(string? reason, bool isForceMajeure)
        => reason != null && isForceMajeure;

    // Belgenin DRAFT olduğunu firma-ait doğrular
    private async Task<bool> IsDraftAsync(IDbConnection conn, Guid id, CancellationToken ct)
        => await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM MaterialIssueHeader WHERE Id = @Id AND CompanyId = @CompanyId AND Status = @St",
            new { Id = id, CompanyId = company.Id, St = DocStatus.Draft }, cancellationToken: ct)) > 0;

    public record HeaderDto
    {
        public Guid    Id           { get; set; }
        public Guid    WarehouseId  { get; set; }
        public string  DocNo        { get; set; } = "";
        public DateTime IssueDate   { get; set; }
        public Guid?    CostCenterId        { get; set; }
        public string?  ReasonCode          { get; set; }   // NULL=normal tüketim; sözlük (MATERIAL_ISSUE_REASON) kodu atanırsa zayiat
        public bool     IsForceMajeure      { get; set; }    // mücbir sebep (yalnız FIRE; Maliye-ilan → KDV korunur)
        public decimal  KdvAdjustmentAmount { get; set; }    // POST'ta hesaplanan ilave-KDV (salt-okuma)
        public string?  LegalDocRef         { get; set; }    // takdir komisyonu / itfaiye-sigorta rapor no
        public string?  Notes               { get; set; }
        public string   Status              { get; set; } = DocStatus.Draft;
    }

    public record LineDto(Guid Id, string ItemCode, string ItemName, string? UomCode, decimal Qty);

    // Sözlükten gelen tek zayiat/sarf nedeni satırı.
    //   Code     : sistem-çapa İngilizce kod (örn. DAMAGE) — SP/iş mantığı buna bakar, kullanıcıya gösterilmez.
    //   Name     : DictionaryValue.NameTr — kullanıcıya gösterilen Türkçe etiket (örn. "Hasar / Bozulma").
    //   RequiresKdv : DictionaryValue.RequiresKdvAdjustment — bu neden indirilen KDV'nin düzeltilmesini (md.30/c)
    //                 gerektiriyor mu? UI'da mevzuat açıklamasını seçmek için view'a taşınır (true=düzeltme, false=nötr).
    public record ReasonDto(string Code, string Name, bool RequiresKdv);
}
