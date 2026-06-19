using System.Data;
using Dapper;

namespace Operax.Web.Lib;

/// <summary>
/// Fiyat listesi toplu giriş işlemleri (Plan 31) — grid bulk-save, Excel import önizleme,
/// tüm-ürün ekleme, listeden çoğaltma. Tümü SQL-first: sp_PriceListBulkUpsert (TVP) /
/// sp_PriceListClone üzerinden tek transaction. C# yalnız TVP'yi kurar ve SP'yi çağırır.
/// </summary>
public sealed class PriceListBulkService(Db db)
{
    /// <summary>Toplu upsert satır girdisi (grid → @ItemId, Excel → @ItemCode).</summary>
    public sealed record BulkLine(
        int RowNo, Guid? ItemId, string? ItemCode,
        decimal UnitPrice, decimal MinQty, string LineType, string? DiscountChain);

    /// <summary>Önizleme hata satırı (DryRun).</summary>
    public sealed record PreviewError(int RowNo, string ItemCode, string Hata);

    /// <summary>Önizleme özeti (DryRun).</summary>
    public sealed record PreviewSummary(int Toplam, int Hatali, int Gecerli, IReadOnlyList<PreviewError> Hatalar);

    // Grid "Kaydet": görünen satırlar TAM durum (SyncDelete=1 → eksikler soft-delete).
    public async Task<int> BulkSaveAsync(Guid companyId, Guid priceListId, Guid userId,
        IEnumerable<BulkLine> lines, CancellationToken ct = default)
        => await ExecUpsertAsync(companyId, priceListId, userId, lines, dryRun: false, syncDelete: true, ct);

    // Excel import gerçek koşusu: ek-mod (SyncDelete=0 → kısmi dosya mevcutları silmez).
    public async Task<int> ImportAsync(Guid companyId, Guid priceListId, Guid userId,
        IEnumerable<BulkLine> lines, CancellationToken ct = default)
        => await ExecUpsertAsync(companyId, priceListId, userId, lines, dryRun: false, syncDelete: false, ct);

    // Excel import önizleme: yazma yok, satır-bazlı hata + özet döner.
    public async Task<PreviewSummary> PreviewAsync(Guid companyId, Guid priceListId, Guid userId,
        IEnumerable<BulkLine> lines, CancellationToken ct = default)
    {
        using var conn = db.Open();
        var p = BuildParams(companyId, priceListId, userId, lines, dryRun: true, syncDelete: false);
        using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            "sp_PriceListBulkUpsert", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));
        var errors = (await multi.ReadAsync<PreviewError>()).ToList();
        var sum = await multi.ReadSingleAsync<(int Toplam, int Hatali, int Gecerli)>();
        return new PreviewSummary(sum.Toplam, sum.Hatali, sum.Gecerli, errors);
    }

    // Tüm aktif ürünleri listeye ekle (fiyat = ortalama maliyet veya 0). Ek-mod (mevcutları korur).
    public async Task<int> AddAllItemsAsync(Guid companyId, Guid priceListId, Guid userId,
        CancellationToken ct = default)
    {
        using var conn = db.Open();
        // Ürün + ortalama maliyet (öneri fiyatı); kod tekil (Plan 31 index) → @ItemId ile gönderilir
        var items = await conn.QueryAsync<(Guid Id, decimal Cost)>(new CommandDefinition(
            @"SELECT i.Id, ISNULL(MAX(ic.AvgCost), 0) AS Cost
              FROM Item i LEFT JOIN ItemCost ic ON ic.ItemId = i.Id AND ic.CompanyId = @CompanyId
              WHERE i.CompanyId = @CompanyId AND i.IsActive = 1 AND i.IsDeleted = 0
              GROUP BY i.Id",
            new { CompanyId = companyId }, cancellationToken: ct));

        var lines = items.Select((it, idx) => new BulkLine(
            idx + 1, it.Id, null, it.Cost, 0m, PriceLineType.Fixed, null)).ToList();
        if (lines.Count == 0) return 0;
        // Ek-mod: mevcut satırları silmeden eksikleri ekler/günceller
        return await ExecUpsertAsync(companyId, priceListId, userId, lines, dryRun: false, syncDelete: false, ct);
    }

    // Kaynak listenin satır + iskontolarını hedefe kopyala (hedefte var olan ürünü atlar).
    public async Task<int> CloneAsync(Guid companyId, Guid sourceId, Guid targetId, Guid userId,
        CancellationToken ct = default)
    {
        using var conn = db.Open();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "sp_PriceListClone",
            new { CompanyId = companyId, SourceId = sourceId, TargetId = targetId, UserId = userId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    // ─── Yardımcılar ────────────────────────────────────────────

    private async Task<int> ExecUpsertAsync(Guid companyId, Guid priceListId, Guid userId,
        IEnumerable<BulkLine> lines, bool dryRun, bool syncDelete, CancellationToken ct)
    {
        using var conn = db.Open();
        var p = BuildParams(companyId, priceListId, userId, lines, dryRun, syncDelete);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "sp_PriceListBulkUpsert", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    // Satır demetini dbo.PriceLineTVP yapısına (kolon sırası birebir) çevirir + parametreleri kurar.
    private static DynamicParameters BuildParams(Guid companyId, Guid priceListId, Guid userId,
        IEnumerable<BulkLine> lines, bool dryRun, bool syncDelete)
    {
        var tvp = new DataTable();
        tvp.Columns.Add("RowNo", typeof(int));
        tvp.Columns.Add("ItemId", typeof(Guid));
        tvp.Columns.Add("ItemCode", typeof(string));
        tvp.Columns.Add("UnitPrice", typeof(decimal));
        tvp.Columns.Add("MinQty", typeof(decimal));
        tvp.Columns.Add("LineType", typeof(string));
        tvp.Columns.Add("DiscountChain", typeof(string));
        foreach (var l in lines)
        {
            tvp.Rows.Add(
                l.RowNo,
                (object?)l.ItemId ?? DBNull.Value,
                (object?)l.ItemCode ?? DBNull.Value,
                l.UnitPrice, l.MinQty,
                string.IsNullOrWhiteSpace(l.LineType) ? PriceLineType.Fixed : l.LineType,
                (object?)l.DiscountChain ?? DBNull.Value);
        }

        var p = new DynamicParameters();
        p.Add("@CompanyId", companyId);
        p.Add("@PriceListId", priceListId);
        p.Add("@UserId", userId);
        p.Add("@Lines", tvp.AsTableValuedParameter("dbo.PriceLineTVP"));
        p.Add("@DryRun", dryRun);
        p.Add("@SyncDelete", syncDelete);
        return p;
    }
}
